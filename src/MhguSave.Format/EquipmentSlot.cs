namespace MhguSave.Format;

/// <summary>
/// One 36-byte equipment box record: a weapon, an armor piece, a talisman or Palico gear.
/// Field offsets follow the community save map (<c>base + 0x62EE</c>, 2000 × 36 bytes).
/// </summary>
public sealed class EquipmentSlot
{
    /// <summary>Size of one record.</summary>
    public const int RecordSize = 36;

    /// <summary>Low five bits of the flag word: the equipment type code.</summary>
    public const int TypeMask = 0x1F;

    /// <summary>Type code of a talisman.</summary>
    public const int TalismanType = 6;

    /// <summary>Type code of an Insect Glaive (the only weapon with a kinsect payload).</summary>
    public const int InsectGlaiveType = 20;

    /// <summary>Highest level the five level bits can hold (level is stored as level - 1).</summary>
    public const int MaxLevel = 32;

    /// <summary>Number of decoration fields per record.</summary>
    public const int DecorationCount = 3;

    /// <summary>Type code, 0 when empty. 1 head, 2 chest, 3 arms, 4 waist, 5 legs, 6 talisman, 7-21 weapons, 22-24 Palico gear.</summary>
    public int Type { get; set; }

    /// <summary>Level, 1-32. Stored as level - 1 in bits 5-9 of the flag word.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Equipment id: armor series, weapon-tree index, or Palico record.</summary>
    public int Id { get; set; }

    /// <summary>Transmog appearance id (armor only, 0 = the piece's own look).</summary>
    public int TransmogId { get; set; }

    /// <summary>Level of the transmog source piece, 1 when unused.</summary>
    public int TransmogLevel { get; set; } = 1;

    /// <summary>Decoration item ids filling the record's three deco fields, 0 = empty.</summary>
    public int Deco1 { get; set; }

    /// <inheritdoc cref="Deco1"/>
    public int Deco2 { get; set; }

    /// <inheritdoc cref="Deco1"/>
    public int Deco3 { get; set; }

    /// <summary>Talisman skill tree 1 (0 = none).</summary>
    public int Skill1 { get; set; }

    /// <summary>Points for <see cref="Skill1"/>, -128..127.</summary>
    public int SkillPoints1 { get; set; }

    /// <summary>Talisman skill tree 2 (0 = none).</summary>
    public int Skill2 { get; set; }

    /// <summary>Points for <see cref="Skill2"/>.</summary>
    public int SkillPoints2 { get; set; }

    /// <summary>Talisman decoration slots, 0-3.</summary>
    public int SlotCount { get; set; }

    /// <summary>Charm the talisman came from: 353 Mystery, 354 Shining, 355 Timeworn, 356 Enduring.</summary>
    public int CharmItem { get; set; }

    /// <summary>Insect Glaive kinsect id (only meaningful for type 20).</summary>
    public int KinsectId { get; set; }

    /// <summary>Insect Glaive kinsect level.</summary>
    public int KinsectLevel { get; set; }

    /// <summary>Decorations as an array, so the UI can loop over them.</summary>
    public int[] Decorations
    {
        get => [Deco1, Deco2, Deco3];
        set
        {
            Deco1 = value.Length > 0 ? value[0] : 0;
            Deco2 = value.Length > 1 ? value[1] : 0;
            Deco3 = value.Length > 2 ? value[2] : 0;
        }
    }

    /// <summary>True when the record holds nothing.</summary>
    public bool IsEmpty => Type == 0 && Id == 0 && Deco1 == 0 && Deco2 == 0 && Deco3 == 0;

    /// <summary>Reads a record from raw save bytes.</summary>
    public static EquipmentSlot Read(ReadOnlySpan<byte> record)
    {
        var flags = (int)(record[0] | (record[1] << 8));
        var slot = new EquipmentSlot
        {
            Type = flags & TypeMask,
            Level = ((flags >> 5) & 0x1F) + 1,
            TransmogLevel = ((flags >> 10) & 0x1F) + 1,
            Id = record[2] | (record[3] << 8),
            TransmogId = record[4] | (record[5] << 8),
            Deco1 = record[6] | (record[7] << 8),
            Deco2 = record[8] | (record[9] << 8),
            Deco3 = record[10] | (record[11] << 8),
        };

        if (slot.Type == TalismanType)
        {
            slot.Skill1 = record[12];
            slot.Skill2 = record[13];
            slot.SkillPoints1 = (sbyte)record[14];
            slot.SkillPoints2 = (sbyte)record[15];
            slot.SlotCount = record[16];
            slot.CharmItem = record[18] | (record[19] << 8);
        }
        else if (slot.Type == InsectGlaiveType)
        {
            slot.KinsectId = record[12];
            slot.KinsectLevel = record[13] + 1;
        }

        return slot;
    }

