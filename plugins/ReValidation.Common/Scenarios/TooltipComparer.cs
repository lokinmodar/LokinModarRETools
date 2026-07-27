using ReValidation.Common.Models;

namespace ReValidation.Common.Scenarios;

public static class TooltipComparer
{
    public static ScenarioCompareResult Compare(TooltipSnapshot left, TooltipSnapshot right)
    {
        var differences = new List<string>();

        if (!string.Equals(left.DetailKind, right.DetailKind, StringComparison.Ordinal))
            differences.Add($"DetailKind mismatch: '{left.DetailKind}' vs '{right.DetailKind}'.");

        if (left.ResolvedId != right.ResolvedId)
            differences.Add($"ResolvedId mismatch: {left.ResolvedId} vs {right.ResolvedId}.");

        if (left.PayloadLines.Count != right.PayloadLines.Count)
            differences.Add($"Payload line count mismatch: {left.PayloadLines.Count} vs {right.PayloadLines.Count}.");

        for (var i = 0; i < Math.Min(left.PayloadLines.Count, right.PayloadLines.Count); i++)
        {
            if (!string.Equals(left.PayloadLines[i], right.PayloadLines[i], StringComparison.Ordinal))
                differences.Add($"Payload line {i} mismatch: '{left.PayloadLines[i]}' vs '{right.PayloadLines[i]}'.");
        }

        if (!string.Equals(left.VisibleText, right.VisibleText, StringComparison.Ordinal))
            differences.Add($"Visible text mismatch: '{left.VisibleText}' vs '{right.VisibleText}'.");

        return new ScenarioCompareResult(differences.Count == 0, $"{left.DetailKind} tooltip compared", differences);
    }
}
