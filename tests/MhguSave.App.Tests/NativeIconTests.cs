using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MhguSave.App.Services;
using MhguSave.App.ViewModels;
using MhguSave.App.Views;
using MhguSave.Format;
using MhguSave.Format.Tests;

namespace MhguSave.App.Tests;

[Collection("Avalonia UI")]
public class NativeIconTests
{
    [Fact]
    public async Task NativePackCoversEveryItemEquipmentTypeAndMonsterWithSafeFallbacks()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(() =>
        {
            foreach (var key in new[] { "ui:profile", "ui:inventory", "ui:equipment", "ui:quests", "ui:collections", "ui:loadouts", "ui:palico", "ui:monsters", "item:4095", "monster:138" })
            {
                Assert.NotNull(IconSymbols.Get(key));
                Assert.IsType<DrawingImage>(IconSymbols.GetFallback(key));
            }
            Assert.Null(IconSymbols.Get(""));
            Assert.Null(IconSymbols.Get("artwork:missing"));
            if (!GameVisuals.HasIcon("ui:inventory")) return; // clean source checkout
            for (var id = 1; id <= 2990; id++)
                Assert.Equal(new PixelSize(48, 48), Assert.IsType<Bitmap>(GameVisuals.Get($"item:{id}")).PixelSize);
            for (var type = 1; type <= 24; type++)
                Assert.Equal(new PixelSize(48, 48), Assert.IsType<Bitmap>(GameVisuals.Get($"equipment:{type}")).PixelSize);
            for (var index = 1; index <= 137; index++)
                Assert.Equal(new PixelSize(72, 72), Assert.IsType<Bitmap>(GameVisuals.Get($"monster:{index}")).PixelSize);
            byte[] Encoded(string key)
            {
                using var stream = new MemoryStream(); GameVisuals.Get(key)!.Save(stream, new PngBitmapEncoderOptions()); return stream.ToArray();
            }
            // The executable maps these unused entries to the same sprite, not to
            // seven successive atlas cells (which include unrelated village symbols).
            Assert.Equal(Encoded("monster:106"), Encoded("monster:112"));
            Assert.NotEqual(Encoded("monster:1"), Encoded("monster:113"));
            Assert.NotEqual(Encoded("monster:121"), Encoded("monster:131"));
            Assert.NotSame(GameVisuals.Get("equipment:1"), GameVisuals.Get("equipment:2"));
            if (GameRarity.IsAvailable)
            {
                for (var type = 1; type <= 24; type++)
                    for (var rare = 1; rare <= 11; rare++)
                        Assert.NotNull(GameVisuals.Get(GameRarity.Icon(type, rare)));
                Assert.NotEqual(Encoded("equipment:1:rarity:1"), Encoded("equipment:1:rarity:6"));
                for (var award = 0; award < 132; award++) Assert.NotNull(GameVisuals.Get($"award:{award}"));
            }
        }, CancellationToken.None);
    }

    [Theory]
    [InlineData(1120, 740)]
    [InlineData(1920, 1080)]
    public async Task NavigationTabsAndRecordIconsStayAlignedAndRefreshWithSelection(int width, int height)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(() => VerifyWindow(width, height), CancellationToken.None);
    }

    private static void VerifyWindow(int width, int height)
    {
        var directory = Path.Combine(Path.GetTempPath(), "mhgu-icons-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "system");
        var data = SyntheticSave.Create();
        System.Text.Encoding.UTF8.GetBytes("Miso").CopyTo(data.AsSpan(SyntheticSave.Slot1Pointer + 36 + 0x23BB6));
        var save = SaveContainer.Parse(data, "system");
        var slot = save.Slots[0]; slot.Name = "Icon Hunter"; slot.VillageStar = 1;
        slot.ItemBox[0] = new(9, 5); slot.EquipmentBox.Add(new EquipmentSlot { Type = 1, Id = 446 });
        File.WriteAllBytes(path, save.ToArray());
        var original = File.ReadAllBytes(path);
        var vm = TestEditor.Create(); vm.Load(path);
        var window = new MainWindow { DataContext = vm, Width = width, Height = height };
        void Settle() { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Capture(string name)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "MhguSaveEditor.sln"))) root = root.Parent;
            if (root is null) return;
            var output = Path.Combine(root.FullName, "artifacts", "native-icons"); Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var frame = window.CaptureRenderedFrame(); frame?.Save(Path.Combine(output, $"{name}-{width}.png"), new PngBitmapEncoderOptions());
        }
        try
        {
            window.Show(); Settle();
            var nav = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("nav"));
            var keys = new HashSet<string>();
            for (var section = 0; section < 8; section++)
            {
                vm.SelectedSection = section; Settle();
                var entry = nav.GetVisualDescendants().OfType<ListBoxItem>().Single(i => i.IsSelected);
                entry.BringIntoView(); Settle();
                var icon = Assert.Single(entry.GetVisualDescendants().OfType<GameIcon>());
                Assert.NotNull(icon.Source); Assert.True(icon.IsEffectivelyVisible); keys.Add(icon.Key!);
                Assert.Equal(22, icon.Bounds.Width);
            }
            Assert.Equal(8, keys.Count);
            vm.SelectedSection = 2; Settle();
            var equipment = window.GetVisualDescendants().OfType<EquipmentView>().Single();
            var tabs = equipment.FindControl<TabControl>("EquipmentEditorTabs")!;
            foreach (var tab in tabs.GetVisualDescendants().OfType<TabItem>())
            {
                var expected = new TabIconKeyConverter().Convert(tab.Header, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
                var icon = tab.GetVisualDescendants().OfType<GameIcon>().First(i => Equals(i.Key, expected));
                Assert.NotNull(icon.Source);
                var pos = icon.TranslatePoint(default, tabs)!.Value;
                Assert.InRange(pos.X, 0, tabs.Bounds.Width - icon.Bounds.Width + 0.5);
            }
            Capture("equipment");
            if (GameRarity.IsAvailable)
            {
                vm.AddEquipType = 6; vm.AddEquipId = 10; Settle();
                Assert.Equal("RARE 10", vm.CreationRarityLabel);
                vm.SelectedEquip = vm.EquipRows.Single(r => r.Type == 1); Settle();
                Assert.Equal("RARE 6", vm.PreviewRarityLabel);
                Assert.Equal("equipment:1:rarity:6", vm.SelectedEquip.IconKey);
                Capture("rarity-edit");
            }
            vm.SelectedSection = 5; Settle();
            Assert.Contains(vm.LoadoutDraft!.Pieces[1].Choices, c => c.Type == 1 && c.Rarity == GameRarity.Equipment(1, 446));
            Capture("loadouts");
            vm.SelectedSection = 6; Settle(); Capture("palicoes");
            vm.SelectedSection = 7; Settle();
            vm.SelectedMonster = vm.Monsters.Single(m => m.Index == 113); Settle();
            var monsterView = window.GetVisualDescendants().OfType<MonstersView>().Single();
            var portrait = monsterView.GetVisualDescendants().OfType<GameIcon>().Single(i => i.Width == 64);
            Assert.Equal("monster:113", portrait.Key);
            Assert.Same(IconSymbols.Get("monster:113"), portrait.Source);
            vm.SelectedMonster = vm.Monsters.Single(m => m.Index == 137); Settle();
            Assert.Equal("monster:137", portrait.Key);
            Assert.Same(IconSymbols.Get("monster:137"), portrait.Source);
            Capture("monsters");
            vm.SelectedSection = 1; Settle(); Capture("items");
            vm.SelectedSection = 4; vm.CollectionKind = 2; Settle();
            Assert.Equal("item:1", vm.CollectionEntries.First().IconKey);
            Assert.False(vm.HasUnsavedChanges);
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally { vm.Load(path); window.Close(); Directory.Delete(directory, true); }
    }
}
