"""CHAR.9c — does the animated dragon pass through scene obstacles or other dragons at the distances the runtime allows?

  python scripts/art/character/measure_contact_clearance.py <contract.fbx> <SanctuaryLocations.asset> <out.json>
      [--clips-json <contract_rig.json>] [--png <map.png>] [--spacing 0.55] [--grid 0.1]
  (bpy 5.3 module, or blender --background --python <this> -- <args>; Pillow only for --png)

1. Swept reach (FBX imported into an empty scene, linear skinning, EVERY frame): per clip, the root-relative
   horizontal reach of the skinned mesh in 360 one-degree directions (0 deg = the dragon's facing, measured from
   Pelvis -> Head at rest) plus front / back / side / height. The root is the dragon's world position at runtime.
2. Scene (real navigation data of that zone, read from the Unity asset, not re-authored): every point of a
   grid that the ground navigator accepts (inside a habitat polygon, outside water, farther than walkClearance
   from every obstacle polygon - SanctuaryLocationData.IsWalkable) is a place the dragon may stand. For each
   point, each of 360 yaws and each clip: depth = max over directions of (reach - distance to the nearest obstacle
   edge along that ray). Obstacles are full-height prisms (top 0), as the navigator treats them.
3. Dragon vs dragon: two residents at the separation the runtime enforces (root to root, default 0.55 m,
   LivingSanctuaryPresentation.Separated) facing any way: overlap = reachA(towards B) + reachB(towards A) - spacing.
Nothing is written to the FBX or the asset. Numbers are geometry only: Unity physics, camera framing and the
obstacle meshes themselves (the navigator only knows their footprint polygons) are not simulated.
"""
import argparse
import json
import math
import re
import sys
from pathlib import Path

import numpy as np

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
ap = argparse.ArgumentParser()
ap.add_argument('fbx', type=Path)
ap.add_argument('locations', type=Path)
ap.add_argument('out_json', type=Path)
ap.add_argument('--png', type=Path)
ap.add_argument('--spacing', type=float, default=0.55)
ap.add_argument('--grid', type=float, default=0.1)
A = ap.parse_args(argv)
CLIPS = [('Idle', 1, 181), ('Walk', 182, 217), ('Eat', 218, 278), ('Doze', 279, 399), ('Happy', 400, 445)]
NB = 360


# ------------------------------------------------------------------ 1. swept reach from the FBX
def swept_reach():
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(A.fbx))
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
    o = [x for x in bpy.data.objects if x.type == 'MESH' and x.find_armature() == arm][0]
    for m in o.modifiers:
        if m.type == 'ARMATURE':
            m.use_deform_preserve_volume = False
    act = arm.animation_data.action
    off = int(round(act.frame_range[0])) - 1
    root = np.array((arm.matrix_world @ arm.pose.bones['Root'].head)[:]) if 'Root' in arm.pose.bones else np.zeros(3)
    pel = np.array((arm.matrix_world @ arm.data.bones['Pelvis'].head_local)[:])
    head = np.array((arm.matrix_world @ arm.data.bones['Head'].head_local)[:])
    fwd = (head - pel)[:2]
    fwd /= np.linalg.norm(fwd)
    side = np.array([-fwd[1], fwd[0]])
    dg = bpy.context.evaluated_depsgraph_get()
    sc = bpy.context.scene
    MW = np.array(o.matrix_world)
    out = {}
    for name, a, b in CLIPS:
        reach = np.zeros(NB)
        ext = dict(front=0.0, back=0.0, side=0.0, height=0.0)
        worst = {}
        for f in range(a, b + 1):
            sc.frame_set(f + off)
            ev = o.evaluated_get(dg)
            m = ev.to_mesh()
            P = np.empty(len(m.vertices) * 3)
            m.vertices.foreach_get('co', P)
            ev.to_mesh_clear()
            P = P.reshape(-1, 3) @ MW[:3, :3].T + MW[:3, 3]
            rel = P[:, :2] - root[:2]
            u, v = rel @ fwd, rel @ side
            ang = (np.degrees(np.arctan2(v, u)) % 360).astype(int) % NB
            r = np.hypot(u, v)
            np.maximum.at(reach, ang, r)
            ext['front'] = max(ext['front'], float(u.max()))
            ext['back'] = max(ext['back'], float(-u.min()))
            ext['side'] = max(ext['side'], float(np.abs(v).max()))
            ext['height'] = max(ext['height'], float(P[:, 2].max()))
            k = int(np.argmax(r))
            if r[k] > worst.get('r', 0):
                worst = {'r': float(r[k]), 'frame': f}
        # a bin with no vertex inherits its neighbours (thin mesh), never shrinks the reach
        for _ in range(3):
            reach = np.maximum(reach, np.maximum(np.roll(reach, 1), np.roll(reach, -1)) * (reach == 0))
        out[name] = {'reach': reach, 'extent_m': {k: round(v, 3) for k, v in ext.items()},
                     'maxRadius_m': round(float(reach.max()), 3), 'maxRadiusFrame': worst.get('frame')}
    return out


