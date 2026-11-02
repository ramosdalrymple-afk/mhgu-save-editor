using System.Buffers.Binary;

namespace MhguSave.Format.Tests;

/// <summary>
/// Optional read-only checks against a real save. Skipped unless <c>MHGU_TEST_SAVE</c>
/// points at a <c>system</c> file, e.g.
/// <c>set MHGU_TEST_SAVE=C:\path\to\save\system</c>.
/// </summary>
public class GoldenSaveTests
{
    private static string? SavePath => Environment.GetEnvironmentVariable("MHGU_TEST_SAVE");

    [RealSaveFact]
    public void RealSave_PassesStructuralInvariants()
    {
        var path = SavePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return; // No real save configured; the synthetic tests cover the parser.
        }

        var save = SaveContainer.Load(path);
        Assert.Equal(SaveContainer.ExpectedBodyVersion, save.SaveVersion);
        Assert.True(save.Slots.Count == 3);

        var usedSlots = save.Slots.Where(slot => slot.InUse).ToList();
        Assert.NotEmpty(usedSlots);

        foreach (var slot in usedSlots)
        {
            Assert.False(string.IsNullOrWhiteSpace(slot.Name), $"Slot {slot.Index + 1} has no name.");
            Assert.InRange(slot.Zenny, 0, CharacterSlot.PointCap);
            Assert.InRange(slot.WycademyPoints, 0, CharacterSlot.PointCap);
            Assert.InRange(slot.QuestsClearedCount, 0, 1509);
            Assert.InRange(slot.EquipmentBoxUsed, 0, 2000);

            // The slot header copy of zenny is written by the same save routine.
            Assert.Equal(slot.Zenny, (int)BinaryPrimitives.ReadUInt32LittleEndian(
                save.Span.Slice(slot.BaseOffset + 0x24)));

            foreach (var index in slot.ItemBox.UsedIndexes())
            {
                var item = slot.ItemBox[index];
                Assert.InRange(item.Count, 1, 99);
            }

            for (var i = 0; i < slot.Pouch.Count; i++)
            {
                var item = slot.Pouch[i];
                if (!item.IsEmpty)
                {
                    Assert.InRange(item.Count, 1, 99);
                }
            }
        }
    }

    [RealSaveFact]
    public void RealSave_CopyCanBeEditedAndWrittenBack()
    {
        var path = SavePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var source = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var directory = Path.Combine(Path.GetTempPath(), "mhgu-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var copyPath = Path.Combine(directory, "system");
            File.Copy(path, copyPath);
            var siblingSource = Path.Combine(source, "system_backup");
            if (File.Exists(siblingSource))
            {
                File.Copy(siblingSource, Path.Combine(directory, "system_backup"));
            }

            var save = SaveContainer.Load(copyPath);
            var slot = save.Slots.First(candidate => candidate.InUse);
            var originalName = slot.Name;
            var originalZenny = slot.Zenny;
            var newZenny = originalZenny == 1_234_567 ? 7_654_321 : 1_234_567;

            slot.Zenny = newZenny;
            var before = File.ReadAllBytes(copyPath);
            OfflineSaveWriter.Write(copyPath, save);
            var after = File.ReadAllBytes(copyPath);

            var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
            var allowed = new[]
            {
                (reloadedBase: slot.BaseOffset + 0x24, Size: 4),
                (reloadedBase: slot.BaseOffset + 0x280F, Size: 4),
            };
            Assert.All(changed, index => Assert.Contains(allowed,
                range => index >= range.reloadedBase && index < range.reloadedBase + range.Size));

            var reloaded = SaveContainer.Load(copyPath);
            var reloadedSlot = reloaded.Slots.First(candidate => candidate.InUse);
            Assert.Equal(originalName, reloadedSlot.Name);
            Assert.Equal(newZenny, reloadedSlot.Zenny);
            Assert.Equal(newZenny, (int)BinaryPrimitives.ReadUInt32LittleEndian(
                reloaded.Span.Slice(reloadedSlot.BaseOffset + 0x24)));

            if (File.Exists(Path.Combine(directory, "system_backup")))
            {
                var backup = SaveContainer.Load(Path.Combine(directory, "system_backup"));
                Assert.Equal(newZenny, backup.Slots.First(candidate => candidate.InUse).Zenny);
            }

            // Nothing but the intended bytes may differ. Note the backup files were written
            // next to the copy, so they do not affect this comparison.
            Assert.NotEqual(originalZenny, reloadedSlot.Zenny);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

public sealed class RealSaveFactAttribute : FactAttribute
{
    public RealSaveFactAttribute()
    {
        var path = Environment.GetEnvironmentVariable("MHGU_TEST_SAVE");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Skip = "Set MHGU_TEST_SAVE to run real-save checks. Synthetic tests run without it.";
    }
}