    /// <summary>Writes the record back into raw save bytes, keeping payload bytes it does not own.</summary>
    public void Write(Span<byte> record)
    {
        CheckRange(Type, 0, TypeMask, nameof(Type));
        CheckRange(Level, 1, MaxLevel, nameof(Level));
        CheckRange(TransmogLevel, 1, MaxLevel, nameof(TransmogLevel));
        CheckRange(Id, 0, 0xFFFF, nameof(Id));
        CheckRange(TransmogId, 0, 0xFFFF, nameof(TransmogId));
        CheckRange(Deco1, 0, 0xFFFF, nameof(Deco1));
        CheckRange(Deco2, 0, 0xFFFF, nameof(Deco2));
        CheckRange(Deco3, 0, 0xFFFF, nameof(Deco3));

        // Bit 15 is not owned by the editor.
        var flags = (record[1] & 0x80) << 8 | Type | ((Level - 1) << 5) | ((TransmogLevel - 1) << 10);
        record[0] = (byte)flags;
        record[1] = (byte)(flags >> 8);
        Put16(record, 2, Id);
        Put16(record, 4, TransmogId);
        Put16(record, 6, Deco1);
        Put16(record, 8, Deco2);
        Put16(record, 10, Deco3);

        if (Type == TalismanType)
        {
            CheckRange(Skill1, 0, 255, nameof(Skill1));
            CheckRange(Skill2, 0, 255, nameof(Skill2));
            CheckRange(SkillPoints1, -128, 127, nameof(SkillPoints1));
            CheckRange(SkillPoints2, -128, 127, nameof(SkillPoints2));
            CheckRange(SlotCount, 0, 3, nameof(SlotCount));
            CheckRange(CharmItem, 0, 0xFFFF, nameof(CharmItem));

            record[12] = (byte)Skill1;
            record[13] = (byte)Skill2;
            record[14] = (byte)(sbyte)SkillPoints1;
            record[15] = (byte)(sbyte)SkillPoints2;
            record[16] = (byte)SlotCount;
            Put16(record, 18, CharmItem);
        }
        else if (Type == InsectGlaiveType)
        {
            CheckRange(KinsectId, 0, 255, nameof(KinsectId));
            CheckRange(KinsectLevel, 1, 256, nameof(KinsectLevel));

            record[12] = (byte)KinsectId;
            record[13] = (byte)(KinsectLevel - 1);
        }
    }

    private static void Put16(Span<byte> record, int offset, int value)
    {
        record[offset] = (byte)value;
        record[offset + 1] = (byte)(value >> 8);
    }

    private static void CheckRange(int value, int min, int max, string name)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be {min}-{max}.");
        }
    }
}

/// <summary>The 2000-record equipment box of one character.</summary>
public sealed class EquipmentBox
{
    /// <summary>Number of records in the box.</summary>
    public const int Capacity = 2000;

    private readonly SaveContainer _save;
    private readonly int _offset;

    internal EquipmentBox(SaveContainer save, int byteOffset)
    {
        _save = save;
        _offset = byteOffset;
    }

    /// <summary>Number of records the box can hold.</summary>
    public int Count => Capacity;

    /// <summary>Reads or writes one record.</summary>
    public EquipmentSlot this[int index]
    {
        get
        {
            CheckIndex(index);
            return EquipmentSlot.Read(_save.Raw.AsSpan(_offset + (index * EquipmentSlot.RecordSize), EquipmentSlot.RecordSize));
        }
        set
        {
            CheckIndex(index);
            var span = _save.Raw.AsSpan(_offset + (index * EquipmentSlot.RecordSize), EquipmentSlot.RecordSize);
            if (value.IsEmpty)
            {
                span.Clear();
            }
            else
            {
                // Validate and serialize on a copy: a rejected payload must never partially
                // modify the live save (talisman and kinsect checks run after common fields).
                var staged = span.ToArray();
                value.Write(staged);
                staged.CopyTo(span);
            }

            _save.MarkDirty();
        }
    }

    /// <summary>Number of non-empty records.</summary>
    public int UsedCount()
    {
        var used = 0;
        for (var i = 0; i < Capacity; i++)
        {
            if (!this[i].IsEmpty)
            {
                used++;
            }
        }

        return used;
    }

    /// <summary>Index of the first empty record, or -1 when the box is full.</summary>
    public int FirstEmptyIndex()
    {
        for (var i = 0; i < Capacity; i++)
        {
            if (this[i].IsEmpty)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Puts a piece in the first empty record and returns its index, or -1 when full.</summary>
    public int Add(EquipmentSlot piece)
    {
        if (piece.IsEmpty)
        {
            throw new ArgumentException("Cannot add an empty equipment record.", nameof(piece));
        }

        var index = FirstEmptyIndex();
        if (index >= 0)
        {
            this[index] = piece;
        }

        return index;
    }

    private void CheckIndex(int index)
    {
        if ((uint)index >= Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Equipment index must be 0-{Capacity - 1}.");
        }
    }
}
