using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

public partial class MainViewModel
{
    public IReadOnlyList<CatalogChoice> SkillChoices { get; private set; } = [];
    public IReadOnlyList<CatalogChoice> DecorationChoices { get; private set; } = [];
    public IReadOnlyList<CatalogChoice> CharmChoices { get; } =
    [
        new(353, "Mystery Charm"), new(354, "Shining Charm"),
        new(355, "Timeworn Charm"), new(356, "Enduring Charm"),
    ];
    public ObservableCollection<CatalogChoice> DecorationMatches { get; } = [];
    [ObservableProperty] private int _equipmentPane = 1;
    [ObservableProperty] private string _decorationQuery = string.Empty;
    [ObservableProperty] private CatalogChoice? _selectedDecoration;
    [ObservableProperty] private decimal _decorationQuantity = 1;
    [ObservableProperty] private decimal _addEquipSkill1;
    [ObservableProperty] private decimal _addEquipSkill2;
    [ObservableProperty] private decimal _addEquipSkillPoints1;
    [ObservableProperty] private decimal _addEquipSkillPoints2;
    [ObservableProperty] private decimal _addEquipSlots;
    [ObservableProperty] private decimal _addEquipCharm = 353;
    [ObservableProperty] private decimal _addKinsectId;
    [ObservableProperty] private decimal _addKinsectLevel = 1;
    [ObservableProperty] private bool _hasEquipmentDraft;
    [ObservableProperty] private bool _hasBoxDraft;
    [ObservableProperty] private bool _hasPouchDraft;
    private bool _loadingEditors;
    private bool _refreshingViews;
    private bool _selectionGuard;

    public bool HasEquipmentSelection => SelectedEquip is { IsEmpty: false };
    public bool CanUseEquipmentMaximum => HasEquipmentSelection && ValidInteger(EditEquipType, 1, 24) && ValidInteger(EditEquipId, 1, 65535) && _equipCatalog.TypeOf((int)EditEquipType).IsWeapon
        && _equipCatalog.HasEquipment((int)EditEquipType, (int)EditEquipId);
    public bool CanAddEquipment => IsLoaded && ValidInteger(AddEquipType, 1, 24) && ValidInteger(AddEquipId, 1, 65535)
        && _equipCatalog.HasEquipment((int)AddEquipType, (int)AddEquipId);
    public bool IsAddingTalisman => AddEquipType == EquipmentSlot.TalismanType;
    public bool IsAddingGlaive => AddEquipType == EquipmentSlot.InsectGlaiveType;
    public string EquipmentDraftMessage => HasEquipmentDraft
        ? "Unapplied edits. Apply now, or Save changes to apply and save."
        : "Select a piece to edit. Changes stay in memory until Save changes.";
    public string AddLevelHint => ValidInteger(AddEquipType, 1, 24) && ValidInteger(AddEquipId, 1, 65535) && _equipCatalog.TypeOf((int)AddEquipType).IsWeapon
        ? $"Weapon maximum: {_equipCatalog.MaxLevel((int)AddEquipType, (int)AddEquipId)}"
        : "Enter the upgrade level. This catalog has no verified armor level cap.";

    private void InitializeEquipmentEditors()
    {
        SkillChoices = _equipCatalog.Skills;
        DecorationChoices = _equipCatalog.Decorations;
        SelectedEditSkill1 = SelectedEditSkill2 = SkillChoices[0];
        SelectedAddSkill1 = SelectedAddSkill2 = SkillChoices[0];
        SelectedEditDeco1 = SelectedEditDeco2 = SelectedEditDeco3 = DecorationChoices[0];
        SelectedEditCharm = SelectedAddCharm = CharmChoices[0];
        RefreshDecorationMatches();
    }

    partial void OnHasEquipmentDraftChanged(bool value) => OnPropertyChanged(nameof(EquipmentDraftMessage));
    partial void OnDecorationQueryChanged(string value) => RefreshDecorationMatches();
    private void RefreshDecorationMatches()
    {
        var previous = SelectedDecoration;
        DecorationMatches.Clear();
        var query = DecorationQuery.Trim();
        foreach (var decoration in DecorationChoices.Where(c => c.Id > 0 &&
                     (c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || c.Id.ToString() == query)))
            DecorationMatches.Add(decoration);
        SelectedDecoration = previous is not null && DecorationMatches.Contains(previous)
            ? previous : DecorationMatches.FirstOrDefault();
    }

