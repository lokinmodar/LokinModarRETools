using ReValidation.Common.Evidence;
using ReValidation.Common.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        using var payload = JsonDocument.Parse(await File.ReadAllTextAsync(json.OutputPath));
        Assert.Equal("journal.completed-entries", payload.RootElement.GetProperty("ScenarioId").GetString());
        Assert.Contains("## Verdict", await File.ReadAllTextAsync(markdown.OutputPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonWriter_ExportsOnlyAllowlistedEvidenceFields()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var secret = "capture-secret";
        var report = ScenarioRunReport.Started(
                new ValidationScenarioDefinition("journal.completed-entries", "Journal completed entries"),
                ValidationRoute.LocalClientStructs,
                ValidationMode.FullProof)
            .WithPrecondition(new ScenarioPreconditionResult(true, null))
            .WithCapture(new ScenarioCapture("capture", new JsonObject { ["secret"] = secret }))
            .MarkSuccess();
        var context = new ScenarioExecutionContext(
            ValidationRoute.LocalClientStructs,
            ValidationMode.FullProof,
            new Dictionary<string, string?> { ["secret"] = "metadata-secret" },
            root);

        var evidence = await new JsonEvidenceWriter(new EvidencePathBuilder()).WriteAsync(report, context, CancellationToken.None);
        var payload = await File.ReadAllTextAsync(evidence.OutputPath);

        Assert.DoesNotContain(secret, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("metadata-secret", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Capture", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Metadata", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void PathBuilder_RejectsScenarioIdsThatCouldEscapeEvidenceRoot()
    {
        var paths = new EvidencePathBuilder();
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.Throws<ArgumentException>(() => paths.BuildPath(root, "..", ValidationRoute.LocalClientStructs, DateTimeOffset.UtcNow, "json"));
        Assert.Throws<ArgumentException>(() => paths.BuildPath(root, "nested\\scenario", ValidationRoute.LocalClientStructs, DateTimeOffset.UtcNow, "json"));
        Assert.Throws<ArgumentException>(() => paths.BuildPath(root, Path.GetFullPath(root), ValidationRoute.LocalClientStructs, DateTimeOffset.UtcNow, "json"));
    }

    [Fact]
    public async Task Writers_UseSharedRunIdWithoutOverwritingSameSecondArtifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.LocalClientStructs, ValidationMode.FullProof, root);
        var firstReport = ScenarioRunReport.CreateForTests("journal.completed-entries", ValidationRoute.LocalClientStructs, ValidationMode.FullProof);
        var secondReport = ScenarioRunReport.CreateForTests("journal.completed-entries", ValidationRoute.LocalClientStructs, ValidationMode.FullProof);
        var paths = new EvidencePathBuilder();
        var jsonWriter = new JsonEvidenceWriter(paths);
        var markdownWriter = new MarkdownEvidenceWriter(paths);

        var firstJson = await jsonWriter.WriteAsync(firstReport, context, CancellationToken.None);
        var firstMarkdown = await markdownWriter.WriteAsync(firstReport, context, CancellationToken.None);
        var secondJson = await jsonWriter.WriteAsync(secondReport, context, CancellationToken.None);

        Assert.NotEqual(firstJson.OutputPath, secondJson.OutputPath);
        Assert.Equal(
            Path.GetFileNameWithoutExtension(firstJson.OutputPath),
            Path.GetFileNameWithoutExtension(firstMarkdown.OutputPath));
    }
}
