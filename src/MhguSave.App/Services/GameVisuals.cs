using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MhguSave.App.Services;

/// <summary>Optional, locally imported game visuals. Missing assets never prevent save editing.</summary>
public static class GameVisuals
{
    private const string Root = "avares://MhguSave.App/Assets/Game/";
    private const string CategoryRoot = "avares://MhguSave.App/Assets/Equipment/";
    // Bundled community symbols remain available when the optional native pack is absent.
    private static readonly Dictionary<string, string> CategoryIcons = new()
    {
        ["equipment:1"] = "armor_head.png",
        ["equipment:2"] = "armor_body.png",
        ["equipment:3"] = "armor_arms.png",
        ["equipment:4"] = "armor_waist.png",
        ["equipment:5"] = "armor_legs.png",
        ["equipment:23"] = "palico_head.png",
        ["equipment:24"] = "palico_body.png",
    };
    private static readonly Lazy<Dictionary<string, string>> Manifest = new(ReadManifest);
    private static readonly Dictionary<string, Bitmap?> Cache = new();

    public static bool IsAvailable => Get("artwork:hunter") is not null;

    public static bool HasIcon(string key) => Get(key) is not null;

    private static Dictionary<string, string> ReadManifest()
    {
        try
        {
            var uri = new Uri(Root + "manifest.json");
            if (!AssetLoader.Exists(uri)) return new();
            using var stream = AssetLoader.Open(uri);
            var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
            // Older packs used an incorrect common-atlas crop for armor. Only the
            // new exporter supplies verified native armor/category mappings.
            foreach (var key in CategoryIcons.Keys)
                if (manifest.TryGetValue(key, out var filename) && filename?.StartsWith("native-equipment-", StringComparison.Ordinal) != true)
                    manifest.Remove(key);
            return manifest;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            return new();
        }
    }

    public static Bitmap? Get(string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            Bitmap? bitmap = null;
            if (Manifest.Value.TryGetValue(key, out var filename)
                && !string.IsNullOrWhiteSpace(filename)
                && filename == Path.GetFileName(filename)
                && (filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var uri = new Uri(Root + filename);
                    if (AssetLoader.Exists(uri))
                    {
                        using var stream = AssetLoader.Open(uri);
                        bitmap = new Bitmap(stream);
                    }
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException)
                {
                    // A missing or invalid optional picture must not break the editor.
                }
            }
            if (bitmap is null && CategoryIcons.TryGetValue(key, out var fallback))
            {
                try
                {
                    using var stream = AssetLoader.Open(new Uri(CategoryRoot + fallback));
                    bitmap = new Bitmap(stream);
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { }
            }
            // Only cache known keys: arbitrary unknown item IDs must not grow the cache.
            if (Manifest.Value.ContainsKey(key) || CategoryIcons.ContainsKey(key)) Cache[key] = bitmap;
            return bitmap;
        }
    }
}

public sealed class GameIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        IconSymbols.Get(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
