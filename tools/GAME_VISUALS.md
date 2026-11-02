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

The game supplies grayscale sprite masks plus color indices. The RGB palette comes
from [MHGU Database's color.xml](https://github.com/gatheringhallstudios/MHGenDatabase/blob/develop/app/src/main/res/values/color.xml).
The silhouettes, item mapping, and artwork are extracted from the user's game.

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

The seven bundled armor symbols come from [MHGU Database](https://github.com/gatheringhallstudios/MHGenDatabase),
commit `6ec3ce73f4f4def80f04413094fa0fc033787ee2`, directory
`app/src/main/icon-res/drawable`. Their original filenames are `icon_armor_head.png`,
`icon_armor_body.png`, `icon_armor_arms.png`, `icon_armor_waist.png`,
`icon_armor_legs.png`, `icon_palico_head.png`, and `icon_palico_body.png`.
These are community category symbols, separate from the extracted game sprites.
`src/MhguSave.App/Assets/Equipment/provenance.json` records each source path and
SHA-256; the MIT license is included beside the images. The runtime overrides any
stale armor entries in an older optional game manifest. Rebuilding that manifest
now omits the erroneous armor mappings.

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
