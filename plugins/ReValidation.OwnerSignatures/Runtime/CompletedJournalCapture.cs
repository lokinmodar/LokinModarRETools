using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Extensions;
using Lumina.Excel.Sheets;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;

namespace ReValidation.OwnerSignatures.Runtime;

public sealed class CompletedJournalCapture(IReadOnlyList<Quest> quests, string source)
    : IJournalCompletedEntriesProbe, IJournalCompletedEntriesComparisonSource
{
    private readonly IReadOnlyList<Quest> quests = quests ?? throw new ArgumentNullException(nameof(quests));
    private readonly string source = string.IsNullOrWhiteSpace(source) ? throw new ArgumentException("Source is required.", nameof(source)) : source;

    public ValueTask<JournalCompletedEntriesSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(CaptureSnapshot());

    public ValueTask<JournalCompletedEntriesSnapshot> CaptureReferenceAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(CaptureSnapshot());

    public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Journal override proof is not configured.");

    public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Journal override proof is not configured.");

    public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Journal override proof is not configured.");

    private JournalCompletedEntriesSnapshot CaptureSnapshot()
    {
        var entries = quests
            .Where(quest => QuestManager.IsQuestComplete(quest.RowId))
            .Select(quest => new { quest.RowId, Name = quest.Name.ExtractText() })
            .Where(quest => !string.IsNullOrWhiteSpace(quest.Name))
            .Select((quest, index) => new JournalEntryRecord(0, index, quest.Name, quest.RowId.ToString(), source))
            .ToArray();

        return new JournalCompletedEntriesSnapshot(entries, $"{entries.Length} completed journal entries");
    }
}