    private void TrackEditorChange(string? property)
    {
        if (_syncing || _loadingEditors || !IsLoaded || property is null) return;
        if (SelectedEquip is { IsEmpty: false } && (property.StartsWith("EditEquip", StringComparison.Ordinal)
                && property != nameof(EditEquipMaxLevel) || property is nameof(EditTransmogId) or nameof(EditTransmogLevel)
                or nameof(EditKinsectId) or nameof(EditKinsectLevel)))
            HasEquipmentDraft = HasUnsavedChanges = true;
        if (SelectedBox is not null && property is nameof(EditBoxId) or nameof(EditBoxCount))
            HasBoxDraft = HasUnsavedChanges = true;
        if (SelectedPouch is not null && property is nameof(EditPouchId) or nameof(EditPouchCount))
            HasPouchDraft = HasUnsavedChanges = true;
    }

    private bool ApplyPendingEditors()
    {
        if (HasEquipmentDraft && !TryApplyEquipment(SelectedEquip)) return false;
        if (HasBoxDraft && !TryApplyItem(SelectedBox, EditBoxId, EditBoxCount, "box")) return false;
        if (HasPouchDraft && !TryApplyItem(SelectedPouch, EditPouchId, EditPouchCount, "pouch")) return false;
        return true;
    }

    [RelayCommand]
    private void ShowAddEquipment() => EquipmentPane = 1;

    [RelayCommand]
    private void ResetEquipmentEditor()
    {
        HasEquipmentDraft = false;
        LoadEquipmentEditor(SelectedEquip);
        EquipStatus = "Equipment editor reset to the last applied values.";
        HasUnsavedChanges = _save?.IsDirty == true || HasBoxDraft || HasPouchDraft || _slot is not null &&
            (HunterName != _slot.Name || Zenny != _slot.Zenny || Wycademy != _slot.WycademyPoints
             || HrPoints != _slot.HrPoints || VillageStar != _slot.VillageStar || HubStar != _slot.HubStar
             || QuestCounter != _slot.QuestCounter);
    }

    [RelayCommand]
    private void AddDecorationToBox()
    {
        if (_slot is null || SelectedDecoration is null)
        {
            EquipStatus = Status = "Choose a decoration first.";
            return;
        }
        if (!ValidInteger(DecorationQuantity, 1, PackedItemList.MaxCount))
        {
            EquipStatus = Status = "Decoration quantity must be a whole number from 1–127.";
            return;
        }
        if (!ApplyPendingEditors()) return;
        var index = _slot.ItemBox.Add(new ItemSlot(SelectedDecoration.Id, (int)DecorationQuantity));
        if (index < 0)
        {
            EquipStatus = Status = "The item box is full.";
            return;
        }
        BoxRows[index].Refresh();
        RefreshFilters();
        RefreshSummary();
        EquipStatus = Status = $"Added {SelectedDecoration.Name} ×{DecorationQuantity} to item box slot {index + 1}. Save changes to write.";
    }

    private void ValidateDecorations(EquipmentSlot piece, EquipmentSlot? original = null)
    {
        var ids = piece.Decorations;
        for (var i = 0; i < ids.Length; i++)
            if (!_equipCatalog.HasDecoration(ids[i]) && (original is null || original.Decorations[i] != ids[i]))
                throw new ArgumentException($"Decoration {i + 1} is not a known jewel. Choose a named decoration or None.");
        var occupied = ids.Sum(_equipCatalog.DecorationSlots);
        var capacity = piece.Type == EquipmentSlot.TalismanType ? piece.SlotCount : 3;
        if (occupied > capacity)
            throw new ArgumentException($"These jewels need {occupied} slots; this piece has at most {capacity} available.");
    }

