using System.Linq;
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using MhguSave.App.ViewModels;

namespace MhguSave.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private bool _closeApproved;
    private bool _promptOpen;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.S)
        {
            TrySave();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => TrySave();

    private bool TrySave()
    {
        var invalidDraft = this.GetVisualDescendants().OfType<IntegerInput>().FirstOrDefault(input =>
            input.HasInvalidDraft && !input.GetVisualAncestors().OfType<Control>().Any(parent => Equals(parent.Tag, "CreationDraft")));
        if (invalidDraft is not null)
        {
            if (ViewModel is not null) ViewModel.Status = $"Fix {invalidDraft.Name?.Replace("Input", "") ?? "the numeric field"} before saving.";
            return false;
        }
        var saveInputs = this.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsVisible && control.GetVisualAncestors().OfType<Control>()
                .All(parent => parent.IsVisible && !Equals(parent.Tag, "CreationDraft"))).ToArray();
        if (saveInputs.Any(DataValidationErrors.GetHasErrors)
            || saveInputs.OfType<IntegerInput>().Any(control => !control.HasValidInput)
            || saveInputs.OfType<NumericUpDown>().Any(control => control.Value is null))
        {
            if (ViewModel is not null) ViewModel.Status = "Fix the highlighted or empty numeric fields before saving.";
            return false;
        }
        ViewModel?.SaveCommand.Execute(null);
        return ViewModel?.HasUnsavedChanges == false;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeApproved || ViewModel?.HasUnsavedChanges != true) return;
        e.Cancel = true;
        if (_promptOpen) return;
        if (await ResolveUnsavedChanges())
        {
            _closeApproved = true;
            Close();
        }
    }

    private async Task<bool> ResolveUnsavedChanges()
    {
        if (ViewModel?.HasUnsavedChanges != true) return true;
        _promptOpen = true;
        try
        {
            var dialog = new Window
            {
                Title = "Unsaved changes",
                Width = 470,
                Height = 215,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark,
            };
            var save = new Button { Content = "Save changes" };
            var discard = new Button { Content = "Discard changes" };
            var cancel = new Button { Content = "Cancel" };
            save.Click += (_, _) => dialog.Close("save");
            discard.Click += (_, _) => dialog.Close("discard");
            cancel.Click += (_, _) => dialog.Close("cancel");
            dialog.Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 20,
                Children =
                {
                    new TextBlock { Text = "Save your changes before continuing?", FontSize = 18 },
                    new TextBlock { Text = "Your edits have not been written to the save file." },
                    new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 10, Children = { save, discard, cancel } },
                },
            };
            var result = await dialog.ShowDialog<string?>(this);
            if (result == "discard") return true;
            if (result != "save") return false;
            return TrySave();
        }
        finally { _promptOpen = false; }
    }

    private async void OnRevertClick(object? sender, RoutedEventArgs e)
    {
        if (await ResolveUnsavedChanges()) ViewModel?.Revert();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open an MHGU save (system)",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MHGU save") { Patterns = ["system", "system_backup"] },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ],
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            if (await ResolveUnsavedChanges()) ViewModel?.Load(path);
        }
    }

    private async void OnLoadNamesClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open an item names CSV (id,name)",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CSV") { Patterns = ["*.csv"] },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ],
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            ViewModel?.LoadItemNames(path);
        }
    }
}
