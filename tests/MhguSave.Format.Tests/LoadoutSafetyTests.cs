using System.Buffers.Binary;
using MhguSave.Format;

namespace MhguSave.Format.Tests;

public class LoadoutSafetyTests
{
    private static SaveContainer NewSave() => SaveContainer.Parse(SyntheticSave.Create(), "system");

    [Fact]
    public void PalicoEquipmentWritesItsOwnBoxAndCannotEnterTheHunterBox()
    {
        var save = NewSave();
        var slot = save.Slots[0];
        var before = save.ToArray();
        Assert.Throws<ArgumentException>(() => slot.EquipmentBox.Add(new EquipmentSlot { Type = 22, Id = 1 }));
        Assert.Equal(before, save.ToArray());
        Assert.Equal(0, slot.PalicoEquipmentBox.Add(new EquipmentSlot { Type = 22, Id = 1 }));
        var bytes = save.ToArray();
        Assert.Equal(22, bytes[slot.BaseOffset + 0x17C2E] & 31);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(slot.BaseOffset + 0x17C30)));
        Assert.Equal(before.AsSpan(slot.BaseOffset + 0x62EE, 72000).ToArray(), bytes.AsSpan(slot.BaseOffset + 0x62EE, 72000).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => slot.PalicoEquipmentBox[1000]);
    }

    [Fact]
    public void ReferencedEquipmentCannotBeRemovedReplacedOrReusedButCanBeUpgraded()
    {
        var save = NewSave();
        var slot = save.Slots[0];
        slot.EquipmentBox[8] = new EquipmentSlot { Type = 7, Id = 1 };
        var set = slot.ReadEquipmentSet(false, 0);
        set.Gear[0] = 8;
        slot.WriteEquipmentSet(false, 0, set with { Name = "Hunt" });
        Assert.Contains(slot.EquipmentBox.References(8), s => s.Contains("My Set 1"));
        var before = save.ToArray();
        Assert.Throws<ArgumentException>(() => slot.EquipmentBox[8] = new EquipmentSlot());
        Assert.Throws<ArgumentException>(() => slot.EquipmentBox[8] = new EquipmentSlot { Type = 7, Id = 2 });
        Assert.Equal(before, save.ToArray());
        slot.EquipmentBox[8] = new EquipmentSlot { Type = 7, Id = 1, Level = 2 };
        slot.ClearEquipmentSet(false, 0);
        slot.EquipmentBox[8] = new EquipmentSlot();
        Assert.True(slot.EquipmentBox[8].IsEmpty);
    }

    [Fact]
    public void WornAndPalicoReferencesProtectEmptySlotsAsWell()
    {
        var data = SyntheticSave.Create();
        var start = SyntheticSave.Slot1Pointer + 36;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(start + 0x23B39), 0);
        var cat = start + 0x23BB6;
        data[cat] = (byte)'C';
        data.AsSpan(cat + 256, 14).Fill(255);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(cat + 256), 0);
        var slot = SaveContainer.Parse(data, "system").Slots[0];
        Assert.Equal(1, slot.EquipmentBox.FirstEmptyIndex());
        Assert.Equal(1, slot.PalicoEquipmentBox.FirstEmptyIndex());
        Assert.Single(slot.PalicoEquipmentBox.References(0));
    }

    [Fact]
    public void SetWriteUsesLoaderBoundaryCopiesDecorationsAndPreservesNeighbors()
    {
        var save = NewSave();
        var slot = save.Slots[0];
        slot.EquipmentBox[3] = new EquipmentSlot { Type = 1, Id = 446, Deco1 = 2638 };
        var data = slot.ReadEquipmentSet(false, 39);
        data.Gear[1] = 3;
        data.Pigment[0] = 0xFF332211;
        var before = save.ToArray();
        slot.WriteEquipmentSet(false, 39, data with { Name = "Armor", Style = 5, Arts = [179, 0, 0], SpArts = 1 });
        var bytes = save.ToArray();
        var at = slot.BaseOffset + 0x208CE + 39 * 136;
        Assert.Equal((byte)'A', bytes[at]);
        Assert.Equal(3, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at + 44)));
        Assert.Equal(2638, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at + 62)));
        Assert.Equal(0xFF332211u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at + 100)));
        Assert.Equal(5, bytes[at + 130]);
        Assert.Equal(179, bytes[at + 131]);
        Assert.Equal(before.AsSpan(0, at).ToArray(), bytes.AsSpan(0, at).ToArray());
        Assert.Equal(before.AsSpan(at + 136).ToArray(), bytes.AsSpan(at + 136).ToArray());
    }

    [Fact]
    public void InvalidSetDoesNotPartiallyChangeItsNameOrReferences()
    {
        var save = NewSave();
        var slot = save.Slots[0];
        var draft = slot.ReadEquipmentSet(false, 0);
        draft.Gear[0] = 20;
        var before = save.ToArray();
        Assert.Throws<ArgumentException>(() => slot.WriteEquipmentSet(false, 0, draft with { Name = "Changed" }));
        Assert.Equal(before, save.ToArray());
        Assert.Throws<ArgumentException>(() => slot.WriteEquipmentSet(false, 0,
            slot.ReadEquipmentSet(false, 0) with { Name = "ElevenChars!" }));
        Assert.Equal(before, save.ToArray());
    }
}
