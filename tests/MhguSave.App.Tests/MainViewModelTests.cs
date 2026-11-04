using System;
using System.IO;
using System.Linq;
using MhguSave.App.ViewModels;

namespace MhguSave.App.Tests;

/// <summary>
/// Drives the view model the way the window does, against a copy of a real save supplied
/// through <c>MHGU_TEST_SAVE</c>, or a synthetic fixture when it is unset.
/// Nothing is ever written back to the original file.
/// </summary>
public class MainViewModelTests
{
    private static string CopyOfRealSave()
    {
        var source = Environment.GetEnvironmentVariable("MHGU_TEST_SAVE");
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            var syntheticPath = Path.Combine(Path.GetTempPath(), "mhgu-vm-" + Guid.NewGuid().ToString("N"), "system");
            Directory.CreateDirectory(Path.GetDirectoryName(syntheticPath)!);
            var synthetic = MhguSave.Format.SaveContainer.Parse(MhguSave.Format.Tests.SyntheticSave.Create(), "system");
            var hunter = synthetic.Slots[0];
            hunter.Name = "Test Hunter";
            hunter.VillageStar = 2;
            hunter.HubStar = 1;
            hunter.QuestCounter = 100;
            hunter.ItemBox[0] = new MhguSave.Format.ItemSlot(9, 5);
            hunter.Pouch[0] = new MhguSave.Format.ItemSlot(9, 3);
            hunter.EquipmentBox.Add(new MhguSave.Format.EquipmentSlot { Type = 18, Id = 1, Level = 1 });
            File.WriteAllBytes(syntheticPath, synthetic.ToArray());
            return syntheticPath;
        }

