using ReValidation.Common.Models;

namespace ReValidation.Common.Scenarios;

public static class JournalCompletedEntriesComparer
{
    public static ScenarioCompareResult Compare(JournalCompletedEntriesSnapshot local, JournalCompletedEntriesSnapshot owner)
    {
        var differences = new List<string>();
        if (local.Entries.Count != owner.Entries.Count)
            differences.Add($"Entry count mismatch: {local.Entries.Count} vs {owner.Entries.Count}.");

        var pairs = local.Entries.Zip(owner.Entries, (left, right) => (left, right)).ToArray();

        foreach (var (left, right) in pairs)
        {
            if (!string.Equals(left.Text, right.Text, StringComparison.Ordinal))
                differences.Add($"Text mismatch at group {left.GroupKey} index {left.EntryIndex}: '{left.Text}' vs '{right.Text}'.");
        }

        return new ScenarioCompareResult(differences.Count == 0, $"{pairs.Length} entries compared", differences);
    }
}
