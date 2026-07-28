using ReValidation.Common.Evidence;
using ReValidation.Common.Models;
using ReValidation.Common.Proof;
using Xunit;

namespace ReValidation.Tests.Evidence;

public sealed class BranchValidationEvidenceWriterTests
{
    [Fact]
    public async Task WriteAsync_ExportsPerTargetProofRecords()
    {
        var report = BranchValidationRunReportFactory.CreatePassed();
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var writer = new BranchJsonEvidenceWriter(new EvidencePathBuilder());

        var result = await writer.WriteAsync(report, root, CancellationToken.None);
        var json = await File.ReadAllTextAsync(result.OutputPath);

        Assert.Contains("\"targetId\":\"AddonItemDetail.GenerateTooltip\"", json, StringComparison.Ordinal);
        Assert.Contains("\"hookInstalled\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_ExportsPerTargetProofRecordsToMarkdown()
    {
        var report = BranchValidationRunReportFactory.CreatePassed();
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var writer = new BranchMarkdownEvidenceWriter(new EvidencePathBuilder());

        var result = await writer.WriteAsync(report, root, CancellationToken.None);
        var markdown = await File.ReadAllTextAsync(result.OutputPath);

        Assert.Contains("`AddonItemDetail.GenerateTooltip`", markdown, StringComparison.Ordinal);
        Assert.Contains("Effect restored: `True`", markdown, StringComparison.Ordinal);
    }

    private static class BranchValidationRunReportFactory
    {
        public static BranchValidationRunReport CreatePassed() => new(
            ValidationRoute.LocalClientStructs,
            requiredProofLevel: 4,
            [
                new ProofGroupRunReport(
                    "TooltipItemDetail:DetourFunction",
                    [
                        new TargetProofRecord("AddonItemDetail.GenerateTooltip", "passed", matchCount: 1, rva: 0x1234, observedHitCount: 1, hookInstalled: true, effectApplied: true, effectRestored: true, blockingReason: null),
                    ],
                    "tooltip group"),
            ]);
    }
}
