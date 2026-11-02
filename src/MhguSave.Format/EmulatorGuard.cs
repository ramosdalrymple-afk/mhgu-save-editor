using System.Diagnostics;

namespace MhguSave.Format;

/// <summary>
/// The emulator keeps the save in memory and writes its copy back on exit, silently
/// discarding external edits. An editor must refuse to write while one of these runs.
/// </summary>
public static class EmulatorGuard
{
    private static readonly (string Process, string Name)[] Known =
    [
        ("ryujinx", "Ryujinx"),
        ("yuzu", "yuzu"),
        ("sudachi", "Sudachi"),
        ("suyu", "Suyu"),
        ("citron", "Citron"),
        ("kenjinx", "Kenji-NX"),
    ];

    /// <summary>Names of the Switch emulators currently running, if any.</summary>
    public static IReadOnlyList<string> RunningEmulators()
    {
        var running = new List<string>();
        foreach (var (process, name) in Known)
        {
            Process[] matches;
            try
            {
                matches = Process.GetProcessesByName(process);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (matches.Length > 0)
            {
                running.Add(name);
            }
            foreach (var match in matches) match.Dispose();
        }

        return running;
    }
}
