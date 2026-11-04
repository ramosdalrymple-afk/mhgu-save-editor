"""Build native MHGU icons from a user's unpacked data/romfs and data/exefs.

All executable access is read-only metadata extraction. No game code is executed.
The output is an optional private asset pack, excluded from source control.
"""
import argparse, hashlib, io, json, os
from pathlib import Path
from PIL import Image
from game_texture import decode_tex
from game_icon_tables import read_icon_tables, read_nso, read_palettes


def build(data_root: Path, output: Path):
    executable = data_root / 'exefs/main'
    romfs = data_root / 'romfs'
    textures = romfs / 'nativeNX/eng/GUI/99_texture'
    item_path = romfs / 'nativeNX/table/itemData.itm'
    monsters, equipment, extra, palette = read_icon_tables(executable)
    _, rarity_palette = read_palettes(read_nso(executable))
    common_path = textures / 'HD_cmn_icon_GSM_NOMIP.tex'
    monster_paths = [textures / f'HD_cmn_micon0{i}_BM_MQ_NOMIP.tex' for i in range(2)]
    common = decode_tex(common_path)
    sheets = [decode_tex(p) for p in monster_paths]
    if common.size != (512, 1024) or [s.size for s in sheets] != [(512, 1024), (512, 512)]:
        raise ValueError('Unexpected native icon atlas dimensions')
    items = item_path.read_bytes()
    if items[:4] != bytes.fromhex('6666aa41') or struct_count(items) != 2991 or len(items) != 8 + 2991 * 44:
        raise ValueError('Unsupported MHGU item table')

    files, manifest, item_records, monster_records, equipment_records = {}, {}, [], [], []
    def put(key, filename, picture):
        if not picture.getchannel('A').getbbox():
            raise ValueError(f'Empty sprite for {key}')
        stream = io.BytesIO(); picture.save(stream, format='PNG')
        files[filename] = stream.getvalue(); manifest[key] = filename

    def sprite(shape, color):
        if not 0 <= shape <= 106: raise ValueError(f'Unsupported item icon {shape}')
        x, y = (shape % 10 * 48, 416 + shape // 10 * 48) if shape < 95 else extra[shape - 95]
        tile = common.crop((x, y, x + 48, y + 48))
        channels = tile.split()
        tile = Image.merge('RGBA', tuple(channels[i].point(lambda n, c=color[i]: n * c // 255) for i in range(3)) + (channels[3],))
        return tile, [x, y, 48, 48]

    for item_id in range(1, 2991):
        at = 8 + item_id * 44
        stored_id = int.from_bytes(items[at:at + 4], 'little')
        shape, color_id = items[at + 16:at + 18]
        if stored_id != item_id or color_id >= len(palette): raise ValueError(f'Invalid item metadata at {item_id}')
        filename = f'native-item-{shape}-{color_id}.png'
        tile, rect = sprite(shape, palette[color_id])
        # Some unused game IDs use an intentionally blank sprite. Preserve the
        # mapping, but display the generic item glyph rather than a misleading icon.
        if not tile.getchannel('A').getbbox(): tile, rect = sprite(45, palette[color_id])
        put(f'item:{item_id}', filename, tile)
        item_records.append({'id': item_id, 'shape': shape, 'color': color_id, 'rect': rect})

    for type_id in range(1, 25):
        tile, rect = sprite(equipment[type_id], (241, 238, 229))
        put(f'equipment:{type_id}', f'native-equipment-{type_id}.png', tile)
        for rarity, color in enumerate(rarity_palette, 1):
            colored, _ = sprite(equipment[type_id], color)
            put(f'equipment:{type_id}:rarity:{rarity}', f'native-equipment-{type_id}-rare-{rarity}.png', colored)
        equipment_records.append({'type': type_id, 'shape': equipment[type_id], 'rect': rect})

    for index, icon in enumerate(monsters, 1):
        sheet, cell = divmod(icon, 98)
        x, y = cell % 7 * 72, cell // 7 * 72
        tile = sheets[sheet].crop((x, y, x + 72, y + 72))
        put(f'monster:{index}', f'native-monster-{index}.png', tile)
        monster_records.append({'index': index, 'icon': icon, 'sheet': sheet, 'rect': [x, y, 72, 72]})

    ui_shapes = {'profile': 70, 'inventory': 23, 'equipment': 55, 'quests': 36,
                 'collections': 84, 'loadouts': 92, 'palico': 85, 'monsters': 24,
                 'pouch': 22, 'edit': 35, 'add': 28, 'jewels': 44, 'arts': 28,
                 'awards': 84, 'appearance': 70}
    for name, shape in ui_shapes.items():
        tile, _ = sprite(shape, (230, 201, 144))
        put('ui:' + name, 'native-ui-' + name + '.png', tile)

    award_paths = [textures / f'HD_lby_deco{suffix}_BM_MQ_NOMIP.tex' for suffix in ('', '2')]
    awards = [decode_tex(path) for path in award_paths]
    for index in range(132):
        sheet, cell = (0, index) if index < 100 else (1, index - 100)
        x, y = cell % 10 * 48, cell // 10 * 48
        put(f'award:{index}', f'native-award-{index}.png', awards[sheet].crop((x, y, x+48, y+48)))

    output.mkdir(parents=True, exist_ok=True)
    manifest_path = output / 'manifest.json'
    previous = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {}
    previous.update(manifest)
    for name, content in files.items(): (output / name).write_bytes(content)
    provenance = {
        'description': 'Native sprites and metadata extracted from user-local MHGU files; no reference-editor datasets imported.',
        'sources': [{'path': str(p.relative_to(data_root)), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
                    for p in [executable, item_path, common_path] + monster_paths + award_paths],
        'tables': {'monsterIcons': 'NSO virtual 0x162E7F8, 137 x u32', 'equipmentIcons': '0x162E7DC, 25 x u8',
                   'extraItemCoordinates': '0x162E1A0, doubled game coordinates', 'itemPalette': 'ARM32 constructor 0x562060; constant arithmetic, no execution'},
        'paletteRGB': palette, 'rarityPaletteRGB': rarity_palette, 'items': item_records, 'equipment': equipment_records, 'monsters': monster_records,
        'uiShapes': ui_shapes,
    }
    (output / 'native-icons-provenance.json').write_text(json.dumps(provenance, indent=2) + '\n', encoding='utf-8')
    staging = output / 'manifest.json.tmp'
    staging.write_text(json.dumps(previous, indent=2) + '\n', encoding='utf-8')
    os.replace(staging, manifest_path)
    print(f'Native icons: 2990 items, 24 equipment types with 11 rarity tints, 137 monster indices, 132 awards, {len(ui_shapes)} UI symbols; {len(files)} PNG files.')


def struct_count(data):
    return int.from_bytes(data[4:8], 'little')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('data', type=Path, help='The Recomp data directory containing romfs/ and exefs/')
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'src/MhguSave.App/Assets/Game')
    args = parser.parse_args()
    build(args.data, args.output)
