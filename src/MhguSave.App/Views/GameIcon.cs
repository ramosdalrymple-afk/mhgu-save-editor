using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MhguSave.App.Services;

namespace MhguSave.App.Views;

public sealed class GameIcon : Image
{
    public static readonly StyledProperty<string?> KeyProperty = AvaloniaProperty.Register<GameIcon, string?>(nameof(Key));
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }
    protected override Type StyleKeyOverride => typeof(Image);
    public GameIcon() { Stretch = Stretch.Uniform; IsHitTestVisible = false; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KeyProperty) Source = IconSymbols.Get(Key);
    }
}
