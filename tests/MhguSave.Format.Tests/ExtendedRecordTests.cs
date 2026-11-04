using System.Buffers.Binary;
using System.Text;

namespace MhguSave.Format.Tests;

public class ExtendedRecordTests
{
    [Fact]
    public void QuestCounterIsFourBytesAndPlaytimeUpdatesAllThreeCopies()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];
        slot.QuestCounter = 1234567;
        slot.PlayTimeSeconds = 987654;
        var bytes = save.ToArray();
        Assert.Equal(1234567u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(slot.BaseOffset + 0x5E4E)));
        foreach (var offset in new[] { 0x20, 0x2248B, 0xC7AD1 })
            Assert.Equal(987654u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(slot.BaseOffset + offset)));
    }

    [Fact]
    public void AwardEditsUpdateBothMapsAndKeepAdjacentBits()
    {
        var raw = SyntheticSave.Create();
        var start = 36 + SyntheticSave.Slot1Pointer;
        raw[start + 0x3157 + 16] = 0xA0;
        raw[start + 0xC8115 + 16] = 0x50;
        var save = SaveContainer.Parse(raw, "system");
        save.Slots[0].Awards.SetAll();
        var bytes = save.ToArray();
        Assert.Equal(0xAF, bytes[start + 0x3157 + 16]);
        Assert.Equal(0x5F, bytes[start + 0xC8115 + 16]);
        save.Slots[0].Awards.ClearAll();
        bytes = save.ToArray();
        Assert.Equal(0xA0, bytes[start + 0x3157 + 16]);
        Assert.Equal(0x50, bytes[start + 0xC8115 + 16]);
        save.Slots[0].Awards[7] = true;
        Assert.Equal(0x80, save.Span[start + 0x3157]);
        Assert.Equal(0x80, save.Span[start + 0xC8115]);
    }

    [Fact]
    public void PalicoIdentityChangesLeaveAbilitiesEquipmentAndOtherRecordsUntouched()
    {
        var raw = SyntheticSave.Create();
        var start = 36 + SyntheticSave.Slot1Pointer;
        var cat = start + 0x23BB6 + 83 * 324;
        Encoding.UTF8.GetBytes("Cat").CopyTo(raw.AsSpan(cat));
        raw[cat + 37] = 1;
        raw[cat + 40] = 12;
        raw[cat + 256] = 23;
        raw[cat + 310] = 0x91;
        var save = SaveContainer.Parse(raw, "system");
        var before = save.ToArray();
        save.Slots[0].WritePalicoIdentity(83, "Miso", 12345, 55, 1, 3, "Ready!", "Hunter");
        var actual = save.ToArray();
        Assert.Equal(before.AsSpan(0, cat).ToArray(), actual.AsSpan(0, cat).ToArray());
        Assert.Equal(before.AsSpan(cat + 324).ToArray(), actual.AsSpan(cat + 324).ToArray());
        Assert.Equal(before.AsSpan(cat + 40, 56).ToArray(), actual.AsSpan(cat + 40, 56).ToArray());
        Assert.Equal(before.AsSpan(cat + 188, 136).ToArray(), actual.AsSpan(cat + 188, 136).ToArray());
        var data = save.Slots[0].ReadPalico(83);
        Assert.Equal("Miso", data.Name);
        Assert.Equal(55, data.Level);
        Assert.Equal(12345u, data.Experience);
        var snapshot = save.ToArray();
        Assert.Throws<ArgumentException>(() => save.Slots[0].WritePalicoIdentity(83, "Bad", 0, 1, 2, 3, "", ""));
        Assert.Equal(snapshot, save.ToArray());
    }

    [Fact]
    public void MonsterArrayEdgesNeverWriteSizeWordsOrEquipment()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var before = save.ToArray();
        var slot = save.Slots[0];
        slot.WriteMonsterCounts(1, 34, 5);
        slot.WriteMonsterCounts(137, 7, 8);
        Assert.Equal(39, slot.ReadMonster(1).Hunted);
        Assert.Equal((ushort)8, slot.ReadMonster(137).Captured);
        var after = save.ToArray();
        var expected = (byte[])before.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(slot.BaseOffset + 0x5EA6), 34);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(slot.BaseOffset + 0x5FB8), 5);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(slot.BaseOffset + 0x5FB6), 7);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(slot.BaseOffset + 0x60C8), 8);
        Assert.Equal(expected, after);
        Assert.Throws<ArgumentOutOfRangeException>(() => slot.WriteMonsterCounts(0, 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => slot.ReadMonster(138));
    }
}
