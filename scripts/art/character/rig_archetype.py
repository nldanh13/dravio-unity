"""Production rig v2: geometry-fitted archetype skeleton, region-restricted skinning, IK-planted clips.

  blender --background --python scripts/art/character/rig_archetype.py -- <prepared.glb> <out_dir> \
      --species zephyr [--stage Baby|Juvenile|Adult|Unstaged] [--limb-count 0|2|4|auto] \
      [--landmarks landmarks.json] [--yaw-offset 0] [--height M] [--name N] [--overwrite-outputs]

Biology comes from canonical data (Data/dragon-archetypes.json): archetype, clip set, wings. A walking
species whose limb count is not stated is refused unless --limb-count is given by the art director, or
--limb-count auto accepts the count measured on THIS mesh (left/right ground-contact pairs, high
confidence only). The rig is never Emberling's rig reused: every model gets its own skeleton.

Skeleton: landmarks from rig_geometry.py (head blob, neck narrowing, chest/pelvis over the feet, tail
taper, ground-contact feet, wing/fin surfaces), optionally replaced by manual landmarks (--landmarks).
Size: kept as authored. Only --height or a species heightM in Data/rig-profiles.json rescales
(emberling 0.90 m from project data); the scale used is reported.

Skinning: bone heat on a voxel proxy transferred to the mesh, then masked per vertex region (legs only
on their leg and hip; head/eyes only head and neck; tail only tail and pelvis; wings/fins only their
own chain), limited to 4 influences, normalised; vertices left without weight get the nearest allowed
bone. Clips: in-place locomotion with feet solved by analytic two-bone IK along planted stance paths
(no foot sliding by construction), body motion from Data/rig-profiles.json, keyed as plain FK (no
constraints in the FBX). Root motion is not used: Root carries only vertical offsets, horizontal travel
is the integration layer's job at rig.json clips.<name>.groundSpeed.

Outputs: <Name>_Rigged.fbx/.blend, <Name>_rig.json. Run anim_qa.py on the .blend for the full QA.
"""
import argparse
import json
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402
import rig_geometry as rg  # noqa: E402

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

REPO = Path(__file__).resolve().parents[3]
ARCHETYPES = REPO / 'Assets/Dravio/Character/Data/dragon-archetypes.json'
PROFILES = REPO / 'Assets/Dravio/Character/Data/rig-profiles.json'
RIG_VERSION = 2
FPS = 30
BASE_FRAMES = {'Idle': 120, 'Eat': 60, 'Sleep': 120, 'Happy': 45, 'Swim': 48, 'Burrow': 60,
               'Fly': 32, 'Glide': 60, 'TakeOff': 40, 'Land': 40}
LOOPING = {'Idle', 'Walk', 'Run', 'Sleep', 'Swim', 'Fly', 'Burrow', 'Glide'}
NO_GROUND = {'Swim', 'Fly', 'Glide'}  # no ground contact: ground IK and clearance are off
BUDGETS = REPO / 'Assets/Dravio/Character/Data/character-budgets.json'
_QA = json.loads(BUDGETS.read_text(encoding='utf-8')).get('animationQa', {})
DEFORM_LIMITS = {'max_stretch_p999': _QA.get('maxStretchP999', 1.6), 'min_compress_p001': _QA.get('minCompressP001', 0.45)}


def deep_merge(a, b):
    out = dict(a)
    for k, v in b.items():
        out[k] = deep_merge(out[k], v) if isinstance(v, dict) and isinstance(out.get(k), dict) else v
    return out


def load_profile(arch, species):
    data = json.loads(PROFILES.read_text(encoding='utf-8'))
    A = data['archetypes']
    base = A.get(arch, {})
    if 'inherits' in base:
        base = deep_merge(A[base['inherits']], base)
    return deep_merge(base, data['species'].get(species, {}))


# ---------------------------------------------------------------- import
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
    src_h = hi.z - lo.z
    s = (height / max(src_h, 1e-9)) if height else 1.0
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for v in obj.data.vertices:
        v.co = (v.co - c) * s
    obj.data.update()
    return obj, {'sourceHeight': round(src_h, 4), 'scale': s, 'height': round(src_h * s, 4)}


def analyse(obj, arch, limb_count, manual):
    me = obj.data
    V = np.array([v.co[:] for v in me.vertices])
    E = np.array([e.vertices[:] for e in me.edges])
    mask = rg.main_component_mask(len(V), E)
    an = rg.Analysis(V)
    res = an.run(arch, limb_count=limb_count, manual=manual, mask=mask)
    return an, res


# ---------------------------------------------------------------- skeleton
def V3(x):
    return Vector((float(x[0]), float(x[1]), float(x[2])))


