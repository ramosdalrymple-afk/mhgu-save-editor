namespace MhguSave.Format.Tests;

public class BitFieldTests
{
    [Fact]
    public void ClearedQuests_SetAllSetsExactly1509Bits()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];

        slot.ClearedQuests.SetAll();

        Assert.Equal(1509, slot.ClearedQuests.SetCount());
        Assert.Equal(1509, slot.QuestsClearedCount);
        Assert.True(slot.ClearedQuests[0]);
        Assert.True(slot.ClearedQuests[1508]);
        Assert.Throws<ArgumentOutOfRangeException>(() => slot.ClearedQuests[1509]);
    }

    [Fact]
    public void ClearedQuests_SetAllDoesNotTouchTheNextField()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];
        var seen = slot.SeenQuests;

        slot.ClearedQuests.SetAll();

        Assert.Equal(0, seen.SetCount());
        Assert.False(slot.FailedQuests[0]);
    }

    [Fact]
    public void ItemsObtained_SetRangeStopsWhereAsked()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];

        slot.ItemsObtained.SetRange(1, 2990);

        Assert.Equal(2990, slot.ItemsObtained.SetCount());
        Assert.False(slot.ItemsObtained[0]);
        Assert.True(slot.ItemsObtained[2990]);
        Assert.False(slot.ItemsObtained[2991]);
    }

    [Fact]
    public void HunterArts_SkipsTheUnusedIds()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];
        const int arts = 0x2C13;

        slot.UnlockAllHunterArts();

        bool Bit(int id) => (save.Span[slot.BaseOffset + arts + (id >> 3)] >> (id & 7) & 1) != 0;
        Assert.True(Bit(1));
        Assert.True(Bit(70));
        Assert.True(Bit(83));
        Assert.True(Bit(190));
        Assert.False(Bit(71));
        Assert.False(Bit(82));
        Assert.False(Bit(191));
    }

    [Fact]
    public void StarLevels_AreRangeChecked()
    {
        var slot = SaveContainer.Parse(SyntheticSave.Create(), "system").Slots[0];

        slot.VillageStar = 10;
        slot.HubStar = 13;

        Assert.Equal(10, slot.VillageStar);
        Assert.Equal(13, slot.HubStar);
        Assert.Throws<ArgumentOutOfRangeException>(() => slot.VillageStar = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => slot.HubStar = 14);
    }
}

public class ItemCatalogTests
{
    [Fact]
    public void EmbeddedCatalogHasTheGameNames()
    {
        var catalog = ItemCatalog.LoadEmbedded();

        Assert.True(catalog.Count > 2900);
        Assert.Equal("Potion", catalog.NameOf(9));
        Assert.Equal("Raw Meat", catalog.NameOf(45));
        Assert.Equal("Rathian Cortex", catalog.NameOf(1177));
        Assert.Equal("Item 9999", catalog.NameOf(9999));
    }

    [Fact]
    public void CsvCanOverrideNames()
    {
        var path = Path.Combine(Path.GetTempPath(), "items-" + Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllLines(path, ["id,name", "9,Mega Potion", "45,\"Raw, Meat\""]);
        try
        {
            var catalog = ItemCatalog.LoadCsv(path);
            Assert.Equal("Mega Potion", catalog.NameOf(9));
            Assert.Equal("Raw, Meat", catalog.NameOf(45));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class ItemListEditTests
{
    [Fact]
    public void AddPlacesTheItemInTheFirstEmptySlot()
    {
        var box = SaveContainer.Parse(SyntheticSave.Create(), "system").Slots[0].ItemBox;
        box[0] = new ItemSlot(1, 5);

        var index = box.Add(new ItemSlot(9, 99));

        Assert.Equal(1, index);
        Assert.Equal(new ItemSlot(9, 99), box[1]);
    }

    [Fact]
    public void ClearAllEmptiesEverySlot()
    {
        var box = SaveContainer.Parse(SyntheticSave.Create(), "system").Slots[0].ItemBox;
        box[0] = new ItemSlot(1, 5);
        box[2299] = new ItemSlot(2, 7);

        box.ClearAll();

        Assert.Equal(0, box.UsedCount());
        Assert.Equal(0, box.FirstEmptyIndex());
    }
}
