using MhguSave.App.ViewModels;
using MhguSave.Format;

namespace MhguSave.App.Tests;

internal static class TestEditor
{
    public static MainViewModel Create() => new(() => Array.Empty<string>(),
        (path, save) => SaveWriter.WriteCore(path, save, true, () => Array.Empty<string>()));
}
