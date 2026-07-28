using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public sealed class JournalCandidateRanker
{
    public IReadOnlyList<JournalCandidateRecord> Rank(IReadOnlyList<JournalCandidateSeed> seeds) =>
        seeds
            .Select(seed => new JournalCandidateRecord(
                seed.CandidateId,
                seed.Address,
                seed.AnchorId,
                seed.LogicalRoleGuess,
                seed.ClassName,
                seed.RegionSummary,
                Classify(seed),
                Score(seed),
                JournalCandidateDisposition.None,
                BuildNotes(seed)))
            .OrderByDescending(candidate => candidate.Confidence)
            .ToArray();

    private static JournalCandidateClassification Classify(JournalCandidateSeed seed)
    {
        if (seed.ClassName?.Contains("TextNode", StringComparison.OrdinalIgnoreCase) == true)
            return JournalCandidateClassification.StringBearingCandidate;
        if (seed.ClassName?.Contains("Agent", StringComparison.OrdinalIgnoreCase) == true)
            return JournalCandidateClassification.AgentState;
        if (seed.ChildPointerCount >= 3)
            return JournalCandidateClassification.ProviderCacheCandidate;
        if (seed.NearbyStringSample is { Length: > 0 })
            return JournalCandidateClassification.StringBearingCandidate;
        return JournalCandidateClassification.Unknown;
    }

    private static int Score(JournalCandidateSeed seed)
    {
        var score = 10;
        if (!seed.LooksLikeLeafTextNode) score += 20;
        if (seed.ChildPointerCount >= 3) score += 40;
        if (!string.IsNullOrWhiteSpace(seed.ClassName)) score += 20;
        if (!string.IsNullOrWhiteSpace(seed.NearbyStringSample)) score += 10;
        return score;
    }

    private static string BuildNotes(JournalCandidateSeed seed) =>
        seed.LooksLikeLeafTextNode
            ? "Looks like a final UI leaf."
            : $"Derived from anchor '{seed.AnchorId}' with {seed.ChildPointerCount} child pointer candidates.";
}
