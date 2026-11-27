"""Animation QA for a rigged dragon (U1-CHAR.4). Runs inside Blender, never edits the asset.

  blender --background --python scripts/art/character/anim_qa.py -- \
      <Name>_Rigged.blend <Name>_rig.json <out_dir> [--lods-json <Name>_lods.json] [--images] [--overwrite-outputs]

Writes <out_dir>/<Name>_animqa.json and, with --images, <Name>_animqa.png (worst frame of every clip,
side and three-quarter view, vertex colour = edge stretch heat: blue compressed, grey ok, red stretched).

Checks, per clip unless stated:
  deformation      edge length ratio p99.9 / p0.1 / max over sampled frames, and the bones that own the
                   worst 0.1 % of edges (so a warning points at a region, not just a number)
  footSliding      planted feet (rig.json contacts) against the in-place track: a stance foot must move
                   +Y at the clip's groundSpeed and nothing else
  groundContact    lowest mesh point below z=0 (penetration) and planted feet above the ground (gap);
                   skipped for Swim, Fly, Glide (no ground)
  loop             pose difference between the first and last frame of looping clips
  bounds           animated bounding box against the rest box (culling bounds in Unity)
  facing           head stays in front of the chest (-Y), Walk/Run feet push backwards (no moonwalk),
                   Eat keeps the face forward
  wings            wing vertices inside the body surface (winged archetype)
Static: weight normalisation and influence count, bone orientation and L/R mirror, height against the
species size, and the LOD deformed deviation read from build_lods.py's report.

Thresholds come from character-budgets.json "animationQa". A failed check is a WARNING with the measured
value; this tool never relaxes a limit to make a model pass.
"""
import argparse
import json
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

REPO = Path(__file__).resolve().parents[3]
BUDGETS = REPO / 'Assets/Dravio/Character/Data/character-budgets.json'
QA_VERSION = 3  # 2 = U1-CHAR.6: authored transition checks (Sleep -> Wake -> Idle); 3 = U1-CHAR.7: flight/water/burrow transitions with root offsets, motion envelope; limits unchanged
NO_GROUND = {'Swim', 'Fly', 'Glide', 'SwimIdle', 'SwimRest', 'SwimSurface', 'WaterEnter', 'WaterExit'}
# authored transitions (U1-CHAR.6/7): (from clip, frame, to clip, frame, root offset key or None). With a key, the
# vertical root offset is not a pose error: it must equal rig.json motion.rootOffsets_m[key] (the Sanctuary
# moves the dragon by it); every rotation and the rest of the pose must still match within the loop-seam limits.
TRANSITIONS = (
    ('Sleep', 'start', 'Wake', 'start', None), ('Wake', 'end', 'Idle', 'start', None),
    ('Idle', 'start', 'TakeOff', 'start', None), ('TakeOff', 'end', 'Fly', 'start', 'TakeOff.end'),
    ('Fly', 'start', 'Land', 'start', 'Land.start'), ('Land', 'end', 'Idle', 'start', None),
    ('Idle', 'start', 'WaterEnter', 'start', None), ('WaterEnter', 'end', 'Swim', 'start', None),
    ('Swim', 'start', 'WaterExit', 'start', None), ('WaterExit', 'end', 'Idle', 'start', None),
    ('Idle', 'start', 'DigPrep', 'start', None), ('DigPrep', 'end', 'BurrowEnter', 'start', None),
    ('BurrowEnter', 'end', 'BurrowEmerge', 'start', None), ('BurrowEmerge', 'end', 'Idle', 'start', None),
)
WATER_CLIPS = ('Swim', 'SwimIdle', 'SwimRest', 'SwimSurface')
AIR_CLIPS = ('Fly', 'Glide', 'TakeOff', 'Land')
SAMPLES = 8
# CHAR.9c, --strict-frame only: largest allowed distance between coincident (seam-split) vertices, relative to
# the height (0.1 %: 0.9 mm on the 0.9 m Emberling). Proposed check, NOT in character-budgets.json yet: a welded
# skin gives exactly 0; rig v7 Emberling measured 5 % (45 mm) at Eat.
SEAM_GAP_REL = 0.001


