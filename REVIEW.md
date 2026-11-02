# Review and implementation notes

The subsequent visual pass adds an optional, local game-asset pack. It maps 2,990
item IDs to original game icon sprites, adds weapon-class/category icons and static
hunter/village artwork, and preserves icon updates when IDs change or slots are
cleared. The hunter scene is explicitly labeled as static. No character model is
rendered. See [the asset import notes](tools/GAME_VISUALS.md) for exact provenance,
format references, and rebuild commands.

Visual-pass validation: 80 tests passed against fresh synthetic and real-save
fixtures; all mappings and all 483 image files were validated. The native packaged
window was inspected for the hunter panel and colored item/equipment icons. The
save used by the preview is a copy. A second, separate test fixture was used after
the interactive preview copy had been edited; the source game save was not written.

Reviewed the format library, view models, existing tests, and Avalonia window. This
workspace has no Git metadata, so this is a review of the current source tree rather
than a commit comparison.

## Functional audit: November 2, 2026

The functional build fixes the draft-to-save path behind item quantity and equipment
editing failures. Save and Ctrl+S apply valid pending item-box, pouch, and equipment
fields before writing. Selection and filter changes preserve valid edits; invalid
model drafts retain the original selection. Typed out-of-range numbers stay visible
instead of reverting to an older valid value on blur. Add/Apply validate their own
forms, and Save also rejects invalid edited numeric fields on another page. An unused
creation form does not block unrelated edits.

Equipment now has separate Edit, Create, and Decorations panels with pinned actions.
Creation searches the entire named catalog instead of silently truncating results to
40 entries, excludes empty/dummy records, initializes kinsects, and supports configured
talisman skills, points, charm, and slot count. Named jewel pickers show slot sizes;
jewels can also be added directly to the item box. Armor creation preserves the
requested upgrade level. The maximum-level shortcut is restricted to verified weapon
tables. Selection-dependent actions are disabled until a record is selected.

Validation of this build: **115 passed, 0 failed, 0 skipped**, with an offline real-save
fixture configured. This includes 54 format checks and 61 application checks. Tests
create, save, and reload all 23 supported equipment categories. Headless Avalonia
window tests use real pointer and keyboard events for adding items by name, typing
quantities, editing both inventories, creating a configured talisman, adding and
socketing jewels, saving by button and Ctrl+S, profile changes, quest flags, and all
three unlock actions. They also reject invalid quantities, allow correction, and
reject invalid profile text after changing pages. Window screenshots are generated
at 1120 × 700 client size to check scrolling and action visibility.

Test-only internal dependency injection isolates process detection so the suite can
write temporary fixtures while the user's emulator remains running. The public writer
and production window continue to check actual emulator processes. Tests explicitly
verify rejection retains drafts and does not invoke the writer. No test writes the
original game save. This verifies UI/binary round trips; in-game validation remains
outstanding. The previous preview executables are preserved; the new output is
`artifacts/release/win-x64-functional/MhguSave.App.exe`, titled **Functional build**.

## Equipment icon and spacing refinement

The previous armor fallback cropped common-atlas item shape 63, a Hunting Horn,
for all five armor types. It is replaced by distinct bundled head, chest, arms,
waist, and legs symbols. Palico head/body also have distinct symbols. The source
and license are recorded in [the visual credits](tools/GAME_VISUALS.md). Weapons
and materials continue using the locally extracted game sprites. The equipment
list, creation catalog, type picker, and selected-piece header use the same type key.
The asset exporter no longer recreates the erroneous armor mappings, and the runtime
corrects stale manifests from older local packs.

The equipment editor now expands with the window up to 560 px, with a 24 px gutter.
Rows give names stronger emphasis and show type, slot, and level as secondary details.
The creation catalog separates names from IDs; a compact selected-piece/upgrade row
reduces unnecessary scrolling. Shared controls use consistent 40 px minimum heights,
20 px card padding, and clearer section spacing. A smaller character summary frees
vertical room; play time remains in the profile and character selector tooltip.
The editor action stays pinned while its form scrolls.

Validation: **117 tests passed**, including pixel-distinct armor symbols, matching
catalog/list/type-picker icons, pinned action bounds at 1120 × 700 and 1920 × 1008,
and the existing typing, add/apply/save, and all-tab regressions. The UI tests share a
nonparallel collection because Avalonia uses process-wide platform registration.
Screenshots for visual review are under `artifacts/spacing-pass`. The packaged output
is `artifacts/release/win-x64-refined-ui/MhguSave.App.exe`, titled **Refined UI**.

## Armor ID correction: November 3, 2026

The equipment CSV used name-array positions as armor save IDs, ignoring the
parallel ID arrays already present in the upstream data. For example, Rathalos
Mail S was offered as ID 401 (Ceanataur Mail R); its actual save ID is 446. This
also mislabeled existing equipment. Corrected all affected hunter and Palico
armor types: 6,862 entries checked, 6,444 IDs corrected. Other columns and equipment
types were verified unchanged. The importer preserves duplicate names as distinct
entries and rejects duplicate IDs or mismatched source lengths.

