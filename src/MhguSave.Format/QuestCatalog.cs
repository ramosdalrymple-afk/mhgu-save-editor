using System.Globalization;
using System.Reflection;
using System.Text;

namespace MhguSave.Format;

/// <summary>One row of the game's quest table: index 1-1508 plus its labels.</summary>
public sealed record QuestInfo(int Index, string Category, string Rank, bool Prowler, string Name)
{
    /// <summary>Bit position inside the cleared / seen / failed maps.</summary>
    public int Bit => Index - 1;

    /// <summary>Label used in list rows, e.g. <c>Village ★3 · Hunt the Great Jaggi</c>.</summary>
    public string Display
    {
        get
        {
            var rank = Rank.Length == 0 ? "★?" : Rank;
            return $"{Index,4}  {Category} ★{rank}  {Name}";
        }
    }
}

/// <summary>
/// Quest table: index, category, rank, prowler flag and name. Ships as an embedded CSV
/// (<c>Data/quest-names.csv</c>) derived from the community quest index, so every bit in
/// the cleared bitmap can be shown as a real quest name.
/// </summary>
public sealed class QuestCatalog
{
    private const string ResourceName = "MhguSave.Format.Data.quest-names.csv";

    /// <summary>Number of quest indices the cleared bitmap carries.</summary>
    public const int QuestCount = 1508;

    private readonly Dictionary<int, QuestInfo> _quests;

    private QuestCatalog(Dictionary<int, QuestInfo> quests)
    {
        _quests = quests;
        Quests = quests.Values.OrderBy(quest => quest.Index).ToList();
    }

    /// <summary>An empty table; every lookup falls back to <c>Quest N</c>.</summary>
    public static QuestCatalog Empty { get; } = new([]);

    /// <summary>Number of known quests.</summary>
    public int Count => _quests.Count;

    /// <summary>All quests, ordered by index.</summary>
    public IReadOnlyList<QuestInfo> Quests { get; }

    /// <summary>Loads the table embedded in the library.</summary>
    public static QuestCatalog LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return Empty;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Load(reader);
    }

    /// <summary>Loads a table from a CSV with <c>index,category,rank,prowler,name</c> rows.</summary>
    public static QuestCatalog LoadCsv(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        return Load(reader);
    }

    /// <summary>Quest for an index, or a stand-in with just the number when unknown.</summary>
    public QuestInfo Get(int index) =>
        _quests.TryGetValue(index, out var quest) ? quest : new QuestInfo(index, "Quest", "?", false, $"Quest {index}");

    private static QuestCatalog Load(TextReader reader)
    {
        var quests = new Dictionary<int, QuestInfo>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var cells = Split(line);
            if (cells.Count < 5 ||
                !int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                continue; // header row or junk
            }

            quests[index] = new QuestInfo(
                index,
                cells[1],
                cells[2],
                cells[3].Equals("yes", StringComparison.OrdinalIgnoreCase),
                cells[4]);
        }

        var catalog = new QuestCatalog(quests);

        return catalog;
    }

    private static List<string> Split(string line)
    {
        var cells = new List<string>(6);
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
