"""Draw habitats, spawn points, transitions and the right-hand UI panel over the
Blender preview renders, to review landscape framing without Unity.

    python scripts/art/environment/preview_overlay.py <preview_16x9.png> <preview_20x9.png> ...

Needs Pillow. Writes <name>_overlay.png next to each input. The aspect is read from
the image size.
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import layout  # noqa: E402

COLORS = {"origin_meadow": (120, 240, 90), "origin_pondbank": (90, 200, 255), "origin_warm_rocks": (255, 165, 70)}


def overlay(path):
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    aspect = w / h
    loc = layout.build_locations(layout.build_layout()["placements"])
    top = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(top)

    def px(p):
        vx, vy = layout.to_screen(p, aspect)
        return vx * w, (1 - vy) * h

    x0, y0, x1, y1 = layout.free_viewport(aspect)
    d.rectangle([x1 * w, 0, w, h], fill=(40, 28, 18, 150))
    d.text((x1 * w + 12, 12), "Fantasy UI panel (reserved)", fill=(255, 230, 180, 255))
    d.rectangle([x0 * w, (1 - y1) * h, x1 * w, (1 - y0) * h], outline=(255, 255, 255, 120), width=1)
    for hab in loc["habitats"]:
        xz = hab["polygonXZ"]
        pts = [px((xz[i], 0, xz[i + 1])) for i in range(0, len(xz), 2)]
        c = COLORS[hab["id"]]
        d.polygon(pts, fill=c + (38,), outline=c + (255,))
        cx, cz = hab["centerXZ"]
        d.text(px((cx, 0, cz - 1.0)), hab["id"], fill=c + (255,))
        if hab["swimPolygonXZ"]:
            sw = hab["swimPolygonXZ"]
            d.polygon([px((sw[i], 0, sw[i + 1])) for i in range(0, len(sw), 2)], outline=(60, 140, 255, 255))
    for o in loc["obstacles"]:
        xz = o["polygonXZ"]
        d.polygon([px((xz[i], 0, xz[i + 1])) for i in range(0, len(xz), 2)], outline=(255, 70, 60, 200))
    for s in loc["spawnPoints"]:
        x, y = px((s["x"], 0, s["z"]))
        d.ellipse([x - 6, y - 4, x + 6, y + 4], fill=COLORS[s["habitatId"]] + (255,), outline=(0, 0, 0, 255))
    for t in loc["transitions"]:
        x, y = px((t["x"], 0, t["z"]))
        d.ellipse([x - 11, y - 7, x + 11, y + 7], outline=(255, 235, 80, 255), width=3)
    for p in loc["perchPoints"]:
        x, y = px((p["x"], p["y"], p["z"]))
        d.rectangle([x - 5, y - 5, x + 5, y + 5], outline=(255, 140, 40, 255), width=2)
    out = path.replace(".png", "_overlay.png")
    Image.alpha_composite(img, top).convert("RGB").save(out)
    return out


if __name__ == "__main__":
    for p in sys.argv[1:]:
        print("wrote", overlay(p))
