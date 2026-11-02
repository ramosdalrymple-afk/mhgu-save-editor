namespace MhguSave.Format.Tests;

internal static class OfflineSaveWriter
{
    public static SaveResult Write(string path, SaveContainer save, bool makeBackup = true) =>
        SaveWriter.WriteCore(path, save, makeBackup, () => Array.Empty<string>());
}
