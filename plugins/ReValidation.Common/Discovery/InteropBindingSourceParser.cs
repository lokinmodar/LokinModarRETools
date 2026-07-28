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
            .Select(declaration => new TypeScope(
                declaration.Groups["type"].Value,
                FindOpeningBrace(source, declaration.Index + declaration.Length)))
            .Select(scope => scope with { ClosingBrace = FindClosingBrace(source, scope.OpeningBrace) })
            .Where(scope => scope.OpeningBrace >= 0 && scope.ClosingBrace >= 0)
            .ToArray();
        var bindings = new List<DiscoveredTarget>();

        foreach (Match binding in BindingRegex().Matches(source))
        {
            var declaringType = declarations
                .Where(declaration => declaration.OpeningBrace < binding.Index && binding.Index < declaration.ClosingBrace)
                .OrderByDescending(declaration => declaration.OpeningBrace)
                .Select(declaration => declaration.Name)
                .FirstOrDefault();
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
                metadata.RequiredProofLevel));
        }

        return bindings;
    }

    private static bool TryGetFamilyMetadata(string sourceFile, out (TargetFamily Family, CueFamily CueFamily, int RequiredProofLevel) metadata)
    {
        switch (NormalizePath(sourceFile))
        {
            case "FFXIVClientStructs\\FFXIV\\Client\\Game\\UI\\Journal.cs":
                metadata = (TargetFamily.Journal, CueFamily.JournalCompletedList, 3);
                return true;
            case "FFXIVClientStructs\\FFXIV\\Client\\UI\\AddonItemDetail.cs":
                metadata = (TargetFamily.ItemTooltip, CueFamily.TooltipItemDetail, 4);
                return true;
            case "FFXIVClientStructs\\FFXIV\\Client\\UI\\AddonActionDetail.cs":
                metadata = (TargetFamily.ActionTooltip, CueFamily.TooltipActionDetail, 4);
                return true;
            default:
                metadata = default;
                return false;
        }
    }

    private static string NormalizePath(string path) => path.Replace('/', '\\');

    private static int FindOpeningBrace(string source, int startIndex) => source.IndexOf('{', startIndex);

    private static int FindClosingBrace(string source, int openingBrace)
    {
        if (openingBrace < 0)
            return -1;

        var depth = 0;
        for (var index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return index;
        }

        return -1;
    }

    private sealed record TypeScope(string Name, int OpeningBrace, int ClosingBrace = -1);

    [GeneratedRegex(@"\b(?:partial\s+)?(?:struct|class)\s+(?<type>[A-Za-z_]\w*)\b")]
    private static partial Regex TypeDeclarationRegex();

    [GeneratedRegex(@"\[(?<kind>MemberFunction|StaticAddress)\(\s*""(?<pattern>[^""]+)""[^\)]*\)\]\s*(?:public|internal|private|protected)\s+(?:static\s+)?(?:unsafe\s+)?partial\s+[^\s]+\s+(?<member>[A-Za-z_]\w*)\s*\(")]
    private static partial Regex BindingRegex();
}