The source revision, JSON hash, importer instructions, fixed Rathalos S ID table,
and handling of existing saves are in [ARMOR_CATALOG.md](tools/ARMOR_CATALOG.md).
The earlier save/reload tests did not verify identity against independent expected
IDs. The new tests assert all ten Rathalos S names resolve to fixed IDs 446/447,
check literal serialized ID bytes, verify other rank/Palico anchors, and exercise
the visible picker/Add/Save flow for the five Blademaster parts.

Validation: **131 passed**, with the offline real-save fixture configured. An initial
parallel application-test run exhausted memory while cloning save fixtures; application
tests now use one worker, and the full suite passed. Original
save files were never written. In-game confirmation of the newly packaged build
remains outstanding. Old equipment is displayed by its actual stored ID, without
guessing intended replacements or migrating legitimate records. Output:
`artifacts/release/win-x64-armor-id-fix/MhguSave.App.exe`, titled **Armor IDs fixed**.

## Findings fixed

| Priority | Finding | Change |
| --- | --- | --- |
| High | `system` could be overwritten before an invalid or locked sibling was discovered. | Preflight both containers, flush staged writes, replace individual files, recover prior replacements on failure. |
| High | An external save update could be silently overwritten. | Compare the opened file's original SHA-256 before writing, and recheck both files after staging. |
| High | Talisman/kinsect validation could reject an edit after common fields had already changed in memory. | Serialize to a copied record and commit only on success. |
| High | Item-name CSV import reloaded the disk file and discarded edits. | Update existing row catalogs and search results in place. |
| Medium | Backups created during the same second overwrote previous backups. | Unique names and `CreateNew`; no overwrite. |
| Medium | Only the first occupied character was accessible. | Open the last played occupied character, allow switching, retain edits across slots, save together. |
| Medium | Quest flag commands replaced repeat-hunt totals with a distinct-quest count. | Preserve the separate lifetime counter; keep it manually editable. |
| Medium | Clear/apply actions depended on selection surviving collection resets. | Capture the target before updates and restore selection only while it remains visible. |
| Medium | Adding an Insect Glaive used kinsect level zero and failed validation. | Initialize a valid kinsect payload and expose kinsect fields. |
| Medium | Equipment writes cleared an unowned flag bit and a reserved byte. | Preserve those bytes during edits. |
| Medium | Invalid profile numbers could partially apply earlier fields before rejection. | Validate every field before mutating the character. |
| Medium | Opening a bad file cleared the current editing session. | Preserve the existing session on read/validation failure. |
| Medium | Character slots could overlap or wrap via corrupt pointers. | Check unsigned bounds and overlapping slot ranges. |
| Medium | App tests silently succeeded without exercising the view model when a real save was absent. | Use synthetic fixtures by default; explicitly skip the two real-save-only format checks when unconfigured. |
| Low | Reloading filter sources could leave dropdown selection blank. | Keep category/type catalogs stable and use a typed equipment picker. |
| Low | Feedback and summaries could become stale after edits. | Preserve save results, notify dependent row labels, show unsaved state and per-section feedback. |

## UI changes

The window uses a dark slate palette with a teal accent, persistent sidebar navigation,
clear page titles, a compact character summary, cards for related fields, and a
persistent save/status bar. Inventory, equipment, and quests separate browsing from
editing. Side panels scroll at smaller window sizes. Equipment creation supports name
search within a named type. Talisman skills, armor appearances, and kinsects appear
only for the matching equipment type. Unsaved edits are guarded on open, close, and
revert. Ctrl+S and the Save button share input validation.

## Validation

- Release solution build and tests, with synthetic fixtures and real-save copies.
  Initial review run: 78 passed, 0 failed, 0 skipped with `MHGU_TEST_SAVE` configured. The
  self-contained win-x64 executable was published and launched successfully using a
  save folder argument. The original `system` hash still matched the untouched
  verification fixture after testing.
- Byte-level tests for paired-file synchronization, header preservation, conflict
  detection, independent backups, malformed pointers, and partial-record rejection.
- Windows test locks the second file against replacement and verifies restoration of
  the first file, including when persistent backups are disabled.
- View-model tests cover character switching, persistence, failed loads, CSV import,
  quests, item clearing, equipment search, glaive creation, transmog, and kinsects.
- Native Windows window launched using a QA copy of the user's save. Inspected the
  empty state, profile, and equipment layout, including navigation and dropdown fixes.
  Confirmed the packaged build displays the All filter and Great Sword type selection.

## Remaining scope and limits

- Equipment sets, Palico companion records, monster tallies, and detailed Guild Card
  fields remain unimplemented. Palico **equipment** is supported in the equipment box.
- Storage bounds are validated; arbitrary IDs, skill combinations, decorations,
  transmog combinations, quest prerequisites, and per-item pouch limits are not a full
  game-legality validator. The embedded armor level values are placeholders.
- Armor appearance and kinsect changes have byte-level round-trip coverage, but have
  not been loaded in the game during this review. No claim of universal in-game
  correctness is made.
- Replacement/recovery covers ordinary I/O failures. A process or power failure
  between the two replacements can still require manual restoration from backups.
- Full game legality is not inferred from storage ranges. Jewel sizes are checked
  against talisman slots or the general three-slot ceiling; actual socket availability
  for every weapon/armor upgrade is not in the embedded catalog.
