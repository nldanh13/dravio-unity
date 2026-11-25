"""Fit an archetype skeleton to a static dragon mesh, bind it and author procedural clips.

  blender --background --python scripts/art/character/rig_archetype.py -- <prepared.glb> <out_dir> \
      --species zephyr [--stage Baby|Juvenile|Adult|Unstaged] [--limb-count 4] [--yaw-offset 0] [--height 1.0]

Archetype and clip list come from Assets/Dravio/Character/Data/dragon-archetypes.json (canonical).
This is NOT the Emberling rig applied to everything: each archetype has its own template
(quadruped / winged / serpentine / aquatic). A walking species whose limb count is not stated in
canonical data is refused until --limb-count (from the art director/GDD) is given.

Binding: bone-heat weights are solved on a watertight voxel proxy, then transferred to the real
mesh (generated meshes are often open/multi-island, where direct bone heat fails), limited to 4
influences and normalised. Anchors (VFX_*) are exported as weightless bones.

Outputs: <Name>_Rigged.fbx/.blend, <Name>_rig.json (bones, clip ranges + loop flags, IK chains,
anchors, deformation report). Clips are one take; Unity splits it with the ranges in rig.json.
Procedural motion is a functional placeholder for review, not final animation.
"""
import argparse
import json
import math
import statistics
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402

REPO = Path(__file__).resolve().parents[3]
ARCHETYPES = REPO / 'Assets/Dravio/Character/Data/dragon-archetypes.json'
FPS = 30
CLIP_FRAMES = {'Idle': 120, 'Walk': 32, 'Run': 20, 'Eat': 60, 'Sleep': 120, 'Happy': 45,
               'Swim': 48, 'Fly': 32, 'Burrow': 60}
LOOPING = {'Idle', 'Walk', 'Run', 'Sleep', 'Swim', 'Fly', 'Burrow'}
DEFORM_LIMITS = {'max_stretch_p999': 1.6, 'min_compress_p001': 0.45}


# ---------------------------------------------------------------- import / normalise
def import_static(path, yaw, height):
    cc.load_model(path, merge_vertices=True)
    objs = cc.mesh_objects()
    if not objs:
        raise SystemExit('No mesh')
    if any(o.type == 'ARMATURE' for o in bpy.context.scene.objects):
        raise SystemExit('Source already rigged; rig_archetype expects a static mesh')
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.parent = None
    obj.matrix_world = Matrix.Rotation(math.radians(yaw), 4, 'Z') @ obj.matrix_world
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    lo, hi = cc.bounds_of([v.co for v in obj.data.vertices])
    s = height / max(hi.z - lo.z, 1e-9)
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for v in obj.data.vertices:
        v.co = (v.co - c) * s
    obj.data.update()
    return obj


# ---------------------------------------------------------------- landmarks
class Shape:
    def __init__(self, obj):
        self.v = [v.co.copy() for v in obj.data.vertices]
        self.lo, self.hi = cc.bounds_of(self.v)
        self.h = self.hi.z - self.lo.z
        self.w = self.hi.x - self.lo.x
        self.d = self.hi.y - self.lo.y

    def pct(self, vals, q):
        vals = sorted(vals)
        return vals[int(round((len(vals) - 1) * q))]

    def slice_centroids(self, n, lo_y=None, hi_y=None):
        """Centroid of the mesh in n slabs along Y (front = -Y), as a spine guide."""
        lo_y = self.lo.y if lo_y is None else lo_y
        hi_y = self.hi.y if hi_y is None else hi_y
        out = []
        for i in range(n):
            a = lo_y + (hi_y - lo_y) * i / n
            b = lo_y + (hi_y - lo_y) * (i + 1) / n
            pts = [p for p in self.v if a <= p.y <= b]
            if pts:
                out.append(Vector((statistics.median(p.x for p in pts), (a + b) / 2,
                                   statistics.median(p.z for p in pts))))
            else:
                out.append(Vector((0, (a + b) / 2, self.h * 0.5)))
        return out

    def feet(self, count):
        """Lowest vertex clusters: count in {2, 4}. Front feet are toward -Y."""
        low = [p for p in self.v if p.z < self.lo.z + 0.15 * self.h]
        res = {}
        for side, sign in (('L', -1), ('R', 1)):
            pts = [p for p in low if p.x * sign > 0.03 * self.w] or low
            ys = sorted(p.y for p in pts)
            groups = [('Hind', 0.75)] if count == 2 else [('Fore', 0.2), ('Hind', 0.8)]
            for part, q in groups:
                y = self.pct(ys, q)
                near = [p for p in pts if abs(p.y - y) < 0.1 * self.d] or pts
                res[(part, side)] = Vector((statistics.median(p.x for p in near),
                                            statistics.median(p.y for p in near), self.lo.z + 0.03 * self.h))
        return res

    def lateral_tip(self, sign, zmin):
        pts = [p for p in self.v if p.x * sign > 0.25 * self.w and p.z > zmin]
        return max(pts, key=lambda p: p.x * sign) if pts else None


