# Known issues

The current build includes the corrected Rathalos S and other armor save-ID mappings.
The following issues were identified in the November 3, 2026 code review and remain
unfixed. Passing save/reload tests does not establish complete in-game correctness.

## Equipment storage and loadouts

- **Palico gear is routed to the hunter equipment box.** The game uses a separate
  Palico box. The current creation form writes types 22–24 into the hunter box and
  does not expose existing gear from the actual Palico box.
- **Clearing equipped or registered gear does not update its references.** A later
  addition can reuse that slot for a different equipment type while equipped-gear
  or My Set indices still point to it. Keep referenced equipment intact until
  reference checks or coherent reference updates are implemented.

## Equipment validation

- **Jewel capacity is not checked against each piece and upgrade level.** Current
  validation assumes three slots for non-talismans, allowing jewels on gear with
  fewer slots or no slots. Actual per-piece/per-level socket data is required.
- **Talisman rarity and charm origin are independent.** Choosing Creator Talisman
  leaves the default Mystery Charm origin. Defaults and allowed combinations need
  to follow the game's rarity-to-origin mapping.

## Verification

The last full regression run passed 131 tests with an offline real-save fixture.
Review checks reproduced the issues using in-memory copies; the original game save
was not written. Additional tests must verify destination offsets, equipped/set
references, socket capacities, and talisman defaults against game data.

Existing source references:

- [Equipment creation and clearing](../src/MhguSave.App/ViewModels/MainViewModel.cs)
- [Decoration and talisman validation](../src/MhguSave.App/ViewModels/MainViewModel.Equipment.cs)
- [Armor ID correction and provenance](../tools/ARMOR_CATALOG.md)

Other unimplemented areas include My Sets editing, Palico companion data, monster
tallies, and detailed Guild Card fields. See [REVIEW.md](../REVIEW.md) for earlier
implementation notes and verification limits.
