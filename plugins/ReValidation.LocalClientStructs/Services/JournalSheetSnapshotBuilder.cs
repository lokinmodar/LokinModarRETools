using ReValidation.Common.Scenarios;

namespace ReValidation.LocalClientStructs.Services;

public sealed record JournalSheetEntry(uint QuestId, string Name, bool IsCompleted);

public sealed class JournalSheetSnapshotBuilder
{
    public JournalCompletedEntriesSnapshot Build(IEnumerable<JournalSheetEntry> entries, string source)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var records = entries
            .Where(entry => entry.IsCompleted && !string.IsNullOrWhiteSpace(entry.Name))
            .Select((entry, index) => new JournalEntryRecord(
                GroupKey: 0,
                EntryIndex: index,
                Text: entry.Name,
                QuestKey: entry.QuestId.ToString(),
                Source: source))
            .ToArray();

        return new JournalCompletedEntriesSnapshot(records, $"{records.Length} completed journal entries");
    }
}
