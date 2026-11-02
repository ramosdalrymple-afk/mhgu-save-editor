using System.Security.Cryptography;

namespace MhguSave.Format;

public sealed record SaveResult(IReadOnlyList<string> Written, IReadOnlyList<string> Backups);

/// <summary>Validates and stages both files before replacing either, with recovery on failure.</summary>
public static class SaveWriter
{
    public static readonly string[] KnownFileNames = ["system", "system_backup"];

    public static SaveResult Write(string savePath, SaveContainer edited, bool makeBackup = true)
        => WriteCore(savePath, edited, makeBackup, EmulatorGuard.RunningEmulators);

    // Process detection is injected only through an internal test seam. The public
    // writer always probes real processes; no UI or environment switch bypasses it.
    internal static SaveResult WriteCore(string savePath, SaveContainer edited, bool makeBackup,
        Func<IReadOnlyList<string>> emulatorProbe)
    {
        ArgumentNullException.ThrowIfNull(edited);
        var running = emulatorProbe();
        if (running.Count > 0)
            throw new IOException($"Close {string.Join(", ", running)} before saving.");

        var fileName = Path.GetFileName(savePath);
        if (!KnownFileNames.Contains(fileName, StringComparer.Ordinal) || edited.EntryName != fileName)
            throw new SaveFormatException("The file must be named system or system_backup and match the opened container.");

        savePath = Path.GetFullPath(savePath);
        var original = File.ReadAllBytes(savePath);
        if (!SHA256.HashData(original).AsSpan().SequenceEqual(edited.OriginalHash))
            throw new IOException("The save changed on disk after it was opened. Open it again before saving to avoid overwriting newer progress.");

        var directory = Path.GetDirectoryName(savePath)!;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        var body = edited.ToArray();
        var targets = new List<(string Path, byte[] Original, byte[] Edited, string Stage, string Recovery)>();
        var backups = new List<string>();
        var written = new List<string>();
        var completed = false;
        try
        {
            // Preflight every existing container before touching any file.
            foreach (var name in KnownFileNames)
            {
                var target = Path.Combine(directory, name);
                if (!File.Exists(target)) continue;
                var bytes = name == fileName ? original : File.ReadAllBytes(target);
                var validated = SaveContainer.Parse(bytes, name);
                var next = validated.ToArray();
                body.AsSpan(SaveContainer.HeaderSize).CopyTo(next.AsSpan(SaveContainer.HeaderSize));
                targets.Add((target, bytes, next, target + ".tmp-" + stamp, target + ".recovery-" + stamp));
            }

            foreach (var target in targets)
            {
                if (makeBackup)
                {
                    var backup = target.Path + ".bak-" + stamp;
                    using var output = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    output.Write(target.Original);
                    output.Flush(true);
                    backups.Add(backup);
                }
                using var stage = new FileStream(target.Stage, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stage.Write(target.Edited);
                stage.Flush(true);
            }

            // Check again after staging, including the sibling, for external writes.
            foreach (var target in targets)
                if (!File.ReadAllBytes(target.Path).AsSpan().SequenceEqual(target.Original))
                    throw new IOException("A save file changed while preparing the write. No files were replaced; reopen the save.");

            foreach (var target in targets)
            {
                File.Replace(target.Stage, target.Path, target.Recovery);
                written.Add(target.Path);
            }
            edited.AcceptChanges();
            completed = true;
            return new SaveResult(written, backups);
        }
        catch (Exception writeError)
        {
            var recoveryErrors = new List<Exception>();
            foreach (var target in targets.Where(t => written.Contains(t.Path)).Reverse())
            {
                try { File.Move(target.Recovery, target.Path, overwrite: true); }
                catch (Exception recoveryError) when (recoveryError is IOException or UnauthorizedAccessException)
                { recoveryErrors.Add(recoveryError); }
            }
            if (recoveryErrors.Count > 0)
                throw new IOException("Save failed and automatic recovery was incomplete. Restore the .bak or .recovery files before playing.",
                    new AggregateException(new[] { writeError }.Concat(recoveryErrors)));
            throw;
        }
        finally
        {
            foreach (var target in targets)
            {
                TryDelete(target.Stage);
                // Keep a recovery file if rollback failed.
                if (completed) TryDelete(target.Recovery);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
