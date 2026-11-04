using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

public partial class MainViewModel
{
    public bool IsLoadoutsSection => SelectedSection == 5;
    public string[] LoadoutOwners { get; } = ["Hunter · My Sets", "Palico equipment sets"];
    public ObservableCollection<LoadoutChoice> Loadouts { get; } = [];
    [ObservableProperty] private int _loadoutOwner;
    [ObservableProperty] private LoadoutChoice? _selectedLoadout;
    [ObservableProperty] private LoadoutEditor? _loadoutDraft;
    [ObservableProperty] private string _loadoutStatus = "Select a registered set to edit.";
    private bool _loadingLoadouts;

    partial void OnLoadoutOwnerChanged(int oldValue, int newValue)
    {
        if (_loadingLoadouts) return;
        if (!ApplyLoadoutDraft())
        {
            _loadingLoadouts = true; LoadoutOwner = oldValue; _loadingLoadouts = false;
            return;
        }
        RefreshLoadouts();
    }

    partial void OnSelectedLoadoutChanged(LoadoutChoice? oldValue, LoadoutChoice? newValue)
    {
        if (_loadingLoadouts || _slot is null || newValue is null) return;
        if (!ApplyLoadoutDraft(oldValue?.Index))
        {
            _loadingLoadouts = true; SelectedLoadout = oldValue; _loadingLoadouts = false;
            return;
        }
        OpenLoadout(newValue.Index);
    }

    private void OpenLoadout(int index)
    {
        if (_slot is null) return;
        LoadoutDraft = new LoadoutEditor(_slot, LoadoutOwner == 1, _slot.ReadEquipmentSet(LoadoutOwner == 1, index),
            _equipCatalog, () => HasUnsavedChanges = true);
    }

    private void RefreshLoadouts()
    {
        _loadingLoadouts = true;
        var selected = SelectedLoadout?.Index ?? 0;
        LoadoutDraft = null;
        Loadouts.Clear();
        if (_slot is not null)
            for (var i = 0; i < (LoadoutOwner == 1 ? CharacterSlot.PalicoSetCount : CharacterSlot.HunterSetCount); i++)
                Loadouts.Add(new(i, $"{i + 1:00} · {_slot.ReadEquipmentSet(LoadoutOwner == 1, i).Name}"));
        SelectedLoadout = Loadouts.FirstOrDefault(s => s.Index == selected) ?? Loadouts.FirstOrDefault();
        _loadingLoadouts = false;
        if (SelectedLoadout is not null) OpenLoadout(SelectedLoadout.Index);
    }

    private bool ApplyLoadoutDraft(int? index = null)
    {
        if (_slot is null || LoadoutDraft?.IsDirty != true) return true;
        try
        {
            var target = index ?? SelectedLoadout?.Index ?? throw new ArgumentException("Choose a set first.");
            // The owner selector has already changed when this callback is entered.
            var palico = !LoadoutDraft.IsHunter;
            var data = LoadoutDraft.ToData();
            _slot.WriteEquipmentSet(palico, target, data);
            LoadoutDraft.AcceptChanges();
            var row = Loadouts.FirstOrDefault(c => c.Index == target);
            if (row is not null)
            {
                _loadingLoadouts = true;
                var choice = new LoadoutChoice(target, $"{target + 1:00} · {data.Name}");
                Loadouts[Loadouts.IndexOf(row)] = choice;
                if (SelectedLoadout?.Index == target) SelectedLoadout = choice;
                _loadingLoadouts = false;
            }
            RefreshSummary();
            LoadoutStatus = "Set staged. Save changes writes it to disk.";
            if (!palico)
            {
                var warnings = _slot.EquipmentSetWarnings(target);
                if (warnings.Count > 0) LoadoutStatus += " Warning: " + string.Join("; ", warnings);
            }
            return true;
        }
        catch (ArgumentException ex) { LoadoutStatus = Status = ex.Message; return false; }
    }

    [RelayCommand] private void ApplyLoadout() => ApplyLoadoutDraft();
    [RelayCommand] private void ResetLoadout()
    {
        if (SelectedLoadout is not null) OpenLoadout(SelectedLoadout.Index);
        LoadoutStatus = "Set editor reset to its last applied values.";
        RecomputeUnsavedChanges();
    }
    [RelayCommand] private void ClearLoadout()
    {
        if (_slot is null || SelectedLoadout is null) return;
        _slot.ClearEquipmentSet(LoadoutOwner == 1, SelectedLoadout.Index);
        RefreshLoadouts(); RefreshSummary();
        LoadoutStatus = "Set cleared. Save changes writes it to disk.";
    }
    [RelayCommand] private void RegisterCurrentLoadout()
    {
        if (_slot is null || SelectedLoadout is null || LoadoutOwner != 0) return;
        if (!ApplyPendingEditors()) return;
        try
        {
            _slot.RegisterCurrentEquipment(SelectedLoadout.Index);
            RefreshLoadouts(); RefreshSummary();
            LoadoutStatus = "Current equipment registered in this set. Save changes writes it to disk.";
        }
        catch (ArgumentException ex) { LoadoutStatus = Status = ex.Message; }
    }
}
