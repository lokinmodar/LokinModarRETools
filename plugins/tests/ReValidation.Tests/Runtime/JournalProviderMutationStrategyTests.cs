using System.Text.Json.Nodes;
using ReValidation.OwnerSignatures.Runtime;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;
using Xunit;

namespace ReValidation.Tests.Runtime;

public sealed class JournalProviderMutationStrategyTests
{
    [Fact]
    public async Task AssertAsync_ReturnsEffectNotProven_WhenMutationCannotProveSemanticEffect()
    {
        var strategy = new JournalProviderMutationStrategy();
        using var session = new OwnerHookSession(
            JournalHookTargetIds.JournalProvider,
            new FakeOwnerHook(),
            strategy);
        session.SetCapturedContext(new JsonObject { ["questId"] = 42 });

        var ticket = await strategy.ApplyAsync(session, CancellationToken.None);
        var assert = await strategy.AssertAsync(session, CancellationToken.None);

        Assert.NotNull(ticket);
        Assert.False(assert!.Passed);
        Assert.Contains("effect_not_proven", assert.Diagnostics, StringComparer.Ordinal);
    }

    private sealed class FakeOwnerHook : IOwnerHook
    {
        public int ObservedHitCount => 0;

        public IReadOnlyList<JsonObject> DrainObservedContexts() => [];

        public void Enable()
        {
        }

        public void Dispose()
        {
        }
    }
}
