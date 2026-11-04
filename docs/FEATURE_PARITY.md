# Reference editor audit and implementation tracker

Compared on November 4, 2026 against [Scrimas/MHGU-Save-editor](https://github.com/Scrimas/MHGU-Save-editor),
revision `44bf9e998c20d8be96595e561c1915d8ee2911c1`. The read-only checkout is under
`artifacts/reference/` and is excluded from Git and release exports.

## Implementation boundary

The user requested independent implementations and discussion before reusing more
reference datasets. New code is written for this application's C#/Avalonia architecture.
No reference source, screenshots, or additional reference CSV/JSON datasets are imported.
Reference format observations are attributed; they are evidence to investigate, not
in-game verification of this application. Existing community catalogs remain credited
in `REFERENCE.md`; this work does not relicense the project or replace that provenance.

An unpacked local game has `data/romfs/nativeNX` and `data/exefs`. Its resources can be read
without exporting the game archives or key files. Generated game resources remain
optional, local, and excluded from Git and release exports.

## Coverage

This is the first implementation wave. Full reference-feature parity is **not yet complete**.

“Existing” means implemented before this audit, and does not imply full parity.

| Area | Existing coverage | Missing/reference capabilities | Implementation status |
| --- | --- | --- | --- |
| File safety | Paired writes, rollback, backups, conflict and emulator detection | Review by change, undo, reload/rebase, snapshot browser/restore/compare | Existing safety retained; UI tools pending |
| Characters | Select any occupied slot; retain edits across slots | Copy, swap, delete, export/import, MHXX transfers | Pending |
| Profile | Name, currencies, HR points, Village/Hub stars | Appearance, playtime editing, derived HR, village points, starting village, housekeeper, Courier | Appearance and playtime implemented; Courier counter corrected to u32; other fields pending |
| Items | Named box/pouch edits, add, clear | Loadouts, sort/merge, quantity shortcuts, CSV exchange, carry-limit warnings | Box sort, lossless merge, and CSV import/export implemented; other tools pending |
| Equipment storage | Hunter box UI, all equipment catalogs | Separate Palico box; protect worn and set-referenced slots | Implemented in this change; no automatic migration of older misplaced gear |
| Equipment detail | Levels, jewels, transmog, talismans, basic kinsect | Charm-table warnings, class/body transmog checks, CSV/Athena exchange | Native socket limits by piece and weapon level, rarity-based charm defaults, and kinsect ID default implemented |
| Equipment sets | None | 40 My Sets, 24 Palico sets; gear, decoration copies, name, pigment, arts/SP/style, register worn gear | Implemented, including optional locally sourced art names; reorder pending |
| Palicoes | Equipment catalog only | 84 companions, 24 hireables; identity, growth, moves/skills, looks, roles, Scout, transfer; Dojo/team inspection | Existing companion/hireable identity and growth editing, role selection, ability-ID inspection implemented; other fields pending |
| Quests | Clear/seen/failed flags, filtered and bulk commands | Unlock explanations and coupled quest-set/Card counters; Arena records; history | Pending |
| Requests | None | Accepted/reported request states and posting dependencies | Pending |
| Collections | Bulk material, award, and art flags | Individual entries, Canteen, Deviant permits/levels/points | Individual material/art/award flags implemented; awards synchronize both maps; Canteen/Deviants pending |
| Unlocks | None beyond those bulk flags | Lab/supply links, pets/costumes, songs, combinations, Gallery, tips, milestones | Pending |
| Monsters | None | Hunts/captures, size bounds/crowns, Notes, coupled Guild Card log | Named index browser, slain/captured editing, read-only sizes implemented; game handles Card log rebuilding; crown/Notes editing and immediate Card synchronization pending |
| Guild Card | Bulk awards | Own title/scene/pose/greeting, usage counters; stored cards/inbox/block list | Pending |
| Options | None | Character and global options, multiplayer, chat groups and shoutouts | Pending |
| Database | Search catalogs inside editors | Linked monster/quest/item/skill/equipment detail, rewards/carves/recipes/upgrade trees | Pending; local game extraction preferred |
| Save map | None | Named read-only byte ranges and fields | Pending; reference map datasets require discussion before reuse |
| Application | Windows packaging, fixed theme | Discovery/recent saves, user settings, localization, sizing, update checking, additional shortcuts | Pending |

## Findings requiring follow-through

- Existing Palico gear in the hunter box is not moved automatically. The intended
  destination cannot be inferred without checking companion and loadout references.
- Equipped indices are at character `+0x23B39`; My Set records begin at `+0x208CE`,
  **not** `+0x208C8`. The earlier origin includes the previous record's last six bytes.
- Hunter box `+0x62EE`: 2,000 × 36 bytes. Palico box `+0x17C2E`: 1,000 × 36 bytes.
- Removal/replacement must protect both direct worn references and registered sets;
  Palico gear also has references in companion and hireable records.
- The Courier quest counter is u32, while the previous app exposed only u16 (fixed).
- The loaded playtime is at `+0x2248B`, with header and Guild Card summary copies (fixed).
- Awards have both a game map (`+0x3157`) and Card map (`+0xC8115`) (fixed).
- Existing tests that write/reload a piece only establish serialization. Fixed-offset,
  neighbor-preservation, incompatible-gear, and reference tests are needed independently.

## Validation policy

Protect storage ranges and references. Preserve unknown bytes and unchanged unknown
values. Warn where a game rule is known; do not label an unverified feature safe just
because it round-trips. Tests write synthetic saves or temporary copies. Game saves
and extracted game resources are never added to source control. Actual in-game checks
remain a separate verification step.

## Reproducing optional local labels

With Node.js installed, run:

```powershell
node tools/import-local-catalogs.mjs "C:\path\to\MHGURecomp\data\romfs"
dotnet build -c Release
```

The original importer validates GMD headers, string counts, UTF-8, and monster-table
anchors. It reads only three English message tables and records their SHA-256 hashes
under `src/MhguSave.Format/Data/Local/`. Those files are ignored by Git. No reference
CSV/JSON or source implementation is used. The raw monster-name table can share names
between variants; the browser always shows the save index to distinguish them.

`dotnet test -c Release -p:IncludeLocalGameCatalog=false` exercises the ID-label fallback.
