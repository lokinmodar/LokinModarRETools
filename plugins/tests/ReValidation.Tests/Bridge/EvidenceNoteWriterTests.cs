using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;
using Xunit;

public sealed class EvidenceNoteWriterTests
{
    [Fact]
    public async Task WriteAsync_ExportsPromisingCandidatesAndNextSteps()
    {
        var session = new JournalProbeSession(
            new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero),
            "ReValidation.DynamisBridge/0.1.0",
            4,
            "ffxiv_dx11.exe sha256=example",
            [
                new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root"),
            ],
            [
                new JournalCandidateRecord("provider", (nint)0x2000, "addon", "provider", "SomeProvider", "size=0x80", JournalCandidateClassification.ProviderCacheCandidate, 95, JournalCandidateDisposition.HighValueForIda, "Likely pre-UI container"),
            ]);

        using var temp = new TemporaryDirectory();
        var writer = new EvidenceNoteWriter(TimeProvider.System);

        var outputPath = await writer.WriteAsync(session, temp.Path, CancellationToken.None);
        var markdown = File.ReadAllText(outputPath);

        Assert.Contains("provider", markdown, StringComparison.Ordinal);
        Assert.Contains("HighValueForIda", markdown, StringComparison.Ordinal);
        Assert.Contains("Likely pre-UI container", markdown, StringComparison.Ordinal);
    }
}
