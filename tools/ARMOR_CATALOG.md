# Armor catalog save IDs

Armor names are stored in display-order arrays. Their positions are **not** the
equipment IDs stored in saves. Hunter armor uses the parallel `EquipHeadIDs`,
`EquipChestIDs`, `EquipArmsIDs`, `EquipWaistIDs`, and `EquipLegsIDs` arrays. Palico
head/body use `PalicoHeadIDs` and `PalicoArmorIDs`. Weapon classes, talismans, and
Palico weapons use direct indices into their respective name arrays.

The original CSV generation ignored those seven ID arrays. The November 3, 2026
fix checked all 6,862 armor entries and corrected 6,444 IDs. Names, level limits,
weapon rows, talisman rows, and Palico weapon rows were preserved.

## Source and reproduction

Source: [Cotto-MHGU-Editor core/game_data.json](https://github.com/Gicotto/Cotto-MHGU-Editor/blob/3e2f9ba421eb4c811003d418f3d489be7bf64bcb/core/game_data.json),
commit `3e2f9ba421eb4c811003d418f3d489be7bf64bcb`.
Source JSON SHA-256: `1963b1e9a1a1f2868bb594ef60eb2178bfc08b6c0f3e6e47b13bd8b3a1283d0b`.
Its [lookup implementation](https://github.com/Gicotto/Cotto-MHGU-Editor/blob/3e2f9ba421eb4c811003d418f3d489be7bf64bcb/core/src/game_data.rs)
also resolves armor IDs through the parallel arrays. This is the same upstream
data already credited in the project README; no upstream implementation was copied.

Download the pinned JSON, then run:

```powershell
python tools/remap_armor_catalog.py C:\path\to\game_data.json
python tools/remap_armor_catalog.py C:\path\to\game_data.json --check
```

The importer validates array lengths, integer ranges, unique save IDs, positional
name agreement, and unique `(type, id)` keys before writing. It retains positional
pairing because some armor names repeat within a body part; a name-only join would
lose those distinct entries. `--check` never writes the CSV or any save.

## Regression anchors

| Part | Blademaster name / ID | Gunner name / ID |
| --- | --- | --- |
| Head | Rathalos Helm S / 446 | Rathalos Cap S / 447 |
| Chest | Rathalos Mail S / 446 | Rathalos Vest S / 447 |
| Arms | Rathalos Braces S / 446 | Rathalos Guards S / 447 |
| Waist | Rathalos Faulds S / 446 | Rathalos Coat S / 447 |
| Legs | Rathalos Greaves S / 446 | Rathalos Leggings S / 447 |

The former list-position ID for Rathalos Mail S was 401, which actually identifies
Ceanataur Mail R. Other fixed anchors include Rathalos Helm (150), Rathalos Helm X
(916), and both Palico Rathalos X armor parts (385).

Tests verify named search against these fixed IDs, creation and save/reload of all
ten Rathalos S pieces, the literal little-endian ID word at record offset `+2`, and
the visible picker/Add/Save flow for all five Blademaster parts. These checks go
beyond a parser/writer round trip using the same catalog. In-game confirmation of
the newly packaged build remains separate from automated verification.

## Existing saves

The corrected catalog displays existing records according to their actual stored
ID. It does not rewrite records on load or guess which pieces were added with an
older catalog: those IDs can also represent legitimate owned equipment. Remove a
mistakenly added record and add the intended piece again with the fixed build.
