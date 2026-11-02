using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MhguSave.App.ViewModels;

namespace MhguSave.App.Views;

/// <summary>Keeps invalid typed text visible instead of restoring an older value on blur.</summary>
public class IntegerInput : NumericUpDown
{
    protected override Type StyleKeyOverride => typeof(NumericUpDown);

    public bool HasValidInput => decimal.TryParse(Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var number)
        && decimal.Truncate(number) == number && number >= Minimum && number <= Maximum && number == Value;

    public bool HasInvalidDraft { get; private set; }

    protected override void OnTextChanged(string? oldValue, string? newValue)
    {
        base.OnTextChanged(oldValue, newValue);
        if (HasValidInput) HasInvalidDraft = false;
        else if (IsKeyboardFocusWithin) HasInvalidDraft = true;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        if (HasValidInput) base.OnLostFocus(e);
        else if (HasInvalidDraft && !this.GetVisualAncestors().OfType<Control>().Any(c => Equals(c.Tag, "CreationDraft"))
                 && DataContext is MainViewModel { IsLoaded: true } vm)
        {
            vm.HasUnsavedChanges = true;
            vm.Status = "Fix the invalid numeric field before saving.";
        }
    }
}
