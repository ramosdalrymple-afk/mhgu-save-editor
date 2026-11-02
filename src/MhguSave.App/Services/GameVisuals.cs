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
    // Explicit part symbols: the old common-atlas fallback was a Hunting Horn.
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

    public static bool IsAvailable => Manifest.Value.ContainsKey("artwork:hunter");

    public static bool HasIcon(string key) => Manifest.Value.ContainsKey(key);

    private static Dictionary<string, string> ReadManifest()
    {
        try
        {
            var uri = new Uri(Root + "manifest.json");
            if (!AssetLoader.Exists(uri)) return new(CategoryIcons);
            using var stream = AssetLoader.Open(uri);
            var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
            foreach (var pair in CategoryIcons) manifest[pair.Key] = pair.Value;
            return manifest;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            return new(CategoryIcons);
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
                    var uri = new Uri((CategoryIcons.ContainsKey(key) ? CategoryRoot : Root) + filename);
                    if (AssetLoader.Exists(uri))
                    {
                        using var stream = AssetLoader.Open(uri);
                        bitmap = new Bitmap(stream);
                    }
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
                {
                    // A missing or invalid optional picture must not break the editor.
                }
            }
            // Only cache known keys: arbitrary unknown item IDs must not grow the cache.
            if (Manifest.Value.ContainsKey(key)) Cache[key] = bitmap;
            return bitmap;
        }
    }
}

public sealed class GameIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        GameVisuals.Get(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
