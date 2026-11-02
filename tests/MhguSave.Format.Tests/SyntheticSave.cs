using System.Buffers.Binary;

namespace MhguSave.Format.Tests;

/// <summary>
/// Builds a structurally valid MHGU save image so the parser can be tested without a
/// real save. Sizes, header fields and slot pointers match a file the game wrote.
/// </summary>
internal static class SyntheticSave
{
    public const int Slot1Pointer = 0x18CC78;
    public const int Slot2Pointer = 0x2AC53C;
    public const int Slot3Pointer = 0x3CBE00;

    public static byte[] Create(string entryName = "system", byte usedSlots = 1)
    {
        var data = new byte[SaveContainer.FileSize];

        WriteU32(data, 0x00, 0);
        WriteU32(data, 0x04, 0);
        WriteU32(data, 0x08, 4);
        WriteU32(data, 0x0C, 0);
        WriteU32(data, 0x10, 0x14);
        WriteU32(data, 0x14, JamCrc.Compute(entryName));
        WriteU32(data, 0x18, 12);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(0x1C), (ulong)(data.Length - SaveContainer.HeaderSize));

        // Body header: version, slot-use bytes, block offsets, slot pointers (relative to 0x24).
        WriteU32(data, 0x24, SaveContainer.ExpectedBodyVersion);
        data[0x28] = (byte)(usedSlots & 1);
        data[0x29] = (byte)((usedSlots >> 1) & 1);
        data[0x2A] = (byte)((usedSlots >> 2) & 1);
        data[0x2B] = 0;
        WriteU32(data, 0x2C, 0xB25E);
        WriteU32(data, 0x30, 0xB281);
        WriteU32(data, 0x34, Slot1Pointer);
        WriteU32(data, 0x38, Slot2Pointer);
        WriteU32(data, 0x3C, Slot3Pointer);

        return data;
    }

    public static void WriteU32(byte[] data, int offset, int value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), (uint)value);

    public static void WriteU32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
}
