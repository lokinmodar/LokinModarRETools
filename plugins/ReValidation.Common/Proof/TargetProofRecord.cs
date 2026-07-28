namespace ReValidation.Common.Proof;

public sealed record TargetProofRecord
{
    public TargetProofRecord(
        string TargetId,
        string Verdict,
        int matchCount,
        long? rva,
        int observedHitCount,
        bool hookInstalled,
        bool effectApplied,
        bool effectRestored,
        string? blockingReason)
    {
        this.TargetId = TargetId;
        this.Verdict = Verdict;
        MatchCount = matchCount;
        Rva = rva;
        ObservedHitCount = observedHitCount;
        HookInstalled = hookInstalled;
        EffectApplied = effectApplied;
        EffectRestored = effectRestored;
        BlockingReason = blockingReason;
    }

    public string TargetId { get; }
    public string Verdict { get; }
    public int MatchCount { get; }
    public long? Rva { get; }
    public int ObservedHitCount { get; }
    public bool HookInstalled { get; }
    public bool EffectApplied { get; }
    public bool EffectRestored { get; }
    public string? BlockingReason { get; }
}
