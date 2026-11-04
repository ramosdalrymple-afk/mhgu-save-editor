using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

public partial class AppearanceField : ObservableObject
{
    public required string Label { get; init; }
    public required IReadOnlyList<CatalogChoice> Choices { get; init; }
    [ObservableProperty] private CatalogChoice? _selected;
}

public sealed class AppearanceEditor
{
    public ObservableCollection<AppearanceField> Fields { get; } = [];
    public ObservableCollection<LoadoutColor> Colors { get; } = [];
    public bool IsDirty { get; private set; }
    private readonly HunterAppearance _original;
    public AppearanceEditor(HunterAppearance data, Action changed)
    {
        _original = data;
        string[] labels = ["Body type", "Face", "Hairstyle", "Voice", "Features", "Clothing"];
        int[] values = [data.Body, data.Face, data.Hair, data.Voice, data.Features, data.Clothing];
        int[] counts = [2, 18, 30, 20, 15, 7];
        for (var i = 0; i < labels.Length; i++)
        {
            var start = i == 3 ? 1 : 0;
            var choices = Enumerable.Range(start, counts[i]).Select(id => new CatalogChoice(id,
                i == 0 ? id == 0 ? "Male" : "Female" : i == 4 && id == 14 ? "None" : $"{labels[i]} {(i == 3 ? id : id + 1)}")).ToList();
            if (choices.All(c => c.Id != values[i])) choices.Add(new(values[i], $"Existing value {values[i]}"));
            var field = new AppearanceField { Label = labels[i], Choices = choices, Selected = choices.Single(c => c.Id == values[i]) };
            field.PropertyChanged += (_, _) => { IsDirty = true; changed(); };
            Fields.Add(field);
        }
        for (var i = 0; i < 2; i++)
        {
            var color = i == 0 ? data.SkinColor : data.ClothingColor;
            var hex = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(color).ToString("X8");
            var row = new LoadoutColor { Label = i == 0 ? "Skin" : "Clothing", Hex = hex, OriginalHex = hex };
            row.PropertyChanged += (_, _) => { IsDirty = true; changed(); };
            Colors.Add(row);
        }
    }

    public HunterAppearance ToData() => new((byte)(Fields[0].Selected?.Id ?? _original.Body),
        (byte)(Fields[1].Selected?.Id ?? _original.Face), (byte)(Fields[2].Selected?.Id ?? _original.Hair),
        (byte)(Fields[3].Selected?.Id ?? _original.Voice), (byte)(Fields[4].Selected?.Id ?? _original.Features),
        (byte)(Fields[5].Selected?.Id ?? _original.Clothing), Colors[0].Parse(), Colors[1].Parse());
    public void AcceptChanges() => IsDirty = false;
}

public partial class MainViewModel
{
    [ObservableProperty] private AppearanceEditor? _appearanceDraft;
    [ObservableProperty] private string _appearanceStatus = "Appearance choices are staged until you save.";
    private void RefreshAppearance()
    {
        AppearanceDraft = _slot is null ? null : new(_slot.ReadAppearance(), () => HasUnsavedChanges = true);
    }
    private bool ApplyAppearanceDraft()
    {
        if (_slot is null || AppearanceDraft?.IsDirty != true) return true;
        try
        {
            _slot.WriteAppearance(AppearanceDraft.ToData());
            AppearanceDraft.AcceptChanges(); RefreshSummary();
            AppearanceStatus = "Appearance staged in the player record, character summary, and Guild Card.";
            return true;
        }
        catch (ArgumentException ex) { AppearanceStatus = Status = ex.Message; return false; }
    }
    [RelayCommand] private void ApplyAppearance() => ApplyAppearanceDraft();
    [RelayCommand] private void ResetAppearance() { RefreshAppearance(); RecomputeUnsavedChanges(); }
}
