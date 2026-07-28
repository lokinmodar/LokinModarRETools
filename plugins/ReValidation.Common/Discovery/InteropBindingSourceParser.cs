using System.Text.RegularExpressions;

namespace ReValidation.Common.Discovery;

public sealed partial class InteropBindingSourceParser
{
    public IReadOnlyList<DiscoveredTarget> Parse(string sourceFile, string source, bool changedAgainstBaseRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(source);

        if (!TryGetFamilyMetadata(sourceFile, out var metadata))
            return [];

        var declarations = TypeDeclarationRegex()
            .Matches(source)
            .Cast<Match>()
            .ToArray();
        var bindings = new List<DiscoveredTarget>();

        foreach (Match binding in BindingRegex().Matches(source))
        {
            var declaringType = declarations
                .LastOrDefault(declaration => declaration.Index < binding.Index)
                ?.Groups["type"].Value;
            if (string.IsNullOrWhiteSpace(declaringType))
                continue;

            var bindingKind = binding.Groups["kind"].Value == "StaticAddress"
                ? BindingKind.StaticAddress
                : BindingKind.MemberFunction;
            bindings.Add(new DiscoveredTarget(
                $"{declaringType}.{binding.Groups["member"].Value}",
                declaringType,
                binding.Groups["member"].Value,
                bindingKind,
                binding.Groups["pattern"].Value,
                NormalizePath(sourceFile),
                changedAgainstBaseRef,
                metadata.Family,
                metadata.CueFamily,
                bindingKind == BindingKind.StaticAddress ? ProofProfile.StaticAddressConsumer : ProofProfile.DetourFunction,
                4));
        }

        return bindings;
    }

    private static bool TryGetFamilyMetadata(string sourceFile, out (TargetFamily Family, CueFamily CueFamily) metadata)
    {
        switch (NormalizePath(sourceFile))
        {
            case "FFXIVClientStructs\\FFXIV\\Client\\Game\\UI\\Journal.cs":
                metadata = (TargetFamily.Journal, CueFamily.JournalCompletedList);
                return true;
            case "FFXIVClientStructs\\FFXIV\\Client\\UI\\AddonItemDetail.cs":
                metadata = (TargetFamily.ItemTooltip, CueFamily.TooltipItemDetail);
                return true;
            case "FFXIVClientStructs\\FFXIV\\Client\\UI\\AddonActionDetail.cs":
                metadata = (TargetFamily.ActionTooltip, CueFamily.TooltipActionDetail);
                return true;
            default:
                metadata = default;
                return false;
        }
    }

    private static string NormalizePath(string path) => path.Replace('/', '\\');

    [GeneratedRegex(@"\b(?:partial\s+)?(?:struct|class)\s+(?<type>[A-Za-z_]\w*)\b")]
    private static partial Regex TypeDeclarationRegex();

    [GeneratedRegex(@"\[(?<kind>MemberFunction|StaticAddress)\(\s*""(?<pattern>[^""]+)""[^\)]*\)\]\s*(?:public|internal|private|protected)\s+(?:static\s+)?(?:unsafe\s+)?partial\s+[^\s]+\s+(?<member>[A-Za-z_]\w*)\s*\(")]
    private static partial Regex BindingRegex();
}
