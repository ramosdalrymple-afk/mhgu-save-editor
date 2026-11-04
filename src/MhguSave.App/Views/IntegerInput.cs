using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MhguSave.App.ViewModels;

namespace MhguSave.App.Views;

/// <summary>Keeps invalid typed text visible instead of restoring an older value on blur.</summary>
public class IntegerInput : NumericUpDown
{
    protected override Type StyleKeyOverride => typeof(NumericUpDown);

    public bool HasValidInput => decimal.TryParse(Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var number)
        && decimal.Truncate(number) == number && number >= Minimum && number <= Maximum && number == Value;

    public bool HasInvalidDraft => Classes.Contains("invalidDraft");
    internal bool IsCreationInput => this.GetLogicalAncestors().OfType<Control>()
        .Any(parent => Equals(parent.Tag, "CreationDraft"));

    internal void MarkInvalidDraft() => Classes.Set("invalidDraft", true);

    internal void ResetInvalidDraft()
    {
        Text = Value?.ToString("0", CultureInfo.CurrentCulture);
        Classes.Set("invalidDraft", false);
    }

    protected override void OnTextChanged(string? oldValue, string? newValue)
    {
        base.OnTextChanged(oldValue, newValue);
        if (HasValidInput) Classes.Set("invalidDraft", false);
        else if (IsKeyboardFocusWithin) MarkInvalidDraft();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        if (HasValidInput) base.OnLostFocus(e);
        else if (HasInvalidDraft && !IsCreationInput)
        {
            var vm = DataContext as MainViewModel ?? this.GetVisualAncestors().OfType<Control>()
                .Select(c => c.DataContext).OfType<MainViewModel>().FirstOrDefault();
            if (vm is { IsLoaded: true })
            {
                vm.HasUnsavedChanges = true;
                vm.Status = "Fix the invalid numeric field before saving.";
            }
        }
    }
}
