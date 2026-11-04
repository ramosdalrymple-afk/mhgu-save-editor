"""Read-only MT Framework TEX v160 decoder used by the local icon exporters."""
from pathlib import Path
from PIL import Image
import io, struct
import numpy as np


def decode_tex(path: Path):
    data = path.read_bytes()
    if len(data) < 24 or data[:4] != b'TEX\0':
        raise ValueError(f'Not an MT Framework texture: {path}')
    _, header1, header2, header3, _ = struct.unpack_from('<5I', data)
    if header1 & 4095 != 160:
        raise ValueError('Unsupported texture version')
    width, height = (header2 >> 6) & 8191, (header2 >> 19) & 8191
    fmt, mips = (header3 >> 8) & 255, header2 & 63
    if not (1 <= width <= 4096 and 1 <= height <= 4096) or not mips or fmt not in (7, 19, 23, 25, 31):
        raise ValueError('Unsupported texture layout')
    offset = 20 + 4 * mips + struct.unpack_from('<I', data, 20)[0]
    block, bytes_per_block = (1, 4) if fmt == 7 else (4, 8 if fmt in (19, 25) else 16)
    rows = (height + block - 1) // block
    row_bytes = ((width + block - 1) // block) * bytes_per_block
    block_height = 16
    while block_height > 1 and rows <= 8 * (block_height // 2):
        block_height //= 2
    y, x = np.indices((rows, row_bytes))
    address = ((y // (8 * block_height)) * 512 * block_height * ((row_bytes + 63) // 64)
               + (x // 64) * 512 * block_height + (y % (8 * block_height) // 8) * 512
               + (x % 64 // 32) * 256 + (y % 8 // 2) * 64 + (x % 32 // 16) * 32
               + (y % 2) * 16 + x % 16)
    if offset + int(address.max()) >= len(data):
        raise ValueError('Truncated texture')
    raw = np.frombuffer(data[offset:], dtype=np.uint8)[address].tobytes()
    if fmt == 7:
        return Image.frombytes('RGBA', (width, height), raw, 'raw', 'BGRA')
    dds = bytearray(128)
    struct.pack_into('<7I', dds, 0, 0x20534444, 124, 0x81007, height, width, len(raw), 0)
    struct.pack_into('<II4s', dds, 76, 32, 4, {19: b'DXT1', 23: b'DXT5', 25: b'BC4U', 31: b'BC5U'}[fmt])
    struct.pack_into('<I', dds, 108, 0x1000)
    return Image.open(io.BytesIO(dds + raw)).convert('RGBA')
