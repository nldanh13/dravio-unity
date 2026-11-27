"""VIS.4b (Issue #91) - VIS.10 butterfly GLB checks + CPU emulation of "Painterly Flow" in UV space.

  python3 scripts/qa/vis4b/check_vis10_butterfly_and_flow.py --out <dir>

Butterfly: clip names, keys, loop endpoints equal, unit quaternions, degenerate faces.
Flow: re-implements the waterfall (silk + foam flipbook band + side fade) and stream sampling of
DravioPainterlyFlow.shader with nearest sampling, renders 4 time steps over a flat green backdrop.
This is a math check of UV / flipbook indexing, not a Unity render. Exit 1 on butterfly failure."""
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image
sys.path.insert(0, str(Path(__file__).parent))
from check_vis10_wind import load, tri_geom

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(sys.argv[sys.argv.index("--out") + 1]); OUT.mkdir(parents=True, exist_ok=True)
fails = []

j, acc = load(str(ROOT / "ArtSource/Origin_VIS10_Butterfly/Origin_Butterfly_Amber.glb"))
rep = {"animations": [a.get("name") for a in j.get("animations", [])], "channels": {}}
for a in j.get("animations", []):
    for ch in a["channels"]:
        sm = a["samplers"][ch["sampler"]]; t = acc(sm["input"])[0].ravel(); v = acc(sm["output"])[0]
        key = f"{j['nodes'][ch['target']['node']].get('name')}.{ch['target']['path']}"
        loop = bool(np.allclose(v[0], v[-1], atol=1e-6))
        unit = bool(np.allclose(np.linalg.norm(v, axis=1), 1, atol=1e-4)) if v.shape[1] == 4 else None
        rep["channels"][key] = {"keys": len(t), "t": [float(t[0]), float(t[-1])], "loop_equal": loop,
                                "unit_quat": unit, "interp": sm.get("interpolation", "LINEAR")}
        if not loop: fails.append(f"{key} loop endpoints differ")
        if unit is False: fails.append(f"{key} non-unit quaternion")
tris = deg = 0
for m in j["meshes"]:
    for pr in m["primitives"]:
        P = acc(pr["attributes"]["POSITION"])[0]
        I = (acc(pr["indices"])[0] if "indices" in pr else np.arange(len(P))[:, None]).reshape(-1, 3).astype(int)
        tris += len(I); deg += int((tri_geom(P, I)[1] < 1e-12).sum())
rep.update(tris=tris, degenerate=deg, nodes=[n.get("name") for n in j["nodes"]],
           materials=[m.get("name") for m in j.get("materials", [])], textures=len(j.get("images", [])))
if deg: fails.append("degenerate faces")
rep["fails"] = fails
print(json.dumps(rep, indent=1))
(OUT / "vis10_butterfly_check.json").write_text(json.dumps(rep, indent=1))


def tex(p): return np.array(Image.open(ROOT / p).convert("RGBA")).astype(float) / 255


silk = tex("ArtSource/Origin_VIS9_Water/T_VIS9_Waterfall_Silk.png")
rip = tex("ArtSource/Origin_VIS9_Water/T_VIS9_Stream_Curved_Ripples.png")
foam = tex("ArtSource/Origin_VIS9_Water/T_VIS9_Foam_Loop_16.png")


def samp(T, u, v, clamp=False):
    h, w, _ = T.shape
    u, v = (np.clip(u, 0, 1), np.clip(v, 0, 1)) if clamp else (u % 1, v % 1)
    x = np.clip((u * w).astype(int), 0, w - 1); y = np.clip(((1 - v) * h).astype(int), 0, h - 1)  # Unity v=1 = image top
    return T[y, x]


def waterfall(t, W=192, H=384, band=0.22, tiles=3, fps=16, speed=0.9, edge=0.08):
    u, v = np.meshgrid((np.arange(W) + .5) / W, (np.arange(H)[::-1] + .5) / H)
    c = samp(silk, u, v + t * speed)
    across = u * tiles; tile = np.floor(across); frame = np.floor(np.fmod(t * fps + tile * 5, 16))
    col = np.fmod(frame, 4); row = np.floor(frame / 4)
    su = (col + across % 1) * .25; sv = 1 - (row + 1 - np.clip(v / band, 0, 1)) * .25
    f = samp(foam, su, sv, True); m = (v < band)[..., None]
    rgb = np.where(m, c[..., :3] * (1 - f[..., 3:]) + f[..., :3] * f[..., 3:], c[..., :3])
    a = np.where(m[..., 0], np.maximum(c[..., 3], f[..., 3]), c[..., 3])
    e = np.clip(u / edge, 0, 1) * np.clip((1 - u) / edge, 0, 1); e = e * e * (3 - 2 * e)
    return rgb, a * e


bg = np.array([0.42, 0.55, 0.38])
pad = lambda x: np.pad(x, ((4, 4), (4, 4), (0, 0)), constant_values=1)
frames = []
for t in (0, 0.25, 0.5, 0.75):
    rgb, a = waterfall(t); frames.append(pad(rgb * a[..., None] + bg * (1 - a[..., None])))
Image.fromarray((np.concatenate(frames, 1) * 255).astype(np.uint8)).save(OUT / "flow_waterfall_uvspace_t0-0.75.png")
u, v = np.meshgrid((np.arange(128) + .5) / 128, np.linspace(1, -1, 512))
s = [samp(rip, u, v + t * 0.22) for t in (0, 1)]
Image.fromarray((np.concatenate([pad(x[..., :3] * x[..., 3:] + bg * (1 - x[..., 3:])) for x in s], 1) * 255)
                .astype(np.uint8)).save(OUT / "flow_stream_uvspace_t0-1.png")
sys.exit(1 if fails else 0)
