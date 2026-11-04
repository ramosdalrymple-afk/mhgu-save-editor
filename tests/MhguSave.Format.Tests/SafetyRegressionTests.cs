namespace MhguSave.Format.Tests;

public sealed class SafetyRegressionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "mhgu-safety-" + Guid.NewGuid().ToString("N"));
    private string SystemPath => Path.Combine(_directory, "system");
    private string SiblingPath => Path.Combine(_directory, "system_backup");

    public SafetyRegressionTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(SystemPath, SyntheticSave.Create());
        File.WriteAllBytes(SiblingPath, SyntheticSave.Create("system_backup"));
    }

    [Fact]
    public void InvalidSiblingDoesNotModifyTheOpenedSave()
    {
        var original = File.ReadAllBytes(SystemPath);
        var save = SaveContainer.Load(SystemPath);
        save.Slots[0].Zenny = 100;
        File.WriteAllBytes(SiblingPath, new byte[100]);
        Assert.Throws<SaveFormatException>(() => OfflineSaveWriter.Write(SystemPath, save));
        Assert.Equal(original, File.ReadAllBytes(SystemPath));
    }

    [Fact]
    public void ConsecutiveSavesKeepEveryBackupAndUpdateTheConflictBaseline()
    {
        var save = SaveContainer.Load(SystemPath);
        save.Slots[0].Zenny = 100;
        var first = OfflineSaveWriter.Write(SystemPath, save);
        save.Slots[0].Zenny = 200;
        var second = OfflineSaveWriter.Write(SystemPath, save);
        Assert.Equal(4, first.Backups.Concat(second.Backups).Distinct().Count());
        Assert.All(first.Backups.Concat(second.Backups), path => Assert.True(File.Exists(path)));
        var previous = SaveContainer.Parse(File.ReadAllBytes(second.Backups.Single(p => Path.GetFileName(p).StartsWith("system.bak-"))), "system");
        Assert.Equal(100, previous.Slots[0].Zenny);
        Assert.False(save.IsDirty);
        Assert.Equal(200, SaveContainer.Load(SiblingPath).Slots[0].Zenny);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp-*"));
        Assert.Empty(Directory.GetFiles(_directory, "*.recovery-*"));
    }

    [Fact]
    public void ChangedSourceIsNotOverwritten()
    {
        var save = SaveContainer.Load(SystemPath);
        save.Slots[0].Zenny = 100;
        var newer = SaveContainer.Load(SystemPath);
        newer.Slots[0].Zenny = 900;
        File.WriteAllBytes(SystemPath, newer.ToArray());
        Assert.Throws<IOException>(() => OfflineSaveWriter.Write(SystemPath, save));
        Assert.Equal(900, SaveContainer.Load(SystemPath).Slots[0].Zenny);
        Assert.Equal(0, SaveContainer.Load(SiblingPath).Slots[0].Zenny);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedCompanionIsNotOverwrittenRegardlessOfWhichFileWasOpened(bool openBackup)
    {
        var openedPath = openBackup ? SiblingPath : SystemPath;
        var companionPath = openBackup ? SystemPath : SiblingPath;
        var opened = SaveContainer.Load(openedPath);
        opened.Slots[0].Zenny = 500;
        var newer = SaveContainer.Load(companionPath);
        newer.Slots[0].Zenny = 987654;
        File.WriteAllBytes(companionPath, newer.ToArray());

        Assert.Contains("companion", Assert.Throws<IOException>(() => OfflineSaveWriter.Write(openedPath, opened)).Message);
        Assert.Equal(0, SaveContainer.Load(openedPath).Slots[0].Zenny);
        Assert.Equal(987654, SaveContainer.Load(companionPath).Slots[0].Zenny);
        Assert.Empty(Directory.GetFiles(_directory, "*.bak-*"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanionAppearanceOrDeletionRequiresReopening(bool appears)
    {
        if (appears) File.Delete(SiblingPath);
        var save = SaveContainer.Load(SystemPath);
        save.Slots[0].Zenny = 500;
        if (appears) File.WriteAllBytes(SiblingPath, SyntheticSave.Create("system_backup"));
        else File.Delete(SiblingPath);

        Assert.Throws<IOException>(() => OfflineSaveWriter.Write(SystemPath, save));
        Assert.Equal(0, SaveContainer.Load(SystemPath).Slots[0].Zenny);
        Assert.Equal(appears, File.Exists(SiblingPath));
    }

    [Fact]
    public void FailureReplacingSecondFileRestoresFirstEvenWithoutBackups()
    {
        if (!OperatingSystem.IsWindows()) return;
        var original = File.ReadAllBytes(SystemPath);
        var save = SaveContainer.Load(SystemPath);
        save.Slots[0].Zenny = 100;
        using var locked = new FileStream(SiblingPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Throws<IOException>(() => OfflineSaveWriter.Write(SystemPath, save, makeBackup: false));
        Assert.Equal(original, File.ReadAllBytes(SystemPath));
        Assert.True(save.IsDirty);
    }

    [Fact]
    public void MissingSourceCannotBeReportedAsSuccessful()
    {
        var save = SaveContainer.Load(SystemPath);
        File.Delete(SystemPath);
        Assert.Throws<FileNotFoundException>(() => OfflineSaveWriter.Write(SystemPath, save));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    [InlineData(0x80000000u)]
    public void CorruptSlotPointersAreRejected(uint pointer)
    {
        var data = SyntheticSave.Create();
        SyntheticSave.WriteU32(data, 0x34, pointer);
        Assert.Throws<SaveFormatException>(() => SaveContainer.Parse(data, "system"));
    }

    [Fact]
    public void OverlappingSlotsAreRejected()
    {
        var data = SyntheticSave.Create();
        SyntheticSave.WriteU32(data, 0x38, SyntheticSave.Slot1Pointer);
        Assert.Throws<SaveFormatException>(() => SaveContainer.Parse(data, "system"));
    }

    [Fact]
    public void InvalidTalismanPayloadDoesNotPartiallyChangeTheLiveRecord()
    {
        var save = SaveContainer.Load(SystemPath);
        var box = save.Slots[0].EquipmentBox;
        box[0] = new EquipmentSlot { Type = 6, Id = 1, SlotCount = 1, CharmItem = 353 };
        var before = save.ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => box[0] = new EquipmentSlot
            { Type = 6, Id = 20, Deco1 = 123, Skill1 = 999 });
        Assert.Equal(before, save.ToArray());
    }

    [Fact]
    public void EquipmentEditPreservesUnknownFlagAndTrailingBytes()
    {
        var data = new byte[EquipmentSlot.RecordSize];
        data[1] = 0x80;
        data[25] = 0xAB;
        var piece = EquipmentSlot.Read(data);
        piece.Type = 1;
        piece.Id = 2;
        piece.Write(data);
        Assert.Equal(0x80, data[1] & 0x80);
        Assert.Equal(0xAB, data[25]);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
