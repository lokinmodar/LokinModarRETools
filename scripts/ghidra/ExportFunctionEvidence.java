// Export static Ghidra evidence for explicit image-base-relative RVAs.
//
// Headless arguments:
//   <markdown-output-path> <binary-sha256> <comma-separated-rvas>
//
// The report documents the analyzer's current view. It does not infer a native
// class owner, ABI, vtable slot, or safe FFXIVClientStructs declaration.
//@category FFXIVClientStructs

import java.io.BufferedWriter;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.LinkedHashSet;
import java.util.Set;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.decompiler.DecompiledFunction;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.InstructionIterator;
import ghidra.program.model.listing.Listing;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;

public class ExportFunctionEvidence extends GhidraScript {

	private static final int MAX_REFERENCES = 100;

	@Override
	public void run() throws Exception {
		String[] arguments = getScriptArgs();
		if (arguments.length != 3) {
			throw new IllegalArgumentException(
				"Expected arguments: <markdown-output-path> <binary-sha256> <comma-separated-rvas>");
		}

		Path outputPath = Paths.get(arguments[0]);
		Path outputDirectory = outputPath.getParent();
		if (outputDirectory == null || !Files.isDirectory(outputDirectory)) {
			throw new IllegalArgumentException("Evidence output directory does not exist: " + outputDirectory);
		}

		long imageBase = currentProgram.getImageBase().getOffset();
		try (BufferedWriter writer = Files.newBufferedWriter(outputPath, StandardCharsets.UTF_8)) {
			writeLine(writer, "# Ghidra Function Evidence");
			writeLine(writer, "");
			writeLine(writer, "- Program: `" + currentProgram.getName() + "`");
			writeLine(writer, "- SHA-256: `" + arguments[1] + "`");
			writeLine(writer, "- Image base: `" + formatHex(imageBase) + "`");
			writeLine(writer, "- Tool: Ghidra headless");
			writeLine(writer, "");
			writeLine(writer,
				"> This is static analyzer output. Confirm ownership, ABI, vtable semantics, and any ClientStructs declaration separately.");

			for (String requestedRva : arguments[2].split(",")) {
				monitor.checkCancelled();
				writeFunctionEvidence(writer, imageBase, parseRva(requestedRva));
			}
		}
	}

	private void writeFunctionEvidence(BufferedWriter writer, long imageBase, long requestedRva)
			throws Exception {
		Address requestedAddress = toAddr(imageBase + requestedRva);
		Function function = currentProgram.getFunctionManager().getFunctionContaining(requestedAddress);

		writeLine(writer, "");
		writeLine(writer, "## Requested RVA `" + formatHex(requestedRva) + "`");
		writeLine(writer, "");
		writeLine(writer, "- Requested VA: `" + requestedAddress + "`");
		if (function == null) {
			writeLine(writer, "- Result: no containing function was defined by Ghidra.");
			return;
		}

		writeLine(writer, "- Function: `" + function.getName() + "`");
		writeLine(writer, "- Entry: `" + formatAddress(function.getEntryPoint(), imageBase) + "`");
		writeLine(writer, "- Body: `" + formatAddress(function.getBody().getMinAddress(), imageBase) + "` to `" +
			formatAddress(function.getBody().getMaxAddress(), imageBase) + "`");
		writeLine(writer, "- Prototype: `" + function.getPrototypeString(false, false) + "`");

		writeReferencesToFunction(writer, function, imageBase);
		writeDirectCalls(writer, function, imageBase);
		writeDecompilerOutput(writer, function);
	}

	private void writeReferencesToFunction(BufferedWriter writer, Function function, long imageBase)
			throws IOException {
		writeLine(writer, "");
		writeLine(writer, "### References to entry");
		ReferenceIterator references = currentProgram.getReferenceManager().getReferencesTo(function.getEntryPoint());
		int count = 0;
		while (references.hasNext() && count < MAX_REFERENCES) {
			Reference reference = references.next();
			Function caller = currentProgram.getFunctionManager().getFunctionContaining(reference.getFromAddress());
			String callerName = caller == null ? "<no containing function>" : caller.getName();
			writeLine(writer, "- `" + formatAddress(reference.getFromAddress(), imageBase) + "` (`" + callerName +
				"`, " + reference.getReferenceType() + ")");
			count++;
		}
		if (count == 0) {
			writeLine(writer, "- None recorded by the analyzer.");
		}
		else if (references.hasNext()) {
			writeLine(writer, "- Truncated after " + MAX_REFERENCES + " references.");
		}
	}

	private void writeDirectCalls(BufferedWriter writer, Function function, long imageBase) throws IOException {
		writeLine(writer, "");
		writeLine(writer, "### Direct call flows");
		Listing listing = currentProgram.getListing();
		InstructionIterator instructions = listing.getInstructions(function.getBody(), true);
		Set<Address> targets = new LinkedHashSet<>();
		while (instructions.hasNext()) {
			Instruction instruction = instructions.next();
			if (!instruction.getFlowType().isCall()) {
				continue;
			}
			for (Address target : instruction.getFlows()) {
				targets.add(target);
			}
		}

		if (targets.isEmpty()) {
			writeLine(writer, "- None resolved by the analyzer.");
			return;
		}

		int count = 0;
		for (Address target : targets) {
			if (count >= MAX_REFERENCES) {
				writeLine(writer, "- Truncated after " + MAX_REFERENCES + " call flows.");
				break;
			}
			Function callee = currentProgram.getFunctionManager().getFunctionAt(target);
			String calleeName = callee == null ? "<no function at target>" : callee.getName();
			writeLine(writer, "- `" + formatAddress(target, imageBase) + "` (`" + calleeName + "`)");
			count++;
		}
	}

	private void writeDecompilerOutput(BufferedWriter writer, Function function) throws IOException {
		writeLine(writer, "");
		writeLine(writer, "### Decompiler output");
		writeLine(writer, "");
		writeLine(writer, "```c");

		DecompInterface decompiler = new DecompInterface();
		try {
			decompiler.toggleCCode(true);
			decompiler.toggleSyntaxTree(false);
			decompiler.setSimplificationStyle("decompile");
			if (!decompiler.openProgram(currentProgram)) {
				writeLine(writer, "// Ghidra could not open the program for decompilation: " + decompiler.getLastMessage());
				return;
			}

			DecompileResults results = decompiler.decompileFunction(function, 60, monitor);
			DecompiledFunction decompiledFunction = results.getDecompiledFunction();
			if (!results.decompileCompleted() || decompiledFunction == null) {
				writeLine(writer, "// Ghidra did not complete decompilation: " + results.getErrorMessage());
				return;
			}
			writer.write(decompiledFunction.getC());
			if (!decompiledFunction.getC().endsWith("\n")) {
				writer.newLine();
			}
		}
		finally {
			decompiler.dispose();
			writeLine(writer, "```");
		}
	}

	private long parseRva(String value) {
		if (!value.startsWith("0x") && !value.startsWith("0X")) {
			throw new IllegalArgumentException("RVA must be 0x-prefixed: " + value);
		}
		return Long.parseLong(value.substring(2), 16);
	}

	private String formatAddress(Address address, long imageBase) {
		return formatHex(address.getOffset()) + " (RVA " + formatHex(address.getOffset() - imageBase) + ")";
	}

	private String formatHex(long value) {
		return String.format("0x%X", value);
	}

	private void writeLine(BufferedWriter writer, String value) throws IOException {
		writer.write(value);
		writer.newLine();
	}
}
