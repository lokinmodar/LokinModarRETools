using ReValidation.Common.Models;

namespace ReValidation.Common.Scenarios;

public sealed record JournalEntryRecord(int GroupKey, int EntryIndex, string Text, string? QuestKey, string Source);

public sealed record JournalCompletedEntriesSnapshot(IReadOnlyList<JournalEntryRecord> Entries, string Summary);

public interface IJournalCompletedEntriesProbe
{
    ValueTask<JournalCompletedEntriesSnapshot> CaptureAsync(CancellationToken cancellationToken);
    ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken);
    ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken);
    ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken);
}