    private void ConfigureNewEquipment(EquipmentSlot piece)
    {
        if (IsAddingTalisman)
        {
            RequireInteger(AddEquipSkill1, 0, 255, "Skill 1");
            RequireInteger(AddEquipSkill2, 0, 255, "Skill 2");
            RequireInteger(AddEquipSkillPoints1, -128, 127, "Skill points 1");
            RequireInteger(AddEquipSkillPoints2, -128, 127, "Skill points 2");
            RequireInteger(AddEquipSlots, 0, 3, "Talisman slots");
            RequireInteger(AddEquipCharm, 353, 356, "Charm item");
            if (!_equipCatalog.HasSkill((int)AddEquipSkill1) || !_equipCatalog.HasSkill((int)AddEquipSkill2))
                throw new ArgumentException("Choose named skills from the list.");
            piece.Skill1 = (int)AddEquipSkill1;
            piece.Skill2 = (int)AddEquipSkill2;
            piece.SkillPoints1 = (int)AddEquipSkillPoints1;
            piece.SkillPoints2 = (int)AddEquipSkillPoints2;
            piece.SlotCount = (int)AddEquipSlots;
            piece.CharmItem = (int)AddEquipCharm;
        }
        if (IsAddingGlaive)
        {
            RequireInteger(AddKinsectId, 0, 255, "Kinsect ID");
            RequireInteger(AddKinsectLevel, 1, 256, "Kinsect level");
            piece.KinsectId = (int)AddKinsectId;
            piece.KinsectLevel = (int)AddKinsectLevel;
        }
    }

    [ObservableProperty] private CatalogChoice? _selectedEditSkill1;
    partial void OnSelectedEditSkill1Changed(CatalogChoice? value)
    {
        if (value is not null) EditEquipSkill1 = value.Id;
    }
    partial void OnEditEquipSkill1Changed(decimal value) => SelectedEditSkill1 = SkillChoices.FirstOrDefault(c => c.Id == value);

    [ObservableProperty] private CatalogChoice? _selectedEditSkill2;
    partial void OnSelectedEditSkill2Changed(CatalogChoice? value)
    {
        if (value is not null) EditEquipSkill2 = value.Id;
    }
    partial void OnEditEquipSkill2Changed(decimal value) => SelectedEditSkill2 = SkillChoices.FirstOrDefault(c => c.Id == value);

    [ObservableProperty] private CatalogChoice? _selectedAddSkill1;
    partial void OnSelectedAddSkill1Changed(CatalogChoice? value)
    {
        if (value is not null) AddEquipSkill1 = value.Id;
    }
    partial void OnAddEquipSkill1Changed(decimal value) => SelectedAddSkill1 = SkillChoices.FirstOrDefault(c => c.Id == value);

    [ObservableProperty] private CatalogChoice? _selectedAddSkill2;
    partial void OnSelectedAddSkill2Changed(CatalogChoice? value)
    {
        if (value is not null) AddEquipSkill2 = value.Id;
    }
    partial void OnAddEquipSkill2Changed(decimal value) => SelectedAddSkill2 = SkillChoices.FirstOrDefault(c => c.Id == value);

    [ObservableProperty] private CatalogChoice? _selectedEditDeco1;
    partial void OnSelectedEditDeco1Changed(CatalogChoice? value)
    {
        if (value is not null) EditEquipDeco1 = value.Id;
    }
    partial void OnEditEquipDeco1Changed(decimal value) => SelectedEditDeco1 = DecorationChoices.FirstOrDefault(c => c.Id == value);

    [ObservableProperty] private CatalogChoice? _selectedEditDeco2;
    partial void OnSelectedEditDeco2Changed(CatalogChoice? value)
    {
        if (value is not null) EditEquipDeco2 = value.Id;
    }
    partial void OnEditEquipDeco2Changed(decimal value) => SelectedEditDeco2 = DecorationChoices.FirstOrDefault(c => c.Id == value);

    [ObservableProperty] private CatalogChoice? _selectedEditDeco3;
    partial void OnSelectedEditDeco3Changed(CatalogChoice? value)
    {
        if (value is not null) EditEquipDeco3 = value.Id;
    }
    partial void OnEditEquipDeco3Changed(decimal value) => SelectedEditDeco3 = DecorationChoices.FirstOrDefault(c => c.Id == value);

    [ObservableProperty] private CatalogChoice? _selectedEditCharm;
    partial void OnSelectedEditCharmChanged(CatalogChoice? value)
    {
        if (value is not null) EditEquipCharm = value.Id;
    }
    partial void OnEditEquipCharmChanged(decimal value) => SelectedEditCharm = CharmChoices.FirstOrDefault(c => c.Id == value);

    [ObservableProperty] private CatalogChoice? _selectedAddCharm;
    partial void OnSelectedAddCharmChanged(CatalogChoice? value)
    {
        if (value is not null) AddEquipCharm = value.Id;
    }
    partial void OnAddEquipCharmChanged(decimal value) => SelectedAddCharm = CharmChoices.FirstOrDefault(c => c.Id == value);
}
