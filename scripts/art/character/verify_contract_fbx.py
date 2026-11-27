"""CHAR.9c — independent check of the Unity contract FBX (Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx) the way
Unity sees it: import the FBX into an EMPTY Blender scene (no rig.json, no pipeline state), linear skinning
(Unity SkinnedMeshRenderer), measure EVERY frame of every contract clip.

  python scripts/art/character/verify_contract_fbx.py <contract.fbx> <out.json> [--render <dir>] [--samples 24]
  (bpy 5.3 module, or blender --background --python <this> -- <args>)

Checks (limits read from Assets/Dravio/Character/Data/character-budgets.json, never edited here):
  structure  22 contract bones of EmberlingAssetAudit.cs, one skinned mesh *Emberling_Baby_Body*, UV0, embedded
             BaseColor, <= 4 influences, 0 unweighted vertices, weights normalised, Armature modifier linear
  take       one action; Blender's FBX importer starts the take at frame 2 (Unity frame 1): offset detected, not assumed
  seams      vertices split at UV/normal seams keep one rest position but share no edge: their distance while
             skinned is the crack width Unity shows (limit 0.1 % of height, proposed, not in the budgets file)
  per clip   edge stretch p99.9 / compression p0.1 (edge length / rest length, rest = all pose channels cleared)
             of the WORST SINGLE FRAME (gate), plus the same percentiles pooled over every frame and pooled over the
             8 frames anim_qa.py samples (SAMPLES = 8), so the three definitions can be compared,
             ground penetration relative to rest height, max single-edge stretch and the bones that own the worst edges
             (diagnostic only, no limit), root drift, loop closure of the evaluated mesh (first vs last frame)
With --render: Cycles CPU renders of the worst frame of every clip (textured + stretch heat map) and a rest-pose
turntable, so the numbers can be checked by eye. The input file is never written (SHA-256 checked).
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
ap = argparse.ArgumentParser()
ap.add_argument('fbx', type=Path)
ap.add_argument('out_json', type=Path)
ap.add_argument('--budgets', type=Path, default=Path('Assets/Dravio/Character/Data/character-budgets.json'))
ap.add_argument('--render', type=Path)
ap.add_argument('--samples', type=int, default=24)
ap.add_argument('--res', type=int, default=640)
A = ap.parse_args(argv)

CONTRACT_BONES = ["Root", "Pelvis", "Chest", "Head", "Tail1", "Tail2", "Flame", "Fore_L", "Fore_R", "Hind_L", "Hind_R",
                  "Eye_L", "Eye_R", "ForeLower_L", "ForeLower_R", "HindLower_L", "HindLower_R",
                  "Spine", "Neck", "Wing_L", "Wing_R", "TailVFX_Anchor"]
# EmberlingAssetAudit.Clips (Unity frames, 30 fps) and loop flags
CLIPS = [('Idle', 1, 181, True), ('Walk', 182, 217, True), ('Eat', 218, 278, False),
         ('Doze', 279, 399, True), ('Happy', 400, 445, False)]

sha0 = hashlib.sha256(A.fbx.read_bytes()).hexdigest()
budget = json.loads(A.budgets.read_text())


def find_limits(d):
    if isinstance(d, dict):
        if 'maxStretchP999' in d:
            return d
        for v in d.values():
            r = find_limits(v)
            if r:
                return r
    return None


LIM = find_limits(budget)
assert LIM, 'animation limits not found in budgets'

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(A.fbx))
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
skinned = [o for o in bpy.data.objects if o.type == 'MESH' and o.find_armature() == arm]
errors, notes = [], []
body = [o for o in skinned if 'emberling_baby_body' in o.name.lower()]
if len(body) != 1:
    errors.append(f'expected one *Emberling_Baby_Body* skinned mesh, found {[o.name for o in skinned]}')
o = body[0] if body else skinned[0]
me = o.data
bones = [b.name for b in arm.data.bones]
missing = [b for b in CONTRACT_BONES if b not in bones]
if missing:
    errors.append(f'missing contract bones {missing}')
mods = [m for m in o.modifiers if m.type == 'ARMATURE']
if any(m.use_deform_preserve_volume for m in mods):
    errors.append('Armature modifier uses Preserve Volume (dual quaternion) - Unity skins linearly')
for m in mods:
    m.use_deform_preserve_volume = False
if not me.uv_layers:
    errors.append('no UV0')
imgs = [i for i in bpy.data.images if i.size[0] > 0]
if not imgs:
    errors.append('no embedded texture')

# weights
gi = {g.index: g.name for g in o.vertex_groups}
deform = set(bones)
infl, unweighted, norm_err = 0, 0, 0.0
owner = np.zeros(len(me.vertices), dtype=int)
names = sorted(deform)
nidx = {n: i for i, n in enumerate(names)}
for v in me.vertices:
    ws = [(gi[g.group], g.weight) for g in v.groups if gi.get(g.group) in deform and g.weight > 1e-6]
    infl = max(infl, len(ws))
    s = sum(w for _, w in ws)
    if s < 1e-6:
        unweighted += 1
        continue
    norm_err = max(norm_err, abs(1 - s))
    owner[v.index] = nidx[max(ws, key=lambda x: x[1])[0]]
if infl > LIM.get('maxInfluences', 4):
    errors.append(f'{infl} influences > {LIM.get("maxInfluences", 4)}')
if unweighted:
    errors.append(f'{unweighted} unweighted vertices')
if norm_err > LIM.get('maxNormalisationError', 1e-3):
    errors.append(f'weight normalisation error {norm_err:.5f}')

act = arm.animation_data.action if arm.animation_data else None
acts = list(bpy.data.actions)
if act is None or len(acts) != 1:
    errors.append(f'expected one take, found {[a.name for a in acts]}')
f0, f1 = (int(round(x)) for x in act.frame_range)
offset = f0 - 1  # importer places Unity frame 1 at Blender frame f0
if f1 - offset != 445:
    errors.append(f'take length {f1 - f0 + 1} frames, expected 445')

E = np.array([e.vertices[:] for e in me.edges])
dg = bpy.context.evaluated_depsgraph_get()
sc = bpy.context.scene
sc.render.fps = 30
MW = np.array(o.matrix_world)


def coords():
    ev = o.evaluated_get(dg)
    m = ev.to_mesh()
    a = np.empty(len(m.vertices) * 3)
    m.vertices.foreach_get('co', a)
    ev.to_mesh_clear()
    a = a.reshape(-1, 3)
    return a @ MW[:3, :3].T + MW[:3, 3]


# rest = every pose channel cleared, action detached
arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = (1, 0, 0, 0)
    pb.location = (0, 0, 0)
    pb.scale = (1, 1, 1)
sc.frame_set(f0)
bpy.context.view_layer.update()
rest = coords()
L0 = np.linalg.norm(rest[E[:, 0]] - rest[E[:, 1]], axis=1)
ok = L0 > 1e-7
H = float(rest[:, 2].max() - rest[:, 2].min())
arm.animation_data.action = act
for pb in arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'

edge_owner = owner[E[:, 0]]
# seam-split vertices (same rest position, no edge between them): their distance is the crack width in Unity
_, grp = np.unique(np.round(rest / 1e-5).astype(np.int64), axis=0, return_inverse=True)
grp = grp.ravel()
order = np.argsort(grp, kind='stable')
same = grp[order][1:] == grp[order][:-1]
SEAM = np.stack([order[:-1][same], order[1:][same]], 1)
SEAM_GAP_REL = 0.001  # proposed (anim_qa.py --strict-frame), not in character-budgets.json
gi_w = {}
seam_wdiff = 0.0
if len(SEAM):
    Wm = np.zeros((len(me.vertices), len(names)))
    for v in me.vertices:
        for g in v.groups:
            nm = gi.get(g.group)
            if nm in nidx:
                Wm[v.index, nidx[nm]] = g.weight
    seam_wdiff = float(np.abs(Wm[SEAM[:, 0]] - Wm[SEAM[:, 1]]).sum(1).max())
res = {}
worst_frames = {}
for name, a, b, loop in CLIPS:
    st, cp, gp, smax, frames = [], [], [], [], list(range(a, b + 1))
    hot = np.zeros(len(names))
    cold = np.zeros(len(names))
    pooled = []
    # anim_qa.py (SAMPLES = 8) samples 8 evenly spaced frames and pools their edges; reproduce that too
    aq = sorted({a + int(round((b - a) * k / 7)) for k in range(8)})
    pooled_aq = []
    worst = (-1, None, None)
    first = last = None
    seam_gap = (0.0, None)
    root0 = None
    drift = 0.0
    for f in frames:
        sc.frame_set(f + offset)
        P = coords()
        if len(SEAM):
            sg = float(np.linalg.norm(P[SEAM[:, 0]] - P[SEAM[:, 1]], axis=1).max())
            if sg > seam_gap[0]:
                seam_gap = (sg, f)
        if first is None:
            first = P
        last = P
        rt = arm.pose.bones['Root'].head if 'Root' in arm.pose.bones else Vector()
        rt = np.array(arm.matrix_world @ rt)
        root0 = rt if root0 is None else root0
        drift = max(drift, float(np.linalg.norm((rt - root0)[:2])))
        r = np.linalg.norm(P[E[:, 0]] - P[E[:, 1]], axis=1) / np.where(ok, L0, 1)
        r = r[ok]
        p999 = float(np.percentile(r, 99.9))
        st.append(p999)
        cp.append(float(np.percentile(r, 0.1)))
        smax.append(float(r.max()))
        gp.append(max(0.0, -float(P[:, 2].min())) / H)
        over = r > LIM['maxStretchP999']
        np.add.at(hot, edge_owner[ok][over], 1)
        np.add.at(cold, edge_owner[ok][r < LIM['minCompressP001']], 1)
        pooled.append(r.astype(np.float32))
        if f in aq:
            pooled_aq.append(r.astype(np.float32))
        if p999 > worst[0]:
            worst = (p999, f, r)
    loop_err = float(np.abs(first - last).max()) / H
    top = [(names[i], round(float(hot[i] / max(1, hot.sum())), 3)) for i in np.argsort(-hot)[:4] if hot[i] > 0]
    topc = [(names[i], round(float(cold[i] / max(1, cold.sum())), 3)) for i in np.argsort(-cold)[:4] if cold[i] > 0]
    R = np.concatenate(pooled)
    Rq = np.concatenate(pooled_aq)
    sp, cm, pen = max(st), min(cp), max(gp)
    status = 'PASS' if (sp <= LIM['maxStretchP999'] and cm >= LIM['minCompressP001'] and
                        pen <= LIM['maxGroundPenetrationRel'] and seam_gap[0] / H <= SEAM_GAP_REL) else 'FAIL'
    res[name] = {'unityFrames': [a, b], 'framesMeasured': len(frames), 'loop': loop,
                 'stretchP999': round(sp, 3), 'compressP001': round(cm, 3), 'groundPenetrationRel': round(pen, 4),
                 'stretchMaxSingleEdge': round(max(smax), 3), 'worstFrame': worst[1],
                 'seamGapRel': round(seam_gap[0] / H, 5), 'seamGap_mm': round(seam_gap[0] * 1000, 2),
                 'seamGapFrame': seam_gap[1],
                 'pooledAllFrames': {'stretchP999': round(float(np.percentile(R, 99.9)), 3),
                                     'compressP001': round(float(np.percentile(R, 0.1)), 3)},
                 'pooledAnimQa8Frames': {'frames': aq, 'stretchP999': round(float(np.percentile(Rq, 99.9)), 3),
                                         'compressP001': round(float(np.percentile(Rq, 0.1)), 3)},
                 'edgesOverStretchLimitByBone': top, 'edgesUnderCompressLimitByBone': topc, 'rootDriftXY_m': round(drift, 4),
                 'loopMeshErrorRel': round(loop_err, 4) if loop else None, 'status': status}
    worst_frames[name] = (worst[1], worst[2])

out = {'tool': 'verify_contract_fbx.py', 'blender': bpy.app.version_string, 'fbx': str(A.fbx), 'sha256': sha0,
       'skinning': 'linear', 'limits': {k: LIM[k] for k in ('maxStretchP999', 'minCompressP001', 'maxGroundPenetrationRel')},
       'structure': {'bones': len(bones), 'contractBonesMissing': missing, 'mesh': o.name, 'vertices': len(me.vertices),
                     'triangles': sum(len(p.vertices) - 2 for p in me.polygons), 'uvLayers': [u.name for u in me.uv_layers],
                     'textures': [[i.name, list(i.size)] for i in imgs], 'maxInfluences': infl,
                     'unweightedVertices': unweighted, 'maxNormalisationError': round(norm_err, 6),
                     'seamSplitPairs': int(len(SEAM)), 'seamPairMaxWeightL1Diff': round(seam_wdiff, 4),
                     'takeBlenderFrames': [f0, f1], 'importerFrameOffset': offset, 'restHeight_m': round(H, 4),
                     'restMinZ_m': round(float(rest[:, 2].min()), 4),
                     'restFootprintCentre_m': [round(float((rest[:, i].min() + rest[:, i].max()) / 2), 4) for i in (0, 1)]},
       'clips': res, 'errors': errors,
       'status': 'PASS' if not errors and all(c['status'] == 'PASS' for c in res.values()) else 'FAIL'}

# ---------------------------------------------------------------- renders
if A.render:
    A.render.mkdir(parents=True, exist_ok=True)
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = A.samples
    sc.cycles.use_denoising = False
    sc.render.resolution_x = sc.render.resolution_y = A.res
    sc.render.film_transparent = False
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('W')
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.82, 0.86, 0.9, 1)
    w.node_tree.nodes['Background'].inputs[1].default_value = 0.9
    sc.world = w
    bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, 0))
    floor = bpy.context.active_object
    fm = bpy.data.materials.new('Floor')
    fm.use_nodes = True
    fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.55, 0.5, 0.42, 1)
    floor.data.materials.append(fm)
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.lens = 50
    sc.collection.objects.link(cam)
    sc.camera = cam
    ctr = Vector((float(rest[:, 0].mean()), float(rest[:, 1].mean()), H * 0.45))

    def aim(yaw_deg, dist=H * 2.7, elev=0.32):
        y = math.radians(yaw_deg)
        cam.location = ctr + Vector((math.sin(y) * dist, -math.cos(y) * dist, dist * elev))
        cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()

    tex_mats = list(me.materials)
    # heat material: vertex colour attribute
    heat = me.color_attributes.new('Heat', 'FLOAT_COLOR', 'POINT')
    hm = bpy.data.materials.new('Heat')
    hm.use_nodes = True
    nt = hm.node_tree
    attr = nt.nodes.new('ShaderNodeAttribute')
    attr.attribute_name = 'Heat'
    nt.links.new(attr.outputs['Color'], nt.nodes['Principled BSDF'].inputs['Base Color'])
    nt.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.8

    def set_heat(r_edges):
        vmax = np.zeros(len(me.vertices))
        vmin = np.full(len(me.vertices), 1.0)
        full = np.ones(len(E))
        full[ok] = r_edges
        np.maximum.at(vmax, E[:, 0], full)
        np.maximum.at(vmax, E[:, 1], full)
        np.minimum.at(vmin, E[:, 0], full)
        np.minimum.at(vmin, E[:, 1], full)
        g = 0.45
        col = np.tile([g, g, g, 1.0], (len(me.vertices), 1))
        s = np.clip((vmax - 1.2) / (LIM['maxStretchP999'] - 1.2), 0, 1)  # red ramps to limit
        c = np.clip((0.8 - vmin) / (0.8 - LIM['minCompressP001']), 0, 1)  # blue ramps to limit
        col[:, 0] = g + (1 - g) * s - g * c
        col[:, 1] = g - g * np.maximum(s, c)
        col[:, 2] = g - g * s + (1 - g) * c
        heat.data.foreach_set('color', np.clip(col, 0, 1).ravel())

    def shot(path, mat=None):
        for i in range(len(me.materials)):
            me.materials[i] = mat if mat else tex_mats[i]
        sc.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)

    renders = []
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    sc.frame_set(f0)
    for yaw in (0, 90, 180, 270):
        aim(yaw - 35)
        p = A.render / f'rest_yaw{yaw:03d}.png'
        shot(p)
        renders.append(p.name)
    arm.animation_data.action = act
    for name, (wf, r) in worst_frames.items():
        sc.frame_set(wf + offset)
        aim(-35)
        p = A.render / f'{name}_f{wf:03d}_tex.png'
        shot(p)
        set_heat(r)
        p2 = A.render / f'{name}_f{wf:03d}_heat.png'
        shot(p2, hm)
        aim(-90, elev=0.15)
        p3 = A.render / f'{name}_f{wf:03d}_side_tex.png'
        shot(p3)
        renders += [p.name, p2.name, p3.name]
    out['renders'] = renders

assert hashlib.sha256(A.fbx.read_bytes()).hexdigest() == sha0, 'input changed'
A.out_json.write_text(json.dumps(out, indent=1))
print(json.dumps({k: out[k] for k in ('status', 'errors')}), json.dumps({k: {x: v[x] for x in ('stretchP999', 'compressP001', 'groundPenetrationRel', 'seamGap_mm', 'status')} for k, v in res.items()}))
