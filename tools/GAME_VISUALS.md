# Local MHGU visuals

The imported asset pack uses the original common-icon atlas and cinematic/loading
artwork from the local Switch game. It is optional: editing remains available when
the asset pack has not been imported. No network requests happen while using the
editor, and icons are decoded once and cached as they are displayed.

The local build includes:

- 2,990 item-to-icon mappings from the game's `itemData.itm` table, including
  material IDs and their icon color indices.
- Original weapon-class, Palico weapon, and charm sprites from the local game.
- Distinct head, chest, arms, waist, legs, Palico head, and Palico body symbols from
  MHGU Database. These are bundled separately so correct armor icons also work
  without an imported game pack. They replace an incorrect Hunting Horn fallback.
  All equipment symbols identify categories rather than individual 3D models.
- A static hunter cinematic still on the profile page and village loading artwork
  on the empty screen. The hunter image does not represent the saved character.

## Native icon expansion (November 4, 2026)

The new local importer supplies native sprites for every item ID (2,990), all 24
equipment type codes, all 137 monster indices, and 15 UI categories. It replaces
the older community-palette colors with the game's actual icon palette. Native
armor symbols take priority over bundled community symbols; the latter remain a
fallback for clean source builds and older packs. Gear uses the category symbols
that the game itself uses, rather than individual equipment model thumbnails.

The sidebar has icons for all eight workspaces. Inner tabs keep their text headers
and show matching icons. Monster lists and inspectors, Palico choices, registered
sets and gear choices, jewel/charm pickers, quest rows, and individual collection
entries also display icons. Empty selections stay empty, and unknown IDs use small
original vector fallbacks. Missing optional artwork does not become an item glyph.

For the unpacked Recomp installation, run from the project root:

```powershell
uv run --with pillow --with numpy --with lz4 --with capstone python tools/export_game_icons.py "C:\path\to\MHGURecomp\data"
dotnet build -c Release
```

This reads `exefs/main`, `romfs/nativeNX/table/itemData.itm`, and three English icon
textures. It never executes the game or opens a save. `game_icon_tables.py` verifies
the NSO segment hashes, reads the native monster/equipment/extended-item mappings,
and evaluates immediate ARM32 arithmetic to recover the 17 icon colors. The
extractor validates all sprites before replacing the manifest. Original game data,
decoded executable inspections, and generated images remain private and ignored.
`Assets/Game/native-icons-provenance.json` records source hashes and crop mappings.

The monster atlases use 72-pixel cells, seven columns, 98 entries per sheet. The
save indices are not sequential atlas indices after the original monster block.
The importer reads the game's u32[137] table at virtual `0x162E7F8`. Equipment
symbols come from its u8[25] table at `0x162E7DC`. Item shapes 95–106 use special
coordinates from `0x162E1A0`; the regular grid formula is not valid for those shapes.
The color constructor is at `0x562060`. No reference-editor source or datasets
are imported; the mapping values come directly from the user's executable.

For asset-free verification, use `-p:IncludeLocalGameVisuals=false` when building
or testing. Original vector UI icons and bundled category symbols remain available.

The game supplies grayscale sprite masks plus color indices. The legacy exporter
below uses RGB values from [MHGU Database's color.xml](https://github.com/gatheringhallstudios/MHGenDatabase/blob/develop/app/src/main/res/values/color.xml).
The native importer above replaces those colors with the values recovered from the
user's executable. Silhouettes, mappings, and artwork come from the user's game.

## Rebuilding the local pack

The extractor requires .NET 9 and the existing game/emulator folder layout. The
converter requires Python with Pillow and NumPy. From the project directory:

```powershell
dotnet run --project tools/GameAssetExtractor -- "C:\Games\Monster Hunter Generations Ultimate" "artifacts/game-assets"
python tools/export_game_visuals.py
dotnet build -c Release
```

The extractor reads `data/games/MHGU.nsp` and the emulator's existing local key files.
It exports only four files, about 5 MB, into the chosen output directory. It never
prints or copies key contents and never opens the save files. The converter produces
`src/MhguSave.App/Assets/Game/manifest.json`, images, and `provenance.json`, including
the original texture's hash, source paths, crop coordinates, and mapping records.

The imported resource directory is excluded from source control. Avalonia embeds it
in local builds, so the published executable works offline and needs no separate
icon downloads. A clean checkout without imported art still builds and edits saves.

## Armor category symbols

The seven bundled fallback armor symbols come from [MHGU Database](https://github.com/gatheringhallstudios/MHGenDatabase),
commit `6ec3ce73f4f4def80f04413094fa0fc033787ee2`, directory
`app/src/main/icon-res/drawable`. Their original filenames are `icon_armor_head.png`,
`icon_armor_body.png`, `icon_armor_arms.png`, `icon_armor_waist.png`,
`icon_armor_legs.png`, `icon_palico_head.png`, and `icon_palico_body.png`.
These are community category symbols, separate from the extracted game sprites.
`src/MhguSave.App/Assets/Equipment/provenance.json` records each source path and
SHA-256; the MIT license is included beside the images. The runtime ignores stale
armor crops in older manifests, accepts the new importer's verified native mappings,
and falls back to these bundled symbols when native ones are unavailable.

## Format references and credits

- [MHGU-Modding / Poto itemData template](https://github.com/RTHKKona/MHGU-Modding/blob/main/templates/itemData_mhgu.bt):
  44-byte record layout; icon ID at byte 16 and palette index at byte 17.
- [SoupTextures](https://github.com/RTHKKona/SoupTextures): MT Framework TEX v160
  format reference. The converter implements the TEX reader and Tegra block-linear
  address calculation directly; it does not invoke or ship SoupTextures or texconv.
- [LibHac 0.19.0](https://www.nuget.org/packages/LibHac/0.19.0): read-only NSP/NCA/RomFS
  access for the developer extraction utility, not a runtime dependency of the editor.
- [MHGU Database](https://github.com/gatheringhallstudios/MHGenDatabase), MIT license:
  icon-palette RGB values. Its license is retained in `MHGU_DATABASE_LICENSE.txt`.

The local imported game art remains separate from the editor's source code and the
community tool licenses.

## Native rarity and socket metadata

`export_game_details.py` reads the local game's packed item, weapon, armor, talisman,
and Palico tables. It recovers the eleven rarity colors from constant ARM32
constructor instructions without executing the game. Run with the same `data`
directory used by the icon exporter:

```powershell
uv run --with lz4 --with capstone python tools/export_game_details.py "C:\path\to\MHGURecomp\data"
```

The private output is `src/MhguSave.Format/Data/Local/game-rarity.json`, with source
hashes. It is ignored by Git and omitted when `IncludeLocalGameCatalog=false`.
The same extraction reads armor sockets, weapon sockets at each displayed level,
and jewel costs. The ranged weapon layouts were cross-checked against
[native table format research](https://github.com/AlexQFMM2/mhgu-xx-se/blob/main/tools/export_game_names.py).
The app uses values extracted from the local dump.
Re-run `export_game_icons.py` to generate rarity-tinted equipment sprites and all
132 native Guild Card award icons. Item sprites retain their native item colors;
rarity appears in their names and labels. Unknown IDs use a neutral presentation.
The implementation and extraction are independent of the reference editor.
