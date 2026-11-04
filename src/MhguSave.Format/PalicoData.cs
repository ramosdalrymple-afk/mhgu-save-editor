using System.Text;

namespace MhguSave.Format;

public sealed record PalicoData(string Name, uint Experience, int Level, int Forte, int Target,
    string Greeting, string OriginalOwner, byte[] EquippedMoves, byte[] EquippedSkills, byte[] LearnedMoves, byte[] LearnedSkills);

public sealed partial class CharacterSlot
{
    public const int CompanionCount = 84;
    public const int HireableCount = 24;

    private int PalicoAddress(int index)
    {
        if ((uint)index >= CompanionCount + HireableCount) throw new ArgumentOutOfRangeException(nameof(index));
        return BaseOffset + (index < CompanionCount ? 0x23BB6 + index * 324 : 0x2A606 + (index - CompanionCount) * 324);
    }

    public PalicoData ReadPalico(int index)
    {
        var at = PalicoAddress(index);
        return new(ReadString(at, 32), _save.ReadUInt32(at + 32), _save.ReadByte(at + 36) + 1,
            _save.ReadByte(at + 37), _save.ReadByte(at + 39), ReadString(at + 96, 60), ReadString(at + 156, 32),
            _save.Raw.AsSpan(at + 40, 8).ToArray(), _save.Raw.AsSpan(at + 48, 8).ToArray(),
            _save.Raw.AsSpan(at + 56, 16).ToArray(), _save.Raw.AsSpan(at + 72, 12).ToArray());
    }

    /// <summary>Edit the identity and growth fields of an existing Palico; preserve the entire remaining record.</summary>
    public void WritePalicoIdentity(int index, string name, uint experience, int level, int forte, int target,
        string greeting, string owner)
    {
        var at = PalicoAddress(index);
        var original = ReadPalico(index);
        if (original.Name.Length == 0) throw new ArgumentException("An empty Palico slot requires a complete imported record, not an identity edit.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The Palico name cannot be empty.");
        if (level is < 1 or > 99 || forte is < 0 or > 7 || target is < 0 or > 5)
            throw new ArgumentException("Palico level must be 1–99, forte 0–7, and target 0–5.");
        if (forte != original.Forte)
            throw new ArgumentException("Changing forte also requires rebuilding innate moves and skills. Forte is currently read-only.");
        void Check(string text, int length, string label)
        {
            if (text.Contains('\0') || Encoding.UTF8.GetByteCount(text) >= length)
                throw new ArgumentException($"{label} must fit in {length - 1} UTF-8 bytes without null characters.");
        }
        Check(name, 32, "Palico name"); Check(greeting, 60, "Greeting"); Check(owner, 32, "Original owner");
        var draft = _save.Raw.AsSpan(at, 324).ToArray();
        void Text(int offset, int length, string value)
        {
            draft.AsSpan(offset, length).Clear();
            Encoding.UTF8.GetBytes(value, draft.AsSpan(offset, length));
        }
        Text(0, 32, name); Text(96, 60, greeting); Text(156, 32, owner);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(draft.AsSpan(32), experience);
        draft[36] = (byte)(level - 1); draft[39] = (byte)target;
        draft.CopyTo(_save.Raw.AsSpan(at));
        _save.MarkDirty();
    }

    public int ReadPalicoRole(int role)
    {
        if ((uint)role >= 3) throw new ArgumentOutOfRangeException(nameof(role));
        var index = _save.ReadByte(BaseOffset + 0x23B9D + role);
        return index < CompanionCount ? index : -1;
    }

    public void WritePalicoRole(int role, int index)
    {
        if ((uint)role >= 3) throw new ArgumentOutOfRangeException(nameof(role));
        if (index < -1 || index >= CompanionCount || index >= 0 && ReadPalico(index).Name.Length == 0)
            throw new ArgumentException("Choose an existing companion or None.");
        for (var r = 0; r < 3; r++)
            if (r == role || index >= 0 && ReadPalicoRole(r) == index)
                _save.Raw[BaseOffset + 0x23B9D + r] = r == role && index >= 0 ? (byte)index : (byte)255;
        _save.MarkDirty();
    }
}
