using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs.Services;
using Xunit;

namespace ReValidation.Tests.Routes;

public sealed class JournalSheetSnapshotBuilderTests
{
    [Fact]
    public void Build_IncludesOnlyCompletedNamedEntriesInRowOrder()
    {
        var builder = new JournalSheetSnapshotBuilder();

        var snapshot = builder.Build(
            [
                new JournalSheetEntry(65633, "A Relic Reborn", IsCompleted: false),
                new JournalSheetEntry(65632, "The Company You Keep", IsCompleted: true),
                new JournalSheetEntry(65634, "", IsCompleted: true),
                new JournalSheetEntry(65635, "Lady of the Vortex", IsCompleted: true),
            ],
            source: "local");

        Assert.Equal("2 completed journal entries", snapshot.Summary);
        Assert.Collection(
            snapshot.Entries,
            entry =>
            {
                Assert.Equal(0, entry.GroupKey);
                Assert.Equal(0, entry.EntryIndex);
                Assert.Equal("The Company You Keep", entry.Text);
                Assert.Equal("65632", entry.QuestKey);
                Assert.Equal("local", entry.Source);
            },
            entry =>
            {
                Assert.Equal(0, entry.GroupKey);
                Assert.Equal(1, entry.EntryIndex);
                Assert.Equal("Lady of the Vortex", entry.Text);
                Assert.Equal("65635", entry.QuestKey);
                Assert.Equal("local", entry.Source);
            });
    }
}
