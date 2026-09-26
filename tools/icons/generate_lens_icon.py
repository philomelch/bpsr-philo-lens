"""Generates src/Stellar.PhiloLens/Icons/lens.png: the profile-card button icon.

A magnifier with a small four-point sparkle inside (so it differs from Entity Inspector's plain
magnifier). White on transparent, because the framework tints the icon to match the game's own
action buttons. Standard library only; anti-aliased by 4x4 supersampling.

Usage: python tools/icons/generate_lens_icon.py
"""
import math
import pathlib
import struct
import zlib

SIZE = 128
SUPERSAMPLE = 4
LENS_CENTER = (50.0, 50.0)
LENS_OUTER_RADIUS = 40.0
LENS_RING_WIDTH = 11.0
HANDLE_START = (80.0, 80.0)
HANDLE_END = (119.0, 119.0)
HANDLE_HALF_WIDTH = 8.0
SPARKLE_RADIUS = 21.0
SPARKLE_WAIST = 0.62  # how pinched the star's sides are (0 = lines, 1 = diamond)
OUTPUT = pathlib.Path(__file__).resolve().parents[2] / "src/Stellar.PhiloLens/Icons/lens.png"


def in_ring(x, y):
    distance = math.hypot(x - LENS_CENTER[0], y - LENS_CENTER[1])
    return LENS_OUTER_RADIUS - LENS_RING_WIDTH <= distance <= LENS_OUTER_RADIUS


def in_handle(x, y):
    (ax, ay), (bx, by) = HANDLE_START, HANDLE_END
    length_sq = (bx - ax) ** 2 + (by - ay) ** 2
    t = max(0.0, min(1.0, ((x - ax) * (bx - ax) + (y - ay) * (by - ay)) / length_sq))
    return math.hypot(x - (ax + t * (bx - ax)), y - (ay + t * (by - ay))) <= HANDLE_HALF_WIDTH


def in_sparkle(x, y):
    # A four-point star: |dx|^w + |dy|^w <= r^w with w < 1 pinches a diamond into a star.
    dx = abs(x - LENS_CENTER[0]) / SPARKLE_RADIUS
    dy = abs(y - LENS_CENTER[1]) / SPARKLE_RADIUS
    return dx ** SPARKLE_WAIST + dy ** SPARKLE_WAIST <= 1.0 if (dx or dy) else True


def coverage(px, py):
    hits = 0
    for sy in range(SUPERSAMPLE):
        for sx in range(SUPERSAMPLE):
            x = px + (sx + 0.5) / SUPERSAMPLE
            y = py + (sy + 0.5) / SUPERSAMPLE
            if in_ring(x, y) or in_handle(x, y) or in_sparkle(x, y):
                hits += 1
    return hits / (SUPERSAMPLE * SUPERSAMPLE)


def png_chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)


def main():
    rows = bytearray()
    for py in range(SIZE):
        rows.append(0)  # filter: none
        for px in range(SIZE):
            rows += bytes((255, 255, 255, round(coverage(px, py) * 255)))
    header = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)  # 8-bit RGBA
    png = b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", header) + png_chunk(b"IDAT", zlib.compress(bytes(rows), 9)) + png_chunk(b"IEND", b"")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(png)
    print(f"wrote {OUTPUT} ({len(png)} bytes)")


if __name__ == "__main__":
    main()
