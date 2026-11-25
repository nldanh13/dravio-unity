"""Tiny particle textures for the living environment (U1-ENV.3).

    python scripts/art/environment/fx_textures.py <out_dir>

T_ENV_FX_SoftDot.png  64x64  white, radial soft alpha (fireflies, dust, sparkles, petals)
T_ENV_FX_Ring.png     64x64  white ring with soft edges (pond ripple rings)
Both are white so the particle colour does all the tinting, and every ambient and
burst particle shares the soft dot.
"""
import os
import sys

import numpy as np

from png_io import write_png

SIZE = 64


def _radius():
    c = (SIZE - 1) / 2.0
    y, x = np.mgrid[0:SIZE, 0:SIZE]
    return np.hypot(x - c, y - c) / c


def soft_dot():
    r = _radius()
    core = np.clip(1.0 - r, 0, 1) ** 2.2
    halo = np.exp(-(r / 0.22) ** 2)
    return np.clip(0.55 * core + 0.6 * halo, 0, 1)


def ring():
    r = _radius()
    a = np.exp(-((r - 0.78) / 0.075) ** 2)
    a += 0.25 * np.exp(-((r - 0.6) / 0.05) ** 2)
    a[r > 0.98] = 0
    return np.clip(a, 0, 1)


def write(path, alpha):
    rgba = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.round(alpha * 255).astype(np.uint8)
    write_png(path, rgba)


def generate_all(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    out = {}
    for name, fn in (("T_ENV_FX_SoftDot.png", soft_dot), ("T_ENV_FX_Ring.png", ring)):
        p = os.path.join(out_dir, name)
        write(p, fn())
        out[name] = p
    return out


if __name__ == "__main__":
    print(generate_all(sys.argv[1] if len(sys.argv) > 1 else "."))
