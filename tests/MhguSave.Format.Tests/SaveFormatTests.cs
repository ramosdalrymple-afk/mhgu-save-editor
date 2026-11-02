using System.Buffers.Binary;
using System.Text;

namespace MhguSave.Format.Tests;

public class SaveContainerTests
{
    [Fact]
    public void JamCrc_MatchesTheContainerKeysWrittenByTheGame()
    {
        Assert.Equal(0x36B2EE74u, JamCrc.Compute("system"));
        Assert.Equal(0xD0819B92u, JamCrc.Compute("system_backup"));
    }

    [Fact]
    public void Parse_AcceptsBothFileNames()
    {
        Assert.Equal("system", SaveContainer.Parse(SyntheticSave.Create("system"), "system").EntryName);
        Assert.Equal("system_backup",
            SaveContainer.Parse(SyntheticSave.Create("system_backup"), "system_backup").EntryName);
    }

    [Fact]
    public void Parse_RejectsAnEntryKeyFromTheOtherFileName()
    {
        var backup = SyntheticSave.Create("system_backup");
        var error = Assert.Throws<SaveFormatException>(() => SaveContainer.Parse(backup, "system"));
        Assert.Contains("entry key", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_RejectsTheWrongFileSize()
    {
        var shortFile = new byte[1024];
        Assert.Throws<SaveFormatException>(() => SaveContainer.Parse(shortFile, "system"));
    }

    [Fact]
    public void Parse_RejectsAnUnknownBodyVersion()
    {
        var data = SyntheticSave.Create();
        SyntheticSave.WriteU32(data, 0x24, 0xC7);
        Assert.Throws<SaveFormatException>(() => SaveContainer.Parse(data, "system"));
    }

    [Fact]
    public void Slots_ResolveThroughThePointerTable()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(usedSlots: 0b101), "system");

        Assert.Equal(SaveContainer.HeaderSize + SyntheticSave.Slot1Pointer, save.Slots[0].BaseOffset);
        Assert.Equal(SaveContainer.HeaderSize + SyntheticSave.Slot3Pointer, save.Slots[2].BaseOffset);
        Assert.True(save.Slots[0].InUse);
        Assert.False(save.Slots[1].InUse);
        Assert.True(save.Slots[2].InUse);
    }

    [Fact]
    public void Character_ReadsNameRankAndFunds()
    {
        var data = SyntheticSave.Create();
        var slot = SaveContainer.Parse(data, "system").Slots[0];

        var name = Encoding.UTF8.GetBytes("keepitcosy");
        name.CopyTo(data.AsSpan(slot.BaseOffset + 0x00));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(slot.BaseOffset + 0x20), 33_512);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(slot.BaseOffset + 0x28), 42);
        SyntheticSave.WriteU32(data, slot.BaseOffset + 0x280F, 19_100);
        SyntheticSave.WriteU32(data, slot.BaseOffset + 0x2817, 16_825);

        var reread = SaveContainer.Parse(data, "system").Slots[0];
        Assert.Equal("keepitcosy", reread.Name);
        Assert.Equal(33_512u, reread.PlayTimeSeconds);
        Assert.Equal(42, reread.HunterRank);
        Assert.Equal(19_100, reread.Zenny);
        Assert.Equal(16_825, reread.WycademyPoints);
    }

    [Fact]
    public void Zenny_WritesBothCopiesAndRejectsOutOfRange()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];

        slot.Zenny = 9_999_999;

        Assert.Equal(9_999_999, slot.Zenny);
        Assert.Equal(9_999_999, (int)BinaryPrimitives.ReadUInt32LittleEndian(
            save.Span.Slice(slot.BaseOffset + 0x24)));
        Assert.True(save.IsDirty);
        Assert.Throws<ArgumentOutOfRangeException>(() => slot.Zenny = 10_000_000);
    }

    [Fact]
    public void HunterName_WritesBothCopiesAndRejectsOverlongNames()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];

        slot.Name = "Prowler Pal";

        Assert.Equal("Prowler Pal", slot.Name);
        Assert.Equal("Prowler Pal", Encoding.UTF8.GetString(
            save.Span.Slice(slot.BaseOffset + 0x23B7D, 11).ToArray()));
        Assert.Throws<ArgumentException>(() => slot.Name = new string('a', 32));
    }
}

public class PackedItemListTests
{
    [Fact]
    public void ItemBox_RoundTripsAcrossByteBoundaries()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var box = save.Slots[0].ItemBox;

        for (var i = 0; i < 40; i++)
        {
            box[i] = new ItemSlot(100 + i, 1 + (i % 99));
        }

        for (var i = 0; i < 40; i++)
        {
            Assert.Equal(new ItemSlot(100 + i, 1 + (i % 99)), box[i]);
        }

        Assert.Equal(40, box.UsedCount());
        Assert.Equal(ItemSlot.Empty, box[40]);
    }

    [Fact]
    public void ItemBox_KeepsNeighbouringSlotsIntact()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var box = save.Slots[0].ItemBox;
        box[7] = new ItemSlot(1, 99);
        box[8] = new ItemSlot(2, 1);
        box[9] = new ItemSlot(3, 7);

        box[8] = ItemSlot.Empty;

        Assert.Equal(new ItemSlot(1, 99), box[7]);
        Assert.Equal(ItemSlot.Empty, box[8]);
        Assert.Equal(new ItemSlot(3, 7), box[9]);
    }

    [Fact]
    public void ItemBox_FullOfOnesEncodesExpectedBits()
    {
        // Slot 0 = id 1, count 1 -> 19-bit word 0x1001, LSB first.
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var slot = save.Slots[0];
        slot.ItemBox[0] = new ItemSlot(1, 1);

        var start = slot.BaseOffset + 0x278;
        Assert.Equal(0x01, save.Span[start]);
        Assert.Equal(0x10, save.Span[start + 1]);
        Assert.Equal(0x00, save.Span[start + 2]);
    }

    [Fact]
    public void ItemBox_RejectsOutOfRangeValues()
    {
        var box = SaveContainer.Parse(SyntheticSave.Create(), "system").Slots[0].ItemBox;

        Assert.Throws<ArgumentOutOfRangeException>(() => box[2300] = new ItemSlot(1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => box[0] = new ItemSlot(0x1000, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => box[0] = new ItemSlot(1, 128));
    }
}

public class SaveWriterTests
{
    [Fact]
    public void Write_SyncsTheSiblingAndKeepsItsEntryKey()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mhgu-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var systemPath = Path.Combine(directory, "system");
            var backupPath = Path.Combine(directory, "system_backup");
            File.WriteAllBytes(systemPath, SyntheticSave.Create("system"));
            File.WriteAllBytes(backupPath, SyntheticSave.Create("system_backup"));

            var save = SaveContainer.Load(systemPath);
            save.Slots[0].Zenny = 5_000_000;
            var result = OfflineSaveWriter.Write(systemPath, save);

            Assert.Equal(2, result.Written.Count);
            Assert.Equal(2, result.Backups.Count);
            Assert.All(result.Backups, path => Assert.True(File.Exists(path)));

            var written = SaveContainer.Load(systemPath);
            Assert.Equal(5_000_000, written.Slots[0].Zenny);

            var backup = SaveContainer.Load(backupPath);
            Assert.Equal(5_000_000, backup.Slots[0].Zenny);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_RefusesAFileThatIsNotPartOfTheSave()
    {
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        Assert.Throws<SaveFormatException>(() => OfflineSaveWriter.Write("C:\\tmp\\notasave.bin", save));
    }
}
