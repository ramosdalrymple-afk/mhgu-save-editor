<p align="center">
  <img src="src/MhguSave.App/Assets/app-logo.png" alt="MHGU Save Editor crest" width="96" height="96">
</p>

# MHGU Save Editor

**A desktop save editor for Monster Hunter Generations Ultimate, by Dalrymple.**

Manage your hunter profile, inventory, equipment, and quest progress in a focused
Windows workspace. Built for Nintendo Switch saves, with searchable catalogs,
character switching, and automatic backups.

## Download

**[Early Access · v0.2.0-preview.5](https://github.com/ramosdalrymple-afk/mhgu-save-editor/releases/tag/v0.2.0-preview.5)**

| Package | Best for |
| --- | --- |
| [Windows installer](https://github.com/ramosdalrymple-afk/mhgu-save-editor/releases/download/v0.2.0-preview.5/MHGU-Save-Editor-0.2.0-preview.5-win-x64-Setup.exe) | Install for your Windows user with Start menu and optional desktop shortcuts. |
| [Portable ZIP](https://github.com/ramosdalrymple-afk/mhgu-save-editor/releases/download/v0.2.0-preview.5/MHGU-Save-Editor-0.2.0-preview.5-win-x64-Portable.zip) | Extract the complete archive and run `MhguSave.App.exe`. |
| [SHA-256 checksums](https://github.com/ramosdalrymple-afk/mhgu-save-editor/releases/download/v0.2.0-preview.5/SHA256SUMS.txt) | Verify the downloaded installer or ZIP. |

Requires **Windows 10 or later, x64**. Both packages include the .NET runtime;
no separate runtime installation is needed. Release notes accompany each build.

## Features

- **Hunter profile** — edit your hunter name, currencies, HR points, and village/hub progression.
- **Item inventory** — search, add, edit, and clear item-box and pouch stacks.
- **Equipment workspace** — browse named gear, create weapons, armor, and talismans, and edit supported equipment fields.
- **Decorations and talismans** — named jewel and skill pickers, talisman configuration, and jewel inventory creation.
- **Quest journal** — search quests, manage completion flags, and apply filtered or bulk updates.
- **Collections** — manage material knowledge, Guild Card awards, and Hunter Art unlocks.
- **Multiple characters** — switch between occupied slots and save their changes together.
- **Registered sets** — edit 40 hunter My Sets and 24 Palico equipment sets, with named gear, style, arts, SP flags, and armor pigment.
- **Palico board** — edit existing companions and hireables, then select your Prowler and hunting buddies.
- **Monster journal** — edit slain/captured counts and inspect size records for all 137 indices.
- **Appearance and collections** — edit character appearance and playtime; toggle individual Arts, awards, and obtained items.
- **Inventory tools** — sort and merge box stacks, or export/import a box CSV from the **Box tools** menu.
- **Save protection** — timestamped backups, paired save writing, external-change detection, and a guard for recognized emulator processes.

The cover-inspired interface combines dark steel, gold, and pale blue with clear
section headings, contextual editors, and action buttons that stay visible while
fields scroll. The crest is used throughout the app and Windows packaging.

## Getting started

1. Close the game or emulator and copy your save folder to a separate working folder.
2. Launch the editor and select **Open save** (`Ctrl+O`).
3. Choose `system` or `system_backup` for the Nintendo Switch title `0100770008DD8000`.
4. Select your hunter, then browse the workspace and make your changes.
5. Select **Save changes** (`Ctrl+S`) to write all edited characters.

Profile edits are staged as you type. **Apply** stages an item or equipment record
without writing to disk; **Save changes** also applies valid pending record edits.
**Revert changes** lets you save, discard, or cancel before reloading the file.
Existing save files receive timestamped backups before they are replaced.

## Build from source

Install the **.NET 9 SDK**, then run:

```powershell
git clone https://github.com/ramosdalrymple-afk/mhgu-save-editor.git
cd mhgu-save-editor
dotnet build -c Release
dotnet run --project src/MhguSave.App -c Release
```

Open a save directly from the command line:

```powershell
dotnet run --project src/MhguSave.App -c Release -- "C:\path\to\system"
```

Run the automated checks:

```powershell
dotnet test -c Release
```

Two optional integration tests use `MHGU_TEST_SAVE` when it points to an offline
`system` fixture. All writes in those tests target temporary copies.

To produce the Windows installer, portable ZIP, notices, and checksums from a
clean commit, install Inno Setup and run:

```powershell
./tools/build-release.ps1 -Version 0.2.0-preview.5 -InnoCompiler "C:\path\to\Inno Setup\ISCC.exe"
```

The release builder exports tracked source before packaging. Private saves,
keys, game archives, and locally imported game artwork are excluded.

## Project structure

| Directory | Purpose |
| --- | --- |
| `src/MhguSave.App` | Avalonia desktop UI and view models. |
| `src/MhguSave.Format` | Save parsing, editing, serialization, and catalogs. |
| `tests` | Format, workflow, and desktop layout checks. |
| `packaging` | Windows installer, release notes, and bundled notices. |
| `tools` | Release builds, catalog imports, and optional asset tooling. |

## Documentation

- [Interface design](docs/UI_DESIGN.md)
- [Technical reference](docs/REFERENCE.md)
- [Reference comparison and remaining feature work](docs/FEATURE_PARITY.md)
- [Armor catalog mapping](tools/ARMOR_CATALOG.md)
- [Optional local game visuals](tools/GAME_VISUALS.md)
- [Third-party notices](packaging/THIRD-PARTY-NOTICES.md)

## Credits

Created by **Dalrymple**. Built with .NET and Avalonia.

Community catalog data and category icons retain their respective licenses and
notices. Public builds include the original crest and community category symbols;
original game artwork and locally extracted sprites are optional local resources.

Monster Hunter Generations Ultimate and related game assets belong to their
respective owners. This is an independent community project.
