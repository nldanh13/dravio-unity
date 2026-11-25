"""Tiny dependency-free PNG writer/reader (RGBA8) usable from Blender's Python.

Blender does not ship Pillow, so textures are written with zlib + struct only.
"""
import struct
import zlib


def _chunk(tag, data):
    body = tag + data
    return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)


def write_png(path, rgba):
    """rgba: numpy uint8 array (H, W, 4) or (H, W, 3), row 0 = top of image."""
    import numpy as np

    arr = np.ascontiguousarray(rgba, dtype=np.uint8)
    h, w, c = arr.shape
    color_type = 6 if c == 4 else 2
    raw = b"".join(b"\x00" + arr[y].tobytes() for y in range(h))
    png = b"\x89PNG\r\n\x1a\n"
    png += _chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, color_type, 0, 0, 0))
    png += _chunk(b"IDAT", zlib.compress(raw, 9))
    png += _chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def read_png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    return struct.unpack(">II", head[16:24])