# ------------------------------------------------------------------ 2. navigation data from the Unity asset
def load_locations(p):
    import yaml
    t = p.read_text(encoding='utf-8').split('\n', 3)[3]
    t = re.sub(r'^--- !u!.*\n', '', t, flags=re.M)
    d = yaml.safe_load(t)['MonoBehaviour']
    poly = lambda f: np.array(f, float).reshape(-1, 2)
    return {'walkClearance': float(d['walkClearance']), 'flatRect': d['flatRectXZ'],
            'habitats': [(h['id'], poly(h['polygonXZ'])) for h in d['habitats']],
            'water': [poly(w['polygonXZ']) for w in (d.get('waterAreas') or []) if w.get('polygonXZ')],
            'obstacles': [(o['source'], poly(o['polygonXZ'])) for o in d['obstacles']],
            'spawns': [(s['id'], float(s['x']), float(s['z'])) for s in d['spawnPoints']]}


def inside(poly, x, z):
    c = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, zi = poly[i]
        xj, zj = poly[j]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / (zj - zi + 1e-12) + xi:
            c = not c
        j = i
    return c


def edge_distance(poly, x, z):
    a, b = poly, np.roll(poly, -1, 0)
    ab = b - a
    t = np.clip(((x - a[:, 0]) * ab[:, 0] + (z - a[:, 1]) * ab[:, 1]) / np.maximum((ab ** 2).sum(1), 1e-12), 0, 1)
    q = a + t[:, None] * ab
    return float(np.hypot(q[:, 0] - x, q[:, 1] - z).min())


def ray_distances(segs, x, z, dirs):
    """Distance from (x, z) along each unit direction to the first obstacle edge (inf if none)."""
    a, b = segs[:, 0], segs[:, 1]
    e = b - a                                   # (S,2)
    w = a - np.array([x, z])                    # (S,2)
    dx, dz = dirs[:, 0][:, None], dirs[:, 1][:, None]   # (D,1)
    den = dx * e[None, :, 1] - dz * e[None, :, 0]       # (D,S)
    with np.errstate(divide='ignore', invalid='ignore'):
        t = (w[None, :, 0] * e[None, :, 1] - w[None, :, 1] * e[None, :, 0]) / den
        s = (w[None, :, 0] * dz - w[None, :, 1] * dx) / den
    ok = (np.abs(den) > 1e-12) & (t >= 0) & (s >= 0) & (s <= 1)
    t = np.where(ok, t, np.inf)
    return t.min(1)


R = swept_reach()
L = load_locations(A.locations)
segs = np.concatenate([np.stack([p, np.roll(p, -1, 0)], 1) for _, p in L['obstacles']])
th = np.radians(np.arange(NB))
dirs = np.stack([np.cos(th), np.sin(th)], 1)
x0, z0, x1, z1 = L['flatRect']
pts = []
for x in np.arange(x0, x1 + 1e-9, A.grid):
    for z in np.arange(z0, z1 + 1e-9, A.grid):
        hab = next((h for h, p in L['habitats'] if inside(p, x, z)), None)
        if hab is None or any(inside(w, x, z) for w in L['water']):
            continue
        if any(inside(p, x, z) or edge_distance(p, x, z) < L['walkClearance'] for _, p in L['obstacles']):
            continue
        pts.append((round(float(x), 3), round(float(z), 3), hab))

scene = {}
per_point = np.zeros(len(pts))
idx = (np.arange(NB)[None, :] + np.arange(NB)[:, None]) % NB   # (yaw, j): dragon direction j looks along world j + yaw
depth = {n: np.zeros(len(pts)) for n, *_ in CLIPS}
worst = {n: (0.0, None, None, None) for n, *_ in CLIPS}
for i, (x, z, _) in enumerate(pts):
    d = ray_distances(segs, x, z, dirs)[idx]           # (yaw, j)
    for name, *_ in CLIPS:
        dep = (R[name]['reach'][None, :] - d).max(1)   # per yaw
        v = max(0.0, float(dep.max()))
        depth[name][i] = v
        if v > worst[name][0]:
            worst[name] = (v, x, z, int(np.argmax(dep)))
