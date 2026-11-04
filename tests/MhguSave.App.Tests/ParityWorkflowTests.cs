using System.Buffers.Binary;
using System.Text;
using MhguSave.App.ViewModels;
using MhguSave.Format;
using MhguSave.Format.Tests;

namespace MhguSave.App.Tests;

public class ParityWorkflowTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "mhgu-parity-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;
    private readonly MainViewModel _vm = TestEditor.Create();

    public ParityWorkflowTests()
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "system");
        var bytes = SyntheticSave.Create(usedSlots: 3);
        var start = SyntheticSave.Slot1Pointer + 36;
        var cat = start + 0x23BB6;
        Encoding.UTF8.GetBytes("Miso").CopyTo(bytes.AsSpan(cat));
        bytes[cat + 37] = 1; bytes[cat + 39] = 3;
        var save = SaveContainer.Parse(bytes, "system");
        foreach (var slot in save.Slots.Where(s => s.InUse)) { slot.Name = "Hunter" + slot.Index; slot.VillageStar = 1; }
        save.Slots[0].EquipmentBox[0] = new EquipmentSlot { Type = 7, Id = 1, Deco1 = 2638 };
        File.WriteAllBytes(_path, save.ToArray());
        _vm.Load(_path);
    }

    [Fact]
    public void PalicoCreationGoesToTheCorrectBoxAndInvalidDraftPreventsBoxSwitch()
    {
        _vm.AddEquipType = 22;
        _vm.AddEquipmentCommand.Execute(null);
        var addedIndex = _vm.SelectedEquip!.Index;
        Assert.Equal(1, _vm.EquipmentStorage);
        Assert.Equal(1000, _vm.EquipRows.Count);
        _vm.EditEquipLevel = 1.5m;
        _vm.EquipmentStorage = 0;
        Assert.Equal(1, _vm.EquipmentStorage);
        _vm.EditEquipLevel = 1;
        _vm.SaveCommand.Execute(null);
        Assert.False(_vm.HasUnsavedChanges, _vm.Status);
        var slot = SaveContainer.Load(_path).Slots[0];
        Assert.Equal(22, slot.PalicoEquipmentBox[addedIndex].Type);
        Assert.Equal(7, slot.EquipmentBox[0].Type);
    }

    [Fact]
    public void SavingPendingLoadoutCopiesGearDecorationsAndProtectsThePiece()
    {
        var editor = _vm.LoadoutDraft!;
        editor.Name = "Hunt";
        editor.Pieces[0].Selected = editor.Pieces[0].Choices.Single(c => c.Index == 0);
        editor.Style = editor.Styles.First(s => s.Id == 0);
        _vm.SaveCommand.Execute(null);
        Assert.False(_vm.HasUnsavedChanges, _vm.Status);
        var bytes = File.ReadAllBytes(_path);
        var start = SyntheticSave.Slot1Pointer + 36;
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start + 0x208CE + 42)));
        Assert.Equal(2638, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start + 0x208CE + 56)));
        _vm.EquipmentStorage = 0;
        _vm.SelectedEquip = _vm.EquipRows[0];
        _vm.ClearSelectedEquipCommand.Execute(null);
        Assert.Contains("My Set 1", _vm.EquipStatus);
        Assert.Equal(7, _vm.EquipRows[0].Type);
    }

    [Fact]
    public void InvalidLoadoutKeepsOwnerAndCharacterAndDoesNotWriteToDisk()
    {
        var before = File.ReadAllBytes(_path);
        _vm.LoadoutDraft!.Colors[0].Hex = "wrong";
        _vm.LoadoutOwner = 1;
        Assert.Equal(0, _vm.LoadoutOwner);
        _vm.SelectedCharacter = _vm.Characters.First(c => c.Index == 1);
        Assert.Equal(0, _vm.SelectedCharacter!.Index);
        _vm.SaveCommand.Execute(null);
        Assert.Equal(before, File.ReadAllBytes(_path));
        Assert.True(_vm.LoadoutDraft.IsDirty);
    }

    [Fact]
    public void PalicoRoleChangesMoveTheRoleAndPendingIdentitySaves()
    {
        _vm.PalicoDraft!.Name = "Nori";
        _vm.PalicoDraft.Level = 55;
        _vm.PalicoDraft.Experience = 12345;
        _vm.SelectedBuddy1 = _vm.PalicoRoleChoices.Single(c => c.Index == 0);
        _vm.SelectedProwler = _vm.PalicoRoleChoices.Single(c => c.Index == 0);
        Assert.Equal(-1, _vm.SelectedBuddy1!.Index);
        _vm.SaveCommand.Execute(null);
        Assert.False(_vm.HasUnsavedChanges, _vm.Status);
        var slot = SaveContainer.Load(_path).Slots[0];
        Assert.Equal("Nori", slot.ReadPalico(0).Name);
        Assert.Equal(55, slot.ReadPalico(0).Level);
        Assert.Equal(0, slot.ReadPalicoRole(0));
        Assert.Equal(-1, slot.ReadPalicoRole(1));
    }

    [Fact]
    public void MonsterCountsAndIndividualAwardsSaveTogetherWithExtendedProfile()
    {
        _vm.MonsterDraft!.Slain = 80;
        _vm.MonsterDraft.Captured = 6;
        _vm.CollectionKind = 1;
        _vm.CollectionEntries.First().Unlocked = true;
        _vm.QuestCounter = 1234567;
        _vm.PlayTimeSeconds = 54321;
        _vm.SaveCommand.Execute(null);
        Assert.False(_vm.HasUnsavedChanges, _vm.Status);
        var slot = SaveContainer.Load(_path).Slots[0];
        Assert.Equal(86, slot.ReadMonster(1).Hunted);
        Assert.True(slot.Awards[0]);
        Assert.Equal(1234567u, slot.QuestCounter);
        Assert.Equal(54321u, slot.PlayTimeSeconds);
    }

    [Fact]
    public void AppearanceAndInventoryExchangeParticipateInTheNormalSaveWorkflow()
    {
        var draft = _vm.AppearanceDraft!;
        draft.Fields[1].Selected = draft.Fields[1].Choices.Single(c => c.Id == 7);
        draft.Colors[0].Hex = "112233FF";
        var csv = Path.Combine(_directory, "box.csv");
        File.WriteAllText(csv, "slot,id,name,count\n1,347,Ignored name,80\n5,347,Ignored name,100\n9,9,Potion,3\n");
        Assert.True(_vm.ImportItemBoxCsv(csv), _vm.Status);
        _vm.MergeItemBoxCommand.Execute(null);
        _vm.SortItemBoxCommand.Execute(null);
        Assert.True(_vm.ExportItemBoxCsv(csv), _vm.Status);
        Assert.Equal(new ItemSlot(9, 3), InventoryCsv.Parse(File.ReadAllText(csv), 2300)[0]);
        _vm.SaveCommand.Execute(null);
        Assert.False(_vm.HasUnsavedChanges, _vm.Status);
        var slot = SaveContainer.Load(_path).Slots[0];
        Assert.Equal((byte)7, slot.ReadAppearance().Face);
        Assert.Equal(0xFF332211u, slot.ReadAppearance().SkinColor);
        Assert.Equal(180, slot.ItemBox.UsedIndexes().Where(i => slot.ItemBox[i].Id == 347).Sum(i => slot.ItemBox[i].Count));
    }

    [Fact]
    public void AnInvalidAppearanceColorBlocksSaveAfterLeavingTheProfilePage()
    {
        var before = File.ReadAllBytes(_path);
        _vm.AppearanceDraft!.Colors[0].Hex = "invalid";
        _vm.SelectedSection = 2;
        _vm.SaveCommand.Execute(null);
        Assert.True(_vm.HasUnsavedChanges);
        Assert.Equal(before, File.ReadAllBytes(_path));
    }

    [Fact]
    public void LoadoutChoicesRefreshAfterCreatingEquipmentAndCsvCannotOverwriteASave()
    {
        _vm.AddEquipType = 18;
        _vm.AddEquipmentCommand.Execute(null);
        var index = _vm.SelectedEquip!.Index;
        _vm.SelectedSection = 5;
        Assert.Contains(_vm.LoadoutDraft!.Pieces[0].Choices, c => c.Index == index);
        var before = File.ReadAllBytes(_path);
        Assert.False(_vm.ExportItemBoxCsv(Path.Combine(_directory, "SYSTEM")));
        Assert.Equal(before, File.ReadAllBytes(_path));
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
