using System.Text.Json.Nodes;
using ReValidation.Common.Evidence;
using ReValidation.Common.Models;
using ReValidation.OwnerSignatures.Runtime.Proof;
using Xunit;

namespace ReValidation.Tests.Evidence;

public sealed class OwnerHookEvidenceProjectionTests
{
    [Fact]
    public void RunEvidenceEnvelope_ProjectsExplicitOwnerHookStages()
    {
        var captureData = new JsonObject
        {
            ["ownerHook"] = new JsonObject
            {
                ["targetId"] = "journalProvider",
                ["stages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["stage"] = "SignatureResolved",
                        ["status"] = "Passed",
                        ["summary"] = "Unique journalProvider signature resolved.",
                    },
                    new JsonObject
                    {
                        ["stage"] = "HitObserved",
                        ["status"] = "NotObserved",
                        ["summary"] = "Hook installed but no Journal hit was observed.",
                    },
                },
            },
        };

        var report = ScenarioRunReport.Started(
                new ValidationScenarioDefinition("journal.hook-validation", "Journal Hook Validation"),
                ValidationRoute.OwnerSignatures,
                ValidationMode.CaptureOnly)
            .WithPrecondition(new ScenarioPreconditionResult(true, null))
            .WithCapture(new ScenarioCapture("Journal hook stages captured", captureData))
            .MarkSuccess();
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var envelope = RunEvidenceEnvelope.From(report, context);

        Assert.NotNull(envelope.OwnerHook);
        Assert.Equal("journalProvider", envelope.OwnerHook!.TargetId);
        Assert.Collection(
            envelope.OwnerHook.Stages,
            stage => Assert.Equal(OwnerHookProofStage.SignatureResolved, stage.Stage),
            stage => Assert.Equal(OwnerHookProofStatus.NotObserved, stage.Status));
    }

    [Fact]
    public async Task MarkdownWriter_RendersExplicitOwnerHookStageRows()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var report = ScenarioRunReport.Started(
                new ValidationScenarioDefinition("journal.hook-validation", "Journal Hook Validation"),
                ValidationRoute.OwnerSignatures,
                ValidationMode.CaptureOnly)
            .WithPrecondition(new ScenarioPreconditionResult(true, null))
            .WithCapture(new ScenarioCapture(
                "Journal hook stages captured",
                new JsonObject
                {
                    ["ownerHook"] = new JsonObject
                    {
                        ["targetId"] = "journalProvider",
                        ["stages"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["stage"] = "SignatureResolved",
                                ["status"] = "Passed",
                                ["summary"] = "Unique journalProvider signature resolved.",
                            },
                        },
                    },
                }))
            .MarkSuccess();
        var context = ScenarioExecutionContext.CreateForTests(
            ValidationRoute.OwnerSignatures,
            ValidationMode.CaptureOnly,
            root);

        var output = await new MarkdownEvidenceWriter(new EvidencePathBuilder()).WriteAsync(report, context, CancellationToken.None);
        var markdown = await File.ReadAllTextAsync(output.OutputPath);

        Assert.Contains("## Owner Hook", markdown, StringComparison.Ordinal);
        Assert.Contains("- Target: `journalProvider`", markdown, StringComparison.Ordinal);
        Assert.Contains("| `SignatureResolved` | `Passed` | Unique journalProvider signature resolved. |", markdown, StringComparison.Ordinal);
    }
}
