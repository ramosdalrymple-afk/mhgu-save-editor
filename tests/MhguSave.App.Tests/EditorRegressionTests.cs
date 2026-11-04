using MhguSave.App.ViewModels;
using MhguSave.Format;
using MhguSave.Format.Tests;

namespace MhguSave.App.Tests;

public sealed class EditorRegressionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "mhgu-editor-" + Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(_directory, "system");
    private readonly MainViewModel _vm = TestEditor.Create();

    public EditorRegressionTests()
    {
        Directory.CreateDirectory(_directory);
        var save = SaveContainer.Parse(SyntheticSave.Create(usedSlots: 7), "system");
        foreach (var slot in save.Slots)
        {
            slot.Name = "Hunter " + slot.Index;
            slot.VillageStar = 1;
            slot.QuestCounter = 100;
            slot.ItemBox[0] = new ItemSlot(9, 5);
            slot.Pouch[0] = new ItemSlot(9, 2);
        }
        File.WriteAllBytes(SavePath, save.ToArray());
        _vm.Load(SavePath);
        Assert.True(_vm.IsLoaded, _vm.Status);
    }

    [Fact]
    public void ItemIconRefreshesWhenAnIdChangesAndClearsWithTheSlot()
    {
        var row = _vm.BoxRows[0];
        var notifications = new List<string?>();
        row.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        Assert.Equal("item:9", row.IconKey);
        row.Id = 347;
        Assert.Equal("item:347", row.IconKey);
        Assert.Contains(nameof(ItemRow.IconKey), notifications);
        row.Id = 0;
        Assert.Equal(string.Empty, row.IconKey);
    }

    [Fact]
    public void EquipmentSearchAndRowsUseTheMatchingWeaponClassIcon()
    {
        _vm.AddEquipType = 18;
        _vm.EquipmentQuery = "Petrified Daggers";
        var pick = _vm.EquipmentMatches.First(hit => hit.Name == "Petrified Daggers");
        Assert.Equal(GameRarity.Icon(18, GameRarity.Equipment(18, pick.Id)), pick.IconKey);
        _vm.SelectedEquipmentPick = pick;
        _vm.AddEquipmentCommand.Execute(null);
        var row = _vm.SelectedEquip!;
        Assert.Equal(pick.IconKey, row.IconKey);
        row.Clear();
        Assert.Equal(string.Empty, row.IconKey);
    }

    [Fact]
    public void EquipmentTypePickerAndSearchStayInSync()
    {
        Assert.Equal(7, _vm.SelectedAddEquipmentType?.Code);
        _vm.SelectedAddEquipmentType = _vm.EquipmentTypes.First(t => t.Code == 18);
        Assert.Equal(18, _vm.AddEquipType);
        _vm.EquipmentQuery = "Petrified Daggers";
        _vm.SelectedEquipmentPick = _vm.EquipmentMatches.First(e => e.Name == "Petrified Daggers");
        Assert.Equal(1, _vm.AddEquipId);
        _vm.AddEquipType = 20;
        Assert.Equal(20, _vm.SelectedAddEquipmentType?.Code);
    }

    [Fact]
    public void FolderOpenAndReloadKeepFilterCatalogsAvailable()
    {
        _vm.Load(_directory);
        Assert.True(_vm.IsLoaded, _vm.Status);
        Assert.Contains("All", _vm.EquipTypeNames);
        Assert.Contains("Village", _vm.QuestCategories);
        _vm.EquipTypeFilter = "Head";
        _vm.Load(_directory);
        Assert.Equal("Head", _vm.EquipTypeFilter);
        Assert.Single(_vm.EquipTypeNames, t => t == "All");
    }

    [Fact]
    public void InvalidEquipmentLevelIsRejectedWithoutChangingThePiece()
    {
        _vm.AddEquipType = 7;
        _vm.AddEquipmentCommand.Execute(null);
        var before = _vm.SelectedEquip!.Piece.Level;
        _vm.EditEquipLevel = 32;
        _vm.ApplySelectedEquipCommand.Execute(null);
        Assert.Contains("Rejected", _vm.EquipStatus);
        Assert.Equal(before, _vm.SelectedEquip.Piece.Level);
    }

    [Fact]
    public void SwitchingCharactersKeepsEditsForBothSlotsThroughSaveAndReload()
    {
        _vm.Zenny = 123;
        _vm.SelectedCharacter = _vm.Characters[1];
        _vm.Zenny = 456;
        _vm.SelectedCharacter = _vm.Characters[0];
        Assert.Equal(123, _vm.Zenny);
        _vm.SaveCommand.Execute(null);
        Assert.False(_vm.HasUnsavedChanges, _vm.Status);
        Assert.Contains("Wrote", _vm.Status);
        var saved = SaveContainer.Load(SavePath);
        Assert.Equal(123, saved.Slots[0].Zenny);
        Assert.Equal(456, saved.Slots[1].Zenny);
        Assert.Equal(0, saved.Slots[2].Zenny);
    }

    [Fact]
    public void BrowsingCharactersDoesNotMakeTheSaveDirty()
    {
        _vm.SelectedCharacter = _vm.Characters[1];
        Assert.False(_vm.HasUnsavedChanges);
    }

    [Fact]
    public void FailedOpenPreservesTheCurrentSessionAndItsChanges()
    {
        _vm.Zenny = 42;
        _vm.Load(Path.Combine(_directory, "missing"));
        Assert.True(_vm.IsLoaded);
        Assert.Equal(42, _vm.Zenny);
        Assert.True(_vm.HasUnsavedChanges);
    }

    [Fact]
    public void ImportingNamesKeepsItemsAndProfileChanges()
    {
        _vm.Zenny = 42;
        _vm.AddItemId = 9;
        _vm.AddItemCount = 7;
        _vm.AddToBoxCommand.Execute(null);
        var csv = Path.Combine(_directory, "names.csv");
        File.WriteAllText(csv, "id,name\n9,Renamed Potion\n");
        _vm.LoadItemNames(csv);
        Assert.Equal(42, _vm.Zenny);
        Assert.Equal(7, _vm.BoxRows[1].Count);
        Assert.Equal("Renamed Potion", _vm.BoxRows[1].Name);
        Assert.True(_vm.HasUnsavedChanges);
    }

    [Fact]
    public void ClearingHiddenEmptyRowsAndEquipmentDoesNotLoseTheStatusTarget()
    {
        _vm.SelectedBox = _vm.BoxRows[0];
        _vm.ClearSelectedBoxCommand.Execute(null);
        Assert.DoesNotContain(_vm.BoxRows[0], _vm.BoxView);
        Assert.Contains("Slot 1 cleared", _vm.Status);
        _vm.SelectedPouch = _vm.PouchRows[0];
        _vm.ClearSelectedPouchCommand.Execute(null);
        Assert.Contains("Pouch slot 1", _vm.Status);
        _vm.AddEquipmentCommand.Execute(null);
        _vm.ClearSelectedEquipCommand.Execute(null);
        Assert.Contains("slot 1 cleared", _vm.EquipStatus);
    }

    [Fact]
    public void AddingInsectGlaiveCreatesValidKinsectPayload()
    {
        _vm.AddEquipType = EquipmentSlot.InsectGlaiveType;
        _vm.AddEquipmentCommand.Execute(null);
        Assert.NotNull(_vm.SelectedEquip);
        Assert.Equal(1, _vm.SelectedEquip.Piece.KinsectLevel);
    }

    [Fact]
    public void QuestCheckboxAndBulkEditsPreserveRepeatHuntCounter()
    {
        _vm.QuestRows[0].IsCleared = true;
        _vm.MarkAllQuestsClearedCommand.Execute(null);
        Assert.Equal(100, _vm.QuestCounter);
        _vm.SaveCommand.Execute(null);
        Assert.Equal(100u, SaveContainer.Load(SavePath).Slots[0].QuestCounter);
        Assert.True(_vm.HasUnsavedChanges == false, _vm.Status);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10000000)]
    [InlineData(1.5)]
    public void InvalidProfileValuesNeverReachDisk(decimal value)
    {
        var original = File.ReadAllBytes(SavePath);
        _vm.HunterName = "New Name";
        _vm.Zenny = value;
        _vm.SaveCommand.Execute(null);
        Assert.Contains("Write failed", _vm.Status);
        Assert.Equal(original, File.ReadAllBytes(SavePath));
        Assert.True(_vm.HasUnsavedChanges);
    }

    [Fact]
    public void TransmogAndKinsectEditsRoundTrip()
    {
        _vm.AddEquipType = 1;
        _vm.AddEquipmentCommand.Execute(null);
        _vm.EditTransmogId = 2;
        _vm.EditTransmogLevel = 3;
        _vm.ApplySelectedEquipCommand.Execute(null);
        _vm.AddEquipType = 20;
        _vm.AddEquipmentCommand.Execute(null);
        _vm.EditKinsectId = 4;
        _vm.EditKinsectLevel = 5;
        _vm.ApplySelectedEquipCommand.Execute(null);
        _vm.SaveCommand.Execute(null);
        var slot = SaveContainer.Load(SavePath).Slots[0];
        Assert.Equal(2, slot.EquipmentBox[0].TransmogId);
        Assert.Equal(3, slot.EquipmentBox[0].TransmogLevel);
        Assert.Equal(4, slot.EquipmentBox[1].KinsectId);
        Assert.Equal(5, slot.EquipmentBox[1].KinsectLevel);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
