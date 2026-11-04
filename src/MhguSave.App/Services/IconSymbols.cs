using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MhguSave.App.Services;

/// <summary>Native game sprites with small original vector fallbacks for portable source builds.</summary>
public static class IconSymbols
{
    // DrawingImage and its geometry belong to the UI thread that created them.
    [ThreadStatic] private static IReadOnlyDictionary<string, IImage>? _fallbacks;

    public static IImage? Get(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var native = GameVisuals.Get(key);
        if (native is not null) return native;
        var rarityAt = key.IndexOf(":rarity:", StringComparison.Ordinal);
        if (rarityAt > 0) return Get(key[..rarityAt]);
        return key.StartsWith("artwork:", StringComparison.Ordinal) ? null : GetFallback(key);
    }

    internal static IImage GetFallback(string key)
    {
        var name = key.StartsWith("item:", StringComparison.Ordinal) ? "inventory"
            : key.StartsWith("equipment:", StringComparison.Ordinal) ? "equipment"
            : key.StartsWith("monster:", StringComparison.Ordinal) ? "monsters"
            : key.StartsWith("award:", StringComparison.Ordinal) ? "awards"
            : key.StartsWith("ui:", StringComparison.Ordinal) ? key[3..] : "inventory";
        var fallbacks = _fallbacks ??= CreateFallbacks();
        return fallbacks.TryGetValue(name, out var icon) ? icon : fallbacks["inventory"];
    }

    private static IReadOnlyDictionary<string, IImage> CreateFallbacks()
    {
        var paths = new Dictionary<string, string>
        {
            ["profile"] = "M12,2 L20,6 20,15 12,22 4,15 4,6 Z M8,9 L16,9 16,14 8,14 Z",
            ["inventory"] = "M3,7 L12,2 21,7 21,18 12,22 3,18 Z M3,7 L12,12 21,7 M12,12 L12,22",
            ["equipment"] = "M5,2 L10,3 18,16 21,16 21,20 17,20 17,17 4,9 Z M4,20 L10,14 M2,18 L7,23",
            ["quests"] = "M5,3 L18,3 20,6 20,22 4,22 4,6 Z M8,8 L16,8 M8,12 L16,12 M8,16 L14,16",
            ["collections"] = "M12,2 L15,8 22,9 17,14 18,21 12,18 6,21 7,14 2,9 9,8 Z",
            ["loadouts"] = "M3,3 L10,3 10,10 3,10 Z M14,3 L21,3 21,10 14,10 Z M3,14 L10,14 10,21 3,21 Z M14,14 L21,14 21,21 14,21 Z",
            ["palico"] = "M4,10 L3,2 9,6 15,6 21,2 20,10 C23,20 17,22 12,22 C7,22 1,20 4,10 Z M7,12 L9,12 M15,12 L17,12 M10,16 L12,18 14,16",
            ["monsters"] = "M3,19 L5,9 9,11 11,3 15,8 21,6 20,17 15,22 8,22 Z M15,12 L17,12 M5,16 L10,16",
            ["pouch"] = "M8,3 L16,3 14,7 C23,12 22,21 16,22 L8,22 C2,21 1,12 10,7 Z M8,7 L16,7",
            ["edit"] = "M3,16 L16,3 21,8 8,21 2,22 Z M13,6 L18,11",
            ["add"] = "M12,3 L12,21 M3,12 L21,12",
            ["jewels"] = "M6,3 L18,3 23,10 12,23 1,10 Z M1,10 L23,10 M6,3 L12,23 18,3",
            ["awards"] = "M12,2 L19,5 21,12 16,18 8,18 3,12 5,5 Z M8,18 L6,23 12,20 18,23 16,18",
        };
        paths["arts"] = paths["collections"];
        paths["appearance"] = paths["profile"];
        var result = new Dictionary<string, IImage>();
        var brush = new SolidColorBrush(Color.Parse("#E6C990"));
        foreach (var pair in paths)
        {
            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing { Brush = Brushes.Transparent, Geometry = new RectangleGeometry(new Rect(0, 0, 24, 24)) });
            drawing.Children.Add(new GeometryDrawing { Geometry = Geometry.Parse(pair.Value), Pen = new Pen(brush, 1.6) });
            result[pair.Key] = new DrawingImage(drawing);
        }
        return result;
    }
}

public sealed class TabIconKeyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString() ?? "";
        return text.StartsWith("Pouch", StringComparison.Ordinal) ? "ui:pouch"
            : text.StartsWith("Item box", StringComparison.Ordinal) ? "ui:inventory"
            : text.StartsWith("Edit", StringComparison.Ordinal) ? "ui:edit"
            : text.StartsWith("Add", StringComparison.Ordinal) ? "ui:add"
            : text == "Jewels" ? "ui:jewels" : "ui:loadouts";
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
