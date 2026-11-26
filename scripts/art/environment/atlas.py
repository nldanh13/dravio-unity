"""Shared texture-atlas layout for the Origin Sanctuary environment kit.

Every opaque prop uses ONE material (ENV_Props) whose base map is a 4x4 painterly
atlas. Geometry zones are unwrapped into the matching tile, so the whole kit can be
static-batched / SRP-batched with a single material on mobile.

Tile order is row-major from the TOP-LEFT of the image (row 0 = top).
"""

PROPS_ATLAS_SIZE = 1024
PROPS_GRID = 4
# Inset (in pixels of the full atlas) kept free inside each tile so mipmaps do not
# bleed neighbouring tiles. 12 px at 1024 keeps tiles clean down to mip 3.
PROPS_TILE_INSET_PX = 12

PROPS_TILES = [
    "stone_warm",      # 0  sun-bleached sandstone (columns, gate pillars)
    "stone_cool",      # 1  grey-green boulder stone (rocks, pond rim)
    "stone_blocks",    # 2  dressed masonry with mortar lines (walls, arch)
    "moss",            # 3  painterly moss caps
    "bark",            # 4  old trunk bark
    "root",            # 5  lighter exposed roots / vines
    "leaf_canopy",     # 6  canopy foliage blobs
    "statue_stone",    # 7  weathered teal-grey statue stone
    "mushroom_cap",    # 8  warm red-orange caps with cream spots
    "mushroom_stem",   # 9  cream stems / gills
    "rune_slate",      # 10 dark slate for rune stones
    "rune_glyph",      # 11 glowing glyph strokes (also the emission tile)
    "earth",           # 12 packed earth / pond basin
    "lily",            # 13 lily pads & small leaves
    "gold_trim",       # 14 aged bronze-gold inlay
    "glow_cap",        # 15 bioluminescent mushroom cap (emissive)
]

# Tiles that light up in the emission map.
EMISSIVE_TILES = {"rune_glyph": (0.55, 0.95, 1.0), "glow_cap": (0.45, 0.85, 1.0)}


def tile_index(name):
    return PROPS_TILES.index(name)


def tile_uv_rect(name, size=PROPS_ATLAS_SIZE, grid=PROPS_GRID, inset_px=PROPS_TILE_INSET_PX):
    """Return (u0, v0, u1, v1) in UV space (v=0 bottom) for a tile, with inset."""
    i = tile_index(name)
    col, row = i % grid, i // grid
    t = 1.0 / grid
    inset = inset_px / size
    u0 = col * t + inset
    u1 = (col + 1) * t - inset
    v1 = 1.0 - row * t - inset
    v0 = 1.0 - (row + 1) * t + inset
    return u0, v0, u1, v1


# Foliage cards: one alpha-clipped material (ENV_Foliage), 8 columns x 2 rows of
# tall 128x256 cells in a 1024x512 texture.
FOLIAGE_SIZE = (1024, 512)
FOLIAGE_GRID = (8, 2)
FOLIAGE_CELLS = [
    "grass_a", "grass_b", "flower_pink", "flower_blue",
    "flower_gold", "fern", "leaf_cluster", "ivy_strand",
    "reed", "cattail", "dry_grass", "meadow_tall",
    "clover", "flower_lilac", "shrub_leaf", "twig_dry",
]
FOLIAGE_INSET_PX = 4


def foliage_uv_rect(name):
    w, h = FOLIAGE_SIZE
    cols, rows = FOLIAGE_GRID
    i = FOLIAGE_CELLS.index(name)
    col, row = i % cols, i // cols
    tu, tv = 1.0 / cols, 1.0 / rows
    iu, iv = FOLIAGE_INSET_PX / w, FOLIAGE_INSET_PX / h
    return (col * tu + iu, 1.0 - (row + 1) * tv + iv, (col + 1) * tu - iu, 1.0 - row * tv - iv)
