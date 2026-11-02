using MhguSave.App.ViewModels;
using MhguSave.Format;
using MhguSave.Format.Tests;

namespace MhguSave.App.Tests;

public sealed class CompleteWorkflowTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "mhgu-complete-" + Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(_directory, "system");
    private MainViewModel Vm { get; } = TestEditor.Create();

    public CompleteWorkflowTests()
    {
        Directory.CreateDirectory(_directory);
        var save = SaveContainer.Parse(SyntheticSave.Create(usedSlots: 3), "system");
        foreach (var slot in save.Slots.Where(s => s.InUse))
        {
            slot.Name = "Hunter " + slot.Index;
            slot.VillageStar = 1;
            slot.QuestCounter = 100;
            slot.EquipmentBox.Add(new EquipmentSlot { Type = 7, Id = 1 });
            slot.EquipmentBox.Add(new EquipmentSlot { Type = 18, Id = 1 });
            slot.ItemBox[0] = new ItemSlot(9, 5);
            slot.Pouch[0] = new ItemSlot(9, 3);
        }
        File.WriteAllBytes(SavePath, save.ToArray());
        Vm.Load(SavePath);
    }

    public static IEnumerable<object[]> EquipmentTypes => EquipmentCatalog.LoadEmbedded().Types.Select(t => new object[] { t.Code });

    [Fact]
    public void RathalosSCreationWritesMappedSaveIdsForBothHunterClasses()
    {
        string[][] names =
        [
            ["Rathalos Helm S", "Rathalos Cap S"], ["Rathalos Mail S", "Rathalos Vest S"],
            ["Rathalos Braces S", "Rathalos Guards S"], ["Rathalos Faulds S", "Rathalos Coat S"],
            ["Rathalos Greaves S", "Rathalos Leggings S"],
        ];
        var added = new List<(int Index, int Type, int Id, string Name)>();
        for (var part = 0; part < names.Length; part++)
        {
            Vm.AddEquipType = part + 1;
            for (var hunterClass = 0; hunterClass < 2; hunterClass++)
            {
                Vm.EquipmentQuery = names[part][hunterClass];
                Vm.SelectedEquipmentPick = Vm.EquipmentMatches.Single(p => p.Name == names[part][hunterClass]);
                Vm.AddEquipmentCommand.Execute(null);
                Assert.Equal(446 + hunterClass, Vm.SelectedEquip!.Id);
                added.Add((Vm.SelectedEquip.Index, part + 1, 446 + hunterClass, names[part][hunterClass]));
            }
        }
        Vm.SaveCommand.Execute(null);
        Assert.False(Vm.HasUnsavedChanges, Vm.Status);
        var bytes = File.ReadAllBytes(SavePath);
        var baseOffset = 0x24 + BitConverter.ToInt32(bytes, 0x34);
        Vm.Load(SavePath);
        foreach (var piece in added)
        {
            var offset = baseOffset + 0x62EE + piece.Index * 36;
            Assert.Equal(piece.Id, bytes[offset + 2] | bytes[offset + 3] << 8);
            Assert.Equal(piece.Type, bytes[offset] & 31);
            Assert.Equal(piece.Name, Vm.EquipRows[piece.Index].Name);
        }
    }

    [Fact]
    public void CreationDoesNotMutateInventoryWhenAnExistingEditIsInvalid()
    {
        Vm.SelectedBox = Vm.BoxRows[0];
        Vm.EditBoxCount = 200;
        Vm.AddToBoxCommand.Execute(null);
        Assert.Single(Vm.BoxRows, r => !r.IsEmpty);
        Vm.SelectedDecoration = Vm.DecorationChoices.First(d => d.Id > 0);
        Vm.AddDecorationToBoxCommand.Execute(null);
        Assert.Single(Vm.BoxRows, r => !r.IsEmpty);
        Assert.Equal(5, Vm.BoxRows[0].Count);
        Assert.True(Vm.HasBoxDraft);
    }

    [Theory]
    [MemberData(nameof(EquipmentTypes))]
    public void EverySupportedEquipmentCategoryCanBeCreatedSavedAndReloaded(int type)
    {
        Vm.AddEquipType = type;
        Assert.True(Vm.CanAddEquipment, $"Type {type}: {Vm.AddEquipName}");
        Assert.DoesNotContain(Vm.EquipmentMatches, p => p.Id == 0 || p.Name == "(None)");
        Vm.AddEquipmentCommand.Execute(null);
        Assert.Equal(type, Vm.SelectedEquip!.Type);
        var index = Vm.SelectedEquip.Index;
        var id = Vm.SelectedEquip.Id;
        Vm.SaveCommand.Execute(null);
        Assert.False(Vm.HasUnsavedChanges, Vm.Status);
        var saved = SaveContainer.Load(SavePath);
        Assert.Equal(type, saved.Slots[0].EquipmentBox[index].Type);
        Assert.Equal(id, saved.Slots[0].EquipmentBox[index].Id);
        Assert.Equal(3, saved.Slots[0].EquipmentBoxUsed);
        Vm.Load(SavePath);
        Assert.Equal(type, Vm.EquipRows[index].Type);
    }

    [Fact]
    public void ConfiguredTalismanAndNamedJewelsRoundTripThroughSaveWithoutApply()
    {
        Vm.AddEquipType = 6;
        Vm.SelectedAddSkill1 = Vm.SkillChoices.First(c => c.Id == 1);
        Vm.AddEquipSkillPoints1 = 7;
        Vm.SelectedAddSkill2 = Vm.SkillChoices.First(c => c.Id == 2);
        Vm.AddEquipSkillPoints2 = -3;
        Vm.AddEquipSlots = 3;
        Vm.SelectedAddCharm = Vm.CharmChoices.First(c => c.Id == 356);
        Vm.AddEquipmentCommand.Execute(null);
        var index = Vm.SelectedEquip!.Index;
        Vm.SelectedEditDeco1 = Vm.DecorationChoices.First(c => c.Id == 2639);
        Vm.SelectedEditDeco2 = Vm.DecorationChoices.First(c => c.Id == 2638);
        Assert.True(Vm.HasEquipmentDraft);
        Vm.SaveCommand.Execute(null);
        Assert.False(Vm.HasUnsavedChanges, Vm.Status);
        var piece = SaveContainer.Load(SavePath).Slots[0].EquipmentBox[index];
        Assert.Equal(1, piece.Skill1);
        Assert.Equal(7, piece.SkillPoints1);
        Assert.Equal(2, piece.Skill2);
        Assert.Equal(-3, piece.SkillPoints2);
        Assert.Equal(3, piece.SlotCount);
        Assert.Equal(356, piece.CharmItem);
        Assert.Equal(2639, piece.Deco1);
        Assert.Equal(2638, piece.Deco2);
    }

    [Fact]
    public void OversizedDecorationCombinationIsRejectedBeforeAnyWrite()
    {
        Vm.SelectedEquip = Vm.EquipRows[0];
        var original = File.ReadAllBytes(SavePath);
        Vm.EditEquipDeco1 = Vm.EditEquipDeco2 = 2639;
        Vm.SaveCommand.Execute(null);
        Assert.True(Vm.HasEquipmentDraft);
        Assert.True(Vm.HasUnsavedChanges);
        Assert.Contains("need 4 slots", Vm.Status);
        Assert.Equal(original, File.ReadAllBytes(SavePath));
        Assert.Equal(0, Vm.EquipRows[0].Piece.Deco1);
    }

    [Fact]
    public void ChangingSelectionAppliesValidDraftAndKeepsInvalidDraftOnItsOriginalTarget()
    {
        Vm.SelectedEquip = Vm.EquipRows[0];
        Vm.EditEquipLevel = 2;
        Vm.SelectedEquip = Vm.EquipRows[1];
        Assert.Equal(2, Vm.EquipRows[0].Level);
        Assert.Equal(1, Vm.EditEquipLevel);
        Vm.EditEquipLevel = 32;
        Vm.SelectedEquip = Vm.EquipRows[0];
        Assert.Same(Vm.EquipRows[1], Vm.SelectedEquip);
        Assert.Equal(32, Vm.EditEquipLevel);
        Vm.ResetEquipmentEditorCommand.Execute(null);
        Assert.Equal(1, Vm.EditEquipLevel);
        Assert.False(Vm.HasEquipmentDraft);
    }

    [Fact]
    public void InvalidDraftCannotSwitchToADifferentCharacter()
    {
        Vm.SelectedEquip = Vm.EquipRows[0];
        Vm.EditEquipLevel = 32;
        Vm.SelectedCharacter = Vm.Characters[1];
        Assert.Equal(0, Vm.SelectedCharacter!.Index);
        Assert.Equal("Hunter 0", Vm.HunterName);
        Assert.True(Vm.HasEquipmentDraft);
    }

    [Fact]
    public void FilteringPreservesPendingEditsAndSaveIncludesBothInventories()
    {
        Vm.SelectedEquip = Vm.EquipRows[0];
        Vm.EditEquipLevel = 2;
        Vm.EquipFilter = "does not match";
        Assert.Null(Vm.SelectedEquip);
        Assert.False(Vm.HasEquipmentDraft);
        Vm.SelectedBox = Vm.BoxRows[0];
        Vm.EditBoxCount = 21;
        Vm.SelectedPouch = Vm.PouchRows[0];
        Vm.EditPouchCount = 6;
        Vm.ItemFilter = "nothing matches";
        Vm.SaveCommand.Execute(null);
        Assert.False(Vm.HasUnsavedChanges, Vm.Status);
        var slot = SaveContainer.Load(SavePath).Slots[0];
        Assert.Equal(2, slot.EquipmentBox[0].Level);
        Assert.Equal(21, slot.ItemBox[0].Count);
        Assert.Equal(6, slot.Pouch[0].Count);
    }

    [Fact]
    public void ArmorCreationKeepsRequestedLevelInsteadOfClampingToPlaceholderMaximum()
    {
        Vm.AddEquipType = 1;
        Vm.AddEquipLevel = 7;
        Vm.AddEquipmentCommand.Execute(null);
        Assert.Equal(7, Vm.SelectedEquip!.Level);
        Assert.False(Vm.CanUseEquipmentMaximum);
        Vm.MaxSelectedEquipLevelCommand.Execute(null);
        Assert.Equal(7, Vm.EditEquipLevel);
    }

    [Fact]
    public void DecorationCatalogAddsAnInventoryJewelWithoutOverwritingItemCreationFields()
    {
        Vm.AddItemId = 9;
        Vm.DecorationQuery = "Antidote Jwl 1";
        Vm.SelectedDecoration = Vm.DecorationMatches.First(c => c.Id == 2638);
        Vm.DecorationQuantity = 5;
        Vm.AddDecorationToBoxCommand.Execute(null);
        Assert.Equal(9, Vm.AddItemId);
        Vm.SaveCommand.Execute(null);
        Assert.Contains(SaveContainer.Load(SavePath).Slots[0].ItemBox.UsedIndexes(),
            i => SaveContainer.Load(SavePath).Slots[0].ItemBox[i] == new ItemSlot(2638, 5));
    }

    [Fact]
    public void QuestAndUnlockActionsPersistAndLeaveRepeatCompletionCounterAlone()
    {
        Vm.QuestCategory = "Training";
        Vm.ClearVisibleQuestsCommand.Execute(null);
        Assert.All(Vm.QuestView, q => Assert.True(q.IsCleared));
        Vm.UnclearVisibleQuestsCommand.Execute(null);
        Assert.All(Vm.QuestView, q => Assert.False(q.IsCleared));
        Vm.MarkAllQuestsSeenCommand.Execute(null);
        Vm.CompleteAllQuestSetsCommand.Execute(null);
        Vm.UnlockAllMaterialsCommand.Execute(null);
        Vm.GrantAllAwardsCommand.Execute(null);
        Vm.UnlockAllArtsCommand.Execute(null);
        Vm.SaveCommand.Execute(null);
        var slot = SaveContainer.Load(SavePath).Slots[0];
        Assert.Equal(1509, slot.SeenQuests.SetCount());
        Assert.Equal(100, slot.QuestCounter);
        Assert.Equal(100, slot.CompletedQuestSets.SetCount());
        Assert.Equal(2990, slot.ItemsObtained.SetCount());
        Assert.Equal(132, slot.Awards.SetCount());
        Assert.Equal(178, slot.UnlockedHunterArtCount);
    }

    [Fact]
    public void EmulatorProtectionStopsWritesWithoutDiscardingEdits()
    {
        var guarded = new MainViewModel(() => new[] { "yuzu" }, (_, _) => throw new Exception("Writer must not be called"));
        guarded.Load(SavePath);
        guarded.Zenny = 99;
        var original = File.ReadAllBytes(SavePath);
        guarded.SaveCommand.Execute(null);
        Assert.True(guarded.HasUnsavedChanges);
        Assert.Contains("Refusing", guarded.Status);
        Assert.Equal(original, File.ReadAllBytes(SavePath));
        Assert.Throws<IOException>(() => SaveWriter.WriteCore(SavePath, SaveContainer.Load(SavePath), true, () => new[] { "yuzu" }));
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
