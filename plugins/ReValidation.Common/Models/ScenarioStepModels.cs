using System.Text.Json.Nodes;

namespace ReValidation.Common.Models;

public sealed record ScenarioPreconditionResult(bool CanRun, string? BlockingReason);

public sealed record ScenarioArmState(bool IsReady, string StatusText);

public sealed record ScenarioCapture(string Summary, JsonObject Data);

public sealed record ScenarioCompareResult(bool IsMatch, string Summary, IReadOnlyList<string> Differences);

public sealed record ScenarioOverrideTicket(string Summary, JsonObject AppliedData);

public sealed record ScenarioAssertResult(bool Passed, string Summary, IReadOnlyList<string> Diagnostics);

public sealed record ScenarioRestoreResult(bool Passed, string Summary, IReadOnlyList<string> Diagnostics);
