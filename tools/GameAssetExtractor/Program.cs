using System.Text;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Path = System.IO.Path;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: GameAssetExtractor <local game folder> <output folder>");
    return 1;
}

var root = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var nspPath = Path.Combine(root, "data", "games", "MHGU.nsp");
// Read the emulator's existing local configuration. Never copy or log key contents.
var keys = ExternalKeyReader.ReadKeyFile(Path.Combine(root, "user", "keys", "prod.keys"),
    File.Exists(Path.Combine(root, "user", "keys", "title.keys"))
        ? Path.Combine(root, "user", "keys", "title.keys") : null);

using var file = File.OpenRead(nspPath);
using var reader = new BinaryReader(file);
if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "PFS0")
    throw new InvalidDataException("Expected an NSP with a PFS0 file table.");
var count = reader.ReadInt32();
var stringsSize = reader.ReadInt32();
reader.ReadInt32();
if (count is < 1 or > 10000 || stringsSize is < 1 or > 16777216
    || 16L + count * 24L + stringsSize > file.Length)
    throw new InvalidDataException("Invalid NSP file table.");
var entries = new List<(long Offset, long Size, int Name)>();
for (var i = 0; i < count; i++)
{
    entries.Add((reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt32()));
    reader.ReadInt32();
}
var names = reader.ReadBytes(stringsSize);
var dataBase = file.Position;
using var storage = new LocalStorage(nspPath, FileAccess.Read);
foreach (var entry in entries.OrderByDescending(e => e.Size))
{
    if (entry.Name < 0 || entry.Name >= names.Length || entry.Offset < 0 || entry.Size < 0
        || entry.Offset > file.Length - dataBase || entry.Size > file.Length - dataBase - entry.Offset)
        throw new InvalidDataException("Invalid NSP entry.");
    var end = Array.IndexOf(names, (byte)0, entry.Name);
    if (end < 0) throw new InvalidDataException("Invalid NSP entry name.");
    var name = Encoding.UTF8.GetString(names, entry.Name, end - entry.Name);
    if (!name.EndsWith(".nca", StringComparison.Ordinal)) continue;
    using var slice = new SubStorage(storage, dataBase + entry.Offset, entry.Size);
    var nca = new Nca(keys, slice);
    if (nca.Header.ContentType != NcaContentType.Program) continue;
    if (nca.Header.TitleId != 0x0100770008DD8000)
        throw new InvalidDataException("This importer requires the MHGU base game (0100770008DD8000).");
    using var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
    string[] wanted =
    [
        "/nativeNX/eng/GUI/99_texture/HD_cmn_icon_GSM_NOMIP.tex",
        "/nativeNX/eng/GUI/99_texture/HD_load00_BM_NOMIP.tex",
        "/nativeNX/eng/GUI/99_texture/staffroll/HD_MHXXstaffroll_img04_BM_NOMIP.tex",
        "/nativeNX/table/itemData.itm",
    ];
    var selected = romfs.EnumerateEntries("/", "*", LibHac.Tools.FsSystem.SearchOptions.RecurseSubdirectories)
        .Where(e => e.Type == DirectoryEntryType.File && wanted.Contains(e.FullPath)).ToArray();
    if (selected.Length != wanted.Length)
        throw new InvalidDataException("The required English UI assets were not found in this game build.");
    foreach (var asset in selected)
    {
        if (asset.Size > 32 * 1024 * 1024) throw new InvalidDataException("Unexpected UI asset size.");
        var destination = Path.GetFullPath(Path.Combine(output, asset.FullPath.TrimStart('/')));
        if (!destination.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Asset path is outside the output directory.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var handle = new UniqueRef<IFile>();
        romfs.OpenFile(ref handle, new U8Span(asset.FullPath), OpenMode.Read).ThrowIfFailure();
        using var owned = handle;
        using var assetStorage = new FileStorage(handle.Get);
        var bytes = new byte[asset.Size];
        assetStorage.Read(0, bytes).ThrowIfFailure();
        File.WriteAllBytes(destination, bytes);
    }
    Console.WriteLine($"Extracted {selected.Length} UI files ({selected.Sum(e => e.Size) / 1048576.0:F1} MiB). Game and saves were opened read-only.");
    return 0;
}
Console.Error.WriteLine("No MHGU program content was found.");
return 1;
