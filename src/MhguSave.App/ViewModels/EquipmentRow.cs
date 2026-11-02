using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

/// <summary>
/// One row of the equipment box. Every edit writes straight into the save image; the file
/// is only touched by <see cref="MainViewModel.Save"/>.
/// </summary>
public sealed class EquipmentRow : ObservableObject
{
    private readonly EquipmentBox _box;
    private readonly EquipmentCatalog _catalog;

    public EquipmentRow(EquipmentBox box, int index, EquipmentCatalog catalog)
    {
        _box = box;
        _catalog = catalog;
        Index = index;
    }

    /// <summary>Raised after the row changed, so the summary can refresh.</summary>
    public event EventHandler? PieceChanged;

    public int Index { get; }

    public int SlotNumber => Index + 1;

    public EquipmentSlot Piece => _box[Index];

    public bool IsEmpty => Piece.IsEmpty;

    public string IconKey => IsEmpty ? string.Empty : $"equipment:{Type}";

    public int Type => Piece.Type;

    public int Id => Piece.Id;

    public int Level => Piece.Level;

    public string TypeName => _catalog.TypeOf(Piece.Type).Name;

    public string Name => Piece.IsEmpty ? "(empty)" : _catalog.NameOf(Piece.Type, Piece.Id);

    public string Display => Piece.IsEmpty
        ? $"{SlotNumber,4}  (empty)"
        : $"{SlotNumber,4}  {TypeName}  {Name}  Lv{Level}" +
          (Piece.Deco1 > 0 ? $"  [{_catalog.DecorationName(Piece.Deco1)}]" : string.Empty);

    /// <summary>Writes a record into this slot.</summary>
    public void SetPiece(EquipmentSlot piece)
    {
        _box[Index] = piece;
        Refresh();
    }

    /// <summary>Empties the slot.</summary>
    public void Clear()
    {
        _box[Index] = new EquipmentSlot();
        Refresh();
    }

    /// <summary>Re-reads the row after an external change.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Piece));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(MaxLevel));
        OnPropertyChanged(nameof(IconKey));
        PieceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Max level the piece accepts, from the game table.</summary>
    public int MaxLevel => _catalog.MaxLevel(Piece.Type, Piece.Id);

    /// <summary>Label for the editor header.</summary>
    public string EditorTitle => IsEmpty ? "Empty slot" : $"{TypeName} · {Name}";
}
