using ReValidation.OwnerSignatures.Runtime.HookTargets;
using Xunit;

namespace ReValidation.Tests.Runtime;

public sealed class OwnerHookTargetRegistryTests
{
    [Fact]
    public void Registry_Get_ThrowsForUnknownTarget()
    {
        var registry = new OwnerHookTargetRegistry([]);

        Assert.Throws<KeyNotFoundException>(() => registry.Get("unknown-target"));
    }
}