def template(arch, an, prof, limb):
    lm = an.landmarks
    h = an.h
    b = []

    def add(name, head, tail, parent, deform=True):
        head, tail = V3(head), V3(tail)
        if (tail - head).length < 0.01 * h:
            tail = head + Vector((0, 0, 0.03 * h))
        b.append((name, head, tail, parent, deform))

    add('Root', (0, 0, 0), (0, 0, 0.1 * h), None)
    head_c, head_tip, neck = lm['headCenter'], lm['headTip'], lm['neckBase']
    head_base = head_c + 0.45 * (neck - head_c)
    n_tail = prof.get('tailBones', 3)
    tail = rg.resample([lm['tailBase']] + [p for p in lm['tailPoints'][1:]], n_tail)
    if arch in ('quadruped', 'winged'):
        pelvis, chest = lm['pelvis'], lm['chest']
        mid = rg.resample([pelvis, chest], 2)[1]
        add('Pelvis', pelvis, mid, 'Root')
        add('Spine', mid, chest, 'Pelvis')
        add('Chest', chest, neck, 'Spine')
        add('Neck', neck, head_base, 'Chest')
        add('Head', head_base, head_tip, 'Neck')
        parent = 'Pelvis'
        add('TailRoot', pelvis, tail[0], 'Pelvis') if np.linalg.norm(tail[0] - pelvis) > 0.08 * h else None
        if any(x[0] == 'TailRoot' for x in b):
            parent = 'TailRoot'
        for i in range(n_tail):
            add(f'Tail{i + 1}', tail[i], tail[i + 1], parent)
            parent = f'Tail{i + 1}'
        hip_parent = lambda part: 'Chest' if part == 'Fore' else 'Pelvis'  # noqa: E731
    else:
        n_sp = prof.get('spineBones', 8 if arch == 'serpentine' else 4)
        body = rg.resample(lm['bodyPoints'], n_sp)
        parent = 'Root'
        for i in range(n_sp):
            add(f'Spine{i + 1}', body[i], body[i + 1], parent)
            parent = f'Spine{i + 1}'
        add('Neck', neck, head_base, 'Spine1')
        add('Head', head_base, head_tip, 'Neck')
        for i in range(n_tail):
            add(f'Tail{i + 1}', tail[i], tail[i + 1], parent)
            parent = f'Tail{i + 1}'
        if arch == 'aquatic':
            last = tail[-1]
            add('TailFin', last, last + (tail[-1] - tail[-2]) * 0.6, f'Tail{n_tail}')

        def hip_parent(part, _body=body, _n=n_sp):
            k = [lm['hips'][x][1] for x in lm['hips'] if x.startswith(part)]
            y = float(np.mean(k)) if k else 0.0
            i = int(np.argmin([abs(0.5 * (_body[j][1] + _body[j + 1][1]) - y) for j in range(_n)]))
            return f'Spine{i + 1}'
    legs = {}
    if limb:
        for key in sorted(lm['feet']):
            part, side = key.split('_')
            foot, hip = lm['feet'][key], lm['hips'][key]
            leg_h = max(hip[2] - foot[2], 0.1 * h)
            ankle = np.array([foot[0], foot[1], foot[2] + max(0.04 * h, 0.14 * leg_h)])
            bend = np.array([0.0, 1.0 if part == 'Fore' else -1.0, 0.0]) * 0.09 * leg_h
            knee = 0.5 * (hip + ankle) + bend
            toe = ankle + np.array([0.0, -0.3 * leg_h, -(ankle[2] - foot[2]) * 0.8])
            add(f'{part}Upper_{side}', hip, knee, hip_parent(part))
            add(f'{part}Lower_{side}', knee, ankle, f'{part}Upper_{side}')
            add(f'{part}Foot_{side}', ankle, toe, f'{part}Lower_{side}')
            legs[key] = {'upper': f'{part}Upper_{side}', 'lower': f'{part}Lower_{side}', 'foot': f'{part}Foot_{side}'}
    for side, a in lm['appendages'].items():
        root, tip = a['root'], a['tip']
        if a['kind'] == 'wing':
            add(f'WingUpper_{side}', root, root + 0.38 * (tip - root), 'Chest' if arch in ('quadruped', 'winged') else 'Spine2')
            add(f'WingFore_{side}', root + 0.38 * (tip - root), root + 0.72 * (tip - root), f'WingUpper_{side}')
            add(f'WingTip_{side}', root + 0.72 * (tip - root), tip, f'WingFore_{side}')
        elif a['kind'] == 'earfin':
            add(f'EarFin_{side}', root, tip, 'Head')
        else:
            par = 'Chest' if arch in ('quadruped', 'winged') else 'Spine2'
            add(f'Fin_{side}', root, tip, par)
    # Anchors (weightless)
    chest_name = 'Chest' if any(x[0] == 'Chest' for x in b) else 'Spine2'
    chest_pt = next(x for x in b if x[0] == chest_name)[1]
    top = float(an.V[an.labels == 'body'][:, 2].max()) if np.any(an.labels == 'body') else chest_pt.z + 0.1 * h
    add('VFX_Mouth', head_tip, V3(head_tip) + Vector((0, -0.04 * an.d, 0)), 'Head', deform=False)
    last_tail = [x for x in b if x[0].startswith('Tail')][-1]
    add('VFX_TailTip', last_tail[2], last_tail[2] + Vector((0, 0.04 * an.d, 0)), last_tail[0], deform=False)
    add('VFX_Back', (0, chest_pt.y, top), (0, chest_pt.y, top + 0.05 * h), chest_name, deform=False)
    add('VFX_Ground', (0, 0, 0), (0, 0, 0.05 * h), 'Root', deform=False)
    return b, legs


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
    for eb in arm_data.edit_bones:
        # Roll: local Z toward world up for horizontal bones, toward -Y (front) for vertical bones (legs).
        eb.align_roll(Vector((0, 0, 1)) if abs((eb.tail - eb.head).normalized().z) < 0.7 else Vector((0, -1, 0)))
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


