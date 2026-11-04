using System.Globalization;
using System.Reflection;
using System.Text;

namespace MhguSave.Format;

/// <summary>One equipment type code the box can hold.</summary>
public sealed record EquipmentTypeInfo(int Code, string Name, string Kind)
{
    public string IconKey => $"equipment:{Code}";
    /// <summary>True for the five armor parts.</summary>
    public bool IsArmor => Kind == "armor";

    /// <summary>True for weapon classes.</summary>
    public bool IsWeapon => Kind == "weapon";

    /// <summary>True for Palico gear.</summary>
    public bool IsPalico => Kind == "palico";
}

/// <summary>
/// Names and limits for the equipment box: type codes, per-type equipment names and max
/// levels, skill trees and decorations. All four tables ship as embedded CSVs.
/// </summary>
public sealed class EquipmentCatalog
{
    private const string TypeResource = "MhguSave.Format.Data.equip-types.csv";
    private const string NameResource = "MhguSave.Format.Data.equip-names.csv";
    private const string SkillResource = "MhguSave.Format.Data.skill-names.csv";
    private const string DecoResource = "MhguSave.Format.Data.deco-names.csv";

    private readonly Dictionary<int, EquipmentTypeInfo> _types;
    private readonly Dictionary<(int Type, int Id), (string Name, int MaxLevel)> _names;
    private readonly Dictionary<int, string> _skills;
    private readonly Dictionary<int, string> _decorations;
    private readonly Dictionary<int, int> _decorationSlots;

    private EquipmentCatalog(
        Dictionary<int, EquipmentTypeInfo> types,
        Dictionary<(int, int), (string, int)> names,
        Dictionary<int, string> skills,
        Dictionary<int, string> decorations,
        Dictionary<int, int> decorationSlots)
    {
        _types = types;
        _names = names;
        _skills = skills;
        _decorations = decorations;
        _decorationSlots = decorationSlots;
        Types = types.Values.OrderBy(type => type.Code).ToList();
    }

    /// <summary>A catalog that knows no names; every lookup falls back to a generated label.</summary>
    public static EquipmentCatalog Empty { get; } = new([], [], [], [], []);

    /// <summary>Every type code the box can hold, ordered by code.</summary>
    public IReadOnlyList<EquipmentTypeInfo> Types { get; }

    /// <summary>Number of named equipment records.</summary>
    public int NameCount => _names.Count;

    public IReadOnlyList<CatalogChoice> Skills => _skills
        .Where(p => p.Key > 0 && !string.IsNullOrWhiteSpace(p.Value))
        .OrderBy(p => p.Value).Select(p => new CatalogChoice(p.Key, p.Value))
        .Prepend(new CatalogChoice(0, "None")).ToArray();

    public IReadOnlyList<CatalogChoice> Decorations => _decorations
        .Where(p => p.Key > 0 && !string.IsNullOrWhiteSpace(p.Value))
        .OrderBy(p => p.Value).Select(p => new CatalogChoice(p.Key, p.Value, DecorationSlots(p.Key), IsItem: true))
        .Prepend(new CatalogChoice(0, "None")).ToArray();

    public bool HasEquipment(int type, int id) => id > 0 && _types.ContainsKey(type)
        && _names.TryGetValue((type, id), out var entry)
        && !string.IsNullOrWhiteSpace(entry.Name) && entry.Name != "(None)";

    public bool HasSkill(int id) => id == 0 || _skills.ContainsKey(id);
    public bool HasDecoration(int id) => id == 0 || _decorations.ContainsKey(id);

    public IEnumerable<EquipmentPick> Search(string query, int type, int limit = 40) =>
        _names.Where(entry => entry.Key.Type == type && HasEquipment(type, entry.Key.Id) &&
                (entry.Value.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) || entry.Key.Id.ToString() == query.Trim()))
            .OrderBy(entry => entry.Key.Id).Take(limit)
            .Select(entry => new EquipmentPick(entry.Key.Id, entry.Value.Name, type));

    /// <summary>Loads the tables embedded in the library.</summary>
    public static EquipmentCatalog LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var types = new Dictionary<int, EquipmentTypeInfo>();
        foreach (var cells in ReadCsv(assembly, TypeResource))
        {
            if (cells.Count >= 3 && int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                types[code] = new EquipmentTypeInfo(code, cells[1], cells[2]);
            }
        }

        var names = new Dictionary<(int, int), (string, int)>();
        foreach (var cells in ReadCsv(assembly, NameResource))
        {
            if (cells.Count >= 4 &&
                int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var type) &&
                int.TryParse(cells[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                var max = int.TryParse(cells[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
                names[(type, id)] = (cells[2], Math.Max(1, max));
            }
        }

        var skills = new Dictionary<int, string>();
        foreach (var cells in ReadCsv(assembly, SkillResource))
        {
            if (cells.Count >= 2 && int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                skills[id] = cells[1];
            }
        }

        var decorations = new Dictionary<int, string>();
        var slots = new Dictionary<int, int>();
        foreach (var cells in ReadCsv(assembly, DecoResource))
        {
            if (cells.Count >= 2 && int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                decorations[id] = cells[1];
                if (cells.Count >= 3 && int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                {
                    slots[id] = size;
                }
            }
        }

        return new EquipmentCatalog(types, names, skills, decorations, slots);
    }

    /// <summary>Type record for a code, or a stand-in when the code is unknown.</summary>
    public EquipmentTypeInfo TypeOf(int code) =>
        _types.TryGetValue(code, out var type) ? type : new EquipmentTypeInfo(code, $"Type {code}", "unknown");

    /// <summary>Display name for a piece, or a generated label when the id is unknown.</summary>
    public string NameOf(int type, int id) =>
        _names.TryGetValue((type, id), out var entry) && !string.IsNullOrWhiteSpace(entry.Name)
            ? entry.Name
            : $"{TypeOf(type).Name} #{id}";

    /// <summary>Highest level the piece accepts, 1 when unknown.</summary>
    public int MaxLevel(int type, int id) =>
        _names.TryGetValue((type, id), out var entry) ? entry.MaxLevel : 1;

    /// <summary>Skill tree name, or <c>Skill N</c>.</summary>
    public string SkillName(int id) =>
        _skills.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : $"Skill {id}";

    /// <summary>Decoration name for a decoration item id, or <c>Jewel N</c>.</summary>
    public string DecorationName(int id) =>
        _decorations.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : $"Jewel {id}";

    /// <summary>Number of slots a decoration occupies, 0 when unknown.</summary>
    public int DecorationSlots(int id) => GameRarity.DecorationCost(id) ?? _decorationSlots.GetValueOrDefault(id);

    private static IEnumerable<IReadOnlyList<string>> ReadCsv(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            yield break;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var cells = Split(line);
            if (int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                yield return cells;
            }
        }
    }

    private static List<string> Split(string line)
    {
        var cells = new List<string>(4);
        var current = new StringBuilder();
        var quoted = false;

        foreach (var ch in line)
        {
            if (quoted)
            {
                if (ch == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                quoted = true;
            }
            else if (ch == ',')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
