"""Hand-painted-look textures for the Ocean and Sky kits (U1-ENV.4).

Same method and brushes as painterly_textures.py (seeded noise + brush stamps, no
photos or third-party images), same atlas layout (atlas.py) so the Kit zone names stay
valid; every tile and foliage cell is simply repainted for the zone:

    python scripts/art/environment/zone_textures.py <out_dir> ocean|sky

  T_ENV_<Zone>_PropsAtlas.png     512^2  4x4 atlas (ENV_<Zone>_Props), painted at 1024^2, box-filtered
  T_ENV_<Zone>_PropsEmission.png  128^2  glow (crystals, anemones, glyphs), painted at 1024^2, box-filtered
  T_ENV_<Zone>_Foliage.png       1024x512 alpha-clipped cards
  T_ENV_<Zone>_Ground.png         512^2  seamless sand / cloud-meadow
  T_ENV_Sky_Water.png             256^2  seamless cloud sea (Sky only, water shader)
  Ocean FX: T_ENV_FX_Caustics (256^2, tiling), T_ENV_FX_LightShaft (64x256),
            T_ENV_FX_Bubble (64^2)
  Sky FX:   T_ENV_FX_CloudWisp (128^2), T_ENV_FX_Petal (64^2)
Atlas tile -> meaning per zone is listed in OCEAN_TILES / SKY_TILES.

ENV Alpha (issue #79): the zone scenes must fit the Unity validator's 6 MiB RGBA32 + mips estimate
(ZoneEnvironmentBuilder.BudgetProblems: width * height * 4 * 1.333 per texture). At 1024^2 the atlas alone was
5.33 MiB (scene 11.1 MiB). Every map is still PAINTED at its original size (same seeds, same brushes) and then
box-filtered to OUTPUT_SIZE, so the layout (UVs are fractions of the atlas) and the look are unchanged, only the
texel density drops. Ground (largest screen area) and foliage (alpha-clipped edges) keep their size.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import atlas  # noqa: E402
import painterly_textures as pt  # noqa: E402
from png_io import write_png  # noqa: E402

T = pt.T
H = pt.hex_rgb

# Output size per map (square); maps not listed keep their painted size.
OUTPUT_SIZE = {"PropsAtlas": 512, "PropsEmission": 128}


def box_downscale(img, size):
    """Area-mean downscale of a uint8 (H, W, C) image by an integer factor (deterministic, no resampling ringing)."""
    h, w, c = img.shape
    f = h // size
    if f <= 1:
        return img
    assert h == w and h % size == 0, (h, w, size)
    m = img.astype(np.float32).reshape(size, f, size, f, c).mean(axis=(1, 3))
    return (m + 0.5).astype(np.uint8)


# Atlas slot -> what it is painted as, per zone (documentation + tests).
OCEAN_TILES = {
    "stone_warm": "sunken sandstone with barnacles", "stone_cool": "reef rock with coralline specks",
    "stone_blocks": "sea-worn masonry", "moss": "algae and coralline crust", "bark": "driftwood / holdfast",
    "root": "pink sea whip", "leaf_canopy": "kelp canopy", "statue_stone": "verdigris statue stone",
    "mushroom_cap": "branch coral with polyps", "mushroom_stem": "pearly shell", "rune_slate": "dark basalt",
    "rune_glyph": "cool glowing crystal", "earth": "rippled sand", "lily": "table coral rings",
    "gold_trim": "barnacled bronze", "glow_cap": "bioluminescent anemone",
}
SKY_TILES = {
    "stone_warm": "white marble with gold veins", "stone_cool": "floating island rock", "stone_blocks": "marble blocks",
    "moss": "bright sky moss", "bark": "pale blossom bark", "root": "hanging island roots",
    "leaf_canopy": "blossom canopy", "statue_stone": "pale blue statue stone", "mushroom_cap": "island underside strata",
    "mushroom_stem": "cloud puff", "rune_slate": "lapis slate", "rune_glyph": "gold glyphs",
    "earth": "underside soil", "lily": "bridge planks", "gold_trim": "bright gold", "glow_cap": "sky crystal glow",
}
OCEAN_FOLIAGE = {
    "grass_a": "seagrass", "grass_b": "short seagrass", "flower_pink": "pink sea pens", "flower_blue": "blue anemone tufts",
    "flower_gold": "gold tube sponges", "fern": "purple sea fan", "leaf_cluster": "kelp blades", "ivy_strand": "kelp strand",
    "reed": "young kelp", "cattail": "sea whips", "dry_grass": "brown algae", "meadow_tall": "tall eelgrass",
    "clover": "sea lettuce", "flower_lilac": "feather stars", "shrub_leaf": "soft coral", "twig_dry": "red gorgonian",
}
SKY_FOLIAGE = {
    "grass_a": "cloud-meadow grass", "grass_b": "short sky grass", "flower_pink": "sky blossoms", "flower_blue": "cloud bells",
    "flower_gold": "sun daisies", "fern": "feather fern", "leaf_cluster": "blossom sprigs", "ivy_strand": "hanging vine",
    "reed": "wind grass", "cattail": "chime reeds", "dry_grass": "silver grass", "meadow_tall": "tall wind grass",
    "clover": "cloud clover", "flower_lilac": "aether lilies", "shrub_leaf": "blossom shrub leaves", "twig_dry": "gold twigs",
}


# ------------------------------------------------------------------ helpers
def specks(c, rng, color, count, size=(3, 7), opacity=0.85, hard=0.7):
    for _ in range(count):
        s = size[0] + rng.random() * (size[1] - size[0])
        c.stamp(rng.random() * c.w, rng.random() * c.h, 0, 0.1, s, H(color), opacity, rng=rng, bristle=0, hardness=hard)
    return c


def tint(c, rgb, amount):
    c.rgb = c.rgb * (1 - amount) + c.rgb * np.asarray(rgb, np.float32) * amount
    return c


def tile_crust(rng, dark, light, spots, accents):
    c, field, _ = pt.base_tile(rng, dark, light, base=4, contrast=1.4)
    pt.strokes(c, rng, field, 480, 0, 3.0, 9, 9, 0.55, tint=0.1, bristle=0.1)
    pt.strokes(c, rng, field, 140, 0, 3.0, 6, 6, 0.6, palette=[H(a) for a in accents], bristle=0)
    specks(c, rng, spots, 60, (2.5, 5.5), 0.8)
    return c


def tile_rings(rng, dark, light, rim):
    c, field, _ = pt.base_tile(rng, dark, light, base=2, contrast=1.1)
    yy, xx = np.mgrid[0:T, 0:T].astype(np.float32)
    for _ in range(3):
        cx, cy = rng.random() * T, rng.random() * T
        d = np.hypot(((xx - cx + T / 2) % T) - T / 2, ((yy - cy + T / 2) % T) - T / 2)
        ring = (np.sin(d * 0.32) * 0.5 + 0.5) ** 6
        c.rgb = c.rgb * (1 - ring[..., None] * 0.35) + H(rim) * ring[..., None] * 0.35
    pt.strokes(c, rng, field, 160, 0.0, 2.0, 18, 8, 0.3)
    return c


def tile_sand(rng, dark, light):
    c, field, _ = pt.base_tile(rng, dark, light, base=3, contrast=1.0)
    yy = np.mgrid[0:T, 0:T][0].astype(np.float32)
    warp = pt.fbm(T, rng, base=3) * 26
    ripple = (np.sin((yy + warp) * 2 * math.pi / 21) * 0.5 + 0.5) ** 3
    c.rgb = c.rgb * (1 - ripple[..., None] * 0.18) + H(light) * ripple[..., None] * 0.12
    pt.strokes(c, rng, field, 200, 0.0, 0.15, 26, 6, 0.3)
    return c


def tile_crystal(rng, dark, light, glow):
    c, field, _ = pt.base_tile(rng, dark, light, base=2, contrast=1.3, bands=4)
    for _ in range(26):  # long facet strokes
        x, y = rng.random() * T, rng.random() * T
        a = math.pi / 2 + rng.normal() * 0.25
        c.stamp(x, y, a, 70 + rng.random() * 60, 16 + rng.random() * 12, H(light), 0.45, rng=rng, bristle=0,
                hardness=0.85)
    for _ in range(18):
        x, y = rng.random() * T, rng.random() * T
        c.stamp(x, y, math.pi / 2, 50, 5, H(glow), 0.85, rng=rng, bristle=0, hardness=0.8)
    return c


def tile_glyph_colored(rng, dark, light, glow, halo):
    c, field, _ = pt.base_tile(rng, dark, light, base=3)
    pt.strokes(c, rng, field, 140, 0.4, 0.6, 26, 12, 0.4)
    mask = pt.glyph_mask(rng, T)
    g = np.clip(mask, 0, 1)[..., None]
    h = np.clip(pt.blur(mask, 6) * 1.6, 0, 1)[..., None]
    c.rgb = c.rgb * (1 - h * 0.5) + H(halo) * h * 0.5
    c.rgb = c.rgb * (1 - g) + H(glow) * g
    return c, mask


def tile_strata(rng, dark, light, band_cols):
    c, field, _ = pt.base_tile(rng, dark, light, base=3)
    yy = np.mgrid[0:T, 0:T][0].astype(np.float32)
    warp = pt.fbm(T, rng, base=2) * 30
    idx = (((yy + warp) / 34).astype(int)) % len(band_cols)
    cols = np.stack([H(b) for b in band_cols])[idx]
    c.rgb = c.rgb * 0.55 + cols * 0.45
    pt.strokes(c, rng, c.rgb.copy(), 260, 0.0, 0.12, 34, 9, 0.4)
    return c


def tile_cloud(rng, shade, light):
    c, field, n = pt.base_tile(rng, shade, light, base=2, contrast=1.6, bands=3)
    for _ in range(90):
        x, y = rng.random() * T, rng.random() * T
        c.stamp(x, y, rng.random() * 3, 30, 28, H(light), 0.35, rng=rng, bristle=0, hardness=0.2)
    return c


def tile_planks(rng, dark, light):
    c, field, _ = pt.base_tile(rng, dark, light, base=3)
    pt.strokes(c, rng, field, 260, 0.0, 0.05, 50, 8, 0.45)
    for r in range(1, 6):
        y = r * T / 6
        for x in range(0, T, 6):
            c.stamp(x, y, 0, 8, 3, H("#4a3626"), 0.8, rng=rng, bristle=0)
    return c


def emissive_from(c, tint_rgb, floor=0.0):
    lum = c.rgb.mean(axis=2, keepdims=True)
    return np.clip((lum - floor) / max(1e-3, 1 - floor) * 1.15, 0, 1) * np.asarray(tint_rgb, np.float32)


# -------------------------------------------------------------- atlases
def ocean_recipes(rng):
    return {
        "stone_warm": lambda: (specks(pt.tile_stone(rng, "#7f7c66", "#c4bd98", specks="#5b5a4a"), rng, "#efe9d8", 70,
                                      (2.5, 6.0))),
        "stone_cool": lambda: (specks(pt.tile_stone(rng, "#3f5961", "#88a4a6", angle=0.8, specks="#2f434a"), rng,
                                      "#c58aa8", 45, (3, 8), 0.55)),
        "stone_blocks": lambda: tint(pt.tile_blocks(rng), (0.78, 0.98, 1.0), 1.0),
        "moss": lambda: tile_crust(rng, "#2f6a5a", "#6fae84", "#e7a0bc", ["#86c79a", "#b9d98a", "#d77fa3"]),
        "bark": lambda: pt.tile_bark(rng, "#4f4532", "#958563"),
        "root": lambda: pt.tile_bark(rng, "#8a4f6e", "#d793b4"),
        "leaf_canopy": lambda: tint(pt.tile_leaves(rng), (0.95, 0.85, 0.55), 1.0),
        "statue_stone": lambda: pt.tile_statue(rng),
        "mushroom_cap": lambda: pt.tile_cap(rng, "#c65a46", "#f2976e", "#ffe2c8", spots=60, size=(3, 7)),
        "mushroom_stem": lambda: tile_rings(rng, "#cbbfae", "#fbf3e8", "#d9c4f0"),
        "rune_slate": lambda: pt.tile_stone(rng, "#24303c", "#465868", angle=0.6, specks="#1a2129"),
        "rune_glyph": lambda: tile_crystal(rng, "#2a6f8c", "#8fe3f2", "#e8fdff"),
        "earth": lambda: tile_sand(rng, "#b09f78", "#e8d8aa"),
        "lily": lambda: tile_rings(rng, "#4f8a96", "#9fd2c9", "#e5f4dc"),
        "gold_trim": lambda: specks(pt.tile_stone(rng, "#8a6c34", "#cfae62", angle=0.0, specks="#54713e"), rng,
                                    "#6fae94", 40, (3, 7), 0.7),
        "glow_cap": lambda: pt.tile_cap(rng, "#2a8fb0", "#7fe6f4", "#f0ffff", spots=30, size=(5, 10)),
    }


def sky_recipes(rng):
    return {
        "stone_warm": lambda: _veins(pt.tile_stone(rng, "#c6bfb2", "#f5f1e8", angle=0.4, specks="#b4a68e"), rng),
        "stone_cool": lambda: pt.tile_stone(rng, "#857a70", "#c8bba8", angle=0.7, specks="#5f554c"),
        "stone_blocks": lambda: tint(pt.tile_blocks(rng), (1.18, 1.16, 1.12), 1.0),
        "moss": lambda: tile_crust(rng, "#6f9a42", "#c3dd78", "#fff3f6", ["#d7ea8e", "#a8cf66", "#f2c7d8"]),
        "bark": lambda: pt.tile_bark(rng, "#7a6556", "#cbbaa5"),
        "root": lambda: pt.tile_bark(rng, "#5e4a3b", "#a68b6e"),
        "leaf_canopy": lambda: _blossom(rng),
        "statue_stone": lambda: pt.tile_stone(rng, "#8b9db2", "#d4dde8", angle=1.1, specks="#6f7f92"),
        "mushroom_cap": lambda: tile_strata(rng, "#6a5a52", "#a3907e", ["#8a7464", "#b59f86", "#6f5d50", "#9c8670"]),
        "mushroom_stem": lambda: tile_cloud(rng, "#c6cde6", "#ffffff"),
        "rune_slate": lambda: pt.tile_stone(rng, "#2d3c63", "#53699c", angle=0.6, specks="#1f2945"),
        "earth": lambda: pt.tile_stone(rng, "#5a483b", "#8e735d", angle=0.0, specks="#3e3128"),
        "lily": lambda: tile_planks(rng, "#866447", "#c79c6c"),
        "gold_trim": lambda: pt.tile_stone(rng, "#b08a3c", "#f2d27a", angle=0.0, specks="#8a6a2a"),
        "glow_cap": lambda: tile_crystal(rng, "#6f9ee6", "#d6e8ff", "#ffffff"),
    }


def _veins(c, rng):
    for _ in range(7):
        x, y, a = rng.random() * T, rng.random() * T, rng.random() * math.pi
        for _ in range(int(rng.integers(8, 16))):
            c.stamp(x, y, a, 10, 2.0, H("#d8b45e"), 0.6, rng=rng, bristle=0)
            x += math.cos(a) * 9
            y += math.sin(a) * 9
            a += rng.normal() * 0.35
    return c


def _blossom(rng):
    c, field, _ = pt.base_tile(rng, "#d98fae", "#fde4ee", base=3, contrast=1.3)
    pt.strokes(c, rng, field, 700, 0, 3.0, 13, 8, 0.65, tint=0.08, bristle=0.05)
    pt.strokes(c, rng, field, 200, 0, 3.0, 10, 6, 0.6, palette=[H("#fff5f8"), H("#ffe0ec")], bristle=0)
    pt.strokes(c, rng, field, 150, 0, 3.0, 10, 6, 0.55, palette=[H("#7fae5a"), H("#b24f7e")], bristle=0)
    return c


EMISSIVE = {
    "ocean": {"rune_glyph": (0.5, 0.95, 1.0), "glow_cap": (0.4, 0.9, 1.0)},
    "sky": {"rune_glyph": (1.0, 0.86, 0.5), "glow_cap": (0.7, 0.85, 1.0)},
}


def build_atlas(zone, seed):
    rng = np.random.default_rng(seed)
    S, G = atlas.PROPS_ATLAS_SIZE, atlas.PROPS_GRID
    out = np.zeros((S, S, 3), np.float32)
    emission = np.zeros((S, S, 3), np.float32)
    recipes = ocean_recipes(rng) if zone == "ocean" else sky_recipes(rng)
    for name in atlas.PROPS_TILES:
        i = atlas.tile_index(name)
        col, row = i % G, i // G
        glyph = None
        if zone == "sky" and name == "rune_glyph":
            c, glyph = tile_glyph_colored(rng, "#2d3c63", "#4f6596", "#fff2c2", "#e8b850")
        else:
            c = recipes[name]()
        out[row * T:(row + 1) * T, col * T:(col + 1) * T] = c.rgb
        em = EMISSIVE[zone].get(name)
        if em is not None:
            if glyph is not None:
                e = np.clip(glyph + pt.blur(glyph, 4) * 0.8, 0, 1)[..., None] * np.asarray(em, np.float32)
            else:
                e = emissive_from(c, em, floor=0.25)
            emission[row * T:(row + 1) * T, col * T:(col + 1) * T] = e
    small = emission.reshape(S // 2, 2, S // 2, 2, 3).mean(axis=(1, 3))
    return (np.clip(out, 0, 1) * 255 + .5).astype(np.uint8), (np.clip(small, 0, 1) * 255 + .5).astype(np.uint8)


# ----------------------------------------------------------------- foliage
class Brush:
    def __init__(self, c, rng):
        self.c, self.rng = c, rng

    def dab(self, x, y, a, L, W, col, hard=0.6, op=1.0):
        hard = min(hard, 0.9)
        self.c.stamp(x, y, a, L, W, col, op, wrap=False, rng=self.rng, bristle=0, write_alpha=True, hardness=hard)

    def blade(self, x0, y0, length, width, bend, dark, light, segs=14, wave=0.0):
        x, y = x0, y0
        ang = -math.pi / 2 + self.rng.normal() * 0.15
        for s in range(segs):
            t = s / segs
            col = H(dark) * (1 - t) + H(light) * t
            step = length / segs
            self.dab(x, y, ang, step * 1.4, width * (1 - t * 0.85), col)
            x += math.cos(ang) * step + math.sin(t * 9 + x0) * wave
            y += math.sin(ang) * step
            ang += bend / segs
        return x, y

    def ribbon(self, x0, y0, length, width, dark, light, amp=10, freq=6):
        for s in range(30):
            t = s / 30
            y = y0 - t * length
            x = x0 + math.sin(t * freq + x0 * 0.1) * amp
            col = H(dark) * (1 - t) + H(light) * t
            self.dab(x, y, -math.pi / 2, length / 30 * 1.6, width * (0.6 + 0.4 * math.sin(t * math.pi)), col)

    def leaf(self, x, y, ang, L, Wd, dark, light):
        for k in range(5):
            t = k / 4
            col = H(dark) * (1 - t) + H(light) * t
            self.dab(x + math.cos(ang) * L * t * 0.8, y + math.sin(ang) * L * t * 0.8, ang, L * 0.35,
                     Wd * (1 - abs(t - 0.45) * 1.2), col)

    def head(self, x, y, petal, centre, r=17, n=6):
        for k in range(n):
            a = k * 2 * math.pi / n + self.rng.random() * 0.3
            self.dab(x + math.cos(a) * r * 0.6, y + math.sin(a) * r * 0.45, a, r * 0.7, r * 0.75, H(petal), 0.65)
        self.dab(x, y, 0, 0.1, r * 0.55, H(centre), 0.7)

    def fan(self, cx, base, w, h, dark, light, knots):
        for k in range(9):  # radiating ribs
            a = -math.pi / 2 + (k - 4) * 0.17
            L = h * (0.8 + 0.2 * self.rng.random())
            for s in range(18):
                t = s / 18
                self.dab(cx + math.cos(a) * L * t, base + math.sin(a) * L * t, a, L / 14, 4.5,
                         H(dark) * (1 - t) + H(light) * t)
        for _ in range(70):  # lattice knots
            a = -math.pi / 2 + self.rng.normal() * 0.55
            L = h * math.sqrt(self.rng.random()) * 0.9
            self.dab(cx + math.cos(a) * L, base + math.sin(a) * L, 0, 0.1, 5, H(knots), 0.75)

    def tubes(self, cx, base, h, dark, light, rim, n=4):
        for k in range(n):
            x = cx + (k - (n - 1) / 2) * 20 + self.rng.normal() * 3
            L = h * (0.45 + 0.4 * self.rng.random())
            for s in range(12):
                t = s / 12
                self.dab(x, base - t * L, -math.pi / 2, L / 9, 15, H(dark) * (1 - t) + H(light) * t)
            self.dab(x, base - L, 0, 6, 9, H(rim), 0.75)

    def lumps(self, cx, cy, n, r, dark, light, dots=None):
        for _ in range(n):
            x, y = cx + self.rng.normal() * r, cy + self.rng.normal() * r * 1.2
            self.dab(x, y, self.rng.random() * 3, r * 0.7, r * 0.8, H(dark) * 0.5 + H(light) * 0.5, 0.55)
            self.dab(x - 3, y - 4, 0, r * 0.3, r * 0.45, H(light), 0.6)
            if dots:
                self.dab(x + self.rng.normal() * 4, y + self.rng.normal() * 4, 0, 0.1, 4, H(dots), 0.8)


def _paint_cell(zone, name, c, rng, cw, ch):
    b = Brush(c, rng)
    base = ch - 6
    R = rng.normal
    if zone == "ocean":
        if name in ("grass_a", "grass_b", "meadow_tall"):
            n = {"grass_a": 11, "grass_b": 15, "meadow_tall": 9}[name]
            Ln = {"grass_a": (0.6, 0.35), "grass_b": (0.4, 0.3), "meadow_tall": (0.8, 0.18)}[name]
            for _ in range(n):
                b.blade(cw * 0.5 + R() * cw * 0.16, base, ch * (Ln[0] + rng.random() * Ln[1]), 8 + rng.random() * 3,
                        R() * 0.5, "#2f5d3a", "#8fc27a" if name != "meadow_tall" else "#a9c86a", wave=0.8)
        elif name in ("reed", "ivy_strand", "leaf_cluster"):
            k = {"reed": 3, "ivy_strand": 2, "leaf_cluster": 4}[name]
            for i in range(k):
                b.ribbon(cw * (0.3 + 0.4 * i / max(1, k - 1)) + R() * 4, base, ch * (0.7 + 0.25 * rng.random()),
                         16 if name != "ivy_strand" else 12, "#4c5a24", "#b0b84e", amp=9, freq=5 + i)
        elif name == "fern":
            b.fan(cw * 0.5, base, cw, ch * 0.92, "#5d3474", "#b77fd0", "#e6b6ef")
        elif name == "cattail" or name == "twig_dry":
            col = ("#7b3a5a", "#e091b6") if name == "cattail" else ("#7a2a2a", "#e0604e")
            for _ in range(5 if name == "cattail" else 3):
                tx, ty = b.blade(cw * 0.5 + R() * 14, base, ch * (0.55 + 0.35 * rng.random()), 6, R() * 1.0, *col, segs=12)
                if name == "twig_dry":
                    for _ in range(6):
                        b.blade(tx + R() * 10, ty + rng.random() * 60, 30, 3, R() * 2, *col, segs=6)
        elif name == "dry_grass":
            for _ in range(12):
                b.blade(cw * 0.5 + R() * cw * 0.18, base, ch * (0.35 + 0.3 * rng.random()), 10, R() * 1.0,
                        "#5a4a1e", "#b49a48", wave=1.2)
        elif name == "clover":
            for _ in range(8):
                x0, y0 = cw * 0.5 + R() * 26, base - rng.random() * ch * 0.3
                for k in range(3):
                    b.leaf(x0, y0, -math.pi / 2 + (k - 1) * 1.1, 34, 26, "#3f8a4c", "#a6e08a")
        elif name in ("flower_pink", "flower_lilac"):
            petal, stem = ("#f39ac0", "#c45a87") if name == "flower_pink" else ("#cfa6f2", "#7b55a8")
            for k in range(3):
                tx, ty = b.blade(cw * (0.3 + 0.2 * k), base, ch * (0.45 + 0.25 * rng.random()), 6, R() * 0.3,
                                 stem, petal)
                if name == "flower_lilac":
                    for j in range(7):
                        a = j * 2 * math.pi / 7
                        b.blade(tx, ty, 26, 4, math.cos(a) * 2, petal, "#f3e8ff", segs=5)
                else:
                    for j in range(5):
                        b.dab(tx + R() * 3, ty + 6 + j * 7, math.pi / 2, 10, 13 - j, H(petal), 0.65)
        elif name == "flower_blue":
            for k in range(3):
                x0 = cw * (0.28 + 0.22 * k)
                for j in range(10):
                    a = -math.pi / 2 + (j - 4.5) * 0.22
                    b.blade(x0, base, ch * 0.32, 6, a + math.pi / 2, "#2f6fa0", "#9fe1ff", segs=8)
                b.dab(x0, base - 6, 0, 0.1, 12, H("#1f4f78"), 0.7)
        elif name == "flower_gold":
            b.tubes(cw * 0.5, base, ch * 0.85, "#b8862e", "#f2cf62", "#6a4a1a")
        elif name == "shrub_leaf":
            b.lumps(cw * 0.5, ch * 0.6, 22, 18, "#c05a7a", "#f7a6bf", dots="#fff1f5")
    else:  # sky
        if name in ("grass_a", "grass_b", "meadow_tall", "reed", "dry_grass"):
            spec = {"grass_a": (11, 0.55, 0.35, "#4e8a45", "#c9e889"), "grass_b": (15, 0.4, 0.3, "#4a8040", "#b6dc7a"),
                    "meadow_tall": (10, 0.7, 0.25, "#4e8a45", "#dcefa0"), "reed": (8, 0.75, 0.2, "#5d8f52", "#e5f2b6"),
                    "dry_grass": (13, 0.45, 0.3, "#8f9aa8", "#eef2f7")}[name]
            for _ in range(spec[0]):
                tx, ty = b.blade(cw * 0.5 + R() * cw * 0.16, base, ch * (spec[1] + rng.random() * spec[2]),
                                 8 + rng.random() * 3, R() * 0.8, spec[3], spec[4])
                if name == "meadow_tall":
                    b.dab(tx, ty + 6, -math.pi / 2, 5, 6, H("#f4f7e0"), 0.75, op=0.9)
        elif name in ("flower_pink", "flower_blue", "flower_gold", "flower_lilac"):
            petal = {"flower_pink": "#f6a8c8", "flower_blue": "#a9d4ff", "flower_gold": "#ffe07a",
                     "flower_lilac": "#d9c2ff"}[name]
            centre = "#fff4b8" if name != "flower_gold" else "#e9993f"
            for k in range(3):
                x0 = cw * (0.3 + 0.2 * k) + R() * 4
                tx, ty = b.blade(x0, base, ch * (0.5 + 0.25 * rng.random()), 5, R() * 0.4, "#4a8040", "#9cd06a")
                b.leaf(x0, base - ch * 0.15, -math.pi / 2 + (0.9 if k % 2 else -0.9), 34, 14, "#4a8040", "#a6d873")
                if name == "flower_blue":
                    for j in range(3):
                        b.dab(tx + 6, ty + 10 + j * 16, math.pi / 2, 12, 14, H(petal), 0.65)
                else:
                    b.head(tx, ty + 6, petal, centre)
        elif name == "fern":
            for s in range(16):
                t = s / 16
                yy = base - t * ch * 0.85
                xx = cw * 0.5 + math.sin(t * 2.2) * 8
                b.dab(xx, yy, -math.pi / 2, 14, 5, H("#6c9a5a"), 1.0)
                for sgn in (-1, 1):
                    b.leaf(xx, yy, -math.pi / 2 + sgn * (1.15 - t * 0.5), 46 * (1 - t * 0.8), 12 * (1 - t * 0.6),
                           "#7fae6a", "#e6f4d6")
        elif name in ("leaf_cluster", "shrub_leaf"):
            for k in range(11 if name == "leaf_cluster" else 24):
                a = -math.pi / 2 + float(np.clip(R() * 0.8, -1.2, 1.2))
                b.leaf(cw * 0.5 + R() * (6 if name == "leaf_cluster" else 18), ch * 0.6 + R() * (14 if name == "leaf_cluster" else 32),
                       a, 40, 20, "#d98fae" if k % 3 else "#6f9e4a", "#fff0f6" if k % 3 else "#b9dc7d")
        elif name == "ivy_strand":
            for s in range(30):
                t = s / 30
                y = 6 + t * (ch - 12)
                xx = cw * 0.5 + math.sin(t * 9) * 10
                b.dab(xx, y, math.pi / 2, 12, 4, H("#6b7a45"), 1.0)
                if s % 3 == 0:
                    b.leaf(xx, y, math.pi / 2 + (1.2 if s % 6 == 0 else -1.2), 30, 20, "#5f9a4a",
                           "#f3b9d0" if s % 9 == 0 else "#b4dc7a")
        elif name == "cattail":
            for k in range(5):
                tx, ty = b.blade(cw * 0.5 + R() * 16, base, ch * (0.7 + 0.2 * rng.random()), 5, R() * 0.2,
                                 "#8a8f7a", "#e2e6d2")
                b.dab(tx, ty + 12, math.pi / 2, 18, 8, H("#d8b45e"), 0.8)
        elif name == "clover":
            for _ in range(7):
                x0, y0 = cw * 0.5 + R() * 24, base - rng.random() * ch * 0.35
                for k in range(3):
                    b.leaf(x0, y0, -math.pi / 2 + (k - 1) * 1.2, 26, 20, "#7fae6a", "#e8f3e0")
                if rng.random() < 0.6:
                    b.dab(x0, y0 - 20, 0, 0.1, 13, H("#ffffff"), 0.7)
        elif name == "twig_dry":
            for _ in range(5):
                tx, ty = b.blade(cw * 0.5 + R() * 10, base, ch * (0.5 + 0.3 * rng.random()), 5, R() * 1.4,
                                 "#8a6a2a", "#f2d27a", segs=10)
                for _ in range(3):
                    b.leaf(tx + R() * 8, ty + rng.random() * 40, rng.random() * 6.28, 16, 9, "#d8b45e", "#fff2c2")


def build_foliage(zone, seed):
    rng = np.random.default_rng(seed)
    W, Hh = atlas.FOLIAGE_SIZE
    cols, rows = atlas.FOLIAGE_GRID
    cw, ch = W // cols, Hh // rows
    out = np.zeros((Hh, W, 4), np.float32)
    for i, name in enumerate(atlas.FOLIAGE_CELLS):
        c = pt.Canvas(cw, ch, rgb=(0.3, 0.5, 0.4), alpha=0.0)
        _paint_cell(zone, name, c, rng, cw, ch)
        col, row = i % cols, i // cols
        a = c.a
        if (a > 0.5).any():
            mean = c.rgb[a > 0.5].mean(axis=0)
            c.rgb = np.where((a > 0.02)[..., None], c.rgb, mean)
        out[row * ch:(row + 1) * ch, col * cw:(col + 1) * cw, :3] = c.rgb
        out[row * ch:(row + 1) * ch, col * cw:(col + 1) * cw, 3] = c.a
    return (np.clip(out, 0, 1) * 255 + .5).astype(np.uint8)


# ------------------------------------------------------------- tiling maps
def build_ground(zone, seed, size=512):
    rng = np.random.default_rng(seed)
    if zone == "ocean":
        field, _ = pt.painted_field(size, rng, H("#a8966f"), H("#e6d5a6"), base=3, contrast=1.1)
        c = pt.Canvas(size, size)
        c.rgb[:] = field
        yy = np.mgrid[0:size, 0:size][0].astype(np.float32)
        warp = pt.fbm(size, rng, base=4) * 40
        rip = (np.sin((yy + warp) * 2 * math.pi / 32) * 0.5 + 0.5) ** 3
        c.rgb = c.rgb * (1 - rip[..., None] * 0.16) + H("#f4e9c6") * rip[..., None] * 0.1
        pt.strokes(c, rng, field, 1800, 0.05, 0.25, 16, 5, 0.4, tint=0.08, bristle=0.1)
        for _ in range(60):
            colr = [H("#f6efe2"), H("#e9b6c4"), H("#cfe6e8"), H("#8a7a5a")][int(rng.integers(4))]
            c.stamp(rng.random() * size, rng.random() * size, rng.random() * 3, 4, 4.5, colr, 0.85, rng=rng,
                    bristle=0, hardness=0.7)
        return c.to_uint8()
    field, _ = pt.painted_field(size, rng, H("#6a9a4a"), H("#c4df7c"), base=3, contrast=1.3)
    c = pt.Canvas(size, size)
    c.rgb[:] = field
    pt.strokes(c, rng, field, 2600, -1.2, 0.5, 14, 5, 0.5, tint=0.1, bristle=0.1)
    pt.strokes(c, rng, field, 500, -1.2, 0.5, 10, 4, 0.5, palette=[H("#dcefa0"), H("#cfe7a0")], bristle=0)
    pt.strokes(c, rng, field, 400, -1.2, 0.5, 10, 4, 0.45, palette=[H("#4f7f3c")], bristle=0)
    for _ in range(90):
        colr = [H("#ffd6e6"), H("#ffffff"), H("#d9e8ff"), H("#fff1b0")][int(rng.integers(4))]
        c.stamp(rng.random() * size, rng.random() * size, 0, 0.1, 4.5, colr, 0.9, rng=rng, bristle=0, hardness=0.7)
    return c.to_uint8()


def build_cloud_sea(seed, size=256):
    rng = np.random.default_rng(seed)
    field, n = pt.painted_field(size, rng, H("#b7c3e6"), H("#ffffff"), base=2, contrast=1.5, bands=4)
    c = pt.Canvas(size, size)
    c.rgb[:] = field
    for _ in range(160):
        c.stamp(rng.random() * size, rng.random() * size, rng.random() * 3, 26, 24,
                [H("#ffffff"), H("#f2ecff"), H("#dde6fb")][int(rng.integers(3))], 0.35, rng=rng, bristle=0,
                hardness=0.15)
    return c.to_uint8()


# --------------------------------------------------------------------- FX
def _r(size):
    cy = (size[0] - 1) / 2.0
    cx = (size[1] - 1) / 2.0
    y, x = np.mgrid[0:size[0], 0:size[1]]
    return (x - cx) / cx, (y - cy) / cy


def fx_rgba(alpha, rgb=(255, 255, 255)):
    out = np.zeros(alpha.shape + (4,), np.uint8)
    out[..., :3] = rgb
    out[..., 3] = np.round(np.clip(alpha, 0, 1) * 255).astype(np.uint8)
    return out


def fx_caustics(seed=31, size=256, supersample=4, depth=0.08, waves=40, kmin=4, kmax=8, amp=0.012):
    """Seamless caustic web, photon traced (VIS.4b follow-up).

    A periodic wave height field (integer wave vectors, so it tiles) refracts a uniform grid of
    light rays onto a flat floor `depth` below; rays are binned with wrap-around. Bright lines
    are where the surface focuses light, like real pool / shallow-sea caustics.
    Wave numbers 4..8 with 40 waves: an irregular web. The caustics shader samples it at
    _TexScale 0.5, so on screen the cell size matches the sparser look the owner chose while the
    pattern repeats only every 2 material tiles.
    """
    rng = np.random.default_rng(seed)
    n = size * supersample
    y, x = np.meshgrid(np.arange(n) / n, np.arange(n) / n, indexing="ij")
    gx = np.zeros_like(x)
    gy = np.zeros_like(x)
    for _ in range(waves):
        kx, ky = 0, 0
        while np.hypot(kx, ky) < kmin:
            kx, ky = rng.integers(-kmax, kmax + 1, 2)
        k = np.hypot(kx, ky)
        a = amp / k * rng.uniform(0.5, 1.0)
        arg = 2 * np.pi * (kx * x + ky * y) + rng.uniform(0, 2 * np.pi)
        gx += a * 2 * np.pi * kx * np.cos(arg)
        gy += a * 2 * np.pi * ky * np.cos(arg)
    px = ((x - depth * gx) % 1.0 * size).astype(int) % size
    py = ((y - depth * gy) % 1.0 * size).astype(int) % size
    light = np.bincount((py * size + px).ravel(), minlength=size * size).reshape(size, size)
    light = light / float(supersample * supersample)
    light = (light + np.roll(light, 1, 0) + np.roll(light, -1, 0) + np.roll(light, 1, 1) + np.roll(light, -1, 1)) / 5
    return fx_rgba(np.clip((light - 0.7) / 1.6, 0, 1) ** 0.9)


def fx_light_shaft(h=256, w=64):
    x, y = _r((h, w))
    across = np.exp(-(x / 0.42) ** 2) * (0.75 + 0.25 * np.cos(x * 3.0))
    along = np.clip(1 - (y + 1) / 2, 0, 1) ** 0.7 * np.clip((y + 1) / 0.25, 0, 1)  # bright at top, soft ends
    return fx_rgba(across * along)


def fx_bubble(size=64):
    x, y = _r((size, size))
    r = np.hypot(x, y)
    ring = np.exp(-((r - 0.78) / 0.09) ** 2) + 0.18 * (r < 0.8)
    hl = np.exp(-(((x + 0.32) ** 2 + (y + 0.32) ** 2) / 0.02))
    a = np.clip(ring + hl, 0, 1)
    a[r > 0.98] = 0
    return fx_rgba(a)


def fx_cloud_wisp(seed=12, size=128):
    rng = np.random.default_rng(seed)
    x, y = _r((size, size))
    blob = np.exp(-((x / 0.8) ** 2 + (y / 0.55) ** 2) * 2.2)
    n = pt.fbm(size, rng, base=3, octaves=3)
    return fx_rgba(np.clip(blob * (0.55 + 0.9 * n) - 0.12, 0, 1))


def fx_petal(size=64):
    x, y = _r((size, size))
    a = np.clip(1 - ((x / 0.5) ** 2 + (y / 0.85) ** 2), 0, 1) ** 0.6
    a *= np.clip(1 - np.exp(-((x) ** 2) / 0.004) * (y < -0.6), 0, 1)  # notch at the tip
    return fx_rgba(a)


# --------------------------------------------------------------------- io
SEEDS = {"ocean": (2401, 2402, 2403, 2404), "sky": (3401, 3402, 3403, 3404)}


def generate_zone(zone, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    s_atlas, s_fol, s_ground, s_water = SEEDS[zone]
    Z = zone.capitalize()
    props, emission = build_atlas(zone, s_atlas)
    props = box_downscale(props, OUTPUT_SIZE["PropsAtlas"])
    emission = box_downscale(emission, OUTPUT_SIZE["PropsEmission"])
    files = {
        f"T_ENV_{Z}_PropsAtlas.png": props,
        f"T_ENV_{Z}_PropsEmission.png": emission,
        f"T_ENV_{Z}_Foliage.png": build_foliage(zone, s_fol),
        f"T_ENV_{Z}_Ground.png": build_ground(zone, s_ground),
    }
    if zone == "ocean":
        files.update({"T_ENV_FX_Caustics.png": fx_caustics(), "T_ENV_FX_LightShaft.png": fx_light_shaft(),
                      "T_ENV_FX_Bubble.png": fx_bubble()})
    else:
        files.update({"T_ENV_Sky_Water.png": build_cloud_sea(s_water), "T_ENV_FX_CloudWisp.png": fx_cloud_wisp(),
                      "T_ENV_FX_Petal.png": fx_petal()})
    for name, img in files.items():
        write_png(os.path.join(out_dir, name), img)
    return {n: os.path.join(out_dir, n) for n in files}


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    zones = sys.argv[2:] or ["ocean", "sky"]
    for z in zones:
        for n, p in generate_zone(z, os.path.join(out, z)).items():
            print("wrote", p)
