# Hunter workspace theme

The visual direction follows the MHGU cover's dark armor, gold lettering, pale sky,
and restrained red/blue accents. The interface keeps ornament around the navigation,
profile banner, and welcome screen; editing controls use plain, readable surfaces.
The crest and navigation drawings are original vector geometry, with no required
cover-image download or extracted artwork.

The same crest is used for the executable, window/taskbar, desktop shortcuts, and
installer icon. Regenerate the bundled multi-resolution ICO after changing the
crest with `python tools/build-app-icon.py` (requires Pillow). The window title
credits the creator as **MHGU Save Editor by Dalrymple**.

## Palette and hierarchy

- Canvas: `#101920`; cards: `#1B252D`; input surfaces: `#111B23`.
- Main text: `#F1EEE5`; secondary text: `#B3BEC5`; metadata: `#9CAEB9`.
- Gold `#D8B976` marks primary actions and section captions. Blue `#8AC6D4`
  supports status/focus, with ember `#C86E55` limited to the crest and decoration.
- Page headings are 28 px, card headings 17 px, body text 14 px, supporting
  descriptions 13 px, and metadata 12 px. Tiny uppercase captions supplement
  larger labels rather than replacing them.
- Main columns use 24 px gutters. Cards use 24 px padding, with compact 18–20 px
  padding in inspectors. Inputs and buttons are at least 40 px tall.
- Selection uses a tinted surface and a gold edge in navigation. Clear-slot
  actions have a separate muted red treatment. Standard keyboard focus indicators
  and numeric validation remain available.

## Layout behavior

The desktop workspace supports widths from 1120 px. The window opens centered.
The save selector, storage totals, and Save action remain above page content, and
the save state stays in a separate bottom bar. Long file paths truncate with a tooltip.

Profile fields use two columns, with identity/progression on the left and
currencies/save guidance on the right. Inventories have Edit/Add tabs beside the
searchable list. Choosing a different inventory row opens its Edit panel. Inspector
fields scroll while Apply/Add actions remain pinned. Equipment retains its Edit,
Add gear, and Jewels panels. Quest lists and bulk actions occupy separate columns.
Collection actions appear as three equal cards.

The optional local game artwork and icons remain supported. Without that pack,
the native vector crest supplies the welcome/profile artwork. Public builds require
no original game assets.

## Verification

Headless window checks cover the welcome screen and all five pages at 1120×740,
1440×940, and 1920×1080. They check navigation, long paths, and pinned primary
actions. Existing interaction tests exercise numeric typing, Add/Apply/Save, invalid
input, all page navigation, and persistence using disposable save copies.
