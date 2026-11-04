using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

public sealed record LoadoutChoice(int Index, string Label)
{
    public string IconKey => "ui:loadouts";
    public override string ToString() => Label;
}

public sealed record GearChoice(ushort Index, string Label, int Type = 0, int Rarity = 0)
{
    public string IconKey => Type > 0 ? GameRarity.Icon(Type, Rarity) : string.Empty;
    public string RarityLabel => GameRarity.Label(Rarity);
    public override string ToString() => Label;
}

public partial class LoadoutPiece : ObservableObject
{
    public required string Label { get; init; }
    public IReadOnlyList<GearChoice> Choices { get; init; } = [];
    [ObservableProperty] private GearChoice? _selected;
}

public partial class LoadoutColor : ObservableObject
{
    public required string Label { get; init; }
    [ObservableProperty] private string _hex = "000000FF";
    [ObservableProperty] private bool _useDefault;
    public byte Mode { get; init; }
    public string OriginalHex { get; init; } = "000000FF";

    public uint Parse()
    {
        var text = Hex.Trim().TrimStart('#');
        if (text.Length != 8 || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgba))
            throw new ArgumentException($"{Label}: enter eight hexadecimal digits (RRGGBBAA).");
        return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(rgba);
    }
}

public partial class LoadoutEditor : ObservableObject
{
    private readonly Action _changed;
    private readonly EquipmentLoadout _original;
    public bool IsHunter { get; }
    public bool IsDirty { get; private set; }
    public ObservableCollection<LoadoutPiece> Pieces { get; } = [];
    public ObservableCollection<LoadoutColor> Colors { get; } = [];
    public IReadOnlyList<CatalogChoice> Styles { get; } = new[] { "Guild", "Striker", "Aerial", "Adept", "Alchemy", "Valor", "Unused" }
        .Select((n, i) => new CatalogChoice(i, n)).ToArray();
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private CatalogChoice? _style;
    [ObservableProperty] private decimal _art1;
    [ObservableProperty] private decimal _art2;
    [ObservableProperty] private decimal _art3;
    [ObservableProperty] private bool _sp1;
    [ObservableProperty] private bool _sp2;
    [ObservableProperty] private bool _sp3;
    public IReadOnlyList<CatalogChoice> ArtChoices => LocalGameCatalog.HunterArts;
    [ObservableProperty] private CatalogChoice? _art1Choice;
    [ObservableProperty] private CatalogChoice? _art2Choice;
    [ObservableProperty] private CatalogChoice? _art3Choice;
    partial void OnArt1ChoiceChanged(CatalogChoice? value) { if (value is not null) Art1 = value.Id; }
    partial void OnArt2ChoiceChanged(CatalogChoice? value) { if (value is not null) Art2 = value.Id; }
    partial void OnArt3ChoiceChanged(CatalogChoice? value) { if (value is not null) Art3 = value.Id; }
    partial void OnArt1Changed(decimal value) => Art1Choice = ArtChoices.FirstOrDefault(c => c.Id == value);
    partial void OnArt2Changed(decimal value) => Art2Choice = ArtChoices.FirstOrDefault(c => c.Id == value);
    partial void OnArt3Changed(decimal value) => Art3Choice = ArtChoices.FirstOrDefault(c => c.Id == value);

    public LoadoutEditor(CharacterSlot slot, bool palico, EquipmentLoadout data, EquipmentCatalog catalog, Action changed)
    {
        _original = data;
        _changed = changed;
        IsHunter = !palico;
        Name = data.Name;
        Style = Styles.FirstOrDefault(s => s.Id == data.Style);
        Art1 = data.Arts[0]; Art2 = data.Arts[1]; Art3 = data.Arts[2];
        Art1Choice = ArtChoices.FirstOrDefault(c => c.Id == data.Arts[0]);
        Art2Choice = ArtChoices.FirstOrDefault(c => c.Id == data.Arts[1]);
        Art3Choice = ArtChoices.FirstOrDefault(c => c.Id == data.Arts[2]);
        Sp1 = (data.SpArts & 1) != 0; Sp2 = (data.SpArts & 2) != 0; Sp3 = (data.SpArts & 4) != 0;
        var box = palico ? slot.PalicoEquipmentBox : slot.EquipmentBox;
        string[] labels = palico ? ["Weapon", "Head", "Body"] : ["Weapon", "Head", "Chest", "Arms", "Waist", "Legs", "Talisman"];
        for (var p = 0; p < labels.Length; p++)
        {
            var choices = new List<GearChoice> { new(CharacterSlot.NoEquipment, "None") };
            for (var i = 0; i < box.Count; i++)
            {
                var piece = box[i];
                if (!piece.IsEmpty && CharacterSlot.FitsPosition(piece.Type, palico, p))
                    choices.Add(new((ushort)i, $"#{i + 1} · {catalog.NameOf(piece.Type, piece.Id)} · Lv {piece.Level}", piece.Type, GameRarity.Equipment(piece.Type, piece.Id)));
            }
            if (choices.All(c => c.Index != data.Gear[p]))
                choices.Add(new(data.Gear[p], $"#{data.Gear[p] + 1} · unresolved existing reference"));
            var row = new LoadoutPiece { Label = labels[p], Choices = choices, Selected = choices.First(c => c.Index == data.Gear[p]) };
            row.PropertyChanged += (_, _) => MarkChanged();
            Pieces.Add(row);
        }
        string[] parts = ["Chest", "Arms", "Waist", "Legs", "Head"];
        for (var p = 0; p < 5; p++)
        {
            var hex = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(data.Pigment[p]).ToString("X8");
            var row = new LoadoutColor { Label = parts[p], Hex = hex, OriginalHex = hex, UseDefault = data.DefaultColors[p], Mode = data.ColorModes[p] };
            row.PropertyChanged += (_, _) => MarkChanged();
            Colors.Add(row);
        }
        PropertyChanged += (_, _) => MarkChanged();
    }

    private void MarkChanged() { IsDirty = true; _changed(); }
    public void AcceptChanges() => IsDirty = false;

    public EquipmentLoadout ToData()
    {
        byte ParseArt(decimal id)
        {
            if (decimal.Truncate(id) != id || id is < 0 or > 255)
                throw new ArgumentException("Hunter Art IDs must be whole numbers from 0–255.");
            return (byte)id;
        }
        return new(Name, Pieces.Select(p => p.Selected?.Index ?? CharacterSlot.NoEquipment).ToArray(),
            (byte)(Style?.Id ?? _original.Style), [ParseArt(Art1), ParseArt(Art2), ParseArt(Art3)],
            (byte)((Sp1 ? 1 : 0) | (Sp2 ? 2 : 0) | (Sp3 ? 4 : 0)), Colors.Select(c => c.Parse()).ToArray(),
            Colors.Select(c => c.Hex.Equals(c.OriginalHex, StringComparison.OrdinalIgnoreCase) ? c.Mode : (byte)0).ToArray(),
            Colors.Select(c => c.UseDefault).ToArray());
    }
}
