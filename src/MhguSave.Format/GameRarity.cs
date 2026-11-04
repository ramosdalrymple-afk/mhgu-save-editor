using System.Text.Json;

namespace MhguSave.Format;

/// <summary>Optional rarity and socket metadata extracted from a local game dump.</summary>
public static class GameRarity
{
    private sealed class Tables
    {
        public Dictionary<string, int> Items { get; set; } = [];
        public Dictionary<string, int> Equipment { get; set; } = [];
        public string[] Colors { get; set; } = [];
        public Dictionary<string, int> ArmorSockets { get; set; } = [];
        public Dictionary<string, int> WeaponSockets { get; set; } = [];
        public Dictionary<int, int> DecorationCosts { get; set; } = [];
    }
    private static readonly Lazy<Tables> Data = new(Load);
    private static Tables Load()
    {
        using var stream = typeof(GameRarity).Assembly.GetManifestResourceStream("MhguSave.Format.Data.Local.game-rarity.json");
        if (stream is null) return new();
        return JsonSerializer.Deserialize<Tables>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }
    public static bool IsAvailable => Data.Value.Colors.Length == 11;
    public static int Item(int id) => Data.Value.Items.GetValueOrDefault(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static int Equipment(int type, int id) => Data.Value.Equipment.GetValueOrDefault($"{type}:{id}");
    public static int? SocketCapacity(int type, int id, int level) => type switch
    {
        >= 1 and <= 5 => Data.Value.ArmorSockets.TryGetValue($"{type}:{id}", out var armor) ? armor : null,
        >= 7 and <= 21 => Data.Value.WeaponSockets.TryGetValue($"{type}:{id}:{level}", out var weapon) ? weapon : null,
        >= 22 and <= 24 => 0,
        _ => null,
    };
    internal static int? DecorationCost(int id) => Data.Value.DecorationCosts.TryGetValue(id, out var cost) ? cost : null;
    public static string Label(int rarity) => rarity == 11 ? "RARE X" : rarity > 0 ? $"RARE {rarity}" : "";
    public static string Color(int rarity) => rarity > 0 && rarity <= Data.Value.Colors.Length ? Data.Value.Colors[rarity - 1] : "#E6E6E6";
    public static string Icon(int type, int rarity) => rarity > 0 ? $"equipment:{type}:rarity:{rarity}" : $"equipment:{type}";
}
