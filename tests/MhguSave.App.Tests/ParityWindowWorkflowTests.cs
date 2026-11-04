using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MhguSave.App.Views;
using MhguSave.Format;
using MhguSave.Format.Tests;

namespace MhguSave.App.Tests;

[Collection("Avalonia UI")]
public class ParityWindowWorkflowTests
{
    [Fact]
    public async Task NewEditorsAcceptRealTypingAndCtrlSWithoutApply()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(Verify, CancellationToken.None);
    }

    private static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mhgu-parity-window-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "system");
        var data = SyntheticSave.Create();
        System.Text.Encoding.UTF8.GetBytes("Miso").CopyTo(data.AsSpan(SyntheticSave.Slot1Pointer + 36 + 0x23BB6));
        var save = SaveContainer.Parse(data, "system");
        save.Slots[0].Name = "Hunter"; save.Slots[0].VillageStar = 1;
        File.WriteAllBytes(path, save.ToArray());
        var vm = TestEditor.Create(); vm.Load(path);
        var window = new MainWindow { DataContext = vm, Width = 1120, Height = 740 };
        void Settle() { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Type(TextBox input, string value)
        {
            input.BringIntoView(); Settle();
            var point = input.TranslatePoint(new Point(input.Bounds.Width / 2, input.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Settle();
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
            window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
            window.KeyTextInput(value); Settle();
            Assert.Equal(value, input.Text);
        }
        void Save()
        {
            window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, null);
            window.KeyRelease(Key.S, RawInputModifiers.Control, PhysicalKey.S, null); Settle();
            Assert.False(vm.HasUnsavedChanges, vm.Status);
        }
        try
        {
            window.Show(); Settle();
            vm.SelectedSection = 5; Settle();
            var sets = window.GetVisualDescendants().OfType<LoadoutsView>().Single();
            Type(sets.FindControl<TextBox>("LoadoutNameInput")!, "WindowSet");
            Save();
            Assert.Equal("WindowSet", SaveContainer.Load(path).Slots[0].ReadEquipmentSet(false, 0).Name);
            vm.SelectedSection = 6; Settle();
            var cats = window.GetVisualDescendants().OfType<PalicoesView>().Single();
            Type(cats.FindControl<TextBox>("PalicoNameInput")!, "Nori");
            Save();
            Assert.Equal("Nori", SaveContainer.Load(path).Slots[0].ReadPalico(0).Name);
            vm.SelectedSection = 7; Settle();
            var monsters = window.GetVisualDescendants().OfType<MonstersView>().Single();
            var number = monsters.FindControl<NumericUpDown>("MonsterSlainInput")!;
            Type(number.GetVisualDescendants().OfType<TextBox>().Single(), "77");
            Save();
            Assert.Equal((ushort)77, SaveContainer.Load(path).Slots[0].ReadMonster(1).Slain);
            Type(number.GetVisualDescendants().OfType<TextBox>().Single(), "invalid");
            vm.SelectedSection = 0; Settle();
            window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, null);
            window.KeyRelease(Key.S, RawInputModifiers.Control, PhysicalKey.S, null); Settle();
            Assert.Equal((ushort)77, SaveContainer.Load(path).Slots[0].ReadMonster(1).Slain);
            vm.SelectedSection = 7; Settle();
            Type(number.GetVisualDescendants().OfType<TextBox>().Single(), "88");
            Save();
            Assert.Equal((ushort)88, SaveContainer.Load(path).Slots[0].ReadMonster(1).Slain);
        }
        finally { vm.Load(path); window.Close(); Directory.Delete(directory, true); }
    }
}
