using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

public sealed record PalicoChoice(int Index, string Label)
{
    public string IconKey => Index >= 0 ? "ui:palico" : string.Empty;
    public override string ToString() => Label;
}

public partial class PalicoEditor : ObservableObject
{
    public int Index { get; }
    public int Forte { get; }
    public string ForteName => Forte is >= 0 and < 8 ? new[] { "Charisma", "Fighting", "Protection", "Assisting", "Healing", "Bombing", "Gathering", "Beast" }[Forte] : $"Forte {Forte}";
    public string EquippedMoves { get; }
    public string EquippedSkills { get; }
    public string LearnedMoves { get; }
    public string LearnedSkills { get; }
    public bool IsDirty { get; private set; }
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _level;
    [ObservableProperty] private decimal _experience;
    [ObservableProperty] private CatalogChoice? _target;
    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _originalOwner = "";
    public CatalogChoice[] Targets { get; } = new[] { "None", "Small only", "Small first", "Balanced", "Large first", "Large only" }
        .Select((n, i) => new CatalogChoice(i, n)).ToArray();

    public PalicoEditor(int index, PalicoData data, Action changed)
    {
        Index = index; Forte = data.Forte;
        Name = data.Name; Level = data.Level; Experience = data.Experience;
        Greeting = data.Greeting; OriginalOwner = data.OriginalOwner;
        Target = Targets.FirstOrDefault(t => t.Id == data.Target);
        EquippedMoves = string.Join(", ", data.EquippedMoves.Where(v => v != 0));
        EquippedSkills = string.Join(", ", data.EquippedSkills.Where(v => v != 0));
        LearnedMoves = string.Join(", ", data.LearnedMoves.Where(v => v != 57));
        LearnedSkills = string.Join(", ", data.LearnedSkills.Where(v => v != 96));
        PropertyChanged += (_, _) => { IsDirty = true; changed(); };
    }
    public void AcceptChanges() => IsDirty = false;
}

public partial class MainViewModel
{
    public bool IsPalicoesSection => SelectedSection == 6;
    public string[] PalicoOwners { get; } = ["Companions · 84 places", "For hire · 24 places"];
    public ObservableCollection<PalicoChoice> Palicoes { get; } = [];
    public ObservableCollection<PalicoChoice> PalicoRoleChoices { get; } = [];
    [ObservableProperty] private int _palicoOwner;
    [ObservableProperty] private PalicoChoice? _selectedPalico;
    [ObservableProperty] private PalicoEditor? _palicoDraft;
    [ObservableProperty] private PalicoChoice? _selectedProwler;
    [ObservableProperty] private PalicoChoice? _selectedBuddy1;
    [ObservableProperty] private PalicoChoice? _selectedBuddy2;
    [ObservableProperty] private string _palicoStatus = "Choose a companion or a Palico offered for hire.";
    private bool _refreshingPalicoes;

    partial void OnPalicoOwnerChanged(int oldValue, int newValue)
    {
        if (_refreshingPalicoes) return;
        if (!ApplyPalicoDraft())
        {
            _refreshingPalicoes = true; PalicoOwner = oldValue; _refreshingPalicoes = false;
            return;
        }
        RefreshPalicoes();
    }
    partial void OnSelectedPalicoChanged(PalicoChoice? oldValue, PalicoChoice? newValue)
    {
        if (_refreshingPalicoes || _slot is null || newValue is null) return;
        if (!ApplyPalicoDraft())
        {
            _refreshingPalicoes = true; SelectedPalico = oldValue; _refreshingPalicoes = false; return;
        }
        PalicoDraft = new(newValue.Index, _slot.ReadPalico(newValue.Index), () => HasUnsavedChanges = true);
    }
    partial void OnSelectedProwlerChanged(PalicoChoice? value) => ChangePalicoRole(0, value);
    partial void OnSelectedBuddy1Changed(PalicoChoice? value) => ChangePalicoRole(1, value);
    partial void OnSelectedBuddy2Changed(PalicoChoice? value) => ChangePalicoRole(2, value);

    private void ChangePalicoRole(int role, PalicoChoice? choice)
    {
        if (_refreshingPalicoes || _slot is null || choice is null) return;
        if (!ApplyPalicoDraft()) { RefreshPalicoRoles(); return; }
        _slot.WritePalicoRole(role, choice.Index);
        RefreshPalicoRoles(); RefreshSummary();
        PalicoStatus = "Palico roles staged. A companion can hold one role at a time.";
    }

    private void RefreshPalicoRoles()
    {
        _refreshingPalicoes = true;
        PalicoRoleChoices.Clear();
        PalicoRoleChoices.Add(new(-1, "None"));
        if (_slot is not null)
        {
            for (var i = 0; i < CharacterSlot.CompanionCount; i++)
                if (_slot.ReadPalico(i) is { Name.Length: > 0 } p) PalicoRoleChoices.Add(new(i, $"#{i + 1} · {p.Name}"));
            SelectedProwler = PalicoRoleChoices.FirstOrDefault(p => p.Index == _slot.ReadPalicoRole(0));
            SelectedBuddy1 = PalicoRoleChoices.FirstOrDefault(p => p.Index == _slot.ReadPalicoRole(1));
            SelectedBuddy2 = PalicoRoleChoices.FirstOrDefault(p => p.Index == _slot.ReadPalicoRole(2));
        }
        _refreshingPalicoes = false;
    }

    private void RefreshPalicoes()
    {
        _refreshingPalicoes = true;
        var selected = SelectedPalico?.Index;
        PalicoDraft = null; Palicoes.Clear();
        if (_slot is not null)
            for (var i = PalicoOwner == 0 ? 0 : 84; i < (PalicoOwner == 0 ? 84 : 108); i++)
                if (_slot.ReadPalico(i) is { Name.Length: > 0 } p) Palicoes.Add(new(i, $"#{i % 84 + 1} · {p.Name} · Lv {p.Level}"));
        SelectedPalico = Palicoes.FirstOrDefault(p => p.Index == selected) ?? Palicoes.FirstOrDefault();
        _refreshingPalicoes = false;
        if (_slot is not null && SelectedPalico is not null)
            PalicoDraft = new(SelectedPalico.Index, _slot.ReadPalico(SelectedPalico.Index), () => HasUnsavedChanges = true);
        RefreshPalicoRoles();
    }

    private bool ApplyPalicoDraft()
    {
        if (_slot is null || PalicoDraft?.IsDirty != true) return true;
        try
        {
            var p = PalicoDraft;
            RequireInteger(p.Level, 1, 99, "Palico level");
            RequireInteger(p.Experience, 0, uint.MaxValue, "Palico experience");
            _slot.WritePalicoIdentity(p.Index, p.Name, (uint)p.Experience, (int)p.Level, p.Forte,
                p.Target?.Id ?? _slot.ReadPalico(p.Index).Target, p.Greeting, p.OriginalOwner);
            p.AcceptChanges(); RefreshSummary();
            PalicoStatus = "Palico staged. Save changes writes all edited companions and hireables.";
            return true;
        }
        catch (ArgumentException ex) { PalicoStatus = Status = ex.Message; return false; }
    }
    [RelayCommand] private void ApplyPalico() { if (ApplyPalicoDraft()) RefreshPalicoes(); }
    [RelayCommand] private void ResetPalico() { RefreshPalicoes(); RecomputeUnsavedChanges(); }
}
