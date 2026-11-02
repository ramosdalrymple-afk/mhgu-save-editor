using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

/// <summary>
/// One row of the item box or pouch. Writes go straight through to the save image; the
/// file is only touched by <see cref="MainViewModel.Save"/>.
/// </summary>
public sealed partial class ItemRow : ObservableObject
{
    private readonly PackedItemList _list;
    private ItemCatalog _catalog;

    public ItemRow(PackedItemList list, int index, ItemCatalog catalog)
    {
        _list = list;
        _catalog = catalog;
        Index = index;
    }

    public int Index { get; }

    public int SlotNumber => Index + 1;

    public int Id
    {
        get => _list[Index].Id;
        set
        {
            var count = value == 0 ? 0 : Math.Max(1, _list[Index].Count);
            _list[Index] = new ItemSlot(value, count);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IconKey));
        }
    }

    public int Count
    {
        get => _list[Index].Count;
        set
        {
            _list[Index] = new ItemSlot(_list[Index].Id, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Display));
        }
    }

    public bool IsEmpty => _list[Index].IsEmpty;

    public string IconKey => IsEmpty ? string.Empty : $"item:{Id}";

    public string Name => IsEmpty ? "(empty)" : _catalog.NameOf(_list[Index].Id);

    public string Display => $"{SlotNumber,4}  {Name}  ·  id {Id}  ×{Count}";

    public void SetCatalog(ItemCatalog catalog)
    {
        _catalog = catalog;
        Refresh();
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(IconKey));
    }
}
