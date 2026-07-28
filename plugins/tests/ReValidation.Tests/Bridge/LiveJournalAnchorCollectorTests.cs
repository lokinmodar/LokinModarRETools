using ReValidation.DynamisBridge.Services;
using Xunit;

public sealed class LiveJournalAnchorCollectorTests
{
    [Fact]
    public void CaptureAnchors_IncludesJournalAddonAndQuestJournalAgent()
    {
        var collector = new LiveJournalAnchorCollector(
            () => (nint)0x1000,
            () => (nint)0x2000);

        var anchors = collector.CaptureAnchors();

        Assert.Collection(
            anchors,
            addon =>
            {
                Assert.Equal("journal-addon", addon.AnchorId);
                Assert.Equal((nint)0x1000, addon.Address);
                Assert.Equal("UI root", addon.Role);
            },
            agent =>
            {
                Assert.Equal("journal-agent", agent.AnchorId);
                Assert.Equal((nint)0x2000, agent.Address);
                Assert.Equal("Agent state", agent.Role);
            });
    }
}
