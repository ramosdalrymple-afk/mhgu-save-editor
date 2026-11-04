using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

public partial class CollectionEntry : ObservableObject
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public string IconKey { get; init; } = "ui:collections";
    public int Rarity { get; init; }
    public string RarityLabel => GameRarity.Label(Rarity);
    [ObservableProperty] private bool _unlocked;
    public Action<bool>? Changed { get; init; }
    partial void OnUnlockedChanged(bool value) => Changed?.Invoke(value);
}

public partial class MainViewModel
{
    public string[] CollectionKinds { get; } = ["Hunter Arts", "Guild Card awards", "Obtained items"];
    public ObservableCollection<CollectionEntry> CollectionEntries { get; } = [];
    [ObservableProperty] private int _collectionKind;
    [ObservableProperty] private string _collectionQuery = "";
    partial void OnCollectionKindChanged(int value) => RefreshCollectionEntries();
    partial void OnCollectionQueryChanged(string value) => RefreshCollectionEntries();

    private void RefreshCollectionEntries()
    {
        CollectionEntries.Clear();
        if (_slot is null) return;
        var slot = _slot;
        var kind = CollectionKind;
        var ids = kind == 0 ? LocalGameCatalog.HunterArts.Where(c => c.Id > 0).Select(c => c.Id)
            : Enumerable.Range(kind == 1 ? 0 : 1, kind == 1 ? 132 : 2990);
        foreach (var id in ids)
        {
            var name = kind == 0 ? LocalGameCatalog.HunterArtName(id) : kind == 1 ? LocalGameCatalog.AwardName(id) : _catalog.NameOf(id);
            var description = kind == 1 ? LocalGameCatalog.AwardDescription(id) : "";
            if (CollectionQuery.Length > 0 && !name.Contains(CollectionQuery, StringComparison.OrdinalIgnoreCase)
                && !description.Contains(CollectionQuery, StringComparison.OrdinalIgnoreCase) && id.ToString() != CollectionQuery.Trim()) continue;
            var unlocked = kind == 0 ? slot.IsHunterArtUnlocked(id) : kind == 1 ? slot.Awards[id] : slot.ItemsObtained[id];
            CollectionEntries.Add(new() { Id = id, Name = name, Description = description,
                IconKey = kind == 2 ? $"item:{id}" : kind == 1 ? $"award:{id}" : "ui:arts", Rarity = kind == 2 ? GameRarity.Item(id) : 0, Unlocked = unlocked, Changed = value =>
            {
                if (kind == 0) slot.SetHunterArtUnlocked(id, value);
                else if (kind == 1) slot.Awards[id] = value;
                else slot.ItemsObtained[id] = value;
                RefreshSummary();
            }});
        }
    }
}
