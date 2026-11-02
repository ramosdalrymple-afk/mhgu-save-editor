using System.Buffers.Binary;
using System.Security.Cryptography;

namespace MhguSave.Format;

/// <summary>
/// The MHGU ("system") save file: a 0x24-byte <c>sSavedata</c> container header
/// followed by the plaintext body. No encryption, no checksum.
/// </summary>
public sealed class SaveContainer
{
    /// <summary>Size of an MHGU Switch save (<c>0x4EB8BC</c>).</summary>
    public const int FileSize = 5_159_100;

    /// <summary>Container header size; the body starts here.</summary>
    public const int HeaderSize = 0x24;

    /// <summary>Distance between character slots.</summary>
    public const int SlotStride = 0x11F8C4;

    /// <summary>Number of character slots.</summary>
    public const int SlotCount = 3;

    /// <summary>Save version written by the game at body offset 0 (<c>0xC6</c>).</summary>
    public const int ExpectedBodyVersion = 0xC6;

    private const int BodyVersionOffset = HeaderSize;
    private const int SlotUsedOffset = HeaderSize + 0x04;
    private const int SlotPointerOffset = HeaderSize + 0x10;

    private readonly byte[] _data;
    private readonly CharacterSlot[] _slots;
    internal byte[] OriginalHash { get; private set; }

    private SaveContainer(byte[] data, string entryName)
    {
        _data = data;
        EntryName = entryName;
        OriginalHash = SHA256.HashData(data);

        var slots = new CharacterSlot[SlotCount];
        for (var i = 0; i < SlotCount; i++)
        {
            var pointer = ReadUInt32(SlotPointerOffset + (i * 4));
            if (pointer < 0x1C || pointer > data.Length - HeaderSize - SlotStride)
                throw new SaveFormatException($"Slot {i + 1} pointer is outside the character data.");
            var baseOffset = HeaderSize + (int)pointer;
            if (slots.Take(i).Any(slot => Math.Abs((long)slot.BaseOffset - baseOffset) < SlotStride))
                throw new SaveFormatException("Character slot regions overlap; the pointer table is corrupt.");
            slots[i] = new CharacterSlot(this, i, baseOffset, SlotUsedOffset + i);
        }

        _slots = slots;
    }

    /// <summary>File name the container was read from (<c>system</c> or <c>system_backup</c>).</summary>
    public string EntryName { get; }

    /// <summary>True once an editable field has been changed.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>The three character slots (empty slots have <see cref="CharacterSlot.InUse"/> false).</summary>
    public IReadOnlyList<CharacterSlot> Slots => _slots;

    /// <summary>Save version byte written by the game (198 on all known builds).</summary>
    public int SaveVersion => (int)ReadUInt32(BodyVersionOffset);

    /// <summary>Index of the last played slot, as stored by the game.</summary>
    public int LastSlot => ReadByte(HeaderSize + 0x07);

    /// <summary>Reads and validates a save file. The entry key must match the file name.</summary>
    public static SaveContainer Load(string path)
    {
        var name = Path.GetFileName(path);
        return Parse(File.ReadAllBytes(path), name);
    }

    /// <summary>Reads and validates a save image.</summary>
    /// <param name="data">Raw file bytes.</param>
    /// <param name="entryName">File name, used to check the container entry key.</param>
    public static SaveContainer Parse(byte[] data, string entryName)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != FileSize)
        {
            throw new SaveFormatException(
                $"Unexpected file size {data.Length} (0x{data.Length:X}); MHGU Switch saves are {FileSize} (0x{FileSize:X}) bytes.");
        }

        var container = new SaveContainer(data, entryName);
        container.Validate(entryName);
        return container;
    }

    private void Validate(string entryName)
    {
        // Container header: sSavedata key/value table with a single "system" entry.
        if (ReadUInt32(0x00) != 0)
            throw new SaveFormatException("Container magic at 0x00 is not 0; this is not an MHGU save.");
        if (ReadUInt32(0x04) != 0)
            throw new SaveFormatException($"Unsupported container version {ReadUInt32(0x04)} at 0x04.");
        if (ReadUInt32(0x08) != 4)
            throw new SaveFormatException($"Unexpected entry table size {ReadUInt32(0x08)} at 0x08 (expected 4).");
        if (ReadUInt32(0x10) != 0x14)
            throw new SaveFormatException($"Unexpected entry offset 0x{ReadUInt32(0x10):X} at 0x10 (expected 0x14).");
        if (ReadUInt32(0x18) != 12)
            throw new SaveFormatException($"Unexpected entry type {ReadUInt32(0x18)} at 0x18 (expected 12).");

        var blobSize = ReadUInt64(0x1C);
        if (blobSize != (ulong)(_data.Length - HeaderSize))
            throw new SaveFormatException(
                $"Container blob size 0x{blobSize:X} does not match file size 0x{_data.Length:X}.");

        var key = ReadUInt32(0x14);
        var expected = JamCrc.Compute(entryName);
        if (key != expected)
        {
            var hint = entryName is "system" or "system_backup"
                ? $"The entry key says 0x{key:X8}, which is the key for another file name."
                : "Rename the file to system (or system_backup) before opening it.";
            throw new SaveFormatException($"Container entry key mismatch for \"{entryName}\". {hint}");
        }

        if (SaveVersion != ExpectedBodyVersion)
            throw new SaveFormatException($"Unsupported save version {SaveVersion} at body offset 0 (expected {ExpectedBodyVersion}).");
    }

    /// <summary>Live view of the file bytes (inspection and tests; mutate through the typed API).</summary>
    public ReadOnlySpan<byte> Span => _data;

    /// <summary>Copy of the current file bytes.</summary>
    public byte[] ToArray() => (byte[])_data.Clone();

    internal byte[] Raw => _data;

    internal void MarkDirty() => IsDirty = true;

    internal void AcceptChanges()
    {
        OriginalHash = SHA256.HashData(_data);
        IsDirty = false;
    }

    internal byte ReadByte(int offset) => _data[offset];

    internal ushort ReadUInt16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(offset));

    internal uint ReadUInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset));

    internal ulong ReadUInt64(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(_data.AsSpan(offset));

    internal void WriteUInt16(int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(offset), value);
        MarkDirty();
    }

    internal void WriteUInt32(int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan(offset), value);
        MarkDirty();
    }

    /// <summary>Reads <paramref name="bitCount"/> bits (LSB first) starting at bit <paramref name="bitIndex"/>.</summary>
    internal uint ReadBits(int byteOffset, int bitIndex, int bitCount)
    {
        uint value = 0;
        for (var i = 0; i < bitCount; i++)
        {
            var bit = bitIndex + i;
            var b = (_data[byteOffset + (bit >> 3)] >> (bit & 7)) & 1;
            value |= (uint)b << i;
        }

        return value;
    }

    /// <summary>Writes <paramref name="bitCount"/> bits (LSB first) starting at bit <paramref name="bitIndex"/>.</summary>
    internal void WriteBits(int byteOffset, int bitIndex, int bitCount, uint value)
    {
        for (var i = 0; i < bitCount; i++)
        {
            var bit = bitIndex + i;
            var index = byteOffset + (bit >> 3);
            var mask = (byte)(1 << (bit & 7));
            var set = ((value >> i) & 1) != 0;
            _data[index] = set ? (byte)(_data[index] | mask) : (byte)(_data[index] & ~mask);
        }

        MarkDirty();
    }
}

/// <summary>Thrown when a file is not a save this editor understands.</summary>
public sealed class SaveFormatException : Exception
{
    public SaveFormatException(string message) : base(message)
    {
    }
}