def region_of(bone):
    for pre, reg in (('Head', 'head'), ('Jaw', 'head'), ('Eye', 'head'), ('Neck', 'neck'), ('Tail', 'tail'),
                     ('Fore', 'foreLeg'), ('Hind', 'hindLeg'), ('Wing', 'wing'), ('EarFin', 'fin'), ('Fin', 'fin'),
                     ('Spine', 'body'), ('Chest', 'body'), ('Pelvis', 'body'), ('Root', 'root')):
        if bone.startswith(pre):
            return reg
    return 'other'


class Mesh:
    def __init__(self, obj):
        self.obj = obj
        me = obj.data
        n = len(me.vertices)
        self.rest = np.empty(n * 3)
        me.vertices.foreach_get('co', self.rest)
        self.rest = self.rest.reshape(-1, 3)
        e = np.empty(len(me.edges) * 2, np.int64)
        me.edges.foreach_get('vertices', e)
        self.E = e.reshape(-1, 2)
        self.L0 = np.linalg.norm(self.rest[self.E[:, 0]] - self.rest[self.E[:, 1]], axis=1)
        keep = self.L0 > 1e-9
        self.E, self.L0 = self.E[keep], self.L0[keep]
        # CHAR.9c: vertices split at UV/normal seams share a rest position but no edge, so edge stretch cannot see
        # the skinned surface tearing open between them; measure their distance directly
        _, grp = np.unique(np.round(self.rest / 1e-5).astype(np.int64), axis=0, return_inverse=True)
        grp = grp.ravel()
        order = np.argsort(grp, kind='stable')
        same = grp[order][1:] == grp[order][:-1]
        self.seam = np.stack([order[:-1][same], order[1:][same]], 1)
        gname = {g.index: g.name for g in obj.vertex_groups}
        self.dominant = []
        self.wsum, self.ninf = np.zeros(n), np.zeros(n, int)
        for v in me.vertices:
            gs = [(gname[g.group], g.weight) for g in v.groups if g.weight > 0]
            self.wsum[v.index] = sum(w for _, w in gs)
            self.ninf[v.index] = len(gs)
            self.dominant.append(max(gs, key=lambda x: x[1])[0] if gs else '')
        self.dominant = np.array(self.dominant, dtype=object)
        self.dom_w = np.array([max([g.weight for g in v.groups] or [0]) for v in me.vertices])

    def evaluated(self):
        dg = bpy.context.evaluated_depsgraph_get()
        ev = self.obj.evaluated_get(dg)
        m = ev.to_mesh()
        co = np.empty(len(m.vertices) * 3)
        m.vertices.foreach_get('co', co)
        ev.to_mesh_clear()
        return (np.array(self.obj.matrix_world)[:3, :3] @ co.reshape(-1, 3).T).T + np.array(self.obj.matrix_world)[:3, 3]

    def ratios(self, co):
        return np.linalg.norm(co[self.E[:, 0]] - co[self.E[:, 1]], axis=1) / self.L0


def bone_world(arm, name, tail=False):
    pb = arm.pose.bones[name]
    return np.array(arm.matrix_world @ (pb.tail if tail else pb.head))


def pose_snapshot(arm):
    return {pb.name: (np.array(pb.matrix), pb.length) for pb in arm.pose.bones}


def pose_difference(a, b, h, ignore_root_z=False):
    """Worst bone rotation (deg) and translation (relative to height) between two poses. ignore_root_z removes
    the Root bone's vertical offset from every bone first (an authored vertical root offset is not an error)."""
    dz = float(b['Root'][0][2, 3] - a['Root'][0][2, 3]) if ignore_root_z and 'Root' in a else 0.0
    worst_deg, worst_rel = 0.0, 0.0
    for n, (ma, _) in a.items():
        mb = b[n][0]
        ra, rb = ma[:3, :3], mb[:3, :3]
        c = np.clip((np.trace(ra.T @ rb) - 1) / 2, -1, 1)
        worst_deg = max(worst_deg, math.degrees(math.acos(c)))
        d = mb[:3, 3] - ma[:3, 3]
        d[2] -= dz
        worst_rel = max(worst_rel, float(np.linalg.norm(d)) / h)
    return worst_deg, worst_rel


