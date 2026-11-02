using System.Globalization;
using System.Reflection;
using System.Text;

namespace MhguSave.Format;

/// <summary>
/// Item id to display name. Names ship as an embedded CSV (<c>Data/item-names.csv</c>,
/// one <c>id,name</c> row per item) taken from the game's own item table; a CSV on disk can
/// override it.
/// </summary>
public sealed class ItemCatalog
{
    private const string ResourceName = "MhguSave.Format.Data.item-names.csv";

    private readonly Dictionary<int, string> _names;

    private ItemCatalog(Dictionary<int, string> names) => _names = names;

    /// <summary>A catalog that knows no names; every lookup falls back to <c>Item N</c>.</summary>
    public static ItemCatalog Empty { get; } = new([]);

    /// <summary>Number of named items.</summary>
    public int Count => _names.Count;

    /// <summary>Loads the names embedded in the library.</summary>
    public static ItemCatalog LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return Empty;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return new ItemCatalog(Parse(reader));
    }

    /// <summary>Loads names from a CSV with <c>id,name</c> rows (header optional).</summary>
    public static ItemCatalog LoadCsv(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        return new ItemCatalog(Parse(reader));
    }

    /// <summary>Name for an item id, or <c>Item N</c> when the id is unknown.</summary>
    public string NameOf(int id) =>
        _names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : $"Item {id}";

    /// <summary>True when the id has a real name.</summary>
    public bool HasName(int id) => _names.ContainsKey(id);

    /// <summary>
    /// Items whose name contains <paramref name="text"/> (case-insensitive), ordered by id.
    /// </summary>
    public IReadOnlyList<(int Id, string Name)> Search(string text, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(text) || limit <= 0)
        {
            return [];
        }

        var query = text.Trim();
        var matches = new List<(int Id, string Name)>();
        foreach (var pair in _names.OrderBy(pair => pair.Key))
        {
            if (pair.Value.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add((pair.Key, pair.Value));
                if (matches.Count >= limit)
                {
                    break;
                }
            }
        }

        return matches;
    }

    private static Dictionary<int, string> Parse(TextReader reader)
    {
        var names = new Dictionary<int, string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var comma = line.IndexOf(',');
            if (comma <= 0)
            {
                continue;
            }

            if (!int.TryParse(line[..comma], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                continue; // header row
            }

            names[id] = Unquote(line[(comma + 1)..]).Trim();
        }

        return names;
    }

    private static string Unquote(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        return value[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
    }
}
