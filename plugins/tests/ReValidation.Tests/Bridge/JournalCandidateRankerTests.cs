using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;
using Xunit;

public sealed class JournalCandidateRankerTests
{
    [Fact]
    public void Rank_PrioritizesProviderLikeObjectsAboveLeafTextNodes()
    {
        var ranker = new JournalCandidateRanker();
        var ranked = ranker.Rank(
        [
            new JournalCandidateSeed("provider", (nint)0x1000, "addon", "candidate", "SomeProvider", "size=0x80", null, false, 4),
            new JournalCandidateSeed("text-node", (nint)0x2000, "addon", "candidate", "AtkTextNode", "size=0x30", "The Company You Keep", true, 0),
        ]);

        Assert.Equal("provider", ranked[0].CandidateId);
        Assert.Equal(JournalCandidateClassification.ProviderCacheCandidate, ranked[0].Classification);
        Assert.Equal(JournalCandidateClassification.StringBearingCandidate, ranked[1].Classification);
    }
}