# ---------------------------------------------------------------- skeleton templates
def spine_chain(shape, names, y0, y1, z_scale=1.0):
    pts = shape.slice_centroids(len(names) + 1, y0, y1)
    return [(n, pts[i], pts[i + 1]) for i, n in enumerate(names)]


def template(arch, shape, limb_count):
    """List of (name, head, tail, parent, deform). Coordinates in Blender space (front = -Y, up = Z)."""
    S = shape
    b = []

    def add(name, head, tail, parent, deform=True):
        head, tail = Vector(head), Vector(tail)
        if (tail - head).length < 0.01 * S.h:
            tail = head + Vector((0, 0, 0.03 * S.h))
        b.append((name, head, tail, parent, deform))

    add('Root', (0, 0, 0), (0, 0, 0.1 * S.h), None)
    front, back = S.lo.y, S.hi.y
    if arch in ('quadruped', 'walker', 'winged'):
        # Body: neck/head at the front 30 %, tail in the back 30 % (slab centroids follow the mesh).
        body = spine_chain(S, ['Pelvis', 'Spine', 'Chest'], front + 0.70 * S.d, front + 0.30 * S.d)
        parent = 'Root'
        for n, h, t in body:
            add(n, h, t, parent); parent = n
        neck = spine_chain(S, ['Neck', 'Head'], front + 0.30 * S.d, front + 0.02 * S.d)
        parent = 'Chest'
        for n, h, t in neck:
            add(n, h, t, parent); parent = n
        tail = spine_chain(S, ['Tail1', 'Tail2', 'Tail3'], front + 0.70 * S.d, back - 0.01 * S.d)
        parent = 'Pelvis'
        for n, h, t in tail:
            add(n, h, t, parent); parent = n
        if limb_count:
            feet = S.feet(limb_count)
            for (part, side), foot in feet.items():
                hip_parent = 'Chest' if part == 'Fore' else 'Pelvis'
                hip = Vector((foot.x * 0.8, foot.y, S.lo.z + 0.42 * S.h))
                knee = Vector((foot.x, foot.y + (0.03 if part == 'Fore' else -0.03) * S.d, S.lo.z + 0.2 * S.h))
                ankle = Vector((foot.x, foot.y, S.lo.z + 0.06 * S.h))
                toe = Vector((foot.x, foot.y - 0.06 * S.d, S.lo.z + 0.02 * S.h))
                add(f'{part}Upper_{side}', hip, knee, hip_parent)
                add(f'{part}Lower_{side}', knee, ankle, f'{part}Upper_{side}')
                add(f'{part}Foot_{side}', ankle, toe, f'{part}Lower_{side}')
        if arch == 'winged':
            for side, sign in (('L', -1), ('R', 1)):
                tip = S.lateral_tip(sign, S.lo.z + 0.35 * S.h) or Vector((sign * 0.5 * S.w, front + 0.45 * S.d, 0.7 * S.h))
                root = Vector((sign * 0.12 * S.w, front + 0.40 * S.d, S.lo.z + 0.62 * S.h))
                elbow = root.lerp(tip, 0.4); wrist = root.lerp(tip, 0.75)
                add(f'WingUpper_{side}', root, elbow, 'Chest')
                add(f'WingFore_{side}', elbow, wrist, f'WingUpper_{side}')
                add(f'WingTip_{side}', wrist, tip, f'WingFore_{side}')
    elif arch == 'serpentine':
        names = ['Spine%d' % i for i in range(1, 9)] + ['Tail%d' % i for i in range(1, 5)]
        chain = spine_chain(S, ['Head', 'Neck'] + names, front, back)
        # head-first slabs: reverse so the chain grows from the body to the head and the tail
        mid = 2 + 4  # Spine4 is the body centre
        body = chain[2:]
        parent = 'Root'
        prev = None
        for n, h, t in body:
            add(n, h, t, parent if prev is None else prev); prev = n
        add('Neck', chain[1][2], chain[1][1], 'Spine1')
        add('Head', chain[1][1], chain[0][1], 'Neck')
        if limb_count:
            feet = S.feet(limb_count)
            for (part, side), foot in feet.items():
                hip_parent = 'Spine2' if part == 'Fore' else 'Spine6'
                hip = Vector((foot.x * 0.7, foot.y, S.lo.z + 0.3 * S.h))
                knee = Vector((foot.x, foot.y, S.lo.z + 0.15 * S.h))
                ankle = Vector((foot.x, foot.y, S.lo.z + 0.04 * S.h))
                add(f'{part}Upper_{side}', hip, knee, hip_parent)
                add(f'{part}Lower_{side}', knee, ankle, f'{part}Upper_{side}')
                add(f'{part}Foot_{side}', ankle, ankle + Vector((0, -0.05 * S.d, 0)), f'{part}Lower_{side}')
        del mid
    elif arch == 'aquatic':
        chain = spine_chain(S, ['Head', 'Neck', 'Spine1', 'Spine2', 'Spine3', 'Spine4', 'Tail1', 'Tail2', 'Tail3'], front, back)
        body = chain[2:]
        prev = None
        for n, h, t in body:
            add(n, h, t, 'Root' if prev is None else prev); prev = n
        add('Neck', chain[1][2], chain[1][1], 'Spine1')
        add('Head', chain[1][1], chain[0][1], 'Neck')
        for side, sign in (('L', -1), ('R', 1)):
            tip = S.lateral_tip(sign, S.lo.z) or Vector((sign * 0.5 * S.w, front + 0.35 * S.d, 0.4 * S.h))
            root = Vector((sign * 0.1 * S.w, tip.y, tip.z))
            add(f'Fin_{side}', root, tip, 'Spine2')
        last = body[-1]
        add('TailFin', last[2], last[2] + (last[2] - last[1]) * 0.6, 'Tail3')
    else:
        raise SystemExit(f'No template for archetype {arch}')
    # Anchors (weightless): mouth, tail tip, back, ground.
    head = next(x for x in b if x[0] == 'Head')
    tail_last = [x for x in b if x[0].startswith('Tail')][-1]
    back_parent = 'Chest' if any(x[0] == 'Chest' for x in b) else 'Spine3'
    back_pt = next(x for x in b if x[0] == back_parent)[2]
    add('VFX_Mouth', head[2], head[2] + Vector((0, -0.04 * S.d, 0)), 'Head', deform=False)
    add('VFX_TailTip', tail_last[2], tail_last[2] + Vector((0, 0.04 * S.d, 0)), tail_last[0], deform=False)
    add('VFX_Back', back_pt + Vector((0, 0, 0.15 * S.h)), back_pt + Vector((0, 0, 0.2 * S.h)), back_parent, deform=False)
    add('VFX_Ground', (0, 0, 0), (0, 0, 0.05 * S.h), 'Root', deform=False)
    return b


