"""CPU emulation of Dravio/Environment/Caustics vs the old single-layer particle quad (Ocean).

  python3 scripts/qa/vis4b/preview_caustics.py --old <old caustics png> --out <png>

Additive over T_ENV_Ocean_Ground at the in-game strength (_BaseColor.a 0.11 from OceanLiving fx),
4 time steps, ~11 m of seabed (2.5 tiles at tiling 5 on the 22 m quad). Bilinear sampling, mip 5
approximated by 32x32 averages. A math preview, not a Unity render."""
import sys
import numpy as np
from PIL import Image

ROOT = __import__("pathlib").Path(__file__).resolve().parents[3]
TEX = ROOT / "Assets/Dravio/Environment/Zones/Ocean/Textures"
old_path = sys.argv[sys.argv.index("--old") + 1]
out = sys.argv[sys.argv.index("--out") + 1]
alpha = lambda p: np.array(Image.open(p))[..., 3].astype(float) / 255
NEW, OLD = alpha(TEX / "T_ENV_FX_Caustics.png"), alpha(old_path)
G = np.array(Image.open(TEX / "T_ENV_Ocean_Ground.png").convert("RGB")).astype(float) / 255
MIP5 = NEW.reshape(8, 32, 8, 32).mean((1, 3))


def bil(M, u, v):
    h, w = M.shape
    x = (u % 1) * w - 0.5; y = (v % 1) * h - 0.5
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int); fx = x - x0; fy = y - y0
    g = lambda yy, xx: M[yy % h, xx % w]
    return (g(y0, x0) * (1 - fx) + g(y0, x0 + 1) * fx) * (1 - fy) + (g(y0 + 1, x0) * (1 - fx) + g(y0 + 1, x0 + 1) * fx) * fy


N = 320
u, v = np.meshgrid(np.arange(N) / N * 2.5, np.arange(N) / N * 2.5)
scrollA = np.array([0.018, 0.011])  # OceanLiving fx scroll (EnvironmentScrollingFx offset)
tint, strength = np.array([0.8, 1, 1]), 0.11



def new(t, warp=0.05, mix=0.35, patches=0.5, rng=(0.07, 0.40), intensity=3.0):
    noise = lambda uu, vv: np.clip((bil(MIP5, uu, vv) - rng[0]) / (rng[1] - rng[0]), 0, 1)
    n1 = noise(u * 0.37 + 0.031 * t, v * 0.37 - 0.019 * t)
    n2 = noise(u * 0.29 + 0.41 - 0.023 * t, v * 0.29 + 0.17 + 0.027 * t)
    ua = u + (n1 - n2) * warp; va = v + (n1 + n2 - 1) * warp
    c, s = np.cos(0.52), np.sin(0.52)
    bu = ua * 1.31 + 0.37; bv = va * 1.31 + 0.21
    a = bil(NEW, ua + scrollA[0] * t, va + scrollA[1] * t)
    b = bil(NEW, bu * c - bv * s - 0.015 * t, bu * s + bv * c + 0.011 * t)
    k = np.clip(a * (1 - mix) + np.minimum(a, b) * mix * 2, 0, 1)
    m = np.clip((n1 * n2 - 0.10) / 0.40, 0, 1); m = 1 + (m * m * (3 - 2 * m) - 1) * patches
    return np.clip(k * m * intensity, 0, 1 / strength)


gb = G[np.arange(N) % G.shape[0]][:, np.arange(N) % G.shape[1]]
pad = lambda x: np.pad(x, ((4, 4), (4, 4), (0, 0)), constant_values=1)
rows = [np.concatenate([pad(np.clip(gb + f(t)[..., None] * tint * strength, 0, 1)) for t in (0, 2, 4, 6)], 1)
        for f in (lambda t: bil(OLD, u + scrollA[0] * t, v + scrollA[1] * t), new)]
Image.fromarray((np.concatenate(rows, 0) * 255).astype(np.uint8)).save(out)
print("wrote", out)
