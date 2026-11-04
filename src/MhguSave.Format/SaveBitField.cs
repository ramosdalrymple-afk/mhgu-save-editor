namespace MhguSave.Format;

/// <summary>
/// A run of single bits inside the save (quest maps, awards, "item obtained" flags).
/// Bits are LSB first inside each byte, and the last byte is masked so bits that belong
/// to the next structure are left alone.
/// </summary>
public sealed class SaveBitField
{
    private readonly SaveContainer _save;
    private readonly int _byteOffset;
    private readonly int? _mirrorOffset;

    internal SaveBitField(SaveContainer save, int byteOffset, int bitCount, int? mirrorOffset = null)
    {
        _save = save;
        _byteOffset = byteOffset;
        _mirrorOffset = mirrorOffset;
        BitCount = bitCount;
    }

    /// <summary>Number of bits in the field.</summary>
    public int BitCount { get; }

    /// <summary>Gets or sets one bit.</summary>
    public bool this[int bit]
    {
        get
        {
            CheckBit(bit);
            return _save.ReadBits(_byteOffset, bit, 1) != 0;
        }
        set
        {
            CheckBit(bit);
            _save.WriteBits(_byteOffset, bit, 1, value ? 1u : 0u);
            if (_mirrorOffset is { } mirror) _save.WriteBits(mirror, bit, 1, value ? 1u : 0u);
        }
    }

    /// <summary>Number of set bits.</summary>
    public int SetCount()
    {
        var count = 0;
        for (var bit = 0; bit < BitCount; bit++)
        {
            if (_save.ReadBits(_byteOffset, bit, 1) != 0)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Sets every bit of the field, leaving bits outside it untouched.</summary>
    public void SetAll()
    {
        if (_mirrorOffset is { } mirror) new SaveBitField(_save, mirror, BitCount).SetAll();
        var fullBytes = BitCount / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            _save.Raw[_byteOffset + i] = 0xFF;
        }

        var remaining = BitCount % 8;
        if (remaining > 0)
        {
            var last = fullBytes;
            var mask = (byte)((1 << remaining) - 1);
            _save.Raw[_byteOffset + last] = (byte)(_save.Raw[_byteOffset + last] | mask);
        }

        _save.MarkDirty();
    }

    /// <summary>Clears every bit of the field, leaving bits outside it untouched.</summary>
    public void ClearAll()
    {
        if (_mirrorOffset is { } mirror) new SaveBitField(_save, mirror, BitCount).ClearAll();
        var fullBytes = BitCount / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            _save.Raw[_byteOffset + i] = 0x00;
        }

        var remaining = BitCount % 8;
        if (remaining > 0)
        {
            var last = fullBytes;
            var keep = (byte)~(byte)((1 << remaining) - 1);
            _save.Raw[_byteOffset + last] = (byte)(_save.Raw[_byteOffset + last] & keep);
        }

        _save.MarkDirty();
    }

    /// <summary>Sets bits <paramref name="first"/> to <paramref name="last"/> inclusive.</summary>
    public void SetRange(int first, int last)
    {
        if (first < 0 || last >= BitCount || first > last)
        {
            throw new ArgumentOutOfRangeException(nameof(first), $"Range {first}-{last} is outside 0-{BitCount - 1}.");
        }

        for (var bit = first; bit <= last; bit++)
        {
            this[bit] = true;
        }
    }

    private void CheckBit(int bit)
    {
        if ((uint)bit >= (uint)BitCount)
        {
            throw new ArgumentOutOfRangeException(nameof(bit), bit, $"Bit index must be 0-{BitCount - 1}.");
        }
    }
}