def build_armature(spec):
    arm_data = bpy.data.armatures.new('DragonSkeleton')
    arm = bpy.data.objects.new('DragonRig', arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    for name, h, t, parent, deform in spec:
        eb = arm_data.edit_bones.new(name)
        eb.head, eb.tail = h, t
        eb.use_deform = deform
        if parent:
            eb.parent = arm_data.edit_bones[parent]
    # Roll: keep local Z close to world up for predictable rotations.
    for eb in arm_data.edit_bones:
        eb.align_roll(Vector((0, 0, 1)) if abs((eb.tail - eb.head).normalized().z) < 0.9 else Vector((0, -1, 0)))
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


# ---------------------------------------------------------------- binding
def _closest_on_segment(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
    return a + ab * t


def bind_via_proxy(obj, arm, max_inf=4):
    """Bone heat on a voxel-remeshed watertight proxy, transferred to the real mesh."""
    proxy = obj.copy()
    proxy.data = obj.data.copy()
    proxy.name = 'WeightProxy'
    bpy.context.scene.collection.objects.link(proxy)
    lo, hi = cc.bounds_of([v.co for v in obj.data.vertices])
    rem = proxy.modifiers.new('Voxel', 'REMESH')
    rem.mode = 'VOXEL'
    voxel = max((hi - lo).length / 140.0, 1e-4)
    rem.voxel_size = voxel
    bpy.context.view_layer.objects.active = proxy
    bpy.ops.object.modifier_apply(modifier=rem.name)
    bpy.ops.object.select_all(action='DESELECT')
    proxy.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    if 'FINISHED' not in bpy.ops.object.parent_set(type='ARMATURE_AUTO'):
        raise RuntimeError('Bone heat failed on the proxy')
    # Transfer to the real mesh.
    for g in proxy.vertex_groups:
        obj.vertex_groups.new(name=g.name)
    dt = obj.modifiers.new('WeightTransfer', 'DATA_TRANSFER')
    dt.object = proxy
    dt.use_vert_data = True
    dt.data_types_verts = {'VGROUP_WEIGHTS'}
    dt.vert_mapping = 'POLYINTERP_NEAREST'
    dt.layers_vgroup_select_src = 'ALL'
    dt.layers_vgroup_select_dst = 'NAME'
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=dt.name)
    bpy.data.objects.remove(proxy, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=1e-4)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=max_inf)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    deform = {b.name for b in arm.data.bones if b.use_deform}
    unweighted = [v.index for v in obj.data.vertices
                  if not any(obj.vertex_groups[g.group].name in deform and g.weight > 0 for g in v.groups)]
    fallback = 0
    if unweighted:
        # Typically tiny floating fragments the voxel proxy dropped: bind each rigidly to the
        # nearest deform bone segment and report the count (cleanup is an art decision).
        segs = [(b.name, b.head_local, b.tail_local) for b in arm.data.bones if b.use_deform]
        for i in unweighted:
            p = obj.data.vertices[i].co
            best = min(segs, key=lambda s: (p - _closest_on_segment(p, s[1], s[2])).length)
            g = obj.vertex_groups.get(best[0]) or obj.vertex_groups.new(name=best[0])
            g.add([i], 1.0, 'REPLACE')
        fallback = len(unweighted)
    obj.parent = arm
    mod = obj.modifiers.new('Armature', 'ARMATURE')
    mod.object = arm
    mod.use_deform_preserve_volume = True
    return {'proxy_voxel_size': round(voxel, 5), 'vertices': len(obj.data.vertices),
            'nearest_bone_fallback_vertices': fallback}


