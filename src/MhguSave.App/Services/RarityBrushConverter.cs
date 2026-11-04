using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using MhguSave.Format;

namespace MhguSave.App.Services;

public sealed class RarityBrushConverter : IValueConverter
{
    private static readonly Dictionary<int, IBrush> Brushes = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var rarity = value is int number && number is >= 1 and <= 11 ? number : 0;
        lock (Brushes)
        {
            if (!Brushes.TryGetValue(rarity, out var brush))
                Brushes[rarity] = brush = new ImmutableSolidColorBrush(Color.Parse(GameRarity.Color(rarity)));
            return brush;
        }
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
