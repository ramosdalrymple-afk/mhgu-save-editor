"""Rasterize the app's existing vector crest into a multi-resolution Windows icon.

Requires Pillow. Run from any directory with: python tools/build-app-icon.py
GuildEmblem.axaml is the source of truth for both the UI logo and executable icon.
"""

from io import BytesIO
from pathlib import Path
import re
import struct
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src/MhguSave.App/Views/GuildEmblem.axaml"
OUTPUT = ROOT / "src/MhguSave.App/Assets/app-icon.ico"
SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def render(size):
    # Supersample the original M/L/Z geometry; retain transparency around the crest.
    supersampling = 8
    image = Image.new("RGBA", (size * supersampling,) * 2)
    draw = ImageDraw.Draw(image)
    scale = size * supersampling / 224
    for path in ET.parse(SOURCE).iter("{https://github.com/avaloniaui}Path"):
        tokens = re.findall(r"[MLZ]|-?\d+(?:\.\d+)?", path.attrib["Data"])
        contours, points, closed, i = [], [], False, 0
        while i < len(tokens):
            token = tokens[i]
            if token == "M":
                if points:
                    contours.append((points, closed))
                points, closed = [], False
                i += 1
            elif token == "L":
                i += 1
            elif token == "Z":
                closed = True
                i += 1
            else:
                points.append(((float(tokens[i]) + 12) * scale, (float(tokens[i + 1]) + 12) * scale))
                i += 2
        if points:
            contours.append((points, closed))
        for points, closed in contours:
            if path.get("Fill"):
                draw.polygon(points, fill=path.get("Fill"))
            if path.get("Stroke"):
                width = max(1, round(float(path.get("StrokeThickness", 1)) * scale))
                draw.line(points + ([points[0]] if closed else []), fill=path.get("Stroke"), width=width, joint="curve")
    return image.resize((size, size), Image.Resampling.LANCZOS)


def main():
    frames = []
    for size in SIZES:
        buffer = BytesIO()
        render(size).save(buffer, format="PNG")
        frames.append(buffer.getvalue())
    # ICO entries contain lossless PNG frames, supported by Windows and Avalonia.
    offset = 6 + 16 * len(SIZES)
    data = bytearray(struct.pack("<HHH", 0, 1, len(SIZES)))
    for size, frame in zip(SIZES, frames):
        data.extend(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(frame), offset))
        offset += len(frame)
    OUTPUT.write_bytes(data + b"".join(frames))
    render(256).save(OUTPUT.with_name("app-logo.png"))
    print(f"Created {OUTPUT.name}: {len(frames)} sizes ({', '.join(map(str, SIZES))})")


if __name__ == "__main__":
    main()
