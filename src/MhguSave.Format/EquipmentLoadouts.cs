using System.Buffers.Binary;
using System.Text;

namespace MhguSave.Format;

/// <summary>An editable snapshot; writing a loadout validates everything before committing bytes.</summary>
public sealed record EquipmentLoadout(string Name, ushort[] Gear, byte Style, byte[] Arts, byte SpArts,
    uint[] Pigment, byte[] ColorModes, bool[] DefaultColors);

public sealed partial class CharacterSlot
{
    public const ushort NoEquipment = ushort.MaxValue;
    public const int HunterSetCount = 40;
    public const int PalicoSetCount = 24;

    private int SetAddress(bool palico, int index)
    {
        if ((uint)index >= (palico ? PalicoSetCount : HunterSetCount))
            throw new ArgumentOutOfRangeException(nameof(index));
        return BaseOffset + (palico ? 0x21E0E + index * 68 : 0x208CE + index * 136);
    }

    public EquipmentLoadout ReadEquipmentSet(bool palico, int index)
    {
        var at = SetAddress(palico, index);
        return new EquipmentLoadout(ReadString(at, 42),
            Enumerable.Range(0, palico ? 3 : 7).Select(p => _save.ReadUInt16(at + 42 + p * 2)).ToArray(),
            palico ? (byte)6 : _save.ReadByte(at + 130),
            palico ? new byte[3] : _save.Raw.AsSpan(at + 131, 3).ToArray(),
            palico ? (byte)0 : (byte)(_save.ReadByte(at + 134) & 7),
            Enumerable.Range(0, 5).Select(p => palico ? 0u : _save.ReadUInt32(at + 100 + p * 4)).ToArray(),
            palico ? new byte[5] : _save.Raw.AsSpan(at + 120, 5).ToArray(),
            Enumerable.Range(0, 5).Select(p => palico || _save.ReadByte(at + 125 + p) != 0).ToArray());
    }

    public void WriteEquipmentSet(bool palico, int index, EquipmentLoadout value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var at = SetAddress(palico, index);
        var count = palico ? 3 : 7;
        if (value.Gear.Length != count || value.Arts.Length != 3 || value.Pigment.Length != 5
            || value.ColorModes.Length != 5 || value.DefaultColors.Length != 5)
            throw new ArgumentException("The loadout has an invalid number of fields.");
        if (value.Name.Contains('\0') || value.Name.EnumerateRunes().Count() > 10 || Encoding.UTF8.GetByteCount(value.Name) > 41)
            throw new ArgumentException("Set names must fit in 10 characters and 41 UTF-8 bytes.");
        var original = ReadEquipmentSet(palico, index);
        var box = palico ? PalicoEquipmentBox : EquipmentBox;
        for (var p = 0; p < count; p++)
        {
            var id = value.Gear[p];
            // Preserve an existing unknown reference if this particular selection was not edited.
            if (id == NoEquipment || id == original.Gear[p]) continue;
            if (id >= box.Count || box[id].IsEmpty || !FitsPosition(box[id].Type, palico, p))
                throw new ArgumentException($"Choose compatible equipment for loadout position {p + 1}.");
        }
        if (!palico && (value.Style > 6 && value.Style != original.Style || value.SpArts > 7
            || value.ColorModes.Where((m, p) => m != original.ColorModes[p]).Any(m => m > 31)))
            throw new ArgumentException("The hunting style, SP bits, or color mode is outside its storage range.");
        var staged = _save.Raw.AsSpan(at, palico ? 68 : 136).ToArray();
        if (value.Name != original.Name)
        {
            staged.AsSpan(0, 42).Clear();
            Encoding.UTF8.GetBytes(value.Name, staged.AsSpan(0, 42));
        }
        for (var p = 0; p < count; p++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(staged.AsSpan(42 + p * 2), value.Gear[p]);
            if (!palico && value.Gear[p] != original.Gear[p])
            {
                var decos = value.Gear[p] == NoEquipment ? new int[3] : box[value.Gear[p]].Decorations;
                for (var j = 0; j < 3; j++)
                    BinaryPrimitives.WriteUInt16LittleEndian(staged.AsSpan(56 + p * 6 + j * 2), (ushort)decos[j]);
            }
        }
        if (!palico)
        {
            for (var p = 0; p < 5; p++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(staged.AsSpan(100 + p * 4), value.Pigment[p]);
                staged[120 + p] = value.ColorModes[p];
                if (value.DefaultColors[p] != original.DefaultColors[p]) staged[125 + p] = value.DefaultColors[p] ? (byte)1 : (byte)0;
            }
            staged[130] = value.Style;
            value.Arts.CopyTo(staged, 131);
            staged[134] = (byte)((staged[134] & ~7) | value.SpArts);
        }
        staged.CopyTo(_save.Raw.AsSpan(at));
        _save.MarkDirty();
    }

    public static bool FitsPosition(int type, bool palico, int position) => palico
        ? type == 22 + position
        : position == 0 ? type is >= 7 and <= 21 : type == position;

