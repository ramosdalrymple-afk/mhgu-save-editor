# Technical reference

Save-writing behavior, binary layouts, and catalog provenance.

## Safety rules the code enforces

1. **The emulator must be closed.** It holds the save in memory and overwrites it on exit.
   `EmulatorGuard` refuses to write while Ryujinx/yuzu/Sudachi/Suyu/Citron runs.
2. **Both files are kept in sync.** Editing writes the body into `system` and into
   `system_backup`, keeping each file's own container header (the entry keys differ).
3. **Unique timestamped backups** (`system.bak-<timestamp>-<suffix>`) are written before any change.
4. **The container header is never rewritten.** It carries the entry key, size and type.
5. All existing sibling containers are validated and staged before either is replaced.
   Each replacement is atomic at the individual-file level. If the second fails, the
   first is restored from a recovery copy. This is not a filesystem transaction across
   both files: power loss can still require manual recovery from backups.
6. A save changed on disk since opening is rejected. Reopen it before editing further.
7. Rejected equipment edits do not partially mutate the in-memory save. Unknown flag
   bits and unrelated payload bytes are preserved.

Quest completion flags and the lifetime completion counter are edited separately:
the counter includes repeat hunts, so clearing flags does not recalculate it.
The embedded equipment level table supplies weapon limits; armor entries contain
placeholder levels and are not used to reject existing armor upgrades.

There is no checksum and no encryption, so a bad write is only detectable by playing the
game. Back up the folder yourself as well.

## Save format (verified against a real save)

All offsets absolute unless marked `base +`. Slot base comes from the pointer table, never
hard-coded.

| Offset | Field |
|---|---|
| `0x00`–`0x23` | `sSavedata` container header: 0, version 0, table size 4, entry offset `0x14`, entry key `0x14` (= JAMCRC of the file name), type 12 at `0x18`, blob size at `0x1C` |
| `0x24` + 0 | body version, `0xC6` |
| `0x28` + 0 | 3 × u8 slot in use; `0x2B` last slot |
| `0x2C` / `0x30` | block A / block B offsets (relative to `0x24`) |
| `0x34` + 0 | 3 × u32 slot offsets (relative to `0x24`), stride `0x11F8C4` |

Per character (`base +`):

| Offset | Field |
|---|---|
| `0x000` | hunter name, char[32] UTF-8 |
| `0x020` | play time, u32 seconds |
| `0x024` | zenny (slot-header copy, rebuilt by the game) |
| `0x028` | Hunter Rank, u16 (derived from HR points) |
| `0x278` | item box, 2300 slots, 19 bits each (12 id + 7 count, LSB first) |
| `0x27BF` | pouch, 32 slots, same packing |
| `0x280B` | HR points, u32 |
| `0x280F` | zenny, u32 (authoritative copy) |
| `0x2817` | Wycademy points, u32 |
| `0x2C13` | hunter arts bitfield (ids 1-70, 83-190) |
| `0x2C4DA` | village star, u16 (1-10) |
| `0x2C4DC` | hub star, u16 (0-13) |
| `0x2C77` | quests cleared bitfield, 1509 bits |
| `0x2D77` | quests seen bitfield |
| `0x2E77` | quests failed bitfield |
| `0x3187` | quest sets, 100 bits |
| `0x5E4E` | quest counter, u16 |
| `0x62EE` | equipment box, 2000 × 36 B |
| `0xC8115` | guild card awards, 132 bits |
| `0x22497` | items obtained bitfield (bit = item id 1-2990) |
| `0x23B7D` | hunter name (player record copy) |

Equipment record (36 bytes, `base + 0x62EE + i * 36`):

| Offset | Field |
|---|---|
| `+0x00` u16 | bits 0-4 type (1 head, 2 chest, 3 arms, 4 waist, 5 legs, 6 talisman, 7-21 weapons, 22-24 Palico), bits 5-9 level - 1, bits 10-14 transmog-source level |
| `+0x02` u16 | equipment id (armor series id, weapon-tree index or Palico record) |
| `+0x04` u16 | transmog appearance id (armor only) |
| `+0x06..` | 3 × u16 decoration ids |
| `+0x0C..` | talisman: skill 1/2 ids, points, slot count, charm item id 353-356 (+`0x12` u16) |
| `+0x0C..` | insect glaive: kinsect id, level - 1 |

Container entry keys: `JAMCRC("system") = 0x36B2EE74`, `JAMCRC("system_backup") = 0xD0819B92`.

## Provenance

Offsets were cross-checked against the published notes of the `Scrimas/MHGU-Save-editor`
project (GPL-3.0) and then re-verified byte by byte against a real save. No source code was
copied from it. The embedded CSV data files do come from community projects and are GPL
therefore:

- `Data/item-names.csv` — from `Gicotto/Cotto-MHGU-Editor` (`core/game_data.json`)
- `Data/quest-names.csv` — derived from `Scrimas/MHGU-Save-editor` `data/quest-index.csv`
- `Data/equip-names.csv`, `Data/equip-types.csv` — from `Gicotto/Cotto-MHGU-Editor`
  (`core/game_data.json`, weapon/armor name arrays and max levels). Armor uses the
  parallel save-ID arrays, not name-array positions; see [the mapping audit](../tools/ARMOR_CATALOG.md).
- `Data/skill-names.csv`, `Data/deco-names.csv` — from the same source (jewel and skill lists)

Anyone reusing those files must respect their GPL-3.0 licence.