# ---------------------------------------------------------------- binding
def _closest_on_segment(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
    return a + ab * t


def heat_weights(obj, arm):
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
    return voxel


def mask_weights(obj, arm, an, legs, max_inf=4):
    """Region masks, 4 influences, normalisation, nearest-allowed fallback. Returns a report."""
    deform = {b.name for b in arm.data.bones if b.use_deform}
    segs = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm.data.bones if b.use_deform}
    gname = {g.index: g.name for g in obj.vertex_groups}
    hips = {k: arm.data.bones[v['upper']].head_local.copy() for k, v in legs.items()}
    hip_r = {k: 0.6 * arm.data.bones[v['upper']].length for k, v in legs.items()}
    removed_mass, total_mass = 0.0, 0.0
    fallback = {}
    # Paw/heel vertices that the leg tracking left in the body or tail region (low and closer to a
    # leg chain than to the tail line) belong to that leg; otherwise Pelvis 1.0 sits next to the paw.
    relabelled = 0
    if legs:
        co0 = np.array([v.co[:] for v in obj.data.vertices])
        lab = np.asarray(an.labels)
        low_z = 0.5 * min(h.z for h in hips.values())
        cand = np.nonzero(np.isin(lab, ['body', 'tail']) & (co0[:, 2] < low_z))[0]
        if len(cand):
            ks = list(legs)
            dl = np.stack([rg._dist_to_polyline(co0[cand], np.array(
                [arm.data.bones[legs[k]['upper']].head_local[:], arm.data.bones[legs[k]['lower']].head_local[:],
                 arm.data.bones[legs[k]['foot']].head_local[:], arm.data.bones[legs[k]['foot']].tail_local[:]]))
                for k in ks], axis=1)
            dt = rg._dist_to_polyline(co0[cand], an.landmarks['tailPoints'])
            for i, j, dmin, dtail in zip(cand, dl.argmin(axis=1), dl.min(axis=1), dt):
                if dmin < dtail:
                    an.labels[i] = 'leg'
                    an.leg_key[i] = ks[j]
                    relabelled += 1
    new_weights = []
    allowed_sets = []
    shoulder_rows = []
    leg_on_body = 0
    for v in obj.data.vertices:
        w = {gname[g.group]: g.weight for g in v.groups if gname.get(g.group) in deform and g.weight > 0}
        region = an.labels[v.index]
        if region == 'fragment':
            region = 'body'
        allowed = rg.allowed_bones(region, deform, leg_key=an.leg_key[v.index] or None,
                                   appendage_side=an.side[v.index] or None,
                                   appendage_kind=next((a['kind'] for s, a in an.landmarks['appendages'].items()
                                                        if s == an.side[v.index]), None))
        kept = {b: x for b, x in w.items() if b in allowed}
        shoulder = {}
        if region == 'body':  # shoulders/hips: the upper leg bone bends the body surface near the joint,
            for k, hp in hips.items():  # with a weight that fades smoothly to 0 at hip_r (no hard step)
                d = (v.co - hp).length
                if d < hip_r[k]:
                    shoulder[legs[k]['upper']] = 0.5 * (1 - d / hip_r[k]) ** 2
                    allowed = allowed | {legs[k]['upper']}
        total_mass += sum(w.values())
        removed_mass += sum(x for b, x in w.items() if b not in allowed)
        if region == 'body':
            leg_on_body += sum(1 for b in w if 'Lower' in b or 'Foot' in b)
        kept = rg.limit_and_normalize(kept, max_inf)
        if kept and shoulder:
            k_sh = min(sum(shoulder.values()), 0.6)
            kept = {b: x * (1 - k_sh) for b, x in kept.items()}
            for b, x in shoulder.items():
                kept[b] = kept.get(b, 0.0) + k_sh * x / sum(shoulder.values())
        if not kept:
            cands = [b for b in allowed if b in segs] or list(segs)
            best = min(cands, key=lambda n: (v.co - _closest_on_segment(v.co, *segs[n])).length)
            kept = {best: 1.0}
            fallback[region] = fallback.get(region, 0) + 1
        new_weights.append(kept)
        allowed_sets.append(allowed)
        shoulder_rows.append(bool(shoulder))
    # Legs and tails: replace bone heat by arc-length chain weights (heat leaks upper-leg
    # weight down to the paw on thick generated limbs, which tears the ankle on knee folds).
    bones = arm.data.bones
    co = np.array([v.co[:] for v in obj.data.vertices])
    chains = {}
    for key, chain in legs.items():
        nm = [chain['upper'], chain['lower'], chain['foot']]
        chains[key] = (nm, [bones[nm[0]].head_local[:]] + [bones[b].tail_local[:] for b in nm])
    unkeyed = [i for i in range(len(co)) if an.labels[i] == 'leg' and not an.leg_key[i]]
    if unkeyed and chains:  # leg-mask vertices the slice tracking did not attach: nearest leg chain
        P = co[unkeyed]
        dist = np.stack([rg._dist_to_polyline(P, np.array(j)) for _, j in chains.values()], axis=1)
        ks = list(chains)
        for i, j in zip(unkeyed, dist.argmin(axis=1)):
            an.leg_key[i] = ks[j]
    for key, chain in legs.items():
        idx = [i for i in range(len(co)) if an.labels[i] == 'leg' and an.leg_key[i] == key]
        if not idx:
            continue
        nm = [chain['upper'], chain['lower'], chain['foot']]
        joints = [bones[nm[0]].head_local[:]] + [bones[b].tail_local[:] for b in nm]
        par = 'Chest' if key.startswith('Fore') and 'Chest' in deform else ('Pelvis' if 'Pelvis' in deform else None)
        for i, w in zip(idx, rg.chain_weights(co[idx], joints, nm, parent=par)):
            new_weights[i] = w
            allowed_sets[i] = allowed_sets[i] | set(w)
    tail = [n for n in deform if n == 'TailRoot' or (n.startswith('Tail') and n[4:].isdigit())]
    tail.sort(key=lambda b: -1 if b == 'TailRoot' else int(b[4:]))
    tidx = [i for i in range(len(co)) if an.labels[i] == 'tail']
    if tail and tidx:
        joints = [bones[tail[0]].head_local[:]] + [bones[b].tail_local[:] for b in tail]
        tp = bones[tail[0]].parent
        while tp is not None and not tp.use_deform:
            tp = tp.parent
        tail_parent = tp.name if tp is not None else None
        for i, w in zip(tidx, rg.chain_weights(co[tidx], joints, tail, parent=tail_parent)):
            new_weights[i] = w
            allowed_sets[i] = allowed_sets[i] | set(w)
    # Neck: Chest -> Neck -> Head chain weights, so throat and nape blend the same way on every
    # vertex (heat gave neighbouring throat vertices different third bones, which pinched in Sleep).
    neck = [b for b in ('Chest', 'Neck', 'Head') if b in deform]
    nidx = [i for i in range(len(co)) if an.labels[i] == 'neck']
    if len(neck) == 3 and nidx:
        joints = [bones[neck[0]].head_local[:]] + [bones[b].tail_local[:] for b in neck]
        for i, w in zip(nidx, rg.chain_weights(co[nidx], joints, neck)):
            new_weights[i] = w
            allowed_sets[i] = allowed_sets[i] | set(w)
    # Smooth region borders (masked), then keep the 4 strongest influences.
    names = sorted(deform)
    col = {n: i for i, n in enumerate(names)}
    W = np.zeros((len(new_weights), len(names)))
    A = np.zeros_like(W)
    for i, (kept, allowed) in enumerate(zip(new_weights, allowed_sets)):
        for b, x in kept.items():
            W[i, col[b]] = x
        for b in allowed | set(kept):
            if b in col:
                A[i, col[b]] = 1.0
    E = np.array([e.vertices[:] for e in obj.data.edges])
    # Body/tail border: the hard region mask leaves Pelvis 1.0 next to TailRoot/Tail1, which
    # tears when the tail coils. Let both sides share Pelvis + the first tail bones there and
    # smooth only that band. (Leg borders are already continuous from the chain weights.)
    # Body interior (generated spikes, leaves, crests): heat leaves hard Spine/Chest steps there.
    # A light smoothing restricted to body vertices that carry no shoulder/hip blend.
    labs = np.asarray(an.labels)
    inner = (labs == 'body') & ~np.array(shoulder_rows, bool)
    if inner.any():
        W = rg.smooth_weights(W, A, E, iterations=3, alpha=0.5, active=inner)
    # Wing/fin roots: share the bones found on both sides of each appendage border.
    for kind in ('wing', 'fin'):
        ends = E[(labs[E[:, 0]] == kind) != (labs[E[:, 1]] == kind)]
        if not len(ends):
            continue
        cols = np.nonzero(W[np.unique(ends.ravel())].sum(axis=0) > 1e-6)[0]
        border = rg.border_mask(np.where(labs == kind, kind, 'other'), E, rings=10)
        if len(cols) >= 2 and border.any():
            A[np.ix_(np.nonzero(border)[0], cols)] = 1.0
            W = rg.smooth_weights(W, A, E, iterations=24, alpha=0.5, active=border)
    # Body/leg (shoulder, hip): band of 2 rings, and on the leg side only rows that are still pure
    # upper-leg (a band reaching the knee would pull Pelvis/Spine into the shin).
    if legs:
        uppers = [col[c['upper']] for c in legs.values() if c['upper'] in col]
        distal = [col[c[x]] for c in legs.values() for x in ('lower', 'foot') if c[x] in col]
        band = rg.border_mask(an.labels, E, rings=2, pair=('body', 'leg'))
        upper_only = W[:, distal].sum(axis=1) < 0.02 if distal else np.ones(len(W), bool)
        band &= (labs == 'body') | ((labs == 'leg') & upper_only)
        trunk = {'Spine', 'Chest', 'Pelvis'} | {arm.data.bones[c['upper']].parent.name for c in legs.values()
                                                if arm.data.bones[c['upper']].parent is not None}
        share = [col[b] for b in sorted(trunk) if b in col] + uppers
        if band.any():
            A[np.ix_(np.nonzero(band)[0], share)] = 1.0
            # a leg-side row may only take its own upper bone + the trunk, never the other legs
            for i in np.nonzero(band & (labs == 'leg'))[0]:
                own = legs.get(an.leg_key[i])
                for u in uppers:
                    if own is None or u != col.get(own['upper']):
                        A[i, u] = 0.0
            W = rg.smooth_weights(W, A, E, iterations=6, alpha=0.5, active=band)
    # Same for body/neck and neck/head (Spine/Chest/Neck/Head shared in the band).
    tail_share = tuple({'Pelvis', 'TailRoot', 'Tail1'} | ({tail_parent} if tail and tidx and tail_parent else set()))
    for pair, shared in ((('body', 'tail'), tail_share),
                         (('body', 'neck'), ('Spine', 'Chest', 'Neck')),

                         (('neck', 'head'), ('Neck', 'Head'))):
        border = rg.border_mask(an.labels, E, rings=4, pair=pair)
        border &= np.isin(np.asarray(an.labels), list(pair))  # never pull Pelvis into a paw
        share = [col[b] for b in shared if b in col]
        if len(share) >= 2 and border.any():
            A[np.ix_(np.nonzero(border)[0], share)] = 1.0
            W = rg.smooth_weights(W, A, E, iterations=10, alpha=0.5, active=border)
    for g in list(obj.vertex_groups):
        obj.vertex_groups.remove(g)
    groups = {n: obj.vertex_groups.new(name=n) for n in names}
    by_bone = {}
    for i in range(len(W)):
        kept = rg.limit_and_normalize({names[j]: W[i, j] for j in np.nonzero(W[i])[0]}, max_inf)
        if not kept:
            kept = new_weights[i]
        for b, x in kept.items():
            by_bone.setdefault((b, round(x, 5)), []).append(i)
    for (b, x), idx in by_bone.items():
        groups[b].add(idx, x, 'REPLACE')
    return {'removedWeightFraction': round(removed_mass / max(total_mass, 1e-9), 4),
            'distalLegWeightsRemovedFromBody': leg_on_body,
            'nearestAllowedFallback': fallback, 'maxInfluences': max_inf,
            'lowBodyVerticesRelabelledLeg': relabelled}


