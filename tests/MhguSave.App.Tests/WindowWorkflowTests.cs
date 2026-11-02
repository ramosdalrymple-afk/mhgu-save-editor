using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MhguSave.App.ViewModels;
using MhguSave.App.Views;
using MhguSave.Format;
using MhguSave.Format.Tests;

[assembly: AvaloniaTestApplication(typeof(MhguSave.App.Tests.TestAppBuilder))]

namespace MhguSave.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

[CollectionDefinition("Avalonia UI", DisableParallelization = true)]
public sealed class AvaloniaUiCollection { }

[Collection("Avalonia UI")]
public class WindowWorkflowTests
{
    [Fact]
    public async Task AddingRathalosSFromTheVisibleCatalogSavesTheActualArmorIds()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(RunRathalosCreation, CancellationToken.None);
    }

    private static void RunRathalosCreation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mhgu-rathalos-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "system");
        var image = SaveContainer.Parse(SyntheticSave.Create(), "system");
        image.Slots[0].Name = "Armor Hunter";
        image.Slots[0].VillageStar = 1;
        File.WriteAllBytes(path, image.ToArray());
        var vm = TestEditor.Create();
        vm.Load(path);
        vm.SelectedSection = 2;
        var window = new MainWindow { DataContext = vm, Width = 1380, Height = 900 };
        void Settle() { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Click(Control control)
        {
            control.BringIntoView(); Settle();
            var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            Assert.InRange(point.Y, 0, window.ClientSize.Height);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle();
        }
        try
        {
            window.Show(); Settle();
            var equipment = window.GetVisualDescendants().OfType<EquipmentView>().Single();
            string[] names = ["Rathalos Helm S", "Rathalos Mail S", "Rathalos Braces S", "Rathalos Faulds S", "Rathalos Greaves S"];
            for (var part = 1; part <= 5; part++)
            {
                equipment.FindControl<TabControl>("EquipmentEditorTabs")!.SelectedIndex = 1;
                equipment.FindControl<ComboBox>("CreateEquipmentType")!.SelectedItem = vm.EquipmentTypes.Single(t => t.Code == part);
                Settle();
                var search = equipment.FindControl<TextBox>("EquipmentNameSearch")!;
                Click(search);
                window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
                window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
                window.KeyTextInput("Rathalos"); Settle();
                var pick = vm.EquipmentMatches.Single(p => p.Name == names[part - 1]);
                var catalog = equipment.FindControl<ListBox>("EquipmentCatalogList")!;
                catalog.ScrollIntoView(pick); Settle();
                Click(catalog.GetVisualDescendants().OfType<ListBoxItem>().Single(c => Equals(c.DataContext, pick)));
                Assert.Equal(446, vm.AddEquipId);
                Click(equipment.FindControl<Button>("AddEquipmentButton")!);
                Assert.Equal(names[part - 1], vm.SelectedEquip!.Name);
                Assert.Equal(446, vm.SelectedEquip.Id);
            }
            Click(window.FindControl<Button>("SaveButton")!);
            Assert.False(vm.HasUnsavedChanges, vm.Status);
            var saved = SaveContainer.Load(path).Slots[0];
            for (var part = 1; part <= 5; part++)
            {
                Assert.Equal(part, saved.EquipmentBox[part - 1].Type);
                Assert.Equal(446, saved.EquipmentBox[part - 1].Id);
            }
        }
        finally { vm.Load(path); window.Close(); Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task EveryTabCanBeUsedThroughTheWindowAndSavedWithoutHiddenDraftsBlockingIt()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(RunEveryTab, CancellationToken.None);
    }

    private static void RunEveryTab()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mhgu-ui-flow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "system");
        var image = SaveContainer.Parse(SyntheticSave.Create(), "system");
        var hunter = image.Slots[0];
        hunter.Name = "Window Hunter";
        hunter.VillageStar = 1;
        hunter.QuestCounter = 100;
        hunter.ItemBox[0] = new ItemSlot(9, 5);
        hunter.Pouch[0] = new ItemSlot(9, 3);
        File.WriteAllBytes(path, image.ToArray());
        var vm = TestEditor.Create();
        vm.Load(path);
        var window = new MainWindow { DataContext = vm, Width = 1120, MinHeight = 0, Height = 700 };
        void Settle() { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Click(Control control)
        {
            control.BringIntoView();
            Settle();
            var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            Assert.InRange(point.X, 0, window.ClientSize.Width);
            Assert.InRange(point.Y, 0, window.ClientSize.Height);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle();
        }
        void Save()
        {
            Click(window.FindControl<Button>("SaveButton")!);
            Assert.False(vm.HasUnsavedChanges, vm.Status);
            Assert.Contains("Wrote", vm.Status);
        }
        void TypeText(TextBox input, string value)
        {
            Click(input);
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
            window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
            window.KeyTextInput(value);
            Settle();
            if (input.Text != value) Capture("input-focus-failure");
            Assert.Equal(value, input.Text);
        }
        void TypeNumber(NumericUpDown input, string value)
        {
            input.BringIntoView(); Settle();
            TypeText(input.GetVisualDescendants().OfType<TextBox>().Single(), value);
        }
        void Navigate(int section)
        {
            var nav = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("nav"));
            Click(nav.GetVisualDescendants().OfType<ListBoxItem>().ElementAt(section));
            Assert.Equal(section, vm.SelectedSection);
        }
        void Capture(string name)
        {
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "MhguSaveEditor.sln"))) repo = repo.Parent;
            if (repo is null) return;
            var output = Path.Combine(repo.FullName, "artifacts", "workflow-audit");
            Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var frame = window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(output, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        try
        {
            window.Show(); Settle();
            Navigate(2);
            var equipment = window.GetVisualDescendants().OfType<EquipmentView>().Single();
            equipment.FindControl<TabControl>("EquipmentEditorTabs")!.SelectedIndex = 1;
            equipment.FindControl<ComboBox>("CreateEquipmentType")!.SelectedItem = vm.EquipmentTypes.First(t => t.Code == 6);
            Settle();
            Assert.True(vm.IsAddingTalisman);
            equipment.FindControl<ComboBox>("SelectedAddSkill1Picker")!.SelectedItem = vm.SkillChoices.First(s => s.Id == 1);
            TypeNumber(equipment.FindControl<NumericUpDown>("AddEquipSkillPoints1Input")!, "7");
            equipment.FindControl<ComboBox>("SelectedAddSkill2Picker")!.SelectedItem = vm.SkillChoices.First(s => s.Id == 2);
            TypeNumber(equipment.FindControl<NumericUpDown>("AddEquipSkillPoints2Input")!, "-3");
            TypeNumber(equipment.FindControl<NumericUpDown>("AddEquipSlotsInput")!, "3");
            equipment.FindControl<ComboBox>("SelectedAddCharmPicker")!.SelectedItem = vm.CharmChoices.First(s => s.Id == 356);
            Capture("equipment-create-talisman");
            var add = equipment.FindControl<Button>("AddEquipmentButton")!;
            Assert.True(add.IsEffectivelyEnabled);
            Click(add);
            Assert.Equal(6, vm.SelectedEquip!.Type);
            Assert.Equal(0, vm.EquipmentPane);
            equipment.FindControl<ComboBox>("SelectedEditDeco1Picker")!.SelectedItem = vm.DecorationChoices.First(s => s.Id == 2639);
            equipment.FindControl<ComboBox>("SelectedEditDeco2Picker")!.SelectedItem = vm.DecorationChoices.First(s => s.Id == 2638);
            Save();
            var piece = SaveContainer.Load(path).Slots[0].EquipmentBox[0];
            Assert.Equal(7, piece.SkillPoints1);
            Assert.Equal(-3, piece.SkillPoints2);
            Assert.Equal(3, piece.SlotCount);
            Assert.Equal(356, piece.CharmItem);
            Assert.Equal(2639, piece.Deco1);
            Assert.Equal(2638, piece.Deco2);
            Capture("equipment-edit-talisman");

            equipment.FindControl<TabControl>("EquipmentEditorTabs")!.SelectedIndex = 2;
            Settle();
            equipment.FindControl<ListBox>("DecorationCatalogList")!.SelectedItem = vm.DecorationChoices.First(s => s.Id == 2638);
            TypeNumber(equipment.FindControl<NumericUpDown>("DecorationQuantityInput")!, "5");
            Capture("decoration-create");
            Click(equipment.FindControl<Button>("AddDecorationButton")!);
            Assert.True(vm.BoxRows[1].Id == 2638 && vm.BoxRows[1].Count == 5,
                $"Selected {vm.SelectedDecoration}; quantity {vm.DecorationQuantity}; {vm.EquipStatus}");
            Save();

            Navigate(1);
            window.FindControl<TabControl>("BoxEditorTabs")!.SelectedIndex = 1; Settle();
            TypeText(window.FindControl<TextBox>("BoxItemSearchInput")!, "Mega Potion");
            var catalog = window.FindControl<ListBox>("BoxItemCatalogList")!;
            Click(catalog.GetVisualDescendants().OfType<ListBoxItem>().First());
            TypeNumber(window.FindControl<NumericUpDown>("AddBoxCountInput")!, "99");
            Click(window.FindControl<Button>("AddBoxItemButton")!);
            Assert.Equal(10, vm.SelectedBox!.Id);
            Assert.Equal(99, vm.SelectedBox.Count);
            Save();
            var box = window.GetVisualDescendants().OfType<ListBox>().Single(l => ReferenceEquals(l.ItemsSource, vm.BoxView));
            box.ScrollIntoView(vm.BoxRows[0]); Settle();
            Click(box.GetVisualDescendants().OfType<ListBoxItem>().Single(c => ReferenceEquals(c.DataContext, vm.BoxRows[0])));
            Assert.Same(vm.BoxRows[0], vm.SelectedBox);
            TypeNumber(window.FindControl<NumericUpDown>("EditBoxCountInput")!, "21");
            Assert.Equal(21, vm.EditBoxCount);
            Assert.True(vm.HasBoxDraft);
            Save();
            Assert.Equal(21, SaveContainer.Load(path).Slots[0].ItemBox[0].Count);
            window.FindControl<TabControl>("InventoryTabs")!.SelectedIndex = 1; Settle();
            var pouch = window.GetVisualDescendants().OfType<ListBox>().Single(l => ReferenceEquals(l.ItemsSource, vm.PouchView));
            Click(pouch.GetVisualDescendants().OfType<ListBoxItem>().First());
            TypeNumber(window.FindControl<NumericUpDown>("EditPouchCountInput")!, "6");
            Save();
            Assert.Equal(6, SaveContainer.Load(path).Slots[0].Pouch[0].Count);
            window.FindControl<TabControl>("PouchEditorTabs")!.SelectedIndex = 1; Settle();
            TypeText(window.FindControl<TextBox>("PouchItemSearchInput")!, "Antidote");
            Click(window.FindControl<ListBox>("PouchItemCatalogList")!.GetVisualDescendants().OfType<ListBoxItem>().First());
            TypeNumber(window.FindControl<NumericUpDown>("AddPouchCountInput")!, "10");
            Click(window.FindControl<Button>("AddPouchItemButton")!);
            Assert.Equal(13, vm.SelectedPouch!.Id);
            Assert.Equal(10, vm.SelectedPouch.Count);
            Save();
            Capture("inventory-pouch");

            Navigate(0);
            TypeText(window.FindControl<TextBox>("HunterNameInput")!, "Updated Hunter");
            TypeNumber(window.FindControl<NumericUpDown>("ZennyInput")!, "5555");
            window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, null);
            window.KeyRelease(Key.S, RawInputModifiers.Control, PhysicalKey.S, null);
            Settle();
            Assert.False(vm.HasUnsavedChanges, vm.Status);
            var savedHunter = SaveContainer.Load(path).Slots[0];
            Assert.Equal("Updated Hunter", savedHunter.Name);
            Assert.Equal(5555, savedHunter.Zenny);
            Capture("hunter-profile");

            Navigate(3);
            window.FindControl<TextBox>("QuestFilterInput")!.Text = "Find the Ferns";
            Settle();
            var questList = window.GetVisualDescendants().OfType<ListBox>().Single(l => ReferenceEquals(l.ItemsSource, vm.QuestView));
            Click(questList.GetVisualDescendants().OfType<CheckBox>().First());
            Assert.True(vm.QuestView[0].IsCleared);
            Save();
            Assert.True(SaveContainer.Load(path).Slots[0].IsQuestCleared(vm.QuestView[0].Index));
            Assert.Equal(100, SaveContainer.Load(path).Slots[0].QuestCounter);
            Capture("quest-progress");

            Navigate(4);
            foreach (var commandName in new[] { "Unlock all materials", "Grant all awards", "Unlock all Hunter Arts" })
                Click(window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, commandName)));
            Save();
            var unlocked = SaveContainer.Load(path).Slots[0];
            Assert.Equal(2990, unlocked.ItemsObtained.SetCount());
            Assert.Equal(132, unlocked.Awards.SetCount());
            Assert.Equal(178, unlocked.UnlockedHunterArtCount);
            Capture("unlocks-rewards");

            Navigate(1);
            window.FindControl<TabControl>("InventoryTabs")!.SelectedIndex = 0; Settle();
            window.FindControl<TabControl>("BoxEditorTabs")!.SelectedIndex = 1; Settle();
            var usedBeforeInvalidAdd = vm.BoxRows.Count(r => !r.IsEmpty);
            TypeNumber(window.FindControl<NumericUpDown>("AddBoxCountInput")!, "128");
            Click(window.FindControl<Button>("AddBoxItemButton")!);
            Assert.Equal("128", window.FindControl<NumericUpDown>("AddBoxCountInput")!.Text);
            Assert.Equal(usedBeforeInvalidAdd, vm.BoxRows.Count(r => !r.IsEmpty));
            Assert.Contains("whole number", vm.Status);
            TypeNumber(window.FindControl<NumericUpDown>("AddBoxCountInput")!, "2");
            Click(window.FindControl<Button>("AddBoxItemButton")!);
            Assert.Equal(usedBeforeInvalidAdd + 1, vm.BoxRows.Count(r => !r.IsEmpty));
            Assert.Equal(2, vm.SelectedBox!.Count);
            Save();
            var beforeInvalidEdit = File.ReadAllBytes(path);
            TypeNumber(window.FindControl<NumericUpDown>("EditBoxCountInput")!, "200");
            Click(window.FindControl<Button>("SaveButton")!);
            Assert.Equal(beforeInvalidEdit, File.ReadAllBytes(path));
            Assert.Contains("Fix", vm.Status);
            TypeNumber(window.FindControl<NumericUpDown>("EditBoxCountInput")!, "12");
            Save();
            Assert.Equal(12, SaveContainer.Load(path).Slots[0].ItemBox[vm.SelectedBox.Index].Count);
            Navigate(0);
            TypeNumber(window.FindControl<NumericUpDown>("ZennyInput")!, "10000000");
            Navigate(4);
            Click(window.FindControl<Button>("SaveButton")!);
            Assert.Contains("Fix Zenny", vm.Status);
            Assert.Equal(5555, SaveContainer.Load(path).Slots[0].Zenny);
            Navigate(0);
            TypeNumber(window.FindControl<NumericUpDown>("ZennyInput")!, "6000");
            Save();
            Assert.Equal(6000, SaveContainer.Load(path).Slots[0].Zenny);
        }
        finally { vm.Load(path); window.Close(); Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task EquipmentRowClickPopulatesEditorAndCreationActionIsReachable()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(() => RunEquipmentClick(), CancellationToken.None);
    }

    private static void RunEquipmentClick()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mhgu-window-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "system");
        var save = SaveContainer.Parse(SyntheticSave.Create(), "system");
        save.Slots[0].Name = "Window test";
        save.Slots[0].VillageStar = 1;
        save.Slots[0].EquipmentBox.Add(new EquipmentSlot { Type = 7, Id = 1 });
        File.WriteAllBytes(path, save.ToArray());
        var vm = TestEditor.Create();
        vm.Load(path);
        vm.SelectedSection = 2;
        var window = new MainWindow { DataContext = vm, Width = 1120, Height = 740 };
        try
        {
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var list = window.GetVisualDescendants().OfType<ListBox>().Single(l => ReferenceEquals(l.ItemsSource, vm.EquipView));
            var item = list.GetVisualDescendants().OfType<ListBoxItem>().First();
            var point = item.TranslatePoint(new Point(80, item.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(vm.SelectedEquip);
            Assert.Equal(1, vm.EditEquipId);
            Assert.Equal(7, vm.EditEquipType);
        }
        finally { vm.Load(path); window.Close(); Directory.Delete(directory, true); }
    }
}
