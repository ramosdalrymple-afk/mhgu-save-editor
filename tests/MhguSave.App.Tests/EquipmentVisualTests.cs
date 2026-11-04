using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MhguSave.App.Services;
using MhguSave.App.Views;
using MhguSave.Format;
using MhguSave.Format.Tests;
using System.Security.Cryptography;

namespace MhguSave.App.Tests;

[Collection("Avalonia UI")]
public class EquipmentVisualTests
{
    [Theory]
    [InlineData(1120, 700)]
    [InlineData(1920, 1008)]
    public async Task ArmorIconsAndCreationLayoutMatchTheEquipmentType(int width, int height)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(() => Verify(width, height), CancellationToken.None);
    }

    private static void Verify(int width, int height)
    {
        var directory = Path.Combine(Path.GetTempPath(), "mhgu-visual-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "system");
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        save.Slots[0].Name = "Visual Hunter";
        save.Slots[0].VillageStar = 1;
        int[] types = [1, 2, 3, 4, 5, 6, 22, 23, 24, 7];
        foreach (var type in types)
            (type >= 22 ? save.Slots[0].PalicoEquipmentBox : save.Slots[0].EquipmentBox).Add(new EquipmentSlot { Type = type, Id = 1 });
        File.WriteAllBytes(path, save.ToArray());
        var vm = TestEditor.Create();
        vm.Load(path);
        vm.SelectedSection = 2;
        var window = new MainWindow { DataContext = vm, Width = width, Height = height, MinHeight = 0 };
        void Settle() { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Capture(string name)
        {
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "MhguSaveEditor.sln"))) repo = repo.Parent;
            if (repo is null) return;
            var output = Path.Combine(repo.FullName, "artifacts", "spacing-pass");
            Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var frame = window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(output, $"{name}-{width}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        try
        {
            window.Show(); Settle();
            var equipment = window.GetVisualDescendants().OfType<EquipmentView>().Single();
            var hashes = new HashSet<string>();
            foreach (var type in new[] { 1, 2, 3, 4, 5, 23, 24 })
            {
                var icon = GameVisuals.Get($"equipment:{type}");
                Assert.NotNull(icon);
                using var encoded = new MemoryStream();
                icon.Save(encoded, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(hashes.Add(Convert.ToHexString(SHA256.HashData(encoded.ToArray()))), "Armor parts must not reuse another category's sprite.");
                vm.AddEquipType = type;
                vm.EquipmentStorage = type >= 22 ? 1 : 0;
                Settle();
                var pick = vm.EquipmentMatches[0];
                var row = vm.EquipRows.Single(r => r.Type == type);
                Assert.Equal(pick.IconKey, row.IconKey);
                Assert.Equal($"equipment:{type}", vm.EquipmentTypes.Single(t => t.Code == type).IconKey);
                var image = equipment.FindControl<ListBox>("EquipmentCatalogList")!.GetVisualDescendants()
                    .OfType<Image>().First(i => Equals(i.DataContext, pick));
                Assert.Same(IconSymbols.Get(pick.IconKey), image.Source);
            }
            vm.EquipmentStorage = 0; vm.AddEquipType = 2; Settle();
            var button = equipment.FindControl<Button>("AddEquipmentButton")!;
            var bounds = button.TranslatePoint(new Point(0, 0), window)!.Value;
            Assert.InRange(bounds.Y, 0, window.ClientSize.Height - button.Bounds.Height);
            Assert.True(button.Bounds.Width >= 280);
            Capture("chest-create");
            vm.AddEquipType = 1; Settle(); Capture("head-create");
            vm.SelectedEquip = vm.EquipRows[1]; Settle(); Capture("chest-edit");
        }
        finally { vm.Load(path); window.Close(); Directory.Delete(directory, true); }
    }
}
