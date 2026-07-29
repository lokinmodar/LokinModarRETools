using System.Text.Json.Nodes;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public sealed class OwnerHookSession(
    string targetId,
    IOwnerHook hook,
    IHookMutationStrategy mutationStrategy) : IDisposable
{
    private readonly List<OwnerHookProofRecord> stageRecords = [];

    public string TargetId { get; } = targetId;

    public IReadOnlyList<OwnerHookProofRecord> StageRecords => stageRecords;

    public IOwnerHook Hook { get; } = hook;

    public IHookMutationStrategy MutationStrategy { get; } = mutationStrategy;

    public JsonObject? CapturedContext { get; private set; }

    public void AddStage(OwnerHookProofRecord stage) => stageRecords.Add(stage);

    public void SetCapturedContext(JsonObject context) => CapturedContext = context;

    public OwnerHookScenarioEvidence BuildEvidence() => new(TargetId, stageRecords.ToArray());

    public void Dispose() => Hook.Dispose();
}
