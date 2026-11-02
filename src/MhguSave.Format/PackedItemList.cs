namespace MhguSave.Format;

/// <summary>One entry of the item box or pouch: a 12-bit item id and a 7-bit count.</summary>
/// <param name="Id">Item id; 0 means an empty slot.</param>
/// <param name="Count">Stack size, 0-127.</param>
public readonly record struct ItemSlot(int Id, int Count)
{
    /// <summary>An unused slot.</summary>
    public static ItemSlot Empty => new(0, 0);

    /// <summary>True when the slot holds no item.</summary>
    public bool IsEmpty => Id == 0;

    /// <summary>Renders as <c>id x count</c>, or a dash when empty.</summary>
    public override string ToString() => IsEmpty ? "-" : $"{Id} x{Count}";
}

/// <summary>
/// A run of packed item slots: 19 bits per slot, least significant bit first,
/// 12 bits of item id then 7 bits of count. Used by the item box (2300 slots at
/// <c>base + 0x278</c>) and the pouch (32 slots at <c>base + 0x27BF</c>).
/// </summary>
public sealed class PackedItemList
{
    /// <summary>Bits per slot.</summary>
    public const int BitsPerSlot = 19;

    /// <summary>Item ids are 12 bits wide.</summary>
    public const int MaxId = 0xFFF;

    /// <summary>Counts are 7 bits wide.</summary>
    public const int MaxCount = 127;

    private const int IdBits = 12;
    private const int CountBits = 7;

    private readonly SaveContainer _save;
    private readonly int _byteOffset;

    internal PackedItemList(SaveContainer save, int byteOffset, int count)
    {
        _save = save;
        _byteOffset = byteOffset;
        Count = count;
    }

    /// <summary>Number of slots in the list.</summary>
    public int Count { get; }

    /// <summary>Gets or sets one slot. Writes are bounds- and range-checked.</summary>
    public ItemSlot this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Slot index must be 0-{Count - 1}.");
            }

            var value = _save.ReadBits(_byteOffset, index * BitsPerSlot, BitsPerSlot);
            return new ItemSlot((int)(value & MaxId), (int)(value >> IdBits));
        }
        set
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Slot index must be 0-{Count - 1}.");
            }

            if ((uint)value.Id > MaxId)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value.Id, $"Item id must be 0-{MaxId}.");
            }

            if ((uint)value.Count > MaxCount)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value.Count, $"Item count must be 0-{MaxCount}.");
            }

            var packed = (uint)(value.Id | (value.Count << IdBits));
            _save.WriteBits(_byteOffset, index * BitsPerSlot, BitsPerSlot, packed);
        }
    }

    /// <summary>Indexes of the slots that hold an item.</summary>
    public IEnumerable<int> UsedIndexes()
    {
        for (var i = 0; i < Count; i++)
        {
            if (!this[i].IsEmpty)
            {
                yield return i;
            }
        }
    }

    /// <summary>Number of non-empty slots.</summary>
    public int UsedCount()
    {
        var used = 0;
        for (var i = 0; i < Count; i++)
        {
            if (!this[i].IsEmpty)
            {
                used++;
            }
        }

        return used;
    }

    /// <summary>Empties every slot.</summary>
    public void ClearAll()
    {
        for (var i = 0; i < Count; i++)
        {
            this[i] = ItemSlot.Empty;
        }
    }

    /// <summary>Index of the first empty slot, or -1 when the list is full.</summary>
    public int FirstEmptyIndex()
    {
        for (var i = 0; i < Count; i++)
        {
            if (this[i].IsEmpty)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Puts an item in the first empty slot and returns the index, or -1 when full.</summary>
    public int Add(ItemSlot item)
    {
        if (item.IsEmpty)
        {
            throw new ArgumentException("Cannot add an empty item.", nameof(item));
        }

        var index = FirstEmptyIndex();
        if (index >= 0)
        {
            this[index] = item;
        }

        return index;
    }
}
