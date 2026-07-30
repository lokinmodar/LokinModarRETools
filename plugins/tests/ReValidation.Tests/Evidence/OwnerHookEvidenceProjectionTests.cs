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

    [Fact]
    public void RunEvidenceEnvelope_DoesNotProjectOwnerHookForLocalRoute()
    {
        var report = CreateReport(new JsonObject
        {
            ["ownerHook"] = CreateOwnerHook("journalProvider", CreateStage("SignatureResolved", "Passed", "Unique owner signature resolved.")),
        });
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.LocalClientStructs, ValidationMode.CaptureOnly);

        var envelope = RunEvidenceEnvelope.From(report, context);

        Assert.Null(envelope.OwnerHook);
    }

    [Fact]
    public void RunEvidenceEnvelope_ProjectsOnlyAllowlistedOwnerHookData()
    {
        var report = CreateReport(new JsonObject
        {
            ["ownerHook"] = CreateOwnerHook(
                "journalProvider",
                CreateStage(
                    "HitObserved",
                    "Passed",
                    "Owner hook observed | runtime hits.",
                    new JsonObject
                    {
                        ["observedHitCount"] = 2,
                        ["secret"] = "must-not-export",
                    })),
        });
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var envelope = RunEvidenceEnvelope.From(report, context);

        var stage = Assert.Single(envelope.OwnerHook!.Stages);
        Assert.Equal(2, stage.Data["observedHitCount"]!.GetValue<int>());
        Assert.DoesNotContain("secret", stage.Data);
        Assert.Equal("Owner hook observed | runtime hits.", stage.Summary);
    }

    [Fact]
    public void RunEvidenceEnvelope_PreservesAllowlistedCapturedContext()
    {
        var report = CreateReport(new JsonObject
        {
            ["ownerHook"] = CreateOwnerHook(
                "journalProvider",
                CreateStage(
                    "ContextCaptured",
                    "Passed",
                    "Owner hook context captured.",
                    new JsonObject
                    {
                        ["questId"] = 42,
                        ["detailKind"] = "item",
                        ["secret"] = "must-not-export",
                    })),
        });
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var envelope = RunEvidenceEnvelope.From(report, context);

        var stage = Assert.Single(envelope.OwnerHook!.Stages);
        Assert.Equal(42, stage.Data["questId"]!.GetValue<int>());
        Assert.Equal("item", stage.Data["detailKind"]!.GetValue<string>());
        Assert.DoesNotContain("secret", stage.Data);
    }

    [Fact]
    public void RunEvidenceEnvelope_RejectsMalformedOwnerHookStages()
    {
        var report = CreateReport(new JsonObject
        {
            ["ownerHook"] = CreateOwnerHook(
                "journalProvider",
                CreateStage("SignatureResolved", "Passed", "Unique owner signature resolved."),
                CreateStage("UnknownStage", "Passed", "Unknown stage.")),
        });
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var envelope = RunEvidenceEnvelope.From(report, context);

        Assert.Null(envelope.OwnerHook);
    }

    [Theory]
    [InlineData("journal provider", "Unique owner signature resolved.")]
    [InlineData("journalProvider", "Unsafe\nsummary")]
    public void RunEvidenceEnvelope_RejectsUnsafeOwnerHookText(string targetId, string summary)
    {
        var report = CreateReport(new JsonObject
        {
            ["ownerHook"] = CreateOwnerHook(targetId, CreateStage("SignatureResolved", "Passed", summary)),
        });
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var envelope = RunEvidenceEnvelope.From(report, context);

        Assert.Null(envelope.OwnerHook);
    }

    [Fact]
    public async Task MarkdownWriter_EscapesOwnerHookSummaryCells()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var report = CreateReport(new JsonObject
        {
            ["ownerHook"] = CreateOwnerHook(
                "journalProvider",
                CreateStage("HitObserved", "Passed", "Owner hook observed | runtime hits.")),
        });
        var context = ScenarioExecutionContext.CreateForTests(
            ValidationRoute.OwnerSignatures,
            ValidationMode.CaptureOnly,
            root);

        var output = await new MarkdownEvidenceWriter(new EvidencePathBuilder()).WriteAsync(report, context, CancellationToken.None);
        var markdown = await File.ReadAllTextAsync(output.OutputPath);

        Assert.Contains("| `HitObserved` | `Passed` | Owner hook observed \\| runtime hits. |", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarkdownWriter_RendersSanitizedOwnerHookStageData()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var report = CreateReport(new JsonObject
        {
            ["ownerHook"] = CreateOwnerHook(
                "journalProvider",
                CreateStage(
                    "ContextCaptured",
                    "Passed",
                    "Owner hook context captured.",
                    new JsonObject { ["questId"] = 42 })),
        });
        var context = ScenarioExecutionContext.CreateForTests(
            ValidationRoute.OwnerSignatures,
            ValidationMode.CaptureOnly,
            root);

        var output = await new MarkdownEvidenceWriter(new EvidencePathBuilder()).WriteAsync(report, context, CancellationToken.None);
        var markdown = await File.ReadAllTextAsync(output.OutputPath);

        Assert.Contains("| Stage | Status | Summary | Data |", markdown, StringComparison.Ordinal);
        Assert.Contains("`questId=42`", markdown, StringComparison.Ordinal);
    }

    private static ScenarioRunReport CreateReport(JsonObject captureData) =>
        ScenarioRunReport.Started(
                new ValidationScenarioDefinition("journal.hook-validation", "Journal Hook Validation"),
                ValidationRoute.OwnerSignatures,
                ValidationMode.CaptureOnly)
            .WithPrecondition(new ScenarioPreconditionResult(true, null))
            .WithCapture(new ScenarioCapture("Journal hook stages captured", captureData))
            .MarkSuccess();

    private static JsonObject CreateOwnerHook(string targetId, params JsonObject[] stages) => new()
    {
        ["targetId"] = targetId,
        ["stages"] = new JsonArray(stages),
    };

    private static JsonObject CreateStage(string stage, string status, string summary, JsonObject? data = null) => new()
    {
        ["stage"] = stage,
        ["status"] = status,
        ["summary"] = summary,
        ["data"] = data,
    };
}