# ---------------------------------------------------------------- animation
def rot_arm(axis, angle):
    return Matrix.Rotation(angle, 3, axis)


def set_rot(arm, pb, R):
    """Apply an armature-space rotation delta R to a pose bone (converted to its local frame)."""
    B = arm.data.bones[pb.name].matrix_local.to_3x3()
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = (B.inverted() @ R @ B).to_quaternion()


def pose_frame(arm, arch, clip, t, has):
    """t in [0,1) within the clip. Armature axes: X right, Y back, Z up."""
    P = arm.pose.bones
    tau = 2 * math.pi
    R = {n: Matrix.Identity(3) for n in P.keys()}
    loc = {}

    def mul(n, axis, a):
        if n in R:
            R[n] = rot_arm(axis, a) @ R[n]

    breathe = math.sin(tau * t * (2 if clip == 'Idle' else 1))
    spine_names = [n for n in ('Spine', 'Chest', 'Spine1', 'Spine2', 'Spine3', 'Spine4') if n in R]
    tails = [n for n in ('Tail1', 'Tail2', 'Tail3', 'Tail4', 'TailFin') if n in R]
    for n in spine_names:
        mul(n, 'X', 0.02 * breathe)
    mul('Head', 'X', 0.04 * math.sin(tau * t))
    for i, n in enumerate(tails):  # tail sway, travelling wave
        mul(n, 'Z', 0.12 * math.sin(tau * t - 0.6 * i))
    legs = [(p, s) for p in ('Fore', 'Hind') for s in ('L', 'R') if f'{p}Upper_{s}' in R]
    phase = {('Fore', 'L'): 0.0, ('Hind', 'R'): 0.0, ('Fore', 'R'): 0.5, ('Hind', 'L'): 0.5}
    if clip in ('Walk', 'Run'):
        amp = 0.35 if clip == 'Walk' else 0.6
        for p, s in legs:
            ph = tau * (t + phase.get((p, s), 0.0))
            mul(f'{p}Upper_{s}', 'X', amp * math.sin(ph))
            mul(f'{p}Lower_{s}', 'X', (-1 if p == 'Fore' else 1) * amp * 0.8 * max(0.0, math.cos(ph)))
        if 'Pelvis' in R or 'Spine1' in R:
            loc['Root'] = Vector((0, 0, 0.02 * abs(math.sin(2 * tau * t)) * (2 if clip == 'Run' else 1)))
        if not legs:  # limbless travel: lateral undulation
            chain = [n for n in R if n.startswith('Spine') or n.startswith('Tail')]
            for i, n in enumerate(sorted(chain)):
                mul(n, 'Z', 0.18 * math.sin(tau * t - 0.7 * i))
    elif clip == 'Swim':
        chain = sorted(n for n in R if n.startswith('Spine') or n.startswith('Tail'))
        for i, n in enumerate(chain):
            mul(n, 'Z', 0.16 * math.sin(tau * t - 0.6 * i))
        for s, sign in (('L', 1), ('R', -1)):
            mul(f'Fin_{s}', 'Y', sign * 0.5 * math.sin(tau * t))
        for p, s in legs:  # paddling
            mul(f'{p}Upper_{s}', 'X', 0.5 * math.sin(tau * (t + phase.get((p, s), 0))))
    elif clip == 'Fly':
        for s, sign in (('L', 1), ('R', -1)):
            flap = math.sin(tau * t)
            mul(f'WingUpper_{s}', 'Y', sign * 0.7 * flap)
            mul(f'WingFore_{s}', 'Y', sign * 0.35 * math.sin(tau * t - 0.5))
            mul(f'WingTip_{s}', 'Y', sign * 0.25 * math.sin(tau * t - 1.0))
        for p, s in legs:  # tucked
            mul(f'{p}Upper_{s}', 'X', 0.6 if p == 'Hind' else -0.6)
            mul(f'{p}Lower_{s}', 'X', -0.8 if p == 'Hind' else 0.8)
        loc['Root'] = Vector((0, 0, 0.03 * math.sin(tau * t)))
    elif clip == 'Eat':
        dip = max(0.0, math.sin(tau * t * 2))
        mul('Neck', 'X', 0.45 * dip)
        mul('Head', 'X', 0.3 * dip)
    elif clip == 'Sleep':
        mul('Neck', 'X', 0.45)
        mul('Head', 'X', 0.2)
        for i, n in enumerate(tails):
            mul(n, 'Z', 0.3)
        for p, s in legs:
            mul(f'{p}Upper_{s}', 'X', 0.5 if p == 'Fore' else -0.45)
            mul(f'{p}Lower_{s}', 'X', -1.0 if p == 'Fore' else 0.9)
        if legs:
            loc['Root'] = Vector((0, 0, -0.18))
        for s, sign in (('L', 1), ('R', -1)):
            mul(f'WingUpper_{s}', 'Y', -sign * 0.25)  # folded closer to body
    elif clip == 'Happy':
        for i, n in enumerate(tails):
            mul(n, 'Z', 0.25 * math.sin(4 * tau * t - 0.5 * i))
        mul('Head', 'X', -0.15 * math.sin(2 * tau * t))
        loc['Root'] = Vector((0, 0, 0.03 * abs(math.sin(2 * tau * t))))
    elif clip == 'Burrow':
        for p, s in legs:
            if p == 'Fore':
                ph = tau * (2 * t + (0.5 if s == 'R' else 0))
                mul(f'ForeUpper_{s}', 'X', 0.6 * math.sin(ph))
                mul(f'ForeLower_{s}', 'X', 0.5 * max(0.0, math.cos(ph)))
        mul('Neck', 'X', 0.3)
    return R, loc


