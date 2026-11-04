namespace MhguSave.Format;

public readonly record struct MonsterRecord(ushort Slain, ushort Captured, ushort SmallestPercent, ushort LargestPercent)
{
    public int Hunted => Slain + Captured;
}

public sealed partial class CharacterSlot
{
    public const int MonsterCount = 137;
    private static void CheckMonster(int index)
    {
        if (index is < 1 or > MonsterCount) throw new ArgumentOutOfRangeException(nameof(index));
    }

    public MonsterRecord ReadMonster(int index)
    {
        CheckMonster(index);
        return new(_save.ReadUInt16(BaseOffset + 0x5EA4 + index * 2),
            _save.ReadUInt16(BaseOffset + 0x5FB6 + index * 2),
            _save.ReadUInt16(BaseOffset + 0x60C6 + index * 4),
            _save.ReadUInt16(BaseOffset + 0x60C8 + index * 4));
    }

    /// <summary>Live hunt records. The separate Guild Card summary is left for the game to rebuild.</summary>
    public void WriteMonsterCounts(int index, ushort slain, ushort captured)
    {
        CheckMonster(index);
        _save.WriteUInt16(BaseOffset + 0x5EA4 + index * 2, slain);
        _save.WriteUInt16(BaseOffset + 0x5FB6 + index * 2, captured);
    }
}
