using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MhguSave.App.ViewModels;

namespace MhguSave.App.Views;

/// <summary>Validates the associated form before executing its command.</summary>
public class ValidatedButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    protected override void OnClick()
    {
        var form = this.GetVisualAncestors().OfType<Control>().FirstOrDefault(c => c.Classes.Contains("inputForm"));
        var invalid = form?.GetVisualDescendants().OfType<IntegerInput>()
            .FirstOrDefault(input => input.HasInvalidDraft || input.IsEffectivelyVisible && !input.HasValidInput);
        if (invalid is not null)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Status = $"Enter a whole number from {invalid.Minimum} to {invalid.Maximum} in the highlighted field.";
                vm.EquipStatus = vm.Status;
            }
            DataValidationErrors.SetError(invalid, new ArgumentException("Enter a whole number within the allowed range."));
            invalid.BringIntoView();
            invalid.Focus();
            return;
        }
        base.OnClick();
    }
}
