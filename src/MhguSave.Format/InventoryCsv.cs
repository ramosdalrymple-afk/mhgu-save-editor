using System.Globalization;
using System.Text;

namespace MhguSave.Format;

public static class InventoryCsv
{
    public static string Export(PackedItemList inventory, ItemCatalog catalog)
    {
        var csv = new StringBuilder("slot,id,name,count\r\n");
        for (var i = 0; i < inventory.Count; i++)
        {
            var item = inventory[i];
            csv.Append(i + 1).Append(',').Append(item.Id).Append(',').Append('"')
                .Append(item.IsEmpty ? "" : catalog.NameOf(item.Id).Replace("\"", "\"\""))
                .Append('"').Append(',').Append(item.Count).Append("\r\n");
        }
        return csv.ToString();
    }

    /// <summary>CSV files describe a full replacement. Omitted slots become empty; duplicate slots are rejected.</summary>
    public static ItemSlot[] Parse(string text, int capacity)
    {
        var records = ParseRecords(text.TrimStart('\uFEFF'));
        if (records.Count == 0 || records[0].Count != 4 || !records[0].SequenceEqual(new[] { "slot", "id", "name", "count" }, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("CSV header must be slot,id,name,count.");
        var result = new ItemSlot[capacity];
        var seen = new HashSet<int>();
        for (var r = 1; r < records.Count; r++)
        {
            var cells = records[r];
            if (cells.Count == 1 && string.IsNullOrWhiteSpace(cells[0])) continue;
            if (cells.Count != 4 || !int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var slot)
                || !int.TryParse(cells[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                || !int.TryParse(cells[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                || slot < 1 || slot > capacity || (uint)id > PackedItemList.MaxId || (uint)count > PackedItemList.MaxCount)
                throw new ArgumentException($"CSV row {r + 1} has an invalid slot, ID, or count.");
            if (!seen.Add(slot)) throw new ArgumentException($"CSV row {r + 1} repeats slot {slot}.");
            result[slot - 1] = new(id, count);
        }
        return result;
    }

    private static List<List<string>> ParseRecords(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var closed = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                }
                else cell.Append(c);
                continue;
            }
            if (c is ',' or '\r' or '\n')
            {
                row.Add(cell.ToString()); cell.Clear(); closed = false;
                if (c != ',')
                {
                    rows.Add(row); row = [];
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
            }
            else if (c == '"' && cell.Length == 0 && !closed) quoted = true;
            else if (closed || c == '"') throw new ArgumentException("CSV contains an invalid quoted field.");
            else cell.Append(c);
        }
        if (quoted) throw new ArgumentException("CSV contains an unterminated quoted field.");
        if (cell.Length > 0 || row.Count > 0 || closed) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}
