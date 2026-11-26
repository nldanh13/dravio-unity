"""Render a QA contact sheet: source vs prepared Base Color, and LOD0/LOD1/LOD2 side by side.

  blender --background --python scripts/art/character/render_preview.py -- \
      --source <source.glb> --prepared <Name>_Prepared.glb --lods <Name>_LODs.blend --out sheet.png [--frame 340]

Cycles CPU, fixed camera and light, flat neutral world, Standard view transform so colours are
comparable. LODs are shown at the given animation frame (deformed by the shared armature).
A picture is evidence for a human reviewer, not an automated pass.
"""
import argparse
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402


def setup_scene(res_x, res_y):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = res_x, res_y
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    world = bpy.data.worlds.new('QA') if not scene.world else scene.world
    scene.world = world
    bg = world.node_tree.nodes.get('Background')
    bg.inputs['Color'].default_value = (0.55, 0.57, 0.60, 1)
    bg.inputs['Strength'].default_value = 0.9
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 2.2
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    scene.collection.objects.link(sun)
    return scene


def frame_camera(scene, objs, side=True):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo, hi = cc.bounds_of(pts)
    center = (lo + hi) / 2 - Vector((0, 0, 0.08 * (hi.z - lo.z)))  # elevated camera: keep feet in frame
    size = max(hi.x - lo.x, (hi.z - lo.z) * scene.render.resolution_x / scene.render.resolution_y)
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = size * 1.35
    # Three-quarter view from the front-left (models face -Y).
    offset = Vector((-2.2, -6.0, 1.8))
    cam.location = center + offset
    cam.rotation_euler = (-offset).to_track_quat('-Z', 'Y').to_euler()
    scene.collection.objects.link(cam)
    scene.camera = cam


def import_glb(path, offset_x, label):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path), merge_vertices=True)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.parent is None:
            o.location.x += offset_x
    return [o for o in new if o.type == 'MESH']


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--source', type=Path)
    ap.add_argument('--prepared', type=Path)
    ap.add_argument('--lods', type=Path, help='<Name>_LODs.blend from build_lods.py')
    ap.add_argument('--frame', type=int, default=1)
    ap.add_argument('--out', type=Path, required=True)
    ap.add_argument('--overwrite-outputs', action='store_true')
    a = ap.parse_args(cc.script_args())
    if a.out.exists() and not a.overwrite_outputs:
        raise SystemExit(f'Output exists: {a.out}')
    if a.lods:
        bpy.ops.wm.open_mainfile(filepath=str(a.lods))
        for o in list(bpy.data.objects):
            if o.type not in {'MESH', 'ARMATURE'} or o.name.endswith('_SOURCE_REFERENCE'):
                bpy.data.objects.remove(o, do_unlink=True)
        meshes = sorted((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: o.name)
        bpy.context.scene.frame_set(a.frame)
        dg = bpy.context.evaluated_depsgraph_get()
        # Freeze each LOD at the requested (deformed) frame into a static copy, then lay out along X.
        frozen = []
        for i, o in enumerate(meshes):
            me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
            f = bpy.data.objects.new(o.name + '_frozen', me)
            f.matrix_world = o.matrix_world.copy()
            bpy.context.scene.collection.objects.link(f)
            frozen.append(f)
        for o in list(bpy.data.objects):
            if o not in frozen:
                bpy.data.objects.remove(o, do_unlink=True)
        pts = [Vector(c) for c in frozen[0].bound_box]
        width = (max(p.x for p in pts) - min(p.x for p in pts)) * 1.3
        for i, f in enumerate(frozen):
            f.location.x += i * width
        meshes = frozen
        scene = setup_scene(1500, 560)
        scene.frame_set(a.frame)
        frame_camera(scene, meshes)
    else:
        cc.reset_scene()
        scene = setup_scene(1200, 560)
        objs = import_glb(a.source, 0.0, 'source')
        pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
        lo, hi = cc.bounds_of(pts)
        objs += import_glb(a.prepared, (hi.x - lo.x) * 1.3, 'prepared')
        # glTF importer multiplies COLOR_0 into Base Color only when present; the source
        # material uses vertex colour, the prepared one the baked texture.
        bpy.context.view_layer.update()
        frame_camera(scene, objs)
    scene.render.filepath = str(a.out)
    scene.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True)
    print('Preview written:', a.out)


if __name__ == '__main__':
    main()
