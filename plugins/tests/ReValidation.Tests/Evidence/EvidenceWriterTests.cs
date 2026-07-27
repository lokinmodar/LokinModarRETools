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
        Assert.DoesNotContain(secret, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("\"secret\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writers_ExportSanitizedRouteMetadataAndPhaseProof()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var report = ScenarioRunReport.Started(
                new ValidationScenarioDefinition("tooltip.item-detail", "Tooltip item detail"),
                ValidationRoute.OwnerSignatures,
                ValidationMode.FullProof)
            .WithRouteMetadata(new Dictionary<string, string?>
            {
                ["signature:itemTooltip"] = "matchCount=1;rva=0x1234",
                ["notAllowlisted"] = "must-not-export",
            })
            .WithPrecondition(new ScenarioPreconditionResult(true, null))
            .WithCapture(new ScenarioCapture(
                "item tooltip captured",
                new JsonObject
                {
                    ["detailKind"] = "item",
                    ["resolvedId"] = 5333,
                    ["rawPayload"] = "must-not-export",
                }))
            .WithCompare(new ScenarioCompareResult(true, "item tooltip compared", []))
            .WithOverride(new ScenarioOverrideTicket("sentinel applied", new JsonObject { ["raw"] = "must-not-export" }))
            .WithAssert(new ScenarioAssertResult(true, "sentinel visible", []))
            .WithRestore(new ScenarioRestoreResult(true, "tooltip restored", []))
            .MarkSuccess();
        var context = ScenarioExecutionContext.CreateForTests(
            ValidationRoute.OwnerSignatures,
            ValidationMode.FullProof,
            root);

        var json = await new JsonEvidenceWriter(new EvidencePathBuilder()).WriteAsync(report, context, CancellationToken.None);
        var markdown = await new MarkdownEvidenceWriter(new EvidencePathBuilder()).WriteAsync(report, context, CancellationToken.None);
        var jsonPayload = await File.ReadAllTextAsync(json.OutputPath);
        var markdownPayload = await File.ReadAllTextAsync(markdown.OutputPath);

        Assert.Contains("signature:itemTooltip", jsonPayload, StringComparison.Ordinal);
        Assert.Contains("\"IsMatch\": true", jsonPayload, StringComparison.Ordinal);
        Assert.Contains("\"RestorePassed\": true", jsonPayload, StringComparison.Ordinal);
        Assert.Contains("\"resolvedId\": 5333", jsonPayload, StringComparison.Ordinal);
        Assert.Contains("## Proof", markdownPayload, StringComparison.Ordinal);
        Assert.Contains("Comparison match: `True`", markdownPayload, StringComparison.Ordinal);
        Assert.Contains("signature:itemTooltip", markdownPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-export", jsonPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-export", markdownPayload, StringComparison.Ordinal);
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