def author_clips(arm, arch, clips, height):
    scene = bpy.context.scene
    scene.render.fps = FPS
    arm.animation_data_create()
    action = bpy.data.actions.new('Dragon_Clips')
    arm.animation_data.action = action
    ranges = {}
    start = 1
    for clip in clips:
        n = CLIP_FRAMES[clip]
        ranges[clip] = {'start': start, 'end': start + n, 'loop': clip in LOOPING}
        for f in range(start, start + n + 1):
            t = (f - start) / n
            R, loc = pose_frame(arm, arch, clip, t % 1.0 if clip in LOOPING else min(t, 1.0), None)
            for pb in arm.pose.bones:
                set_rot(arm, pb, R[pb.name])
                pb.location = loc.get(pb.name, Vector()) * height if pb.name == 'Root' else Vector()
                pb.keyframe_insert('rotation_quaternion', frame=f, group=pb.name)
                pb.keyframe_insert('location', frame=f, group=pb.name)
        start += n + 1
    scene.frame_start, scene.frame_end = 1, start - 1
    return ranges


# ---------------------------------------------------------------- deformation QA
def deformation_report(obj, ranges):
    me = obj.data
    edges = [(e.vertices[0], e.vertices[1]) for e in me.edges]
    rest = [(me.vertices[a].co - me.vertices[b].co).length for a, b in edges]
    scene = bpy.context.scene
    out = {}
    for clip, r in ranges.items():
        ratios = []
        for k in range(4):
            f = r['start'] + int((r['end'] - r['start']) * (k + 0.5) / 4)
            scene.frame_set(f)
            dg = bpy.context.evaluated_depsgraph_get()
            ev = obj.evaluated_get(dg)
            m = ev.to_mesh()
            for (a, b), l0 in zip(edges, rest):
                if l0 > 1e-9:
                    ratios.append((m.vertices[a].co - m.vertices[b].co).length / l0)
            ev.to_mesh_clear()
        ratios.sort()
        p999 = ratios[int(0.999 * (len(ratios) - 1))]
        p001 = ratios[int(0.001 * (len(ratios) - 1))]
        out[clip] = {'edge_stretch_p999': round(p999, 3), 'edge_compress_p001': round(p001, 3),
                     'edge_stretch_max': round(ratios[-1], 3),
                     'ok': p999 <= DEFORM_LIMITS['max_stretch_p999'] and p001 >= DEFORM_LIMITS['min_compress_p001']}
    scene.frame_set(1)
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('source', type=Path)
    ap.add_argument('out_dir', type=Path)
    ap.add_argument('--species', required=True)
    ap.add_argument('--stage', default='Unstaged', choices=['Baby', 'Juvenile', 'Adult', 'Unstaged'],
                    help='Naming/report only; morphology per stage is not inferred')
    ap.add_argument('--limb-count', type=int, choices=[0, 2, 4],
                    help='Walking limb count when canonical data does not state it (art director/GDD value)')
    ap.add_argument('--yaw-offset', type=float, default=0.0, help='Degrees about +Z so the front faces -Y')
    ap.add_argument('--height', type=float, default=1.0,
                    help='Rig-space height in metres. Stage sizes are not in canonical data; scale in Unity.')
    ap.add_argument('--name')
    ap.add_argument('--overwrite-outputs', action='store_true')
    a = ap.parse_args(cc.script_args())
    arch_all = json.loads(ARCHETYPES.read_text(encoding='utf-8'))
    sp = next((s for s in arch_all['species'] if s['id'] == a.species), None)
    if sp is None:
        raise SystemExit(f'Unknown species id {a.species}')
    limb = sp['limbCount'] if sp['limbCount'] is not None else a.limb_count
    if sp['walkingLimbs'] and limb is None:
        raise SystemExit(f'{a.species}: walks (canonical) but limb count is not stated; '
                         'pass --limb-count from the art director/GDD (not guessed).')
    if sp['limbCount'] is not None and a.limb_count is not None and a.limb_count != sp['limbCount']:
        raise SystemExit(f'{a.species}: --limb-count {a.limb_count} contradicts canonical {sp["limbCount"]}')
    if not sp['walkingLimbs']:
        limb = 0
    arch = sp['archetype']
    if arch == 'walker':
        if limb != 4:
            raise SystemExit(f'{a.species}: no walker template for limbCount={limb} yet')
        arch = 'quadruped'
    name = a.name or f'DRG_{a.species}_{a.stage}'
    src = a.source.resolve()
    outs = {k: cc.guard_output(src, a.out_dir / f'{name}{s}', a.overwrite_outputs)
            for k, s in [('fbx', '_Rigged.fbx'), ('blend', '_Rigged.blend'), ('json', '_rig.json')]}
    before = cc.sha256(src)
    obj = import_static(src, a.yaw_offset, a.height)
    obj.name = name
    shape = Shape(obj)
    spec = template(arch, shape, limb)
    arm = build_armature(spec)
    anchors = [n for n, *_r, d in spec if not d]
    for n in anchors:  # kept out of the heat solve
        arm.data.bones[n].use_deform = False
    bind = bind_via_proxy(obj, arm)
    ranges = author_clips(arm, arch, sp['clips'], a.height)
    deform = deformation_report(obj, ranges)
    for n in anchors:  # exported (deform-only FBX) but weightless
        arm.data.bones[n].use_deform = True
    ik = [{'upper': f'{p}Upper_{s}', 'lower': f'{p}Lower_{s}', 'foot': f'{p}Foot_{s}'}
          for p in ('Fore', 'Hind') for s in ('L', 'R') if f'{p}Upper_{s}' in arm.data.bones]
    bpy.ops.wm.save_as_mainfile(filepath=str(outs['blend']), copy=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    r = bpy.ops.export_scene.fbx(filepath=str(outs['fbx']), use_selection=True, object_types={'MESH', 'ARMATURE'},
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, add_leaf_bones=False,
                                 use_armature_deform_only=True, mesh_smooth_type='FACE', path_mode='COPY',
                                 embed_textures=True, bake_anim=True, bake_anim_use_all_actions=False,
                                 bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                                 bake_anim_simplify_factor=0.0)
    if 'FINISHED' not in r:
        raise RuntimeError('FBX export failed')
    after = cc.sha256(src)
    report = {'name': name, 'species': a.species, 'stage': a.stage, 'archetype': arch,
              'archetypeSource': sp['archetypeReason'], 'limbCount': limb,
              'limbCountSource': sp['limbCountSource'] or ('--limb-count' if limb else 'canonical: no walking limbs'),
              'height_m': a.height, 'fps': FPS, 'bones': [b.name for b in arm.data.bones],
              'deformBones': [b.name for b in arm.data.bones if b.name not in anchors],
              'anchors': anchors, 'ikChains': ik, 'clips': ranges, 'bind': bind,
              'skin': cc.skin_stats(obj), 'deformation': deform,
              'deformationLimits': DEFORM_LIMITS,
              'deformationOk': all(v['ok'] for v in deform.values()),
              'triangles': cc.triangle_count(obj.data), 'fbx': outs['fbx'].name,
              'source': str(src), 'source_sha256_before': before, 'source_sha256_after': after,
              'source_unchanged': before == after,
              'limits': 'Procedural placeholder motion and bounding-box/slab-fitted bones; needs animator review.'}
    cc.write_json(outs['json'], report)
    print(f"Rigged {name}: {arch}, {len(report['bones'])} bones, clips {list(ranges)}; "
          f"deformation ok={report['deformationOk']}")


if __name__ == '__main__':
    main()
