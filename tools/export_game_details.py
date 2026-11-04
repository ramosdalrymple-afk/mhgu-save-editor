"""Read rarity and socket metadata from the user's unpacked MHGU tables (private output)."""
import argparse, hashlib, json, struct
from pathlib import Path
from game_icon_tables import read_nso, read_palettes


def build(root, output):
    table = root / 'romfs/nativeNX/table'
    sources = []
    def records(name, stride=None, indexed=True):
        path = table / name
        raw = path.read_bytes(); sources.append(path)
        count = struct.unpack_from('<I', raw, 4)[0]
        size, remainder = divmod(len(raw) - 8, count)
        if remainder or (stride is not None and size != stride):
            raise ValueError(f'Unsupported table layout: {name}')
        rows = [raw[8+i*size:8+(i+1)*size] for i in range(count)]
        if indexed and any(struct.unpack_from('<I', row)[0] != i for i, row in enumerate(rows)):
            raise ValueError(f'Unexpected record IDs: {name}')
        return rows

    items = {str(i): row[5]+1 for i, row in enumerate(records('itemData.itm', 44)) if i}
    gear = {}
    armor_sockets, weapon_sockets = {}, {}
    level_widths = (15, 15, 15, 15, 100, 0, 100, 15, 17, 16, 31, 17, 16, 15, 17)
    for w in range(15):
        if w == 5: continue
        base_rows = records(f'weapon{w:02}BaseData.w{w:02}d')
        for i, row in enumerate(base_rows):
            if i: gear[f'{w+7}:{i}'] = row[11]+1
        # Ranged records place sockets before the ammunition/coating block.
        level_rows = records(f'weapon{w:02}LevelData.w{w:02}d', level_widths[w])
        slot_offset = {4: 10, 6: 10, 10: 12}.get(w, level_widths[w] - 1)
        for row in level_rows[1:]:
            weapon, level, slots = row[4], row[5], row[slot_offset]
            key = f'{w+7}:{weapon}:{level}'
            if not 0 < weapon < len(base_rows) or not 1 <= level <= 32 or slots > 3 or key in weapon_sockets:
                raise ValueError(f'Invalid weapon socket record: {key}')
            weapon_sockets[key] = slots
    for i, row in enumerate(records('armorSeriesData.asd', 127)):
        if i:
            for part in range(5):
                if row[6+part]:
                    key = f'{part+1}:{i}'
                    gear[key] = row[103]+1
                    armor_sockets[key] = row[108+part]
    decoration_costs = {str(2638+i): row[0] for i, row in enumerate(records('decoData.deco', 5, indexed=False))}
    if any(slots > 3 for slots in armor_sockets.values()) or any(not 1 <= cost <= 3 for cost in decoration_costs.values()):
        raise ValueError('Invalid armor socket count or jewel cost')
    for i, row in enumerate(records('amuletData.amlt', 9)):
        if i: gear[f'6:{i}'] = row[8]+1
    # Packed Palico records: rarity follows the attack/element fields in weapons,
    # and the two model identifiers in armor. Both store zero-based mRare.
    for i, row in enumerate(records('otWeaponData.owp', 31)):
        if i: gear[f'22:{i}'] = row[22]+1
    for i, row in enumerate(records('otArmorData.oar', 33)):
        if i:
            gear[f'23:{i}'] = gear[f'24:{i}'] = row[7]+1
    if any(not 1 <= rarity <= 11 for rarity in [*items.values(), *gear.values()]):
        raise ValueError('Rarity outside the native palette')
    executable = root / 'exefs/main'; sources.append(executable)
    _, palette = read_palettes(read_nso(executable))
    result = {'items': items, 'equipment': gear, 'armorSockets': armor_sockets,
              'weaponSockets': weapon_sockets, 'decorationCosts': decoration_costs,
              'colors': ['#' + ''.join(f'{v:02X}' for v in rgb) for rgb in palette],
              'sources': [{'path':str(p.relative_to(root)), 'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sources]}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, separators=(',', ':'))+'\n', encoding='utf-8')
    print(f'Extracted rarities: {len(items)} items, {len(gear)} equipment records, {len(palette)} native colors')
    print(f'Extracted sockets: {len(armor_sockets)} armor pieces, {len(weapon_sockets)} weapon levels, {len(decoration_costs)} jewel costs')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('data', type=Path)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'src/MhguSave.Format/Data/Local/game-rarity.json')
    args = parser.parse_args(); build(args.data, args.output)