def seam_velocity_mismatch(a0, a1, b1, b0):
    """Largest per-bone difference (degrees per frame) between the rotation speed entering the loop seam
    (end-1 -> end) and leaving it (start -> start+1). 0 for a perfectly smooth loop."""
    def ang(x, y):
        c = np.clip((np.trace(x[:3, :3].T @ y[:3, :3]) - 1) / 2, -1, 1)
        return math.degrees(math.acos(c))
    return max(abs(ang(a0[n][0], a1[n][0]) - ang(b1[n][0], b0[n][0])) for n in a0)


def clip_frames(c, n=SAMPLES):
    s, e = c['start'], c['end']
    return sorted({s + int(round((e - s) * k / (n - 1))) for k in range(n)})


def heat_colours(mesh, ratios):
    """Per-vertex worst edge deviation -> RGB."""
    n = len(mesh.rest)
    up = np.ones(n)
    dn = np.ones(n)
    for col in (0, 1):
        np.maximum.at(up, mesh.E[:, col], ratios)
        np.minimum.at(dn, mesh.E[:, col], ratios)
    c = np.full((n, 3), 0.72)
    s = np.clip((up - 1.0) / 0.6, 0, 1)
    k = np.clip((1.0 - dn) / 0.55, 0, 1)
    c[:, 0] = 0.72 + 0.28 * s - 0.6 * k
    c[:, 1] = 0.72 - 0.6 * s - 0.4 * k
    c[:, 2] = 0.72 - 0.6 * s + 0.28 * k
    return np.clip(c, 0, 1)


