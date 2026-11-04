using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MhguSave.App.Views;
using MhguSave.Format;
using MhguSave.Format.Tests;

namespace MhguSave.App.Tests;

[Collection("Avalonia UI")]
public class WorkspaceLayoutTests
{
    [Theory]
    [InlineData(1120, 740)]
    [InlineData(1440, 940)]
    [InlineData(1920, 1080)]
    public async Task EveryWorkspaceKeepsNavigationAndPrimaryActionsInTheViewport(int width, int height)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(() => VerifyWorkspace(width, height), CancellationToken.None);
    }

    private static void VerifyWorkspace(int width, int height)
    {
        var directory = Path.Combine(Path.GetTempPath(), "mhgu-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        // A long path must truncate instead of pushing Open / Save off the edge.
        var saveDirectory = Path.Combine(directory, new string('x', 110));
        Directory.CreateDirectory(saveDirectory);
        var path = Path.Combine(saveDirectory, "system");
        var data = SyntheticSave.Create();
        System.Text.Encoding.UTF8.GetBytes("Miso").CopyTo(data.AsSpan(SyntheticSave.Slot1Pointer + 36 + 0x23BB6));
        var save = SaveContainer.Parse(data, "system");
        save.Slots[0].Name = "Guild Hunter";
        save.Slots[0].VillageStar = 1;
        save.Slots[0].ItemBox[0] = new ItemSlot(9, 99);
        save.Slots[0].EquipmentBox.Add(new EquipmentSlot { Type = 2, Id = 446 });
        File.WriteAllBytes(path, save.ToArray());
        var vm = TestEditor.Create();
        var window = new MainWindow { DataContext = vm, Width = width, Height = height };

        void Settle() { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void AssertInViewport(Control control)
        {
            Assert.True(control.IsEffectivelyVisible);
            var topLeft = control.TranslatePoint(default, window)!.Value;
            Assert.InRange(topLeft.X, 0, window.ClientSize.Width - control.Bounds.Width + 0.5);
            Assert.InRange(topLeft.Y, 0, window.ClientSize.Height - control.Bounds.Height + 0.5);
        }
        void Capture(string name)
        {
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "MhguSaveEditor.sln"))) repo = repo.Parent;
            if (repo is null) return;
            var output = Path.Combine(repo.FullName, "artifacts", "redesign", "screenshots");
            Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var frame = window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(output, $"{name}-{width}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        try
        {
            window.Show(); Settle();
            Assert.NotNull(window.Icon);
            Assert.Contains("MHGU Save Editor by Dalrymple", window.Title);
            AssertInViewport(window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Open your save file  →")));
            Capture("welcome");
            vm.Load(path); Settle();
            Assert.True(vm.IsLoaded, vm.Status);
            var navigation = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("nav"));
            var saveButton = window.FindControl<Button>("SaveButton")!;
            for (var section = 0; section < 8; section++)
            {
                vm.SelectedSection = section; Settle();
                AssertInViewport(saveButton);
                foreach (var item in navigation.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.IsSelected))
                {
                    item.BringIntoView(); Settle(); AssertInViewport(item);
                }
                Capture(new[] { "profile", "inventory", "equipment", "quests", "collections", "loadouts", "palicoes", "monsters" }[section]);
            }
            vm.SelectedSection = 1; Settle();
            window.FindControl<TabControl>("BoxEditorTabs")!.SelectedIndex = 1;
            vm.AddItemId = 4095; vm.AddItemCount = 127; Settle();
            var countInput = window.FindControl<IntegerInput>("AddBoxCountInput")!;
            countInput.BringIntoView(); Settle();
            foreach (var name in new[] { "AddBoxIdInput", "AddBoxCountInput" })
            {
                var number = window.FindControl<IntegerInput>(name)!;
                var text = number.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "PART_TextBox");
                Assert.True(text.Bounds.Width >= 70, $"{name}: number text must have space beside the stepper ({text.Bounds.Width}).");
                var arrows = number.GetVisualDescendants().OfType<RepeatButton>().ToArray();
                Assert.Equal(2, arrows.Length);
                Assert.All(arrows, arrow => Assert.InRange(arrow.Bounds.Width, 20, 32));
            }
            AssertInViewport(window.FindControl<Button>("AddBoxItemButton")!);
            Capture("inventory-add");
            vm.AddItemCount = 126; Settle();
            var up = countInput.GetVisualDescendants().OfType<RepeatButton>().Single(b => b.Name == "PART_IncreaseButton");
            var upPoint = up.TranslatePoint(new Point(up.Bounds.Width / 2, up.Bounds.Height / 2), window)!.Value;
            window.MouseDown(upPoint, MouseButton.Left); window.MouseUp(upPoint, MouseButton.Left); Settle();
            Assert.Equal(127, vm.AddItemCount);
            vm.ItemNameQuery = "Potion"; Settle();
            var catalog = window.FindControl<ListBox>("BoxItemCatalogList")!;
            Assert.True(catalog.Bounds.Height >= 46, "The item catalog needs room for a complete search result.");
            Capture("inventory-search");
            window.FindControl<TabControl>("InventoryTabs")!.SelectedIndex = 1; Settle();
            window.FindControl<TabControl>("PouchEditorTabs")!.SelectedIndex = 1; Settle();
            AssertInViewport(window.FindControl<Button>("AddPouchItemButton")!);
            Capture("pouch-add");
            vm.SelectedSection = 5; Settle();
            AssertInViewport(window.GetVisualDescendants().OfType<LoadoutsView>().Single().FindControl<Button>("ApplyLoadoutButton")!);
            vm.SelectedSection = 6; Settle();
            AssertInViewport(window.GetVisualDescendants().OfType<PalicoesView>().Single().FindControl<Button>("ApplyPalicoButton")!);
            vm.SelectedSection = 7; Settle();
            AssertInViewport(window.GetVisualDescendants().OfType<MonstersView>().Single().FindControl<Button>("ApplyMonsterButton")!);
            vm.SelectedSection = 2;
            vm.EquipmentPane = 1; Settle();
            var equipment = window.GetVisualDescendants().OfType<EquipmentView>().Single();
            AssertInViewport(equipment.FindControl<Button>("AddEquipmentButton")!);
            vm.EquipmentPane = 2; Settle();
            AssertInViewport(equipment.FindControl<Button>("AddDecorationButton")!);
            vm.SelectedSection = 4; Settle();
            foreach (var caption in new[] { "Unlock all materials", "Grant all awards", "Unlock all Hunter Arts" })
            {
                var button = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, caption));
                button.BringIntoView(); Settle();
                AssertInViewport(button);
            }
        }
        finally
        {
            vm.Load(path);
            window.Close();
            Directory.Delete(directory, true);
        }
    }
}