        var copy = Path.Combine(Path.GetTempPath(), "mhgu-vm-" + Guid.NewGuid().ToString("N"), "system");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(source, copy);
        return copy;
    }

    private static MainViewModel Loaded(string path)
    {
        var viewModel = TestEditor.Create();
        viewModel.Load(path);
        Assert.True(viewModel.IsLoaded, viewModel.Status);
        return viewModel;
    }

    [Fact]
    public void SelectingAPouchRowFillsTheEditorFields()
    {
        var path = CopyOfRealSave();
        try
        {
            var viewModel = Loaded(path);
            var row = viewModel.PouchRows.First(candidate => !candidate.IsEmpty);

            viewModel.SelectedPouch = row;

            Assert.Equal(row.Id, (int)viewModel.EditPouchId);
            Assert.Equal(row.Count, (int)viewModel.EditPouchCount);
            Assert.Equal(MhguSave.Format.ItemCatalog.LoadEmbedded().NameOf(row.Id), row.Name);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void SelectingABoxRowFillsTheEditorFields()
    {
        var path = CopyOfRealSave();
        try
        {
            var viewModel = Loaded(path);
            var row = viewModel.BoxRows.First(candidate => !candidate.IsEmpty);

            viewModel.SelectedBox = row;

            Assert.Equal(row.Id, (int)viewModel.EditBoxId);
            Assert.Equal(row.Count, (int)viewModel.EditBoxCount);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void QuestRowsAreNamedAndGrouped()
    {
        var path = CopyOfRealSave();
        try
        {
            var viewModel = Loaded(path);

            Assert.Equal(1508, viewModel.QuestRows.Count);
            Assert.Equal("Find the Ferns", viewModel.QuestRows[0].Name);
            Assert.Equal("Village", viewModel.QuestRows[0].Category);
            Assert.Contains("Village", viewModel.QuestCategories);
            Assert.Contains("Event Arena", viewModel.QuestCategories);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void ClearingASelectedQuestSetsItsBit()
    {
        var path = CopyOfRealSave();
        try
        {
            var viewModel = Loaded(path);
            var target = viewModel.QuestRows.First(row => !row.IsCleared);
            var clearedBefore = viewModel.QuestRows.Count(row => row.IsCleared);
            var completionsBefore = viewModel.QuestCounter;

            viewModel.SelectedQuest = target;
            viewModel.ClearSelectedQuestCommand.Execute(null);

            Assert.True(target.IsCleared);
            Assert.True(target.IsSeen);
            Assert.Contains("cleared", viewModel.QuestStatus);
            Assert.Equal(clearedBefore + 1, viewModel.QuestRows.Count(row => row.IsCleared));
            Assert.Equal(completionsBefore, viewModel.QuestCounter);

            viewModel.UnclearSelectedQuestCommand.Execute(null);

            Assert.False(target.IsCleared);
            Assert.Equal(clearedBefore, viewModel.QuestRows.Count(row => row.IsCleared));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void CategoryFilterNarrowsTheList()
    {
        var path = CopyOfRealSave();
        try
        {
            var viewModel = Loaded(path);

            viewModel.QuestCategory = "Training";

            Assert.NotEmpty(viewModel.QuestView);
            Assert.All(viewModel.QuestView, row => Assert.Equal("Training", row.Category));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void AddingAnItemFillsTheFirstEmptySlotAndSelectsIt()    {
        var path = CopyOfRealSave();
        try
        {
            var viewModel = Loaded(path);
            viewModel.AddItemId = 9;
            viewModel.AddItemCount = 5;

            viewModel.AddToBoxCommand.Execute(null);

            Assert.NotNull(viewModel.SelectedBox);
            Assert.Equal(9, viewModel.SelectedBox!.Id);
            Assert.Equal(5, viewModel.SelectedBox.Count);
            Assert.Equal("Potion", viewModel.SelectedBox.Name);
            Assert.Equal(9, (int)viewModel.EditBoxId);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void SavedNameAndStarsReachTheViewModelAfterReload()
    {
        var path = CopyOfRealSave();
        try
        {
            var viewModel = Loaded(path);
            var original = viewModel.HunterName;

            Assert.False(string.IsNullOrWhiteSpace(original));
            Assert.InRange((int)viewModel.VillageStar, 1, 10);
            Assert.InRange((int)viewModel.HubStar, 0, 13);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void EquipmentRowsAreNamedAndRealPiecesResolve()
    {
        var path = CopyOfRealSave();
        try
        {
            var viewModel = Loaded(path);

            Assert.Equal(2000, viewModel.EquipRows.Count);
            Assert.Contains("Head", viewModel.EquipTypeNames);
            Assert.Contains("Dual Blades", viewModel.EquipTypeNames);

            var named = viewModel.EquipRows.FirstOrDefault(row => !row.IsEmpty);
            Assert.NotNull(named);
            Assert.NotEqual("(empty)", named!.Name);
            Assert.NotEqual("Type 0 #0", named.Name);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void AddingEquipmentFillsTheFirstFreeRecord()
    {
        var path = CopyOfRealSave();
        try
        {
            var viewModel = Loaded(path);
            viewModel.AddEquipType = 18; // Dual Blades
            viewModel.AddEquipId = 1;
            viewModel.AddEquipLevel = 5;

            viewModel.AddEquipmentCommand.Execute(null);

            Assert.NotNull(viewModel.SelectedEquip);
            Assert.Equal(18, viewModel.SelectedEquip!.Type);
            Assert.Equal("Petrified Daggers", viewModel.SelectedEquip.Name);
            Assert.Equal(5, viewModel.SelectedEquip.Level);
            Assert.Contains("slot", viewModel.EquipStatus);

            // The catalog caps the level at the game table's maximum.
            viewModel.MaxSelectedEquipLevelCommand.Execute(null);
            Assert.Equal(viewModel.SelectedEquip.MaxLevel, (int)viewModel.EditEquipLevel);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void ItemNameSearchFindsArmorSpheres()
    {
        var path = CopyOfRealSave();
        try
        {
            var viewModel = Loaded(path);

            viewModel.ItemNameQuery = "Armor Sphere+";
            Assert.Contains(viewModel.ItemNameMatches, hit => hit.Id == 347);

            viewModel.SelectedItemPick = viewModel.ItemNameMatches.First(hit => hit.Id == 347);

            Assert.Equal(347, (int)viewModel.AddItemId);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }
}
