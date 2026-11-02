namespace MhguSave.Format;

/// <summary>
/// CRC-32 (reflected, polynomial <c>0xEDB88320</c>) without the final inversion.
/// The container uses it as the key of an entry name: <c>"system"</c> hashes to
/// <c>0x36B2EE74</c>, <c>"system_backup"</c> to <c>0xD0819B92</c>.
/// </summary>
public static class JamCrc
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[i] = c;
        }

        return table;
    }

    /// <summary>Computes the JAMCRC of an ASCII entry name.</summary>
    public static uint Compute(string value)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in System.Text.Encoding.ASCII.GetBytes(value))
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }
}
