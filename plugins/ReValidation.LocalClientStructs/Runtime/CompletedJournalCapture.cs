using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Extensions;
using Lumina.Excel.Sheets;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs.Services;

namespace ReValidation.LocalClientStructs.Runtime;

public sealed class CompletedJournalCapture(IReadOnlyList<Quest> quests, JournalSheetSnapshotBuilder builder, string source)
    : IJournalCompletedEntriesProbe, IJournalCompletedEntriesComparisonSource
{
    private readonly IReadOnlyList<Quest> quests = quests ?? throw new ArgumentNullException(nameof(quests));
    private readonly JournalSheetSnapshotBuilder builder = builder ?? throw new ArgumentNullException(nameof(builder));
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
        var entries = quests.Select(quest => new JournalSheetEntry(
            quest.RowId,
            quest.Name.ExtractText(),
            QuestManager.IsQuestComplete(quest.RowId)));

        return builder.Build(entries, source);
    }
}
