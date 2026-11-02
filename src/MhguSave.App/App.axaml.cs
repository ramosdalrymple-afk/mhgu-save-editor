using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.IO;
using MhguSave.App.ViewModels;
using MhguSave.App.Views;

namespace MhguSave.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            // Optional: MhguSave.App.exe "<...>\system" opens the save straight away.
            var args = desktop.Args;
            if (args is { Length: > 0 } && (File.Exists(args[0]) || Directory.Exists(args[0])))
            {
                viewModel.Load(args[0]);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