def bind(obj, arm, an, legs):
    voxel = heat_weights(obj, arm)
    rep = mask_weights(obj, arm, an, legs)
    obj.parent = arm
    mod = obj.modifiers.new('Armature', 'ARMATURE')
    mod.object = arm
    mod.use_deform_preserve_volume = True
    rep.update({'proxy_voxel_size': round(voxel, 5), 'vertices': len(obj.data.vertices),
                'nearest_bone_fallback_vertices': sum(rep['nearestAllowedFallback'].values())})
    return rep


# ---------------------------------------------------------------- animation
def rot(axis, a):
    return Matrix.Rotation(a, 3, axis)


class Animator:
    def __init__(self, arm, obj, an, legs, arch, prof):
        self.arm, self.obj, self.an, self.legs, self.arch, self.prof = arm, obj, an, legs, arch, prof
        self.P = arm.pose.bones
        self.h = an.h
        B = arm.data.bones
        self.rest = {b.name: b.matrix_local.copy() for b in B}
        self.leg = {}
        for k, c in legs.items():
            hip, knee, ankle = B[c['upper']].head_local, B[c['lower']].head_local, B[c['lower']].tail_local
            L1, L2 = (knee - hip).length, (ankle - knee).length
            line = (ankle - hip).normalized()
            bend = (knee - hip) - line * (knee - hip).dot(line)
            self.leg[k] = {'ankle0': ankle.copy(), 'L1': L1, 'L2': L2, 'len': L1 + L2,
                           'names': (c['upper'], c['lower'], c['foot']),
                           'bend': bend.normalized() if bend.length > 1e-9 else Vector((0, 1 if k.startswith('Fore') else -1, 0))}
        self.leg_len = min((x['len'] for x in self.leg.values()), default=0.3 * self.h)
        self.tails = [n for n in ('TailRoot', 'Tail1', 'Tail2', 'Tail3', 'Tail4', 'TailFin') if n in self.P]
        self.spine = [n for n in self.P.keys() if n.startswith('Spine') and n[5:].isdigit()]
        self.spine.sort(key=lambda n: int(n[5:]))
        self.contacts = {}
        self.clearance = {}
        self.amp = {}
        self.lift = {}
        self._dom = None
        self.ground_speed = {}
        self.notes = []
        self.wing_down = None
        self.wing_up = None
        self.shape_blink = next((k for k in (obj.data.shape_keys.key_blocks if obj.data.shape_keys else [])
                                 if any(t in k.name.lower() for t in ('blink', 'eye_close', 'eyeclose', 'eyesclosed'))), None)

    # --- helpers
    def set_rot(self, name, R):
        pb = self.P[name]
        B = self.rest[name].to_3x3()
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (B.inverted() @ R @ B).to_quaternion()

    def aim(self, name, target):
        pb = self.P[name]
        M = pb.matrix.copy()
        cur = M.col[1].xyz.normalized()
        want = (target - M.translation).normalized()
        R = cur.rotation_difference(want).to_matrix().to_4x4()
        N = R @ Matrix.Translation(-M.translation) @ M
        N.translation = M.translation
        pb.matrix = N
        bpy.context.view_layer.update()

    def solve_leg(self, key, target):
        c, L = self.legs[key], self.leg[key]
        up = self.P[c['upper']]
        hip = up.matrix.translation.copy()
        d_vec = target - hip
        d = max(min(d_vec.length, L['L1'] + L['L2'] - 1e-4), abs(L['L1'] - L['L2']) + 1e-4)
        dirv = d_vec.normalized()
        # bend plane follows the hip parent's current orientation
        parent = up.parent
        Rp = (parent.matrix.to_3x3() @ self.rest[parent.name].to_3x3().inverted()) if parent else Matrix.Identity(3)
        bend = Rp @ L['bend']
        bend = (bend - dirv * bend.dot(dirv))
        bend = bend.normalized() if bend.length > 1e-9 else Vector((0, 1, 0))
        a = (L['L1'] ** 2 - L['L2'] ** 2 + d * d) / (2 * d)
        k = math.sqrt(max(L['L1'] ** 2 - a * a, 0.0))
        knee = hip + dirv * a + bend * k
        self.aim(c['upper'], knee)
        self.aim(c['lower'], hip + dirv * d)
        foot = self.P[c['foot']]
        F = self.rest[c['foot']].copy()
        root = self.P['Root'].matrix.to_3x3() @ self.rest['Root'].to_3x3().inverted()
        if self.foot_follow > 0:  # let the paw share part of the ankle fold (lying poses)
            low = self.P[c['lower']].matrix.to_3x3() @ self.rest[c['lower']].to_3x3().inverted()
            root = root.to_quaternion().slerp(low.to_quaternion(), self.foot_follow).to_matrix()
        F = (root.to_4x4() @ F)
        F.translation = foot.matrix.translation
        foot.matrix = F
        return (hip + dirv * d - target).length

    # --- clip definitions: return (R dict, root location, feet targets or None, FK leg rotations, root rot)
    def pose(self, clip, t):
        tau = 2 * math.pi
        h, LL = self.h, self.leg_len
        R = {}
        mul = lambda n, ax, a: (R.__setitem__(n, rot(ax, a) @ R.get(n, Matrix.Identity(3))) if n in self.P else None)  # noqa: E731
        root = Vector((0, 0, 0))
        root_rot = Matrix.Identity(3)
        self.foot_follow = 0.0
        feet = {k: L['ankle0'].copy() for k, L in self.leg.items()}
        fk_legs = False
        contacts = {}
        pr = self.prof

        def tail_wave(amp, freq=1.0, lag=0.6, axis='Z', phase=0.0):
            for i, n in enumerate(self.tails):
                mul(n, axis, amp * (0.6 + 0.4 * i / max(1, len(self.tails) - 1)) * math.sin(tau * (freq * t + phase) - lag * i))

        def spine_wave(amp, wl=0.8, freq=1.0, axis='Z'):
            chain = self.spine + [n for n in self.tails if n != 'TailFin']
            for i, n in enumerate(chain):
                s = i / max(1, len(chain) - 1)
                mul(n, axis, amp * (0.35 + 0.65 * s) * math.sin(tau * freq * t - tau * s / max(wl, 1e-3)) * (1.0 / max(1, len(chain) / 6)))
        breathe = math.sin(tau * t * (2 if clip == 'Idle' else 1))
        if clip == 'Idle':
            # breathing: chest pitch only where no leg hangs from the breathing bones (a long serpentine
            # spine would lift the fore hips and pull planted feet off the ground)
            for n in (['Spine', 'Chest'] + ([] if self.leg else self.spine[:2])):
                mul(n, 'X', 0.012 * breathe)
            root.z += (0.0 if self.leg and self.spine else 0.003 * h) * breathe
            mul('Neck', 'Z', 0.08 * math.sin(tau * t))
            mul('Head', 'X', -0.03 * math.sin(tau * 2 * t))
            tail_wave(0.10, 1.0)
            if self.arch == 'aquatic' or (not self.leg and self.arch == 'serpentine'):
                spine_wave(0.04)
        elif clip in ('Walk', 'Run'):
            cfg = pr['walk' if clip == 'Walk' else 'run']
            frames = cfg['frames']
            stride, lift, duty = cfg['strideLegs'] * LL, cfg['liftLegs'] * LL, cfg['duty']
            phases = rg.foot_phases(cfg['gait'], sorted(self.leg))
            for k in self.leg:
                dy, dz = rg.foot_track(t, phases[k], duty, stride, lift)
                feet[k] = self.leg[k]['ankle0'] + Vector((0, dy, dz))
                contacts[k] = dz == 0.0
            self.ground_speed[clip] = stride / (duty * frames / FPS)
            root.z += cfg['bob'] * h * (0.5 - 0.5 * math.cos(2 * tau * t))
            root.z -= cfg['bob'] * h  # bob downward from rest so feet stay reachable
            mul('Chest', 'Y', 0.03 * math.sin(tau * t))
            mul('Pelvis', 'Y', -0.03 * math.sin(tau * t))
            mul('Head', 'X', 0.02 * math.sin(2 * tau * t))
            if cfg.get('bodyWave'):
                spine_wave(cfg['bodyWave'], wl=1.0)
            tail_wave(0.12 if clip == 'Walk' else 0.18, 1.0)
            if not self.leg:
                spine_wave(0.18, wl=0.8)
        elif clip == 'Eat':
            cfg = pr.get('eat', {})
            # down (0-0.25), two nibbles (0.25-0.75), up (0.75-1)
            if t < 0.25:
                k = 0.5 - 0.5 * math.cos(math.pi * t / 0.25)
            elif t < 0.75:
                k = 1.0 - 0.15 * (0.5 - 0.5 * math.cos(tau * 2 * (t - 0.25) / 0.5))
            else:
                k = 0.5 + 0.5 * math.cos(math.pi * (t - 0.75) / 0.25)
            mul('Neck', 'X', cfg.get('neckPitch', 0.45) * k)
            mul('Head', 'X', cfg.get('headPitch', 0.3) * k)
            mul('Chest', 'X', 0.08 * k)
            root.z -= cfg.get('chestDropH', 0.03) * h * k
            tail_wave(0.06, 1.0)
        elif clip == 'Sleep':
            cfg = pr.get('sleep', {})
            self.foot_follow = cfg.get('footFollow', 0.0)
            if self.leg:
                drop = cfg.get('dropLegs', 0.42) * LL
                root.z -= drop
                for k in self.leg:
                    if k.startswith('Fore'):
                        feet[k] = self.leg[k]['ankle0'] + Vector((0, -cfg.get('foreReachLegs', 0.2) * LL, 0))
                    else:  # hind feet slide out behind so the knee folds less while the body settles
                        feet[k] = self.leg[k]['ankle0'] + Vector((0, cfg.get('hindReachLegs', 0.0) * LL, 0))
                    contacts[k] = True
            # head turns to the side and rests low; the turn is spread over neck and head
            mul('Neck', 'X', cfg.get('neckPitch', 0.28))
            mul('Neck', 'Z', cfg.get('neckYaw', 0.22))
            mul('Head', 'X', cfg.get('headPitch', 0.15))
            mul('Head', 'Z', cfg.get('headYaw', 0.12))
            coil = cfg.get('coil', 0.2)
            for i, n in enumerate(self.tails):  # the curl starts past the tail base, not at the hip
                mul(n, 'Z', coil * min(1.0, 0.5 * i) + 0.1 * i)
            for i, n in enumerate(self.spine):
                mul(n, 'Z', coil * 0.5)
            slow = math.sin(tau * t * 2)
            for n in (['Spine', 'Chest'] + self.spine[:2]):
                mul(n, 'X', 0.01 * slow)
            for s, sign in (('L', 1), ('R', -1)):
                mul(f'WingUpper_{s}', 'Y', -sign * min(0.15, self.wing_down if self.wing_down is not None else 0.15))
                mul(f'WingFore_{s}', 'Z', sign * 0.2)
        elif clip == 'Happy':
            cfg = pr.get('happy', {})
            hop = cfg.get('hopH', 0.05) * h
            if t < 0.2:
                root.z -= 0.5 * hop * math.sin(math.pi * t / 0.2)
            elif t < 0.6:
                root.z += hop * math.sin(math.pi * (t - 0.2) / 0.4)
            elif t < 0.8:
                root.z -= 0.3 * hop * math.sin(math.pi * (t - 0.6) / 0.2)
            for k in self.leg:
                feet[k] = self.leg[k]['ankle0'] + Vector((0, 0, max(0.0, root.z)))
                contacts[k] = root.z <= 0.0
            tail_wave(cfg.get('tailWag', 0.3), 4.0, 0.5)
            mul('Head', 'X', -0.12 * math.sin(tau * t))
            if not self.leg:
                spine_wave(0.08, freq=2.0)
        elif clip == 'Burrow':
            cfg = pr.get('burrow', {})
            reach, cyc = cfg.get('digReachLegs', 0.35) * LL, cfg.get('digCycles', 2)
            root.z -= cfg.get('chestDropH', 0.04) * h
            mul('Chest', 'X', cfg.get('chestPitch', 0.10))
            mul('Neck', 'X', 0.2)
            for k in self.leg:
                if k.startswith('Fore'):
                    ph = (cyc * t + (0.5 if k.endswith('_R') else 0.0)) % 1.0
                    # scoop: forward on the surface, then back and up (never below the ground)
                    if ph < 0.5:
                        s = ph / 0.5
                        feet[k] = self.leg[k]['ankle0'] + Vector((0, -reach * (0.5 - s), 0))
                        contacts[k] = False  # dig stroke scrapes the soil: not a planted foot
                    else:
                        s = (ph - 0.5) / 0.5
                        feet[k] = self.leg[k]['ankle0'] + Vector((0, -reach * (s - 0.5), 0.35 * reach * math.sin(math.pi * s)))
                        contacts[k] = False
                else:
                    contacts[k] = True
            tail_wave(0.08, cyc)
        elif clip == 'Swim':
            cfg = pr.get('swim', {})
            fk_legs = True
            if cfg.get('style') == 'paddle':  # round swimmers: tail stroke, fins and paddling legs
                tail_wave(cfg.get('tailStroke', 0.35), 1.0, 0.5)
                for s, sign in (('L', 1), ('R', -1)):
                    mul(f'EarFin_{s}', 'Y', sign * cfg.get('finFlap', 0.45) * math.sin(tau * t))
                    mul(f'Fin_{s}', 'Y', sign * cfg.get('finFlap', 0.45) * math.sin(tau * t))
                root.z += 0.01 * h * math.sin(tau * t)
            else:
                spine_wave(cfg.get('wave', 0.16), wl=cfg.get('waveLength', 0.8))
                for s, sign in (('L', 1), ('R', -1)):
                    mul(f'Fin_{s}', 'Y', sign * cfg.get('finFlap', 0.4) * math.sin(tau * t))
                    mul(f'EarFin_{s}', 'Y', sign * 0.3 * math.sin(tau * t))
            for k in self.leg:
                part, side = k.split('_')
                ph = tau * (t + (0.5 if side == 'R' else 0.0) + (0.25 if part == 'Hind' else 0.0))
                amp = 0.35 if cfg.get('style') == 'paddle' else 0.0
                tuck = 0.0 if cfg.get('style') == 'paddle' else 0.9  # long swimmers tuck short limbs back
                mul(f'{part}Upper_{side}', 'X', tuck + amp * math.sin(ph))
                mul(f'{part}Lower_{side}', 'X', -0.4 * amp * max(0.0, math.cos(ph)))
        elif clip in ('Fly', 'Glide', 'TakeOff', 'Land'):
            fk_legs = True
            fly = pr.get('fly', {})
            up, down = fly.get('upstroke', 0.75), fly.get('downstroke', 0.4)
            if self.wing_down is not None:
                down = min(down, self.wing_down)
            if self.wing_up is not None:
                up = min(up, self.wing_up)

            def flap(u, scale=1.0):
                # downstroke 55 % of the cycle (power), upstroke 45 %
                u %= 1.0
                if u < 0.55:
                    a = up - (up + down) * (0.5 - 0.5 * math.cos(math.pi * u / 0.55))
                else:
                    a = -down + (up + down) * (0.5 - 0.5 * math.cos(math.pi * (u - 0.55) / 0.45))
                fold = fly.get('foreFold', 0.35) * (1.0 if u >= 0.55 else 0.0) * math.sin(math.pi * max(0.0, u - 0.55) / 0.45)
                return a * scale, fold * scale

            def wings(a, fold, lagged=True):
                for s, sign in (('L', 1), ('R', -1)):
                    mul(f'WingUpper_{s}', 'Y', sign * a)
                    mul(f'WingFore_{s}', 'Y', sign * 0.4 * a)
                    mul(f'WingFore_{s}', 'Z', sign * fold)
                    mul(f'WingTip_{s}', 'Y', sign * 0.25 * a)

            def tuck(k_amt):
                k_amt *= fly.get('tuck', 1.0)
                for k in self.leg:
                    part, side = k.split('_')
                    mul(f'{part}Upper_{side}', 'X', (0.7 if part == 'Hind' else 0.5) * k_amt)
                    mul(f'{part}Lower_{side}', 'X', (-0.9 if part == 'Hind' else 0.9) * k_amt)
            if clip == 'Fly':
                a, fold = flap(t)
                wings(a, fold)
                root.z += fly.get('bob', 0.03) * h * math.sin(tau * t + math.pi / 2)
                tuck(1.0)
                tail_wave(0.08, 1.0)
            elif clip == 'Glide':
                g = pr.get('glide', {})
                wings(g.get('dihedral', 0.12) + 0.04 * math.sin(tau * t), 0.0)
                root_rot = rot('Y', g.get('bank', 0.05) * math.sin(tau * t))
                tuck(1.0)
                tail_wave(0.05, 1.0)
            elif clip == 'TakeOff':
                rise = pr.get('takeoff', {}).get('riseH', 0.5) * h
                if t < 0.25:
                    root.z -= 0.08 * h * math.sin(0.5 * math.pi * t / 0.25)
                    wings(0.3 * t / 0.25, 0.0)
                    fk_legs = False
                    for k in self.leg:
                        contacts[k] = True
                else:
                    u = (t - 0.25) / 0.75
                    root.z += -0.08 * h * (1 - u) + rise * (u * u)
                    a, fold = flap(2.0 * u)
                    wings(a, fold)
                    if root.z <= 0.0:
                        fk_legs = False
                        for k in self.leg:
                            contacts[k] = True
                    else:
                        tuck(min(1.0, 2.0 * u))
            else:  # Land
                drop = pr.get('land', {}).get('dropH', 0.4) * h
                if t < 0.6:
                    u = t / 0.6
                    root.z += drop * (1 - u) * (1 - u)
                    wings(0.6, -0.2 * u)  # flare
                    tuck(max(0.0, 1.0 - 1.6 * u))
                else:
                    u = (t - 0.6) / 0.4
                    root.z -= 0.06 * h * math.sin(math.pi * u)
                    wings(0.6 * (1 - u), 0.0)
                    fk_legs = False
                    for k in self.leg:
                        contacts[k] = True
        if fk_legs:
            feet = None
        return R, root, feet, contacts, root_rot

    def author(self, clips):
        scene = bpy.context.scene
        scene.render.fps = FPS
        mod = next((m for m in self.obj.modifiers if m.type == 'ARMATURE'), None)
        if mod:
            mod.show_viewport = False  # no mesh evaluation while authoring (speed)
        if 'WingUpper_L' in self.P and mod:
            self.wing_down = self._wing_down_limit(mod, direction=-1)
            self.wing_up = self._wing_down_limit(mod, direction=1)
        self.arm.animation_data_create()
        action = bpy.data.actions.new('Dragon_Clips')
        self.arm.animation_data.action = action
        ranges = {}
        start = 1
        worst_ik = 0.0
        for clip in clips:
            if clip in ('Walk', 'Run'):
                n = self.prof['walk' if clip == 'Walk' else 'run']['frames']
            else:
                n = self.prof.get(clip.lower(), {}).get('frames', BASE_FRAMES[clip])
            loop = clip in LOOPING
            ranges[clip] = {'start': start, 'end': start + n, 'loop': loop}
            cons, ik = self._author_clip(clip, start, n, loop)
            if clip not in NO_GROUND and mod:
                # Ground clearance, measured on the skinned mesh:
                #  * body/head below z=0 (a low head while eating, a deep sleep drop): the clip's
                #    amplitude is reduced in steps of 15 % (at most 4) - never the ground or the limits;
                #  * paws below z=0 (toes rolling under): the whole creature is lifted by the deepest
                #    point, one constant per clip, so planted feet stay put and the loop still closes.
                info = {}
                for _ in range(4 if self.leg else 0):
                    depth = self._clearance(mod, start, n)
                    if depth['body'] <= 0.002 * self.h:
                        break
                    self.amp[clip] = round(self.amp.get(clip, 1.0) * 0.85, 4)
                    cons, ik = self._author_clip(clip, start, n, loop)
                if clip in self.amp or not self.leg:
                    info['amplitudeScale'] = self.amp.get(clip, 1.0)
                    depth = self._clearance(mod, start, n)
                # legless bodies have no planted feet to hover: lift by the body depth instead
                legs_depth = max([v for k, v in depth.items() if k != 'body' or not self.leg] or [0.0])
                if legs_depth > 0.002 * self.h:
                    self.lift[clip] = round(legs_depth * 1.05, 5)
                    cons, ik = self._author_clip(clip, start, n, loop)
                    info['lift_m'] = self.lift[clip]
                if info:
                    info['depthBeforeFix_m'] = {k: round(v, 4) for k, v in depth.items()}
                    self.clearance[clip] = info
            worst_ik = max(worst_ik, ik)
            self.contacts[clip] = {k: _runs(v) for k, v in cons.items()}
            start += n + 1
        scene.frame_start, scene.frame_end = 1, start - 1
        if mod:
            mod.show_viewport = True
        if self.shape_blink is None:
            self.notes.append('Idle blink not authored: mesh has no eyelid shape key (blink/eye_close); '
                              'generated meshes rarely separate eyelids')
        else:
            self._blink(ranges.get('Idle'))
        self.ik_residual = worst_ik
        return ranges

    def _wing_down_limit(self, mod, wanted=None, direction=-1):
        """Largest downstroke (rad) before the wing membrane enters the body, measured on the skinned
        mesh: wings posed alone, membrane vertices (WingFore/WingTip owned) tested against the rest body
        surface. Wings rooted high on the back can not stroke far below horizontal without hitting it."""
        from mathutils.bvhtree import BVHTree
        key = 'downstroke' if direction < 0 else 'upstroke'
        wanted = wanted or max(self.prof.get('fly', {}).get(key, 0.4 if direction < 0 else 0.75), 0.15)
        me = self.obj.data
        gname = {g.index: g.name for g in self.obj.vertex_groups}
        dom = [gname[max(v.groups, key=lambda x: x.weight).group] if len(v.groups) else '' for v in me.vertices]
        body = {'Spine', 'Chest', 'Pelvis'} | {n for n in self.P.keys() if n.startswith('Spine')}
        polys = [p.vertices[:] for p in me.polygons if all(dom[i] in body for i in p.vertices)]
        probe = [i for i, d in enumerate(dom) if d.startswith(('WingFore', 'WingTip'))][::3]
        if not polys or not probe:
            return wanted
        rest = [v.co.copy() for v in me.vertices]
        tree = BVHTree.FromPolygons(rest, polys)

        def outside(co, i):
            loc, nrm, _, _ = tree.find_nearest(co[i])
            return loc is None or (co[i] - loc).dot(nrm) > -0.01 * self.h
        clear = [i for i in probe if outside(rest, i) and (rest[i] - tree.find_nearest(rest[i])[0]).length > 0.02 * self.h]
        if not clear:
            return wanted
        mod.show_viewport = True

        def hits(a):
            for pb in self.P:
                pb.rotation_quaternion = (1, 0, 0, 0)
            for s_, sign in (('L', 1), ('R', -1)):
                self.set_rot(f'WingUpper_{s_}', rot('Y', direction * sign * a))
            bpy.context.view_layer.update()
            ev = self.obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            m = ev.to_mesh()
            co = [m.vertices[i].co.copy() for i in range(len(m.vertices))]
            ev.to_mesh_clear()
            return sum(1 for i in clear if not outside(co, i)) > 0.002 * len(clear)
        lo, hi = 0.0, wanted
        if hits(hi):
            for _ in range(7):
                mid = 0.5 * (lo + hi)
                lo, hi = (lo, mid) if hits(mid) else (mid, hi)
            limit = lo * 0.9
        else:
            limit = wanted
        for pb in self.P:
            pb.rotation_quaternion = (1, 0, 0, 0)
        mod.show_viewport = False
        if limit < wanted:
            self.notes.append(f'Wing {key} limited to {math.degrees(limit):.1f} deg (wanted '
                              f'{math.degrees(wanted):.1f}): membrane would enter the body below that')
        return limit

    def _author_clip(self, clip, start, n, loop):
        scene = bpy.context.scene
        cons = {k: [] for k in self.leg}
        worst_ik = 0.0
        for f in range(start, start + n + 1):
            t = (f - start) / n
            tt = t % 1.0 if loop else min(t, 1.0)
            if loop and f == start + n:
                tt = 0.0  # last frame == first frame: seamless loop
            R, root, feet, contacts, root_rot = self.pose(clip, tt)
            amp = self.amp.get(clip, 1.0)
            if amp < 1.0:  # scaled-down clip (ground clearance): rotations and root offset
                R = {k: Matrix.Identity(3).to_quaternion().slerp(m.to_quaternion(), amp).to_matrix() for k, m in R.items()}
                root = root * amp
                if feet is not None:
                    feet = {k: self.leg[k]['ankle0'].lerp(v, amp) for k, v in feet.items()}
            lift = self.lift.get(clip, 0.0)
            root = root + Vector((0, 0, lift))
            scene.frame_set(f)
            for pb in self.P:
                pb.rotation_mode = 'QUATERNION'
                pb.rotation_quaternion = (1, 0, 0, 0)
                pb.location = (0, 0, 0)
                pb.scale = (1, 1, 1)
            for name, Rm in R.items():
                self.set_rot(name, Rm)
            if root_rot is not None:
                self.set_rot('Root', root_rot @ R.get('Root', Matrix.Identity(3)))
            self.P['Root'].location = self.rest['Root'].to_3x3().inverted() @ root
            bpy.context.view_layer.update()
            if feet is not None:
                for k, target in feet.items():
                    contacts.setdefault(k, True)  # IK-driven legs without a gait are planted
                    target = target.copy()
                    if contacts[k]:  # a planted foot never sinks below its rest height
                        target.z = max(target.z, self.leg[k]['ankle0'].z)
                    target.z += lift  # same lift as the root: fold unchanged
                    worst_ik = max(worst_ik, self.solve_leg(k, target))
            for k in self.leg:
                cons[k].append(feet is not None and bool(contacts.get(k, False)))
            for pb in self.P:
                pb.keyframe_insert('rotation_quaternion', frame=f, group=pb.name)
                pb.keyframe_insert('location', frame=f, group=pb.name)
        return cons, worst_ik

    def _clearance(self, mod, start, n):
        """Deepest point below z=0 over the clip (metres) of each leg's vertices and of the rest."""
        scene = bpy.context.scene
        if self._dom is None:
            gname = {g.index: g.name for g in self.obj.vertex_groups}
            owner = {}
            for k, L in self.leg.items():
                for b in (L['names']):
                    owner[b] = k
            dom = []
            for v in self.obj.data.vertices:
                g = max(v.groups, key=lambda x: x.weight) if len(v.groups) else None
                dom.append(owner.get(gname[g.group], 'body') if g else 'body')
            self._dom = np.array(dom, dtype=object)
        mod.show_viewport = True
        depth = {k: [] for k in list(self.leg) + ['body']}
        mw = np.array(self.obj.matrix_world)
        for f in range(start, start + n + 1):
            scene.frame_set(f)
            ev = self.obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            m = ev.to_mesh()
            co = np.empty(len(m.vertices) * 3)
            m.vertices.foreach_get('co', co)
            ev.to_mesh_clear()
            z = (co.reshape(-1, 3) @ mw[:3, :3].T + mw[:3, 3])[:, 2]
            for k in depth:
                sel = self._dom == k
                depth[k].append(max(0.0, -float(z[sel].min())) if sel.any() else 0.0)
        mod.show_viewport = False
        return {k: max(v) for k, v in depth.items()}

    def _blink(self, r):
        if not r:
            return
        kb = self.shape_blink
        for f in range(r['start'], r['end'] + 1):
            u = (f - r['start']) / max(1, r['end'] - r['start'])
            kb.value = 1.0 if abs(u - 0.6) < 0.025 else 0.0
            kb.keyframe_insert('value', frame=f)