def render_sheet(mesh, arm, worst, out_png, h):
    """Cycles CPU, two orthographic views per clip at its worst frame, stitched into one PNG."""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 6
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = 360, 270
    scene.view_settings.view_transform = 'Standard'
    world = scene.world or bpy.data.worlds.new('QA')
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.35, 0.36, 0.38, 1)
    sun = bpy.data.objects.new('QASun', bpy.data.lights.new('QASun', 'SUN'))
    sun.data.energy = 1.6
    sun.rotation_euler = (math.radians(45), 0, math.radians(-30))
    scene.collection.objects.link(sun)
    obj = mesh.obj
    me = obj.data
    attr = me.color_attributes.new('QAHeat', 'FLOAT_COLOR', 'POINT')
    mat = bpy.data.materials.new('QAHeatMat')
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get('Principled BSDF')
    vc = nt.nodes.new('ShaderNodeVertexColor')
    vc.layer_name = 'QAHeat'
    nt.links.new(vc.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.8
    saved = [s.material for s in obj.material_slots]
    for s in obj.material_slots:
        s.material = mat
    if not obj.material_slots:
        obj.data.materials.append(mat)
    ground = bpy.data.meshes.new('QAGround')
    ground.from_pydata([(-5 * h, -5 * h, 0), (5 * h, -5 * h, 0), (5 * h, 5 * h, 0), (-5 * h, 5 * h, 0)], [], [(0, 1, 2, 3)])
    gobj = bpy.data.objects.new('QAGround', ground)
    scene.collection.objects.link(gobj)
    cam = bpy.data.objects.new('QACam', bpy.data.cameras.new('QACam'))
    cam.data.type = 'ORTHO'
    scene.collection.objects.link(cam)
    scene.camera = cam
    tiles = []
    tmp = Path(out_png).with_suffix('.tile.png')
    for clip, (frame, ratios) in worst.items():
        scene.frame_set(frame)
        attr.data.foreach_set('color', np.c_[heat_colours(mesh, ratios), np.ones(len(mesh.rest))].ravel())
        co = mesh.evaluated()
        lo, hi = co.min(axis=0), co.max(axis=0)
        center = Vector(((lo + hi) / 2).tolist())
        size = float(max(hi - lo)) * 1.25
        row = []
        for offset in (Vector((6.0, 0, 0.6)), Vector((-3.0, -5.5, 2.0))):
            cam.data.ortho_scale = size
            cam.location = center + offset
            cam.rotation_euler = (-offset).to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = str(tmp)
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(str(tmp))
            px = np.array(img.pixels[:]).reshape(img.size[1], img.size[0], 4)
            bpy.data.images.remove(img)
            row.append(px)
        tiles.append((clip, np.concatenate(row, axis=1)))
    tmp.unlink(missing_ok=True)
    sheet = np.concatenate([t for _, t in tiles][::-1], axis=0)  # pixel rows are bottom-up
    out = bpy.data.images.new('QASheet', sheet.shape[1], sheet.shape[0], alpha=True)
    out.pixels = sheet.ravel().tolist()
    out.filepath_raw = str(out_png)
    out.file_format = 'PNG'
    out.save()
    for s, m in zip(obj.material_slots, saved):
        s.material = m
    me.color_attributes.remove(attr)
    return [c for c, _ in tiles]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('blend', type=Path)
    ap.add_argument('rig_json', type=Path)
    ap.add_argument('out_dir', type=Path)
    ap.add_argument('--lods-json', type=Path)
    ap.add_argument('--images', action='store_true')
    ap.add_argument('--overwrite-outputs', action='store_true')
    # CHAR.9c: default stays 8 evenly spaced frames pooled (U1-CHAR.4 definition, keeps every earlier report
    # comparable). '--samples all' measures every frame; '--strict-frame' additionally applies the SAME limits to
    # the worst single frame (a pooled p0.1 over 8 frames can hide one bad frame: Emberling Eat 0.494 pooled vs
    # 0.428 at its worst frame).
    ap.add_argument('--samples', default=str(SAMPLES), help="frames per clip: integer or 'all'")
    ap.add_argument('--strict-frame', action='store_true')
    a = ap.parse_args(cc.script_args())
    rig = json.loads(a.rig_json.read_text(encoding='utf-8'))
    lim = json.loads(BUDGETS.read_text(encoding='utf-8'))['animationQa']
    name = rig['name']
    a.out_dir.mkdir(parents=True, exist_ok=True)
    out_json = a.out_dir / f'{name}_animqa.json'
    if out_json.exists() and not a.overwrite_outputs:
        raise SystemExit(f'Output exists: {out_json} (use --overwrite-outputs)')
    bpy.ops.wm.open_mainfile(filepath=str(a.blend))
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    obj = next(o for o in bpy.data.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers))
    mesh = Mesh(obj)
    # Unity's SkinnedMeshRenderer (and FBX) skin linearly; Blender's Preserve Volume is dual quaternion. Record which
    # one these numbers measured so a Blender PASS is never mistaken for the skinning Unity plays.
    skinning = 'dualQuaternion' if any(m.type == 'ARMATURE' and m.use_deform_preserve_volume for m in obj.modifiers) else 'linear'
    scene = bpy.context.scene
    fps = rig.get('fps', 30)
    scene.frame_set(1)
    rest_co = mesh.evaluated()
    rlo, rhi = rest_co.min(axis=0), rest_co.max(axis=0)
    h = float(rhi[2] - rlo[2])
    rest_diag = float(np.linalg.norm(rhi - rlo))
    warnings = []

    def warn(clip, check, msg, value, limit):
        warnings.append({'clip': clip, 'check': check, 'message': msg, 'value': value, 'limit': limit})

    # ---------------------------------------------------------------- static checks
    deform = [b.name for b in arm.data.bones if b.use_deform]
    weights = {
        'maxInfluences': int(mesh.ninf.max()), 'unweightedVertices': int((mesh.ninf == 0).sum()),
        'maxNormalisationError': round(float(np.abs(mesh.wsum[mesh.ninf > 0] - 1).max()), 6),
        'nonDeformGroups': sorted({g.name for g in obj.vertex_groups} - set(deform)),
    }
    if weights['maxInfluences'] > lim['maxInfluences']:
        warn(None, 'weights', 'too many influences', weights['maxInfluences'], lim['maxInfluences'])
    if weights['unweightedVertices']:
        warn(None, 'weights', 'unweighted vertices', weights['unweightedVertices'], 0)
    if weights['maxNormalisationError'] > lim['maxNormalisationError']:
        warn(None, 'weights', 'weights not normalised', weights['maxNormalisationError'], lim['maxNormalisationError'])
    bones_rep = {'zeroLength': [], 'mirrorErrorRel': 0.0, 'chainDirection': []}
    B = arm.data.bones
    for b in B:
        if b.use_deform and b.length < 1e-5:
            bones_rep['zeroLength'].append(b.name)
        if b.name.endswith('_L') and b.name[:-2] + '_R' in B:
            o = B[b.name[:-2] + '_R']
            m = Vector((-o.head_local.x, o.head_local.y, o.head_local.z))
            bones_rep['mirrorErrorRel'] = max(bones_rep['mirrorErrorRel'], (b.head_local - m).length / h)
    for n in ('Spine', 'Chest', 'Neck'):  # forward chain points to -Y (Blender front)
        if n in B and (B[n].tail_local.y - B[n].head_local.y) > 0.01 * h:
            bones_rep['chainDirection'].append(f'{n} points backwards (+Y)')
    for n in [x for x in B.keys() if x.startswith('Tail') and x[4:].isdigit()]:
        if (B[n].tail_local.y - B[n].head_local.y) < -0.01 * h and B[n].head_local.z > 0.05 * h:
            bones_rep['chainDirection'].append(f'{n} points forwards (-Y)')
    bones_rep['mirrorErrorRel'] = round(bones_rep['mirrorErrorRel'], 5)
    if bones_rep['zeroLength'] or bones_rep['chainDirection'] or bones_rep['mirrorErrorRel'] > 0.01:
        warn(None, 'bones', 'bone orientation / mirror issue', bones_rep, {'mirrorErrorRel': 0.01})
    height = {'restHeight_m': round(h, 4), 'rigHeight_m': rig.get('height_m'), 'source': rig.get('heightSource')}
    if rig.get('height_m') and abs(h - rig['height_m']) > lim['heightToleranceRel'] * rig['height_m']:
        warn(None, 'height', 'rest height differs from rig.json', round(h, 4), rig['height_m'])
    lods = None
    if a.lods_json and a.lods_json.exists():
        lj = json.loads(a.lods_json.read_text(encoding='utf-8'))
        lods = []
        for e in lj.get('lods', []):
            ok = e.get('limits', {}).get('p95_deformed_ok', True)
            lods.append({'level': e.get('level'), 'deformedP95RelWorst': e.get('deformed_p95_rel_worst'),
                         'ok': ok})
            if not ok:
                warn(None, 'lod', f"LOD{e.get('level')} deformed deviation over budget", e.get('deformed_p95_rel_worst'), None)

    # ---------------------------------------------------------------- per clip
    ik = {}
    for ch in rig.get('ikChains', []):
        key = ('Fore' if ch['foot'].startswith('Fore') else 'Hind') + '_' + ch['foot'][-1]
        ik[key] = ch
    head = 'Head' if 'Head' in B else None
    chest = 'Chest' if 'Chest' in B else next((n for n in ('Spine4', 'Spine3', 'Spine2') if n in B), None)
    # wing probe: vertices owned by WingFore/WingTip (the membrane away from the shoulder blend)
    wing_v = np.nonzero([str(d).startswith(('WingFore', 'WingTip')) for d in mesh.dominant])[0]
    body_set = {'Spine', 'Chest', 'Pelvis'} | {n for n in B.keys() if n.startswith('Spine')}
    body_f = [p for p in obj.data.polygons if all(mesh.dominant[i] in body_set for i in p.vertices)]
    probe = wing_v[::max(1, len(wing_v) // 400)] if len(wing_v) else wing_v
    polys = [p.vertices[:] for p in body_f]

    def wing_inside(co):
        verts = [Vector(p) for p in co]
        tree = BVHTree.FromPolygons(verts, polys)
        out = np.zeros(len(probe), bool)
        for j, i in enumerate(probe):
            loc, nrm, _, dist = tree.find_nearest(verts[i])
            # inside = on the inner side of the nearest body face, 1-8 % of the height deep. The body
            # patch is an open surface (holes at neck, legs, wings), so the normal side is only trusted
            # close to it; a membrane far out beside an opening is not 'inside'.
            out[j] = loc is not None and dist < 0.08 * h and (verts[i] - loc).dot(nrm) < -0.01 * h
        return out

    if len(probe) and polys:  # keep only membrane vertices clearly outside the body at rest (> 2 % h)
        verts0 = [Vector(p) for p in rest_co]
        tree0 = BVHTree.FromPolygons(verts0, polys)
        clear = [i for i in probe if (lambda r: r[0] is not None and (verts0[i] - r[0]).dot(r[1]) > 0.02 * h)(tree0.find_nearest(verts0[i]))]
        probe = np.array(clear, int)
    rest_inside = np.zeros(len(probe), bool)
    wing_probe_count = int(len(probe))
    clips = {}
    worst_frames = {}
    for clip, c in rig['clips'].items():
        rep = {'frames': [c['start'], c['end']], 'loop': c.get('loop', False), 'groundSpeed': c.get('groundSpeed', 0)}
        frames = (list(range(c['start'], c['end'] + 1)) if a.samples == 'all'
                  else clip_frames(c, max(2, int(a.samples))))
        allr = []
        fr_st, fr_cp = [], []
        seam_gap = 0.0
        worst = (None, -1, None)
        lo, hi = rest_co.min(axis=0), rest_co.max(axis=0)
        min_z = np.inf
        head_behind = 0
        wing_in = 0.0
        for f in frames:
            scene.frame_set(f)
            co = mesh.evaluated()
            r = mesh.ratios(co)
            allr.append(r)
            if len(mesh.seam):
                seam_gap = max(seam_gap, float(np.linalg.norm(co[mesh.seam[:, 0]] - co[mesh.seam[:, 1]], axis=1).max()))
            fr_st.append(float(np.percentile(r, 99.9)))
            fr_cp.append(float(np.percentile(r, 0.1)))
            score = max(fr_st[-1] - 1, 1 - fr_cp[-1])
            if score > worst[1]:
                worst = (f, score, r)
            lo, hi = np.minimum(lo, co.min(axis=0)), np.maximum(hi, co.max(axis=0))
            min_z = min(min_z, float(co[:, 2].min()))
            if head and chest and bone_world(arm, head)[1] > bone_world(arm, chest)[1]:
                head_behind += 1
            if len(probe) and body_f:  # vertices that were clearly outside the body at rest
                now = wing_inside(co)
                wing_in = max(wing_in, float(now.sum()) / len(now))
        R = np.concatenate(allr)
        p999, p001 = float(np.percentile(R, 99.9)), float(np.percentile(R, 0.1))
        bad = (R > np.percentile(R, 99.9)) | (R < np.percentile(R, 0.1))
        owners = np.tile(mesh.dominant[mesh.E[:, 0]], len(frames))[bad]
        vals, counts = np.unique(owners.astype(str), return_counts=True)
        top = sorted(zip(vals, counts), key=lambda x: -x[1])[:4]
        rep['deformation'] = {'stretchP999': round(p999, 3), 'compressP001': round(p001, 3),
                              'stretchMax': round(float(R.max()), 3), 'worstFrame': worst[0],
                              'framesMeasured': len(frames), 'aggregation': 'pooled',
                              'worstFrameStretchP999': round(max(fr_st), 3),
                              'worstFrameCompressP001': round(min(fr_cp), 3),
                              'worstBones': [{'bone': b, 'region': region_of(b), 'share': round(int(n) / int(bad.sum()), 3)}
                                             for b, n in top]}
        worst_frames[clip] = (worst[0], worst[2])
        if p999 > lim['maxStretchP999']:
            warn(clip, 'deformation', 'edge stretch over limit', round(p999, 3), lim['maxStretchP999'])
        if p001 < lim['minCompressP001']:
            warn(clip, 'deformation', 'edge compression over limit', round(p001, 3), lim['minCompressP001'])
        rep['seamGapRel'] = round(seam_gap / h, 5)
        if a.strict_frame and seam_gap / h > SEAM_GAP_REL:
            warn(clip, 'seams', 'skinned surface opens at split (UV/normal seam) vertices', round(seam_gap / h, 5), SEAM_GAP_REL)
        if a.strict_frame and max(fr_st) > lim['maxStretchP999'] and p999 <= lim['maxStretchP999']:
            warn(clip, 'deformation', 'edge stretch over limit at the worst single frame', round(max(fr_st), 3), lim['maxStretchP999'])
        if a.strict_frame and min(fr_cp) < lim['minCompressP001'] and p001 >= lim['minCompressP001']:
            warn(clip, 'deformation', 'edge compression over limit at the worst single frame', round(min(fr_cp), 3), lim['minCompressP001'])
        # bounds
        growth = float(np.linalg.norm(hi - lo)) / rest_diag
        rep['bounds'] = {'min': [round(float(x), 4) for x in lo], 'max': [round(float(x), 4) for x in hi],
                         'diagGrowth': round(growth, 3)}
        if growth > lim['maxBoundsGrowth']:
            warn(clip, 'bounds', 'animated bounds grow past limit', round(growth, 3), lim['maxBoundsGrowth'])
        # facing
        rep['facing'] = {'headBehindChestFrames': head_behind}
        if head_behind > lim['maxHeadBehindChestFrames']:
            warn(clip, 'facing', 'head behind the chest (face turned backwards)', head_behind, lim['maxHeadBehindChestFrames'])
        if len(wing_v):
            rep['wingBodyPenetrationFrac'] = round(wing_in, 4)
            if wing_in > lim['maxWingBodyPenetrationFrac']:
                warn(clip, 'wings', 'wing vertices inside the body', round(wing_in, 4), lim['maxWingBodyPenetrationFrac'])
        # ground
        if clip not in NO_GROUND:
            pen = max(0.0, -min_z) / h
            rep['groundPenetrationRel'] = round(pen, 4)
            if pen > lim['maxGroundPenetrationRel']:
                warn(clip, 'ground', 'mesh below the ground plane', round(pen, 4), lim['maxGroundPenetrationRel'])
        # feet: sliding against the in-place track, contact gap, moonwalk
        contacts = rig.get('contacts', {}).get(clip, {})
        slide, gap, push = 0.0, 0.0, []
        v = c.get('groundSpeed', 0.0)
        if contacts and ik:
            track = {}
            for f in range(c['start'], c['end'] + 1):
                scene.frame_set(f)
                for k, ch in ik.items():
                    track.setdefault(k, []).append(bone_world(arm, ch['foot']))
            for k, ivs in contacts.items():
                if k not in track:
                    continue
                P = np.array(track[k])
                for s, e in ivs:
                    if e - s < 2:
                        continue
                    seg = P[s:e + 1]
                    t = np.arange(len(seg)) / fps
                    exp = seg[0] + np.c_[np.zeros_like(t), v * t, np.zeros_like(t)]
                    slide = max(slide, float(np.linalg.norm((seg - exp)[:, :2], axis=1).max()) / h)
                    gap = max(gap, float(seg[:, 2].max() - P[:, 2].min()) / h) if clip not in NO_GROUND else gap
                    push.append(float(seg[-1, 1] - seg[0, 1]))
            rep['footSlideRel'] = round(slide, 4)
            rep['contactGapRel'] = round(gap, 4)
            if slide > lim['maxFootSlideRel']:
                warn(clip, 'footSliding', 'planted foot slides', round(slide, 4), lim['maxFootSlideRel'])
            if gap > lim['maxContactGapRel']:
                warn(clip, 'groundContact', 'planted foot lifts off the ground', round(gap, 4), lim['maxContactGapRel'])
            if clip in ('Walk', 'Run'):
                rep['stanceTravel_m'] = round(float(np.mean(push)) if push else 0.0, 4)
                if v <= 0 or (push and np.mean(push) <= 0):
                    warn(clip, 'facing', 'feet do not push backwards (walking backwards / moonwalk)',
                         rep['stanceTravel_m'], '> 0')
        # loop continuity: same pose at both ends, and matching velocity across the seam
        if c.get('loop'):
            scene.frame_set(c['start'])
            a0 = pose_snapshot(arm)
            scene.frame_set(c['start'] + 1)
            a1 = pose_snapshot(arm)
            scene.frame_set(c['end'] - 1)
            b1 = pose_snapshot(arm)
            scene.frame_set(c['end'])
            b0 = pose_snapshot(arm)
            deg, rel = pose_difference(a0, b0, h)
            rep['loop'] = {'poseErrorDeg': round(deg, 3), 'poseErrorRel': round(rel, 5),
                           'seamSpeedMismatchDegPerFrame': round(seam_velocity_mismatch(a0, a1, b1, b0), 3)}
            if deg > lim['maxLoopPoseErrorDeg'] or rel > lim['maxLoopPoseErrorRel']:
                warn(clip, 'loop', 'loop does not close', [round(deg, 3), round(rel, 5)],
                     [lim['maxLoopPoseErrorDeg'], lim['maxLoopPoseErrorRel']])
        clips[clip] = rep
    # authored transitions (U1-CHAR.6): a clip that starts or ends in another clip's pose must match it exactly,
    # same limits as a loop seam (Sleep -> Wake -> Idle has no jump)
    transitions = []
    rc = rig['clips']
    offsets = (rig.get('motion') or {}).get('rootOffsets_m', {})
    for a_clip, a_frame, b_clip, b_frame, off_key in TRANSITIONS:
        if a_clip in rc and b_clip in rc:
            scene.frame_set(rc[a_clip][a_frame])
            pa = pose_snapshot(arm)
            scene.frame_set(rc[b_clip][b_frame])
            pb = pose_snapshot(arm)
            deg, rel = pose_difference(pa, pb, h, ignore_root_z=off_key is not None)
            item = {'from': f'{a_clip}.{a_frame}', 'to': f'{b_clip}.{b_frame}', 'poseErrorDeg': round(deg, 3), 'poseErrorRel': round(rel, 5)}
            if off_key is not None:
                measured = float(pb['Root'][0][2, 3] - pa['Root'][0][2, 3])
                # Land.start sits above Fly by the offset; TakeOff.end sits above Fly by the offset (sign: a -> b)
                expected = -offsets.get(off_key, 0.0) if a_clip == 'TakeOff' else offsets.get(off_key, 0.0)
                item.update({'rootOffsetMeasured_m': round(measured, 4), 'rootOffsetDeclared_m': round(expected, 4)})
                if abs(measured - expected) > lim['maxLoopPoseErrorRel'] * h:
                    warn(b_clip, 'transition', f'{a_clip}.{a_frame} -> {b_clip}.{b_frame} root offset differs from motion.rootOffsets_m',
                         [round(measured, 4), round(expected, 4)], lim['maxLoopPoseErrorRel'])
            transitions.append(item)
            if deg > lim['maxLoopPoseErrorDeg'] or rel > lim['maxLoopPoseErrorRel']:
                warn(b_clip, 'transition', f'{a_clip}.{a_frame} -> {b_clip}.{b_frame} poses differ', [round(deg, 3), round(rel, 5)],
                     [lim['maxLoopPoseErrorDeg'], lim['maxLoopPoseErrorRel']])
    # motion envelope (U1-CHAR.7): space the animated body needs per medium, for Environment integration
    def extent(names, axis):
        v = [c['bounds']['max'][axis] - c['bounds']['min'][axis] for n, c in clips.items() if n in names and 'bounds' in c]
        return round(max(v), 4) if v else None
    ground = [n for n in clips if n not in WATER_CLIPS and n not in AIR_CLIPS and n not in ('WaterEnter', 'WaterExit')]
    envelope = {
        'groundHeight_m': extent(ground, 2), 'groundLength_m': extent(ground, 1), 'groundWidth_m': extent(ground, 0),
        'swimVerticalExtent_m': extent(WATER_CLIPS, 2), 'swimWidth_m': extent(WATER_CLIPS, 0),
        'flightSpan_m': extent(AIR_CLIPS, 0), 'flightVerticalExtent_m': extent(('Fly', 'Glide'), 2),
        'note': 'Largest animated extent over the clips of each medium. Submerged swimming needs water deeper than '
                'swimVerticalExtent_m; flight needs a clear width of flightSpan_m. Margins are the Sanctuary\'s choice.',
    }
    scene.frame_set(1)
    images = None
    if a.images:
        png = a.out_dir / f'{name}_animqa.png'
        if png.exists() and not a.overwrite_outputs:
            raise SystemExit(f'Output exists: {png}')
        images = {'file': png.name, 'rows': render_sheet(mesh, arm, worst_frames, png, h),
                  'views': ['side (+X)', 'three-quarter front-left']}
    report = {
        'qaVersion': QA_VERSION, 'name': name, 'species': rig.get('species'), 'archetype': rig.get('archetype'),
        'rigVersion': rig.get('rigVersion'), 'skinning': skinning, 'limits': lim, 'weights': weights, 'bones': bones_rep,
        'height': height, 'lods': lods, 'wingProbeVertices': wing_probe_count if len(wing_v) else None, 'clips': clips, 'transitions': transitions, 'motionEnvelope': envelope, 'images': images,
        'warnings': warnings, 'status': 'PASS' if not warnings else 'PASS_WITH_WARNINGS',
        'note': 'Automated geometric checks. They do not replace an animator review of the motion.',
    }
    cc.write_json(out_json, report)
    print(f"Animation QA {name}: {report['status']}, {len(warnings)} warning(s) -> {out_json}")


if __name__ == '__main__':
    main()
