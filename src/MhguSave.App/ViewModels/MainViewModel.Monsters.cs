using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

public sealed record MonsterChoice(int Index, string Name, int Hunted, int Captured)
{
    public string RecordLine => $"#{Index} · Hunted {Hunted} · Captured {Captured}";
    public string IconKey => $"monster:{Index}";
}

public partial class MonsterEditor : ObservableObject
{
    public int Index { get; }
    public string Name { get; }
    public string IconKey => $"monster:{Index}";
    public string SizeRecord { get; }
    public bool IsDirty { get; private set; }
    [ObservableProperty] private decimal _slain;
    [ObservableProperty] private decimal _captured;
    public MonsterEditor(MonsterChoice choice, MonsterRecord record, Action changed)
    {
        Index = choice.Index; Name = choice.Name;
        Slain = record.Slain; Captured = record.Captured;
        SizeRecord = record.SmallestPercent == 0 && record.LargestPercent == 0 ? "No size record"
            : $"Smallest {record.SmallestPercent}% · largest {record.LargestPercent}%";
        PropertyChanged += (_, _) => { IsDirty = true; changed(); };
    }
    public void AcceptChanges() => IsDirty = false;
}

public partial class MainViewModel
{
    public bool IsMonstersSection => SelectedSection == 7;
    public ObservableCollection<MonsterChoice> Monsters { get; } = [];
    [ObservableProperty] private string _monsterQuery = "";
    [ObservableProperty] private MonsterChoice? _selectedMonster;
    [ObservableProperty] private MonsterEditor? _monsterDraft;
    [ObservableProperty] private string _monsterStatus = "Hunted totals include slain and captured monsters.";
    private bool _refreshingMonsters;
    partial void OnMonsterQueryChanged(string? oldValue, string newValue)
    {
        if (_refreshingMonsters) return;
        if (!ApplyMonsterDraft())
        {
            _refreshingMonsters = true; MonsterQuery = oldValue ?? ""; _refreshingMonsters = false; return;
        }
        RefreshMonsters();
    }
    partial void OnSelectedMonsterChanged(MonsterChoice? oldValue, MonsterChoice? newValue)
    {
        if (_refreshingMonsters || _slot is null || newValue is null) return;
        if (!ApplyMonsterDraft())
        {
            _refreshingMonsters = true; SelectedMonster = oldValue; _refreshingMonsters = false; return;
        }
        MonsterDraft = new(newValue, _slot.ReadMonster(newValue.Index), () => HasUnsavedChanges = true);
    }
    private void RefreshMonsters()
    {
        _refreshingMonsters = true;
        var index = SelectedMonster?.Index;
        MonsterDraft = null; Monsters.Clear();
        if (_slot is not null)
            for (var i = 1; i <= CharacterSlot.MonsterCount; i++)
            {
                var name = LocalGameCatalog.MonsterName(i);
                if (MonsterQuery.Length > 0 && !name.Contains(MonsterQuery, StringComparison.OrdinalIgnoreCase) && i.ToString() != MonsterQuery.Trim()) continue;
                var record = _slot.ReadMonster(i);
                Monsters.Add(new(i, name, record.Hunted, record.Captured));
            }
        SelectedMonster = Monsters.FirstOrDefault(m => m.Index == index) ?? Monsters.FirstOrDefault();
        _refreshingMonsters = false;
        if (_slot is not null && SelectedMonster is not null)
            MonsterDraft = new(SelectedMonster, _slot.ReadMonster(SelectedMonster.Index), () => HasUnsavedChanges = true);
    }
    private bool ApplyMonsterDraft()
    {
        if (_slot is null || MonsterDraft?.IsDirty != true) return true;
        try
        {
            var m = MonsterDraft;
            RequireInteger(m.Slain, 0, ushort.MaxValue, "Slain count");
            RequireInteger(m.Captured, 0, ushort.MaxValue, "Capture count");
            _slot.WriteMonsterCounts(m.Index, (ushort)m.Slain, (ushort)m.Captured);
            m.AcceptChanges(); RefreshSummary();
            MonsterStatus = "Monster counts staged. Guild Card monster summary refresh is handled by the game.";
            if (m.Slain > 9999 || m.Captured > 9999) MonsterStatus += " Warning: a count exceeds the game's 9,999 cap.";
            return true;
        }
        catch (ArgumentException ex) { MonsterStatus = Status = ex.Message; return false; }
    }
    [RelayCommand] private void ApplyMonster() { if (ApplyMonsterDraft()) RefreshMonsters(); }
    [RelayCommand] private void ResetMonster() { RefreshMonsters(); RecomputeUnsavedChanges(); }
}
