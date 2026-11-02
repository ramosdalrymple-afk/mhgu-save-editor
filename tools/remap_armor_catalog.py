"""Rebuild armor save IDs using the parallel ID arrays, never name-array positions.

Input: Cotto-MHGU-Editor core/game_data.json. See ARMOR_CATALOG.md for provenance.
Weapons and talismans already use direct array indexes and remain byte-identical.
"""
import argparse
import csv
import io
import json
from pathlib import Path

ARRAYS = {
    1: "EquipHead", 2: "EquipChest", 3: "EquipArms", 4: "EquipWaist", 5: "EquipLegs",
    23: "PalicoHead", 24: "PalicoArmor",
}


def remap(source, records):
    counts = {}
    for equipment_type, prefix in ARRAYS.items():
        names, ids = source[prefix + "Names"], source[prefix + "IDs"]
        group = [row for row in records if int(row["type"]) == equipment_type]
        if len(names) != len(ids) or len(group) != len(ids):
            raise ValueError(f"{prefix}: mismatched name/ID/catalog lengths")
        if len(set(ids)) != len(ids) or any(type(i) is not int or not 0 <= i <= 65535 for i in ids):
            raise ValueError(f"{prefix}: duplicate or invalid save IDs")
        changed = 0
        # Preserve positional pairing: names can be duplicated within an armor part.
        for row, name, save_id in zip(group, names, ids, strict=True):
            if row["name"] != name:
                raise ValueError(f"{prefix}: source names differ; review the source revision")
            changed += int(row["id"]) != save_id
            row["id"] = str(save_id)
        counts[equipment_type] = changed
    keys = [(row["type"], row["id"]) for row in records]
    if len(set(keys)) != len(keys):
        raise ValueError("Duplicate (type, save ID) in the resulting catalog")
    return counts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--catalog", type=Path, default=Path(__file__).resolve().parents[1] / "src/MhguSave.Format/Data/equip-names.csv")
    parser.add_argument("--check", action="store_true", help="Verify without writing")
    args = parser.parse_args()
    source = json.loads(args.source.read_text(encoding="utf-8"))
    original = args.catalog.read_text(encoding="utf-8")
    reader = csv.DictReader(io.StringIO(original))
    fields = reader.fieldnames
    rows = list(reader)
    counts = remap(source, rows)
    if args.check:
        if any(counts.values()):
            raise SystemExit(f"Incorrect armor IDs: {counts}")
    else:
        output = io.StringIO(newline="")
        writer = csv.DictWriter(output, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
        args.catalog.write_text(output.getvalue(), encoding="utf-8", newline="")
    print(f"Checked {sum(1 for r in rows if int(r['type']) in ARRAYS)} armor mappings; corrected {sum(counts.values())}: {counts}")


if __name__ == "__main__":
    main()
