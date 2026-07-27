using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ReValidation.Common.Models;

namespace ReValidation.Common.Evidence;

public sealed record RunEvidenceEnvelope(
    DateTimeOffset Timestamp,
    string RunId,
    string ScenarioId,
    string ScenarioName,
    ValidationRoute Route,
    ValidationMode Mode,
    string Status,
    string? FailedPhase,
    IReadOnlyDictionary<string, string?> RouteMetadata,
    RunProofEvidence Proof,
    IReadOnlyList<EvidenceExportFailure> ExportFailures)
{
    public static RunEvidenceEnvelope From(ScenarioRunReport report, ScenarioExecutionContext context) =>
        new(
            report.Timestamp,
            report.EvidenceRunId,
            report.Scenario.Id,
            report.Scenario.Name,
            context.Route,
            context.Mode,
            report.Status,
            report.FailedPhase,
            EvidenceSanitizer.FilterRouteMetadata(context.Route, report.RouteMetadata),
            RunProofEvidence.From(report),
            report.Evidence
                .Where(evidence => !evidence.IsSuccess)
                .Select(evidence => new EvidenceExportFailure(evidence.Kind, "Evidence export failed."))
                .ToArray());
}

public sealed record RunProofEvidence(
    bool? PreconditionsPassed,
    bool CaptureCompleted,
    JsonObject CaptureMetrics,
    bool ComparisonPerformed,
    bool? IsMatch,
    int ComparisonDifferenceCount,
    bool OverrideAttempted,
    bool OverrideTicketCreated,
    bool? AssertPassed,
    bool RestoreAttempted,
    bool? RestorePassed)
{
    public static RunProofEvidence From(ScenarioRunReport report) =>
        new(
            report.Precondition?.CanRun,
            report.Capture is not null,
            EvidenceSanitizer.FilterCaptureMetrics(report.Scenario.Id, report.Capture?.Data),
            report.CompareResult is not null,
            report.CompareResult?.IsMatch,
            report.CompareResult?.Differences.Count ?? 0,
            report.OverrideAttempted,
            report.OverrideResult is not null,
            report.AssertResult?.Passed,
            report.RestoreResult is not null,
            report.RestoreResult?.Passed);
}

public sealed record EvidenceExportFailure(string Kind, string FailureReason);

internal static partial class EvidenceSanitizer
{
    private static readonly HashSet<string> LocalMetadataKeys =
    [
        "clientStructsBranch",
        "clientStructsCommit",
        "clientStructsDirty",
        "clientStructsAssemblySha256",
        "clientStructsAvailable",
        "clientStructsBlockingReason",
    ];

    public static IReadOnlyDictionary<string, string?> FilterRouteMetadata(
        ValidationRoute route,
        IReadOnlyDictionary<string, string?> metadata)
    {
        var filtered = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in metadata)
        {
            if (route is ValidationRoute.LocalClientStructs && LocalMetadataKeys.Contains(key) && IsSafeLocalValue(key, value))
                filtered[key] = value;
            else if (route is ValidationRoute.OwnerSignatures && IsSafeSignatureMetadata(key, value))
                filtered[key] = value;
        }

        return filtered;
    }

    public static JsonObject FilterCaptureMetrics(string scenarioId, JsonObject? data)
    {
        var metrics = new JsonObject();
        if (data is null)
            return metrics;

        if (string.Equals(scenarioId, "journal.completed-entries", StringComparison.Ordinal))
        {
            if (data["entryCount"] is JsonValue entryCount && entryCount.TryGetValue<int>(out var count) && count >= 0)
                metrics["entryCount"] = count;
        }
        else if (scenarioId is "tooltip.item-detail" or "tooltip.action-detail")
        {
            if (data["detailKind"] is JsonValue detailKind
                && detailKind.TryGetValue<string>(out var kind)
                && kind is "item" or "action")
                metrics["detailKind"] = kind;

            if (data["resolvedId"] is JsonValue resolvedId)
            {
                if (resolvedId.TryGetValue<uint>(out var unsignedId))
                    metrics["resolvedId"] = unsignedId;
                else if (resolvedId.TryGetValue<int>(out var signedId) && signedId >= 0)
                    metrics["resolvedId"] = signedId;
            }
        }

        return metrics;
    }

    private static bool IsSafeLocalValue(string key, string? value)
    {
        if (value is null || value.Length > 256 || value.Contains('\r') || value.Contains('\n'))
            return false;

        return key switch
        {
            "clientStructsDirty" or "clientStructsAvailable" => value is "true" or "false",
            "clientStructsAssemblySha256" => Sha256Regex().IsMatch(value),
            "clientStructsCommit" => CommitRegex().IsMatch(value),
            "clientStructsBranch" => BranchRegex().IsMatch(value),
            "clientStructsBlockingReason" => value is
                "plugins/local/LocalClientStructs.props is missing."
                or "ClientStructsProjectPath could not be resolved.",
            _ => false,
        };
    }

    private static bool IsSafeSignatureMetadata(string key, string? value) =>
        key.StartsWith("signature:", StringComparison.Ordinal)
        && SignatureIdRegex().IsMatch(key["signature:".Length..])
        && value is not null
        && SignatureValueRegex().IsMatch(value);

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex("^[0-9a-fA-F]{7,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitRegex();

    [GeneratedRegex("^[A-Za-z0-9._/-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex BranchRegex();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex SignatureIdRegex();

    [GeneratedRegex("^(?:[0-9]+ matches|matchCount=[0-9]+;rva=0x[0-9A-Fa-f]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SignatureValueRegex();
}
