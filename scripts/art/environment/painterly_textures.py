"""Procedural *hand-painted look* textures for the Origin Sanctuary kit.

Everything is generated from seeded noise and brush-stroke stamps: no photos, no
third-party textures, no copyrighted material. Runs inside Blender (numpy is
bundled) or with any Python 3 + numpy:

    python scripts/art/environment/painterly_textures.py <output_dir>

Outputs (sRGB PNG):
  T_ENV_PropsAtlas.png      1024^2  4x4 atlas for every opaque prop (ENV_Props)
  T_ENV_PropsEmission.png    512^2  same layout, only rune glyph / glow caps lit
  T_ENV_Foliage.png          512^2  RGBA alpha-clipped cards (grass, flowers, ivy)
  T_ENV_Ground.png           512^2  seamless meadow (tiles)
  T_ENV_Water.png            256^2  seamless painterly ripples (tiles)
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import atlas  # noqa: E402
from png_io import write_png  # noqa: E402


def hex_rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


# --------------------------------------------------------------------------- noise
def periodic_noise(size, cells, rng):
    """Seamless value noise in [0,1], shape (size, size)."""
    grid = rng.random((cells, cells)).astype(np.float32)
    coords = np.arange(size, dtype=np.float32) * cells / size
    i0 = np.floor(coords).astype(int) % cells
    i1 = (i0 + 1) % cells
    f = coords - np.floor(coords)
    f = f * f * (3 - 2 * f)
    g00 = grid[np.ix_(i0, i0)]
    g01 = grid[np.ix_(i0, i1)]
    g10 = grid[np.ix_(i1, i0)]
    g11 = grid[np.ix_(i1, i1)]
    fy = f[:, None]
    fx = f[None, :]
    return (g00 * (1 - fx) + g01 * fx) * (1 - fy) + (g10 * (1 - fx) + g11 * fx) * fy


def fbm(size, rng, base=4, octaves=4, gain=0.5):
    total = np.zeros((size, size), np.float32)
    amp, norm, cells = 1.0, 0.0, base
    for _ in range(octaves):
        total += periodic_noise(size, cells, rng) * amp
        norm += amp
        amp *= gain
        cells *= 2
    return total / norm


# ------------------------------------------------------------------------- strokes
class Canvas:
    """Float RGB(A) canvas with wrap-around brush stamping."""

    def __init__(self, w, h, rgb=(0, 0, 0), alpha=1.0):
        self.w, self.h = w, h
        self.rgb = np.empty((h, w, 3), np.float32)
        self.rgb[:] = rgb
        self.a = np.full((h, w), alpha, np.float32)

    def stamp(self, cx, cy, angle, length, width, color, opacity, wrap=True,
              bristle=0.25, rng=None, write_alpha=False, hardness=0.55):
        r = int(length * 0.5 + width) + 2
        ys, xs = np.mgrid[-r:r + 1, -r:r + 1].astype(np.float32)
        ca, sa = math.cos(angle), math.sin(angle)
        u = xs * ca + ys * sa
        v = -xs * sa + ys * ca
        du = np.clip(np.abs(u) - length * 0.5, 0, None)
        d = np.sqrt(du * du + v * v) / max(width * 0.5, 0.5)
        m = smoothstep(1.0, hardness, d)
        if bristle > 0:
            phase = rng.random() * 6.283 if rng is not None else 0.0
            m *= (1.0 - bristle) + bristle * (0.5 + 0.5 * np.sin(v * (5.0 / max(width, 1)) * 3.0 + phase))
        a = (m * opacity)[..., None]
        ix = np.rint(cx + xs).astype(int)
        iy = np.rint(cy + ys).astype(int)
        if wrap:
            ix %= self.w
            iy %= self.h
            sel = (slice(None), slice(None))
        else:
            ok = (ix >= 0) & (ix < self.w) & (iy >= 0) & (iy < self.h)
            if not ok.any():
                return
            ix, iy, a = ix[ok], iy[ok], a[ok]
            m = m[ok]
            sel = None
        col = np.asarray(color, np.float32)
        if sel is not None:
            self.rgb[iy, ix] = self.rgb[iy, ix] * (1 - a) + col * a
            if write_alpha:
                self.a[iy, ix] = np.maximum(self.a[iy, ix], m * opacity)
        else:
            self.rgb[iy, ix] = self.rgb[iy, ix] * (1 - a) + col * a
            if write_alpha:
                self.a[iy, ix] = np.maximum(self.a[iy, ix], m * opacity)

    def to_uint8(self, with_alpha=False):
        rgb = np.clip(self.rgb, 0, 1)
        if with_alpha:
            out = np.concatenate([rgb, np.clip(self.a, 0, 1)[..., None]], axis=2)
        else:
            out = rgb
        return (out * 255 + 0.5).astype(np.uint8)


def painted_field(size, rng, dark, light, base=3, contrast=1.0, bands=5):
    """Low-frequency colour field with soft posterized bands (painted value steps)."""
    n = fbm(size, rng, base=base, octaves=4)
    n = np.clip((n - 0.5) * contrast + 0.5, 0, 1)
    if bands:
        q = np.round(n * bands) / bands
        n = n * 0.45 + q * 0.55
    return dark[None, None, :] * (1 - n[..., None]) + light[None, None, :] * n[..., None], n


def strokes(canvas, rng, field, count, angle, jitter, length, width, opacity,
            tint=0.06, palette=None, bristle=0.3):
    for _ in range(count):
        cx = rng.random() * canvas.w
        cy = rng.random() * canvas.h
        if palette is not None:
            col = palette[rng.integers(len(palette))]
        else:
            col = field[int(cy) % canvas.h, int(cx) % canvas.w]
        col = np.clip(col * (1 + (rng.random() - 0.5) * tint * 2)
                      + (rng.random(3) - 0.5) * tint * 0.5, 0, 1)
        canvas.stamp(cx, cy, angle + rng.normal() * jitter,
                     length * (0.6 + 0.8 * rng.random()),
                     width * (0.6 + 0.8 * rng.random()), col, opacity, rng=rng,
                     bristle=bristle)


# ------------------------------------------------------------------- atlas tiles
T = 256  # one atlas tile


def base_tile(rng, dark, light, base=3, contrast=1.2, bands=5):
    field, n = painted_field(T, rng, hex_rgb(dark), hex_rgb(light), base, contrast, bands)
    c = Canvas(T, T)
    c.rgb[:] = field
    return c, field, n


def tile_stone(rng, dark, light, angle=0.15, specks="#5d5444"):
    c, field, _ = base_tile(rng, dark, light, base=3)
    strokes(c, rng, field, 220, angle, 0.35, 34, 15, 0.45)
    strokes(c, rng, field * 1.12, 90, angle, 0.5, 18, 8, 0.35)
    for _ in range(5):  # hairline cracks
        x, y, a = rng.random() * T, rng.random() * T, rng.random() * math.pi
        for _ in range(int(rng.integers(5, 10))):
            c.stamp(x, y, a, 10, 2.2, hex_rgb(specks), 0.55, rng=rng, bristle=0)
            x += math.cos(a) * 9
            y += math.sin(a) * 9
            a += rng.normal() * 0.5
    return c


def tile_blocks(rng):
    c, field, _ = base_tile(rng, "#94815f", "#d3c39e", base=3)
    strokes(c, rng, field, 160, 0.0, 0.25, 30, 13, 0.4)
    mortar, hi, lo = hex_rgb("#5a4e3c"), hex_rgb("#efe2bf"), hex_rgb("#7b6a4f")
    rows = 4
    rh = T // rows
    for r in range(rows):
        y0 = r * rh
        x = (r % 2) * 40 - 20
        while x < T + 40:
            bw = int(rng.integers(70, 120))
            # bevel light on top edge, shade on bottom edge, mortar line around
            for k in range(0, bw, 6):
                c.stamp(x + k, y0 + 4, 0, 8, 4, hi, 0.45, rng=rng, bristle=0.1)
                c.stamp(x + k, y0 + rh - 6, 0, 8, 5, lo, 0.4, rng=rng, bristle=0.1)
            for k in range(0, rh, 6):
                c.stamp(x + 3, y0 + k, math.pi / 2, 8, 3, mortar, 0.8, rng=rng, bristle=0)
            x += bw
        for k in range(0, T, 6):
            c.stamp(k, y0 + 1, 0, 8, 3.5, mortar, 0.85, rng=rng, bristle=0)
    return c


def tile_moss(rng):
    c, field, _ = base_tile(rng, "#4b7330", "#93b84f", base=4, contrast=1.4)
    strokes(c, rng, field, 520, 0, 3.0, 9, 9, 0.55, tint=0.1, bristle=0.1)
    strokes(c, rng, field, 140, 0, 3.0, 6, 6, 0.6,
            palette=[hex_rgb("#c0d66d"), hex_rgb("#a9c95e")], bristle=0)
    strokes(c, rng, field, 120, 0, 3.0, 6, 5, 0.5, palette=[hex_rgb("#3a5d28")], bristle=0)
    return c


def tile_bark(rng, dark="#5a3d2a", light="#94704c"):
    c, field, _ = base_tile(rng, dark, light, base=3)
    strokes(c, rng, field, 260, math.pi / 2, 0.08, 46, 10, 0.5)
    for _ in range(14):
        x = rng.random() * T
        y = rng.random() * T
        for _ in range(8):
            c.stamp(x, y, math.pi / 2, 14, 3.2, hex_rgb("#3a271b"), 0.6, rng=rng, bristle=0)
            y += 12
            x += rng.normal() * 2.5
    strokes(c, rng, field * 1.2, 60, math.pi / 2, 0.1, 30, 4, 0.35)
    return c


def tile_leaves(rng):
    c, field, _ = base_tile(rng, "#3f6d33", "#86b44c", base=3, contrast=1.3)
    strokes(c, rng, field, 700, 0, 3.0, 13, 8, 0.65, tint=0.08, bristle=0.05)
    strokes(c, rng, field, 220, 0, 3.0, 10, 6, 0.6,
            palette=[hex_rgb("#b9d567"), hex_rgb("#cfe17e")], bristle=0)
    strokes(c, rng, field, 200, 0, 3.0, 10, 6, 0.55, palette=[hex_rgb("#2f5428")], bristle=0)
    return c


def tile_statue(rng):
    c = tile_stone(rng, "#5b706b", "#9cb0a5", angle=1.2, specks="#44534f")
    # verdigris / lichen stains
    for _ in range(30):
        c.stamp(rng.random() * T, rng.random() * T, rng.random() * 3, 18, 14,
                hex_rgb("#7d9a6a"), 0.35, rng=rng, bristle=0.2)
    return c


def tile_cap(rng, dark, light, spot, spots=18, size=(10, 18)):
    c, field, _ = base_tile(rng, dark, light, base=2, contrast=1.2)
    strokes(c, rng, field, 200, 0.3, 0.6, 26, 12, 0.45)
    for _ in range(spots):
        s = size[0] + rng.random() * (size[1] - size[0])
        c.stamp(rng.random() * T, rng.random() * T, 0, 0.1, s, hex_rgb(spot), 0.9,
                rng=rng, bristle=0, hardness=0.75)
    return c


def tile_glyph(rng):
    c, field, _ = base_tile(rng, "#2f353f", "#4b5462", base=3)
    strokes(c, rng, field, 140, 0.4, 0.6, 26, 12, 0.4)
    mask = glyph_mask(rng, T)
    glow = np.clip(mask * 1.0, 0, 1)[..., None]
    halo = np.clip(blur(mask, 6) * 1.6, 0, 1)[..., None]
    c.rgb = c.rgb * (1 - halo * 0.5) + hex_rgb("#4fb9cf") * halo * 0.5
    c.rgb = c.rgb * (1 - glow) + hex_rgb("#d8fbff") * glow
    return c, mask


def blur(img, r):
    out = img.copy()
    for axis in (0, 1):
        acc = np.zeros_like(out)
        for k in range(-r, r + 1):
            acc += np.roll(out, k, axis=axis)
        out = acc / (2 * r + 1)
    return out


def glyph_mask(rng, size):
    """Original rune-like sigils drawn from staves, arcs and dots (not a real script)."""
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    mask = np.zeros((size, size), np.float32)

    def seg(x0, y0, x1, y1, w):
        dx, dy = x1 - x0, y1 - y0
        L2 = dx * dx + dy * dy + 1e-6
        t = np.clip(((xx - x0) * dx + (yy - y0) * dy) / L2, 0, 1)
        d = np.hypot(xx - (x0 + t * dx), yy - (y0 + t * dy))
        return smoothstep(w, w * 0.45, d)

    def ring(cx, cy, r, w):
        d = np.abs(np.hypot(xx - cx, yy - cy) - r)
        return smoothstep(w, w * 0.45, d)

    cells = 2
    cs = size / cells
    kinds = rng.permutation(4)
    for gy in range(cells):
        for gx in range(cells):
            ox, oy = gx * cs + cs / 2, gy * cs + cs / 2
            h = cs * 0.36
            w = 4.5
            m = seg(ox, oy - h, ox, oy + h, w)  # central stave
            kind = kinds[gy * cells + gx]
            if kind == 0:
                m = np.maximum(m, seg(ox, oy - h * 0.3, ox + h * 0.6, oy - h * 0.8, w))
                m = np.maximum(m, seg(ox, oy + h * 0.1, ox - h * 0.6, oy - h * 0.4, w))
            elif kind == 1:
                m = np.maximum(m, ring(ox, oy - h * 0.35, h * 0.38, w))
            elif kind == 2:
                m = np.maximum(m, seg(ox - h * 0.6, oy, ox + h * 0.6, oy, w))
                m = np.maximum(m, seg(ox - h * 0.6, oy, ox - h * 0.2, oy + h * 0.6, w))
            else:
                m = np.maximum(m, seg(ox, oy - h, ox + h * 0.55, oy - h * 0.3, w))
                m = np.maximum(m, seg(ox + h * 0.55, oy - h * 0.3, ox, oy + h * 0.2, w))
            m = np.maximum(m, ring(ox, oy + h * 1.15, 3.5, 3.5))  # dot
            mask = np.maximum(mask, m)
    return mask


def build_props_atlas(seed=1303):
    rng = np.random.default_rng(seed)
    S, G = atlas.PROPS_ATLAS_SIZE, atlas.PROPS_GRID
    out = np.zeros((S, S, 3), np.float32)
    emission = np.zeros((S, S, 3), np.float32)
    recipes = {
        "stone_warm": lambda: tile_stone(rng, "#9a8460", "#d6c49c"),
        "stone_cool": lambda: tile_stone(rng, "#68705f", "#a7ab97", angle=0.8),
        "stone_blocks": lambda: tile_blocks(rng),
        "moss": lambda: tile_moss(rng),
        "bark": lambda: tile_bark(rng),
        "root": lambda: tile_bark(rng, "#7a5a3e", "#b5916a"),
        "leaf_canopy": lambda: tile_leaves(rng),
        "statue_stone": lambda: tile_statue(rng),
        "mushroom_cap": lambda: tile_cap(rng, "#b0402c", "#e27748", "#f6e4bb"),
        "mushroom_stem": lambda: tile_bark(rng, "#cdb48a", "#f3e6c6"),
        "rune_slate": lambda: tile_stone(rng, "#343b45", "#5a6370", angle=0.6, specks="#252a31"),
        "earth": lambda: tile_stone(rng, "#6e5440", "#a3835d", angle=0.0, specks="#4b3a2c"),
        "lily": lambda: tile_leaves(rng),
        "gold_trim": lambda: tile_stone(rng, "#a37a33", "#e8c46b", angle=0.0, specks="#7a5a24"),
        "glow_cap": lambda: tile_cap(rng, "#2c8fb3", "#78dcef", "#e6fdff", spots=14, size=(8, 14)),
    }
    glyph = None
    for name in atlas.PROPS_TILES:
        i = atlas.tile_index(name)
        col, row = i % G, i // G
        if name == "rune_glyph":
            c, glyph = tile_glyph(rng)
        else:
            c = recipes[name]()
        out[row * T:(row + 1) * T, col * T:(col + 1) * T] = c.rgb
        if name in atlas.EMISSIVE_TILES:
            tint = np.array(atlas.EMISSIVE_TILES[name], np.float32)
            if name == "rune_glyph":
                e = np.clip(glyph + blur(glyph, 4) * 0.8, 0, 1)[..., None] * tint
            else:
                lum = c.rgb.mean(axis=2, keepdims=True)
                e = np.clip(lum * 1.1, 0, 1) * tint
            emission[row * T:(row + 1) * T, col * T:(col + 1) * T] = e
    # Emission map is half resolution: the glow is soft anyway.
    em_small = emission.reshape(S // 2, 2, S // 2, 2, 3).mean(axis=(1, 3))
    return (np.clip(out, 0, 1) * 255 + .5).astype(np.uint8), (np.clip(em_small, 0, 1) * 255 + .5).astype(np.uint8)


# ------------------------------------------------------------------ tiling maps
def build_ground(seed=77, size=512):
    rng = np.random.default_rng(seed)
    field, _ = painted_field(size, rng, hex_rgb("#5f8236"), hex_rgb("#a4bf5c"), base=3, contrast=1.3)
    c = Canvas(size, size)
    c.rgb[:] = field
    strokes(c, rng, field, 2600, -1.2, 0.5, 14, 5, 0.5, tint=0.1, bristle=0.1)
    strokes(c, rng, field, 500, -1.2, 0.5, 10, 4, 0.5,
            palette=[hex_rgb("#c2d477"), hex_rgb("#b3cc68")], bristle=0)
    strokes(c, rng, field, 500, -1.2, 0.5, 10, 4, 0.45, palette=[hex_rgb("#46682b")], bristle=0)
    for _ in range(70):  # tiny painted blossoms
        col = [hex_rgb("#f2c0d6"), hex_rgb("#fff1b0"), hex_rgb("#bfe6f2")][int(rng.integers(3))]
        c.stamp(rng.random() * size, rng.random() * size, 0, 0.1, 4.5, col, 0.9, rng=rng, bristle=0, hardness=0.7)
    return c.to_uint8()


def build_water(seed=5, size=256):
    rng = np.random.default_rng(seed)
    field, _ = painted_field(size, rng, hex_rgb("#2f7f86"), hex_rgb("#5fb3ad"), base=2, contrast=1.1)
    c = Canvas(size, size)
    c.rgb[:] = field
    strokes(c, rng, field, 500, 0.0, 0.25, 30, 7, 0.45, tint=0.06, bristle=0.2)
    strokes(c, rng, field, 120, 0.0, 0.2, 22, 3, 0.7,
            palette=[hex_rgb("#bfeee6"), hex_rgb("#9fded6")], bristle=0)
    return c.to_uint8()


# --------------------------------------------------------------------- foliage
def build_foliage(seed=21):
    rng = np.random.default_rng(seed)
    W, H = atlas.FOLIAGE_SIZE
    cols, rows = atlas.FOLIAGE_GRID
    cw, ch = W // cols, H // rows
    out = np.zeros((H, W, 4), np.float32)

    def blade(c, x0, y0, length, width, bend, dark, light, segs=14):
        x, y = x0, y0
        ang = -math.pi / 2 + rng.normal() * 0.15
        for s in range(segs):
            t = s / segs
            col = hex_rgb(dark) * (1 - t) + hex_rgb(light) * t
            w = width * (1 - t * 0.85)
            step = length / segs
            c.stamp(x, y, ang, step * 1.4, w, col, 1.0, wrap=False, rng=rng,
                    bristle=0, write_alpha=True, hardness=0.6)
            x += math.cos(ang) * step
            y += math.sin(ang) * step
            ang += bend / segs
        return x, y

    def leaf(c, x, y, ang, L, Wd, dark, light):
        for k in range(5):
            t = k / 4
            col = hex_rgb(dark) * (1 - t) + hex_rgb(light) * t
            px = x + math.cos(ang) * L * t * 0.8
            py = y + math.sin(ang) * L * t * 0.8
            c.stamp(px, py, ang, L * 0.35, Wd * (1 - abs(t - 0.45) * 1.2), col, 1.0,
                    wrap=False, rng=rng, bristle=0, write_alpha=True, hardness=0.6)

    def flower_head(c, x, y, petal, center, r=17):
        n = 6
        for k in range(n):
            a = k * 2 * math.pi / n + rng.random() * 0.3
            c.stamp(x + math.cos(a) * r * 0.6, y + math.sin(a) * r * 0.45, a, r * 0.7, r * 0.75,
                    hex_rgb(petal), 1.0, wrap=False, rng=rng, bristle=0, write_alpha=True, hardness=0.65)
        c.stamp(x, y, 0, 0.1, r * 0.55, hex_rgb(center), 1.0, wrap=False, rng=rng, bristle=0,
                write_alpha=True, hardness=0.7)

    for i, name in enumerate(atlas.FOLIAGE_CELLS):
        c = Canvas(cw, ch, rgb=(0.35, 0.5, 0.2), alpha=0.0)
        base_y = ch - 6
        if name.startswith("grass"):
            light = "#b8d468" if name == "grass_a" else "#9fc35a"
            for _ in range(11 if name == "grass_a" else 15):
                blade(c, cw * 0.5 + rng.normal() * cw * 0.16, base_y,
                      ch * (0.55 + rng.random() * 0.35), 9 + rng.random() * 4,
                      rng.normal() * 0.9, "#3e6a2a", light)
        elif name.startswith("flower"):
            petal = {"flower_pink": "#ef9cc2", "flower_blue": "#86cfe3", "flower_gold": "#ffd56e",
                     "flower_lilac": "#c3a3e8"}[name]
            for k in range(3):
                x0 = cw * (0.3 + 0.2 * k) + rng.normal() * 4
                tx, ty = blade(c, x0, base_y, ch * (0.5 + 0.25 * rng.random()), 5, rng.normal() * 0.4,
                               "#3d6a2c", "#7fae4a")
                leaf(c, x0, base_y - ch * 0.15, -math.pi / 2 + (0.9 if k % 2 else -0.9), 34, 14,
                     "#3f6e2d", "#86b54e")
                flower_head(c, tx, ty + 6, petal, "#fff0a6" if name != "flower_gold" else "#e08a3a")
        elif name == "fern":
            x, y = cw * 0.5, base_y
            for s in range(16):
                t = s / 16
                yy = y - t * ch * 0.85
                xx = x + math.sin(t * 2.2) * 8
                c.stamp(xx, yy, -math.pi / 2, 14, 5, hex_rgb("#4b7a32"), 1.0, wrap=False, rng=rng,
                        bristle=0, write_alpha=True)
                L = 46 * (1 - t * 0.8)
                for sgn in (-1, 1):
                    leaf(c, xx, yy, -math.pi / 2 + sgn * (1.15 - t * 0.5), L, 10 * (1 - t * 0.6),
                         "#3e6b2c", "#9cc458")
        elif name == "leaf_cluster":
            for k in range(11):
                a = -math.pi / 2 + float(np.clip(rng.normal() * 0.7, -1.1, 1.1))
                leaf(c, cw * 0.5 + rng.normal() * 6, ch * 0.66 + rng.normal() * 14, a,
                     44 + rng.random() * 18, 22, "#3d6a30", "#9cc255")
        elif name == "ivy_strand":
            x = cw * 0.5
            for s in range(30):
                t = s / 30
                y = 6 + t * (ch - 12)
                xx = x + math.sin(t * 9) * 10
                c.stamp(xx, y, math.pi / 2, 12, 4, hex_rgb("#5b6b35"), 1.0, wrap=False, rng=rng,
                        bristle=0, write_alpha=True)
                if s % 3 == 0:
                    sgn = 1 if s % 6 == 0 else -1
                    leaf(c, xx, y, math.pi / 2 + sgn * 1.2, 34, 22, "#3a6a2f", "#8fbd52")
        elif name in ("reed", "cattail"):
            for k in range(7 if name == "reed" else 5):
                x0 = cw * 0.5 + rng.normal() * cw * 0.13
                tx, ty = blade(c, x0, base_y, ch * (0.72 + rng.random() * 0.22), 7, rng.normal() * 0.25,
                               "#4d6b33", "#a3b866")
                if name == "cattail" and k % 2 == 0:
                    for j in range(6):  # brown seed head
                        c.stamp(tx, ty + 10 + j * 5, math.pi / 2, 8, 11, hex_rgb("#7a4f2e"), 1.0, wrap=False,
                                rng=rng, bristle=0, write_alpha=True, hardness=0.7)
        elif name == "dry_grass":
            for _ in range(14):
                blade(c, cw * 0.5 + rng.normal() * cw * 0.18, base_y, ch * (0.4 + rng.random() * 0.35),
                      8 + rng.random() * 3, rng.normal() * 1.1, "#8a7a3e", "#e3cf86")
        elif name == "meadow_tall":
            for _ in range(10):
                tx, ty = blade(c, cw * 0.5 + rng.normal() * cw * 0.16, base_y, ch * (0.7 + rng.random() * 0.25),
                               7, rng.normal() * 0.6, "#3e6a2a", "#b9d36a")
                c.stamp(tx, ty + 8, -math.pi / 2, 14, 7, hex_rgb("#d9d79a"), 1.0, wrap=False, rng=rng,
                        bristle=0, write_alpha=True)
        elif name == "clover":
            for _ in range(7):
                x0, y0 = cw * 0.5 + rng.normal() * 24, base_y - rng.random() * ch * 0.35
                for k in range(3):
                    a = -math.pi / 2 + (k - 1) * 1.2
                    leaf(c, x0, y0, a, 26, 20, "#3f7a35", "#8fc860")
                if rng.random() < 0.5:
                    c.stamp(x0, y0 - 20, 0, 0.1, 13, hex_rgb("#f4f0f5"), 1.0, wrap=False, rng=rng,
                            bristle=0, write_alpha=True, hardness=0.7)
        elif name == "shrub_leaf":
            for k in range(26):
                a = -math.pi / 2 + rng.normal() * 1.0
                leaf(c, cw * 0.5 + rng.normal() * 18, ch * 0.55 + rng.normal() * 34, a,
                     34 + rng.random() * 14, 18, "#335f2c", "#7fb04c")
        elif name == "twig_dry":
            for _ in range(5):
                tx, ty = blade(c, cw * 0.5 + rng.normal() * 10, base_y, ch * (0.5 + rng.random() * 0.3),
                               5, rng.normal() * 1.4, "#5e4630", "#a08060", segs=10)
                for _ in range(3):
                    leaf(c, tx + rng.normal() * 8, ty + rng.random() * 40, rng.random() * 6.28, 16, 9,
                         "#9a7d3e", "#d8b866")
        col, row = i % cols, i // cols
        # Bleed RGB under transparent pixels so mipmaps stay green, not black.
        a = c.a
        if (a > 0.5).any():
            mean = c.rgb[a > 0.5].mean(axis=0)
            c.rgb = np.where((a > 0.02)[..., None], c.rgb, mean)
        out[row * ch:(row + 1) * ch, col * cw:(col + 1) * cw, :3] = c.rgb
        out[row * ch:(row + 1) * ch, col * cw:(col + 1) * cw, 3] = c.a
    return (np.clip(out, 0, 1) * 255 + .5).astype(np.uint8)


def generate_all(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    props, emission = build_props_atlas()
    files = {
        "T_ENV_PropsAtlas.png": props,
        "T_ENV_PropsEmission.png": emission,
        "T_ENV_Foliage.png": build_foliage(),
        "T_ENV_Ground.png": build_ground(),
        "T_ENV_Water.png": build_water(),
    }
    for name, img in files.items():
        write_png(os.path.join(out_dir, name), img)
    return [os.path.join(out_dir, n) for n in files]


if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else "art-exports/environment/Textures"
    for p in generate_all(target):
        print("wrote", p)
