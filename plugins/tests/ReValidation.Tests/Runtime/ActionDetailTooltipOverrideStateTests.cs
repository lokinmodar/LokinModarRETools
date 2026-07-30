using ReValidation.OwnerSignatures.Runtime;
using Xunit;

namespace ReValidation.Tests.Runtime;

public sealed class ActionDetailTooltipOverrideStateTests
{
    [Fact]
    public void TryTakeRestoreText_RejectsRebuiltAddonAndClearsState()
    {
        var state = new ActionDetailTooltipOverrideState();
        state.Commit((nint)0x1000, (nint)0x2000, "Original");

        var matched = state.TryTakeRestoreText((nint)0x3000, (nint)0x4000, out var originalText);

        Assert.False(matched);
        Assert.Null(originalText);
        Assert.False(state.IsActive);
    }

    [Fact]
    public void FailedApply_DoesNotCommitRestoreState()
    {
        var state = new ActionDetailTooltipOverrideState();

        Assert.False(state.IsActive);
        Assert.False(state.TryTakeRestoreText((nint)0x1000, (nint)0x2000, out _));
    }
}