for name, *_ in CLIPS:
    per_point = np.maximum(per_point, depth[name])
    w = worst[name]
    scene[name] = {'walkablePoints': len(pts),
                   'pointsWithPenetration>1cm_share': round(float((depth[name] > 0.01).mean()), 4),
                   'pointsWithPenetration>10cm_share': round(float((depth[name] > 0.10).mean()), 4),
                   'maxDepth_m': round(w[0], 3), 'worstPoint': [w[1], w[2]], 'worstYawDeg': w[3]}
pair = {}
for na, *_ in CLIPS:
    for nb, *_ in CLIPS:
        ra, rb = R[na]['reach'], R[nb]['reach']
        # A at origin facing yaw ya; B at +X at spacing facing yb. Direction A->B in A's frame = -ya; B->A = 180 - yb
        ov = ra[(-np.arange(NB)) % NB][:, None] + rb[(180 - np.arange(NB)) % NB][None, :] - A.spacing
        pair[f'{na}+{nb}'] = {'maxOverlap_m': round(float(max(0.0, ov.max())), 3),
                              'shareOfYawPairsOverlapping': round(float((ov > 0.01).mean()), 4)}
need_obstacle = max(R[n]['maxRadius_m'] for n, *_ in CLIPS)
need_pair = max(R[n]['reach'].max() for n, *_ in CLIPS) * 2
out = {'tool': 'measure_contact_clearance.py', 'fbx': str(A.fbx), 'locations': str(A.locations),
       'runtime': {'walkClearance_m': L['walkClearance'], 'dragonSpacing_m': A.spacing,
                   'sources': ['SanctuaryLocationData.IsWalkable (walkClearance)', 'LivingSanctuaryPresentation.Separated (0.55 m)']},
       'reach': {n: {k: v for k, v in R[n].items() if k != 'reach'} | {'reachEvery30deg_m': [round(float(R[n]['reach'][k]), 3) for k in range(0, 360, 30)]} for n, *_ in CLIPS},
       'requiredObstacleClearanceAnyYaw_m': round(need_obstacle, 3),
       'requiredDragonSpacingAnyYaw_m': round(float(need_pair), 3),
       'scene': scene, 'dragonPairs': pair, 'obstacles': len(L['obstacles']), 'grid_m': A.grid}
A.out_json.write_text(json.dumps(out, indent=1))
print(json.dumps({'reach': {n: out['reach'][n]['maxRadius_m'] for n, *_ in CLIPS}, 'scene': scene,
                  'required': [out['requiredObstacleClearanceAnyYaw_m'], out['requiredDragonSpacingAnyYaw_m']]}, indent=1))

if A.png:
    from PIL import Image, ImageDraw
    S = 60
    W, H = int((x1 - x0) * S) + 40, int((z1 - z0) * S) + 60
    im = Image.new('RGB', (W, H), (245, 243, 236))
    dr = ImageDraw.Draw(im)
    tx = lambda x, z: (20 + (x - x0) * S, H - 40 - (z - z0) * S)
    for _, p in L['habitats']:
        dr.polygon([tx(*q) for q in p], outline=(120, 160, 110))
    for w in L['water']:
        dr.polygon([tx(*q) for q in w], fill=(170, 200, 235))
    for _, p in L['obstacles']:
        dr.polygon([tx(*q) for q in p], fill=(110, 100, 90))
    for (x, z, _), dpt in zip(pts, per_point):
        c = (60, 170, 80) if dpt <= 0.01 else ((235, 170, 40) if dpt <= 0.10 else (215, 40, 40))
        X, Y = tx(x, z)
        dr.rectangle([X - S * A.grid / 2, Y - S * A.grid / 2, X + S * A.grid / 2, Y + S * A.grid / 2], fill=c)
    dr.text((20, H - 32), 'Origin: points the ground navigator accepts. green = no clip at any yaw/clip; '
            'orange = up to 10 cm into an obstacle; red = more. Grey = obstacle footprints, blue = water.', fill=(0, 0, 0))
    im.save(A.png)
