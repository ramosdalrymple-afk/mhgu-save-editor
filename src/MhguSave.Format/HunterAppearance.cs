namespace MhguSave.Format;

public sealed record HunterAppearance(byte Body, byte Face, byte Hair, byte Voice, byte Features, byte Clothing, uint SkinColor, uint ClothingColor);

public sealed partial class CharacterSlot
{
    public HunterAppearance ReadAppearance()
    {
        var at = BaseOffset + 0x23B47;
        return new(_save.ReadByte(at + 4), _save.ReadByte(at + 2), _save.ReadByte(at + 6), _save.ReadByte(at + 1),
            _save.ReadByte(at + 8), _save.ReadByte(at + 3), _save.ReadUInt32(at + 12 + 5 * 4), _save.ReadUInt32(at + 12 + 8 * 4));
    }

    public void WriteAppearance(HunterAppearance value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var original = ReadAppearance();
        var fields = new[] {
            (Offset: 4, Value: value.Body, Original: original.Body, Min: 0, Max: 1),
            (Offset: 2, Value: value.Face, Original: original.Face, Min: 0, Max: 17),
            (Offset: 6, Value: value.Hair, Original: original.Hair, Min: 0, Max: 29),
            (Offset: 1, Value: value.Voice, Original: original.Voice, Min: 1, Max: 20),
            (Offset: 8, Value: value.Features, Original: original.Features, Min: 0, Max: 14),
            (Offset: 3, Value: value.Clothing, Original: original.Clothing, Min: 0, Max: 6),
        };
        foreach (var field in fields)
            if (field.Value != field.Original && (field.Value < field.Min || field.Value > field.Max))
                throw new ArgumentException($"Appearance field {field.Offset} is outside the game's choices.");
        int[] copies = [0x23B47, 0x240, 0xC71D5];
        foreach (var field in fields.Where(f => f.Value != f.Original))
        {
            foreach (var at in copies) _save.Raw[BaseOffset + at + field.Offset] = field.Value;
            if (field.Offset == 4) _save.Raw[BaseOffset + 0x2A] = field.Value;
        }
        if (value.SkinColor != original.SkinColor)
            foreach (var at in copies) _save.WriteUInt32(BaseOffset + at + 12 + 5 * 4, value.SkinColor);
        if (value.ClothingColor != original.ClothingColor)
            foreach (var at in copies) _save.WriteUInt32(BaseOffset + at + 12 + 8 * 4, value.ClothingColor);
        _save.MarkDirty();
    }
}
