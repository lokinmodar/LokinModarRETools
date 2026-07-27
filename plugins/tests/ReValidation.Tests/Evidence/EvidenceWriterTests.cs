using ReValidation.Common.Evidence;
using ReValidation.Common.Models;
using Xunit;

namespace ReValidation.Tests.Evidence;

public sealed class EvidenceWriterTests
{
    [Fact]
    public async Task Writers_CreateJsonAndMarkdownArtifactsInScenarioFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var report = ScenarioRunReport.CreateForTests(
            scenarioId: "journal.completed-entries",
            route: ValidationRoute.LocalClientStructs,
            mode: ValidationMode.FullProof);

        var context = ScenarioExecutionContext.CreateForTests(
            ValidationRoute.LocalClientStructs,
            ValidationMode.FullProof,
            evidenceRoot: root);

        var jsonWriter = new JsonEvidenceWriter(new EvidencePathBuilder());
        var markdownWriter = new MarkdownEvidenceWriter(new EvidencePathBuilder());

        var json = await jsonWriter.WriteAsync(report, context, CancellationToken.None);
        var markdown = await markdownWriter.WriteAsync(report, context, CancellationToken.None);

        Assert.True(File.Exists(json.OutputPath));
        Assert.True(File.Exists(markdown.OutputPath));
        Assert.EndsWith(".json", json.OutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".md", markdown.OutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine("journal.completed-entries"), json.OutputPath, StringComparison.OrdinalIgnoreCase);
    }
}
