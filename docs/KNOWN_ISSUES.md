# Known issues

The current build includes the corrected Rathalos S and other armor save-ID mappings.
The November 4 reference-comparison work fixes separate Palico storage, equipment
reference protection, talisman defaults, the Courier counter width, playtime copies,
and award-map synchronization. Passing save/reload tests does not establish complete
in-game correctness. The remaining parity work is tracked in [FEATURE_PARITY.md](FEATURE_PARITY.md).

## Equipment storage and loadouts

- **Older builds could put Palico gear in the hunter box.** New creation uses the
  separate 1,000-slot Palico box. Existing misplaced entries are not migrated: doing
  so requires resolving their intended references.
- **Referenced gear is protected.** Clearing or replacing an identity used by worn
  gear, My Sets, Palico sets, or companions is refused. Empty records with dangling
  references are also skipped when adding equipment. Level/deco edits keep identity.

## Equipment validation

- **Hunter gear jewel edits require the local socket tables.** Preview.5 checks
  actual armor sockets and weapon sockets at the selected upgrade level. Builds
  without the tables allow removing jewels and preserving existing setups, and
  require the tables to validate a new socket setup on hunter weapons or armor.
- **Talisman origin can still be overridden.** Creation now defaults rarity 1–2 to
  Mystery, 3–4 to Shining, 5–7 to Timeworn, and 8–10 to Enduring. Full skill-table and
  origin validation remains pending.

## Newly exposed records

- **Palico editing is partial.** Identity, level/experience, targets, and roles are
  editable. Forte, ability lists, and appearance are preserved until dependent rules
  can be implemented; empty companion slots cannot be created through identity fields.
- **Monster sizes are inspection-only.** Count editing changes live tallies. The
  game handles Guild Card log rebuilding; immediate offline Card synchronization,
  species-specific bounds, crown editing, and Notes still need implementation.
- **Loadout warnings are partial.** Hunter Art slot/class/unlock/family checks are
  warnings. Transmog compatibility and full game legality still need verification.

## Verification

Automated checks cover serialized edits and desktop workflows. Review checks
reproduced these issues using in-memory copies; the original game save was not
written. Additional tests must verify destination offsets, equipped/set
references, socket capacities, and talisman defaults against game data.

Existing source references:

- [Equipment creation and clearing](../src/MhguSave.App/ViewModels/MainViewModel.cs)
- [Decoration and talisman validation](../src/MhguSave.App/ViewModels/MainViewModel.Equipment.cs)
- [Armor ID correction and provenance](../tools/ARMOR_CATALOG.md)

Detailed Guild Card fields, item loadouts, requests, most unlock categories, and the
linked game database remain unimplemented. See [REVIEW.md](../REVIEW.md) for earlier
implementation notes and verification limits.
