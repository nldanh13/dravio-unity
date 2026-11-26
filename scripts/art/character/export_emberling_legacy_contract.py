"""CHAR.9 — export the REAL Emberling (ArtSource/Dragons/images/01_emberling.glb, rig v7 from batch_import.py) as
Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx, the file the existing Origin gameplay scene generator
(Dravio.Editor.EmberlingModelPipeline / EmberlingAssetAudit, AI 1) requires.

  python scripts/art/character/export_emberling_legacy_contract.py <DRG_emberling_Unstaged_Rigged.blend> <out.fbx> \
      [--basecolor <DRG_emberling_Unstaged_BaseColor.png>] [--rig-json <DRG_emberling_Unstaged_rig.json>]
  (bpy 5.3 module; or blender --background --python <this> -- <args>)

Contract (EmberlingAssetAudit.cs): bones Root Pelvis Spine Chest Neck Head Eye_L/R Fore_L/R ForeLower_L/R Hind_L/R
HindLower_L/R Tail1 Tail2 Flame Wing_L/R TailVFX_Anchor; one take with Idle 1-181 (6 s, loop), Walk 182-217 (35/30 s,
loop), Eat 218-278 (2 s), Doze 279-399 (4 s, loop), Happy 400-445 (1.5 s); skinned mesh named *Emberling_Baby_Body*.

Mapping from rig v7 (weights and motion are rig v7's, nothing is re-weighted):
  ForeUpper_* -> Fore_*, HindUpper_* -> Hind_*, Tail3 -> Flame, VFX_TailTip -> TailVFX_Anchor (other bones kept).
  Eye_L/R, Wing_L/R: added, unweighted (Emberling has no wings; eyes are textured, as in the legacy exporter).
  Clips: Eat <- Eat (60 f), Doze <- Sleep (120 f), Happy <- Happy (45 f) frame for frame; Idle <- Idle 120 f
  time-stretched to 180 f, Walk <- Walk time-stretched to 35 f (both stay closed loops; Walk is slower than the
  biomechanical default, so the legacy wander speed must match its stride: see the report).
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

RENAME = {'ForeUpper_L': 'Fore_L', 'ForeUpper_R': 'Fore_R', 'HindUpper_L': 'Hind_L', 'HindUpper_R': 'Hind_R',
          'Tail3': 'Flame', 'VFX_TailTip': 'TailVFX_Anchor'}
TARGET = [('Idle', 'Idle', 1, 181), ('Walk', 'Walk', 182, 217), ('Eat', 'Eat', 218, 278),
          ('Doze', 'Sleep', 279, 399), ('Happy', 'Happy', 400, 445)]
CONTRACT_BONES = ["Root", "Pelvis", "Chest", "Head", "Tail1", "Tail2", "Flame", "Fore_L", "Fore_R", "Hind_L", "Hind_R",
                  "Eye_L", "Eye_R", "ForeLower_L", "ForeLower_R", "HindLower_L", "HindLower_R",
                  "Spine", "Neck", "Wing_L", "Wing_R", "TailVFX_Anchor"]


def args():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument('rigged_blend', type=Path)
    ap.add_argument('out', type=Path)
    ap.add_argument('--rig-json', type=Path)
    ap.add_argument('--basecolor', type=Path)
    return ap.parse_args(argv)


def main():
    a = args()
    rig = json.loads(a.rig_json.read_text()) if a.rig_json else None
    bpy.ops.wm.open_mainfile(filepath=str(a.rigged_blend))
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    mesh = next(o for o in bpy.data.objects if o.type == 'MESH' and o.find_armature() == arm)
    for o in list(bpy.data.objects):
        if o not in (arm, mesh):
            bpy.data.objects.remove(o, do_unlink=True)
    clips = rig['clips'] if rig else None
    src_action = arm.animation_data.action
    sc = bpy.context.scene

    # 1. sample the source poses (matrix_basis per bone) at the remapped times, BEFORE renaming
    samples = {}
    for name, src, t0, t1 in TARGET:
        s0, s1 = (clips[src]['start'], clips[src]['end']) if clips else None
        n_t, n_s = t1 - t0, s1 - s0
        for t in range(t0, t1 + 1):
            f = s0 + (t - t0) * n_s / n_t
            fi = int(math.floor(f))
            sc.frame_set(fi, subframe=f - fi)
            samples[t] = {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones}

    # 2. rename bones (vertex groups follow), add unweighted Eye/Wing bones
    bpy.context.view_layer.objects.active = arm
    for old, new in RENAME.items():
        if old in arm.data.bones:
            arm.data.bones[old].name = new
    for old, new in RENAME.items():
        if old in mesh.vertex_groups:
            mesh.vertex_groups[old].name = new
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    head = eb['Head']
    hl = (head.tail - head.head).length
    fwd = (head.tail - head.head).normalized()
    for side, sx in (('L', 1), ('R', -1)):
        e = eb.new('Eye_' + side)
        e.head = head.head + fwd * hl * 0.55 + Vector((sx * hl * 0.35, 0, hl * 0.25))
        e.tail = e.head + fwd * hl * 0.15
        e.parent = head
        e.use_deform = True
        w = eb.new('Wing_' + side)
        ch = eb['Chest']
        w.head = ch.tail + Vector((sx * 0.06, 0, 0.04))
        w.tail = w.head + Vector((sx * 0.12, 0, 0.04))
        w.parent = ch
        w.use_deform = True
    eb['TailVFX_Anchor'].use_deform = True
    bpy.ops.object.mode_set(mode='OBJECT')
    for n in ('Eye_L', 'Eye_R', 'Wing_L', 'Wing_R', 'TailVFX_Anchor'):
        if n not in mesh.vertex_groups:
            mesh.vertex_groups.new(name=n)        # empty group: bone exported, influences nothing

    # 3. write the contract take
    act = bpy.data.actions.new('EmberlingRig|Scene')
    arm.animation_data.action = act
    inv = {v: k for k, v in RENAME.items()}
    for t, poses in samples.items():
        for pb in arm.pose.bones:
            src = inv.get(pb.name, pb.name)
            if src not in poses:
                continue
            pb.matrix_basis = poses[src]
            pb.keyframe_insert('location', frame=t)
            pb.keyframe_insert('rotation_quaternion', frame=t)
            pb.keyframe_insert('scale', frame=t)
    for fc in getattr(act, 'fcurves', []):
        for k in fc.keyframe_points:
            k.interpolation = 'LINEAR'
    sc.frame_start, sc.frame_end = 1, 445
    if src_action is not None and src_action.users == 0:
        bpy.data.actions.remove(src_action)

    mesh.name = 'Emberling_Baby_Body_GLB'
    if a.basecolor:
        # the scene generator copies _BaseMap from the imported FBX material: point the material at the baked
        # BaseColor (pipeline output of the real GLB) and embed it in the FBX
        img = bpy.data.images.load(str(a.basecolor))
        img.name = 'EmberlingRefinedGLB_BaseColor'
        for m in mesh.data.materials:
            if m and m.use_nodes:
                bsdf = m.node_tree.nodes.get('Principled BSDF')
                tex = next((n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE'), None) or m.node_tree.nodes.new('ShaderNodeTexImage')
                tex.image = img
                if bsdf:
                    m.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    names = {b.name for b in arm.data.bones}
    missing = [b for b in CONTRACT_BONES if b not in names]
    if missing:
        raise SystemExit(f'contract bones missing: {missing}')
    a.out.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    r = bpy.ops.export_scene.fbx(filepath=str(a.out), use_selection=True, object_types={'MESH', 'ARMATURE'},
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, add_leaf_bones=False,
                                 use_armature_deform_only=True, mesh_smooth_type='FACE', path_mode='COPY',
                                 embed_textures=True, bake_anim=True, bake_anim_use_all_actions=False,
                                 bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                                 bake_anim_simplify_factor=0.0)
    if 'FINISHED' not in r:
        raise SystemExit('FBX export failed')
    bpy.ops.wm.save_as_mainfile(filepath=str(a.out.with_suffix('.blend')))
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    report = {'model': a.out.name, 'source': 'ArtSource/Dragons/images/01_emberling.glb via batch_import.py rig v7',
              'rigVersion': rig.get('rigVersion') if rig else None, 'bones': sorted(names), 'triangles': tris,
              'vertices': len(mesh.data.vertices), 'fps': 30,
              'clip_frames': {n: [t0, t1] for n, _, t0, t1 in TARGET},
              'clip_sources': {n: src for n, src, _, _ in TARGET},
              'walkStretch': (35 / (clips['Walk']['end'] - clips['Walk']['start'])) if clips else None}
    a.out.with_suffix('.qa.json').write_text(json.dumps(report, indent=1))
    if rig:
        # contract rig.json so anim_qa.py can measure the exported take exactly like the rig v7 source:
        # renamed bones, contract clip ranges, contacts and groundSpeed rescaled by each clip's time stretch
        t = json.dumps(rig)
        for old, new in RENAME.items():
            t = t.replace('"%s"' % old, '"%s"' % new)
        cr = json.loads(t)
        contacts = cr.get('contacts', {})
        cr['clips'], cr['contacts'] = {}, {}
        for name, src, t0, t1 in TARGET:
            sc0 = clips[src]
            f = (t1 - t0) / max(1, sc0['end'] - sc0['start'])
            c = {'start': t0, 'end': t1, 'loop': name in ('Idle', 'Walk', 'Doze')}
            if sc0.get('groundSpeed'):
                c['groundSpeed'] = round(sc0['groundSpeed'] / f, 4)
            cr['clips'][name] = c
            if src in contacts:
                cr['contacts'][name] = {k: [[round(a0 * f), round(a1 * f)] for a0, a1 in v] for k, v in contacts[src].items()}
        cr['name'] = a.out.stem
        a.out.with_name(a.out.stem + '_contract_rig.json').write_text(json.dumps(cr, indent=1))
    print('exported', a.out, tris, 'tris')


if __name__ == '__main__':
    main()
