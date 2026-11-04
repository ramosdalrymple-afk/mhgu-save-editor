using System.Text.Json;

namespace MhguSave.Format;

/// <summary>Optional user-local game text. Clean builds use descriptive ID fallbacks.</summary>
public static class LocalGameCatalog
{
    private static readonly Lazy<JsonDocument?> Document = new(() =>
    {
        using var stream = typeof(LocalGameCatalog).Assembly.GetManifestResourceStream("MhguSave.Format.Data.Local.game-catalog.json");
        return stream is null ? null : JsonDocument.Parse(stream);
    });

    public static string MonsterName(int index)
    {
        if (index is < 1 or > 137) throw new ArgumentOutOfRangeException(nameof(index));
        var name = Text("monsters", index - 1);
        if (name is null) return $"Monster {index}";
        return string.IsNullOrWhiteSpace(name) || name.StartsWith("dummy", StringComparison.OrdinalIgnoreCase)
            ? $"Unused monster index {index}" : name;
    }

    public static string HunterArtName(int id) => id == 0 ? "None" : Text("hunterArtMessages", id * 4) ?? $"Hunter Art {id}";
    public static string AwardName(int id) => Text("awardMessages", id) ?? $"Guild Card award {id + 1}";
    public static string AwardDescription(int id) => Text("awardMessages", 132 + id) ?? "";

    private static string? Text(string table, int index)
    {
        if (Document.Value is not { } document || !document.RootElement.TryGetProperty(table, out var values)
            || index < 0 || index >= values.GetArrayLength()) return null;
        return values[index].GetString();
    }

    public static IReadOnlyList<CatalogChoice> HunterArts { get; } = Enumerable.Range(0, 191)
        .Where(id => id == 0 || id is >= 1 and <= 70 || id is >= 83 and <= 190)
        .Select(id => new CatalogChoice(id, HunterArtName(id))).ToArray();
}