    public void ClearEquipmentSet(bool palico, int index)
    {
        var at = SetAddress(palico, index);
        var data = new byte[palico ? 68 : 136];
        Encoding.UTF8.GetBytes("---", data);
        data.AsSpan(42, palico ? 6 : 14).Fill(255);
        if (palico) data.AsSpan(48, 3).Fill(1);
        else { data.AsSpan(125, 5).Fill(1); data[130] = 6; }
        data.CopyTo(_save.Raw.AsSpan(at));
        _save.MarkDirty();
    }

    public void RegisterCurrentEquipment(int index)
    {
        var current = ReadEquipmentSet(false, index);
        var gear = Enumerable.Range(0, 7).Select(p => _save.ReadUInt16(BaseOffset + 0x23B39 + p * 2)).ToArray();
        for (var p = 0; p < 7; p++)
            if (gear[p] != NoEquipment && (gear[p] >= EquipmentBox.Count || EquipmentBox[gear[p]].IsEmpty
                || !FitsPosition(EquipmentBox[gear[p]].Type, false, p)))
                throw new ArgumentException($"The currently worn gear has an invalid reference at position {p + 1}.");
        var defaults = _save.ReadUInt16(BaseOffset + 0x23B7B);
        var modes = _save.ReadUInt32(BaseOffset + 0x23B77);
        if (Enumerable.Range(0, 3).Any(p => _save.ReadUInt16(BaseOffset + 0x23A59 + p * 2) > 255))
            throw new ArgumentException("The currently equipped Hunter Arts contain an unsupported ID.");
        var draft = new EquipmentLoadout(string.IsNullOrWhiteSpace(current.Name) || current.Name == "---" ? $"Set {index + 1:00}" : current.Name,
            gear, _save.ReadByte(BaseOffset + 0x23B4C),
            Enumerable.Range(0, 3).Select(p => checked((byte)_save.ReadUInt16(BaseOffset + 0x23A59 + p * 2))).ToArray(),
            (byte)(_save.ReadUInt16(BaseOffset + 0x23A5F) & 7),
            Enumerable.Range(0, 5).Select(p => _save.ReadUInt32(BaseOffset + 0x23B53 + p * 4)).ToArray(),
            Enumerable.Range(0, 5).Select(p => (byte)((modes >> (p * 5)) & 31)).ToArray(),
            Enumerable.Range(0, 5).Select(p => (defaults & (1 << p)) != 0).ToArray());
        WriteEquipmentSet(false, index, draft);
        // Register refreshes the cached decorations even if the box indices stayed the same.
        var at = SetAddress(false, index);
        for (var p = 0; p < 7; p++)
        {
            var decos = gear[p] == NoEquipment ? new int[3] : EquipmentBox[gear[p]].Decorations;
            for (var j = 0; j < 3; j++) _save.WriteUInt16(at + 56 + p * 6 + j * 2, (ushort)decos[j]);
        }
    }

    private IReadOnlyList<string> FindEquipmentReferences(bool palico, int index)
    {
        var result = new List<string>();
        bool PointsAt(int offset, int n) => Enumerable.Range(0, n).Any(p => _save.ReadUInt16(offset + p * 2) == index);
        if (!palico && PointsAt(BaseOffset + 0x23B39, 7)) result.Add("currently equipped gear");
        for (var s = 0; s < (palico ? PalicoSetCount : HunterSetCount); s++)
        {
            var at = SetAddress(palico, s);
            if (PointsAt(at + 42, palico ? 3 : 7))
                result.Add($"{(palico ? "Palico set" : "My Set")} {s + 1} ({ReadString(at, 42)})");
        }
        if (palico)
        {
            for (var p = 0; p < 108; p++)
            {
                var at = BaseOffset + (p < 84 ? 0x23BB6 + p * 324 : 0x2A606 + (p - 84) * 324);
                if (_save.ReadByte(at) != 0 && PointsAt(at + 256, 7))
                    result.Add($"Palico {p + 1} ({ReadString(at, 32)})");
            }
        }
        return result;
    }

    public IReadOnlyList<string> EquipmentSetWarnings(int index)
    {
        var set = ReadEquipmentSet(false, index);
        var warnings = new List<string>();
        int[] slots = [2, 3, 1, 1, 3, 1, 0];
        var available = set.Style < slots.Length ? slots[set.Style] : 0;
        var weapon = set.Gear[0] < EquipmentBox.Count ? EquipmentBox[set.Gear[0]].Type : 0;
        var families = new System.Collections.Generic.HashSet<int>();
        for (var p = 0; p < 3; p++)
        {
            var art = set.Arts[p];
            if (art == 0) continue;
            if (p >= available) warnings.Add($"Art slot {p + 1} is unavailable for this style");
            if (art > 190 || art is >= 71 and <= 82) { warnings.Add($"Art ID {art} is not a known Hunter Art"); continue; }
            if (_save.ReadBits(BaseOffset + HunterArtsOffset, art, 1) == 0) warnings.Add($"Art {art} is locked");
            if (art > 10 && (art - 11) / 12 + 7 != weapon) warnings.Add($"Art {art} does not match the weapon class");
            var family = art <= 10 ? art : 11 + (art - 11) / 3 * 3;
            if (!families.Add(family)) warnings.Add($"Art {art} repeats a Hunter Art family");
        }
        return warnings;
    }
}
