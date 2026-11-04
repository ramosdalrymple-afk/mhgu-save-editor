namespace MhguSave.Format.Tests;

public class InventoryExchangeTests
{
    [Fact]
    public void CsvImportHandlesQuotedNamesAndRejectsBadRowsBeforeReplacingInventory()
    {
        var slot = SaveContainer.Parse(SyntheticSave.Create(), "system").Slots[0];
        slot.ItemBox[0] = new(9, 30);
        var text = InventoryCsv.Export(slot.ItemBox, ItemCatalog.LoadEmbedded());
        var data = InventoryCsv.Parse(text, 2300);
        Assert.Equal(new ItemSlot(9, 30), data[0]);
        Assert.Equal(2300, data.Length);
        Assert.Equal(new ItemSlot(347, 99), InventoryCsv.Parse("slot,id,name,count\r\n1,347,\"Name, with \"\"quotes\"\"\",99", 2300)[0]);
        Assert.Throws<ArgumentException>(() => InventoryCsv.Parse("slot,id,name,count\n1,9,Potion,10\n1,9,Potion,20", 2300));
        Assert.Throws<ArgumentException>(() => InventoryCsv.Parse("slot,id,name,count\n1,9,Potion,128", 2300));
        Assert.Throws<ArgumentException>(() => InventoryCsv.Parse("slot,id,name,count\n1,9,\"unfinished,10", 2300));
        Assert.Equal(new ItemSlot(9, 30), slot.ItemBox[0]);
    }

    [Fact]
    public void MergePreservesQuantitiesAndSortKeepsStacksIntact()
    {
        var box = SaveContainer.Parse(SyntheticSave.Create(), "system").Slots[0].ItemBox;
        box[0] = new(347, 80); box[4] = new(9, 4); box[7] = new(347, 100);
        box.MergeStacks();
        Assert.Equal(new ItemSlot(347, 99), box[0]);
        Assert.Equal(new ItemSlot(347, 81), box[1]);
        box.SortById();
        Assert.Equal(new ItemSlot(9, 4), box[0]);
        Assert.Equal(180, box.UsedIndexes().Select(i => box[i]).Where(i => i.Id == 347).Sum(i => i.Count));
    }

    [Fact]
    public void AppearanceUpdatesTheThreeCopiesAndPreservesUnchangedUnknownFields()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];
        var before = save.ToArray();
        slot.WriteAppearance(slot.ReadAppearance() with { Face = 7, Hair = 19, SkinColor = 0xFF332211 });
        var expected = (byte[])before.Clone();
        foreach (var at in new[] { 0x23B47, 0x240, 0xC71D5 })
        {
            expected[slot.BaseOffset + at + 2] = 7;
            expected[slot.BaseOffset + at + 6] = 19;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(slot.BaseOffset + at + 32), 0xFF332211);
        }
        Assert.Equal(expected, save.ToArray());
        var snapshot = save.ToArray();
        Assert.Throws<ArgumentException>(() => slot.WriteAppearance(slot.ReadAppearance() with { Face = 99 }));
        Assert.Equal(snapshot, save.ToArray());
    }
}