def _runs(flags):
    out, s = [], None
    for i, f in enumerate(flags + [False]):
        if f and s is None:
            s = i
        elif not f and s is not None:
            out.append([s, i - 1])
            s = None
    return out


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


def resolve_limbs(sp, arg, detected):
    """Returns (limbCount, source). Never guesses: canonical > explicit > accepted measurement."""
    if not sp['walkingLimbs']:
        return 0, 'canonical: no walking limbs'
    if sp['limbCount'] is not None:
        if arg not in (None, 'auto') and int(arg) != sp['limbCount']:
            raise SystemExit(f"{sp['id']}: --limb-count {arg} contradicts canonical {sp['limbCount']}")
        return sp['limbCount'], sp['limbCountSource']
    if arg is None:
        raise SystemExit(f"{sp['id']}: walks (canonical) but limb count is not stated. Mesh measurement: "
                         f"{detected['limbCountDetected']} feet ({detected['confidence']} confidence). Pass "
                         "--limb-count N (art director/GDD) or --limb-count auto to accept the measurement.")
    if arg == 'auto':
        if detected['confidence'] != 'high' or detected['limbCountDetected'] not in (2, 4):
            raise SystemExit(f"{sp['id']}: --limb-count auto refused: {detected['limbCountDetected']} feet measured "
                             f"with {detected['confidence']} confidence; give the count or feet landmarks.")
        return detected['limbCountDetected'], 'mesh measurement accepted (--limb-count auto)'
    return int(arg), '--limb-count'


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('source', type=Path)
    ap.add_argument('out_dir', type=Path)
    ap.add_argument('--species', required=True)
    ap.add_argument('--stage', default='Unstaged', choices=['Baby', 'Juvenile', 'Adult', 'Unstaged'],
                    help='Naming/report only; morphology per stage is not inferred')
    ap.add_argument('--limb-count', choices=['0', '2', '4', 'auto'],
                    help='Walking limb count when canonical data does not state it, or auto = accept the mesh measurement')
    ap.add_argument('--landmarks', type=Path, help='Manual landmarks JSON (same space as the normalised mesh)')
    ap.add_argument('--yaw-offset', type=float, default=0.0, help='Degrees about +Z so the front faces -Y')
    ap.add_argument('--height', type=float, help='Rescale to this height (m). Default: species heightM or keep source size')
    ap.add_argument('--name')
    ap.add_argument('--overwrite-outputs', action='store_true')
    a = ap.parse_args(cc.script_args())
    arch_all = json.loads(ARCHETYPES.read_text(encoding='utf-8'))
    sp = next((s for s in arch_all['species'] if s['id'] == a.species), None)
    if sp is None:
        raise SystemExit(f'Unknown species id {a.species}')
    arch = sp['archetype']
    name = a.name or f'DRG_{a.species}_{a.stage}'
    src = a.source.resolve()
    outs = {k: cc.guard_output(src, a.out_dir / f'{name}{s}', a.overwrite_outputs)
            for k, s in [('fbx', '_Rigged.fbx'), ('blend', '_Rigged.blend'), ('json', '_rig.json')]}
    manual = json.loads(a.landmarks.read_text(encoding='utf-8')) if a.landmarks else {}
    prof_species = json.loads(PROFILES.read_text(encoding='utf-8'))['species'].get(a.species, {})
    height = a.height or prof_species.get('heightM')
    height_src = '--height' if a.height else (prof_species.get('heightSource') if height else 'source size kept')
    before = cc.sha256(src)
    obj, size = import_static(src, a.yaw_offset, height)
    obj.name = name
    # pass 1: measure limbs; pass 2: full analysis with the resolved count
    probe = rg.Analysis(np.array([v.co[:] for v in obj.data.vertices]))
    E = np.array([e.vertices[:] for e in obj.data.edges])
    detected = probe.detect_limbs(rg.main_component_mask(len(probe.V), E))
    limb_arg = str(manual['limbCount']) if 'limbCount' in manual else a.limb_count
    limb, limb_src = resolve_limbs(sp, limb_arg, detected)
    if arch == 'walker':
        if limb != 4:
            raise SystemExit(f'{a.species}: no walker template for limbCount={limb}')
        arch = 'quadruped'
    prof = load_profile(arch, a.species)
    an, res = analyse(obj, arch, limb, manual)
    if limb and len(an.landmarks['feet']) != limb:
        raise SystemExit(f'{a.species}: {limb} limbs expected but {len(an.landmarks["feet"])} feet located; '
                         'add "feet" to the landmarks file')
    spec, legs = template(arch, an, prof, limb)
    arm = build_armature(spec)
    anchors = [n for n, *_r, d in spec if not d]
    bind_rep = bind(obj, arm, an, legs)
    clips = list(sp['clips']) + [c for c in prof.get('extraClips', []) if 'Fly' in sp['clips']]
    anim = Animator(arm, obj, an, legs, arch, prof)
    ranges = anim.author(clips)
    for c, r in ranges.items():
        r['groundSpeed'] = round(anim.ground_speed.get(c, 0.0), 4)
    deform = deformation_report(obj, ranges)
    for n in anchors:
        arm.data.bones[n].use_deform = True
    ik = [{'upper': v['upper'], 'lower': v['lower'], 'foot': v['foot']} for k, v in sorted(legs.items())]
    bpy.ops.wm.save_as_mainfile(filepath=str(outs['blend']), copy=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    arm.select_set(True)
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
    h = an.h
    report = {'rigVersion': RIG_VERSION, 'name': name, 'species': a.species, 'stage': a.stage, 'archetype': arch,
              'archetypeSource': sp['archetypeReason'], 'limbCount': limb, 'limbCountSource': limb_src,
              'limbMeasurement': {k: v for k, v in detected.items() if k != 'feet'},
              'height_m': size['height'], 'size': size, 'heightSource': height_src, 'fps': FPS,
              'orientation': {'blender': 'Z up, front -Y', 'unity': 'Y up, forward +Z (FBX axis_forward -Z, axis_up Y)',
                              'pivot': 'ground, centred under the body'},
              'rootMotion': 'none: clips are in place; Root bone carries vertical offsets only. Move the transform at clips.<name>.groundSpeed (m/s).',
              'rootVertical': {'TakeOff': round(prof.get('takeoff', {}).get('riseH', 0.5) * h, 4),
                               'Land': round(-prof.get('land', {}).get('dropH', 0.4) * h, 4)} if 'TakeOff' in ranges else {},
              'bones': [b.name for b in arm.data.bones], 'deformBones': [b.name for b in arm.data.bones if b.name not in anchors],
              'anchors': anchors, 'ikChains': ik, 'clips': ranges, 'contacts': anim.contacts, 'groundClearanceLift_m': anim.clearance,
              'bind': bind_rep, 'skin': cc.skin_stats(obj), 'deformation': deform, 'deformationLimits': DEFORM_LIMITS,
              'deformationOk': all(v['ok'] for v in deform.values()),
              'landmarks': an.summary(), 'profile': prof, 'ikResidualMax': round(anim.ik_residual, 5),
              'notes': anim.notes + an.notes,
              'triangles': cc.triangle_count(obj.data), 'fbx': outs['fbx'].name, 'blend': outs['blend'].name,
              'source': str(src), 'source_sha256_before': before, 'source_sha256_after': after,
              'source_unchanged': before == after,
              'limits': 'Procedural motion fitted to measured landmarks; needs animator review before shipping.'}
    cc.write_json(outs['json'], report)
    print(f"Rigged {name}: {arch}, {len(report['bones'])} bones, clips {list(ranges)}; "
          f"deformation ok={report['deformationOk']} limbs={limb} ({limb_src})")


if __name__ == '__main__':
    main()
