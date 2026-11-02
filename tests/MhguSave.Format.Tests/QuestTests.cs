namespace MhguSave.Format.Tests;

public class QuestCatalogTests
{
    [Fact]
    public void EmbeddedCatalogCoversEveryQuestIndex()
    {
        var catalog = QuestCatalog.LoadEmbedded();

        Assert.Equal(QuestCatalog.QuestCount, catalog.Count);

        var first = catalog.Get(1);
        Assert.Equal("Find the Ferns", first.Name);
        Assert.Equal("Village", first.Category);
        Assert.Equal(0, first.Bit);

        var last = catalog.Get(1508);
        Assert.Equal("Event Arena", last.Category);
        Assert.Equal(1507, last.Bit);
    }

    [Fact]
    public void UnknownIndicesFallBackToANumberedName()
    {
        var quest = QuestCatalog.LoadEmbedded().Get(9999);

        Assert.Equal("Quest 9999", quest.Name);
    }

    [Fact]
    public void CategoriesMatchTheGameBoards()
    {
        var catalog = QuestCatalog.LoadEmbedded();
        var categories = catalog.Quests.Select(quest => quest.Category).Distinct().ToList();

        Assert.Contains("Village", categories);
        Assert.Contains("Hub", categories);
        Assert.Contains("Special Permit", categories);
        Assert.Contains("Training", categories);
        Assert.Contains("Arena", categories);
        Assert.Contains("Event Hub", categories);
        Assert.Contains("Event Arena", categories);
    }
}

public class QuestBitMappingTests
{
    [Fact]
    public void SetQuestClearedMapsIndexOneToBitZeroAndMarksSeen()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];

        slot.SetQuestCleared(1, true);

        Assert.True(slot.IsQuestCleared(1));
        Assert.True(slot.IsQuestSeen(1));
        Assert.Equal(1, slot.ClearedQuests.SetCount());
        Assert.Equal(1, slot.QuestsClearedCount);
    }

    [Fact]
    public void UnclearingLeavesTheSeenFlagAlone()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];
        slot.SetQuestCleared(7, true);

        slot.SetQuestCleared(7, false);

        Assert.False(slot.IsQuestCleared(7));
        Assert.True(slot.IsQuestSeen(7));
    }

    [Fact]
    public void TheLastCatalogIndexIsInsideTheBitmap()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];

        slot.SetQuestCleared(1508, true);

        Assert.True(slot.IsQuestCleared(1508));
        Assert.True(slot.ClearedQuests[1507]);
        Assert.False(slot.ClearedQuests[1508]);
    }

    [Fact]
    public void IndexesOutsideTheBitmapAreRejected()
    {
        var slot = SaveContainer.Parse(SyntheticSave.Create(), "system").Slots[0];

        Assert.Throws<ArgumentOutOfRangeException>(() => slot.SetQuestCleared(0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => slot.IsQuestCleared(1510));
    }
}
