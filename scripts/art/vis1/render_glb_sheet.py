"""VIS.1 - render a 3/4 thumbnail of each GLB (Cycles CPU) for the art inventory.

  blender --factory-startup --background --python scripts/art/vis1/render_glb_sheet.py -- \
      --out <dir> ArtSource/Dragons/images/01_emberling.glb ...
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out = Path(argv[argv.index("--out") + 1])
files = [a for a in argv if a.endswith(".glb")]
out.mkdir(parents=True, exist_ok=True)
for f in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=f)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    ctr, rad = (lo + hi) / 2, (hi - lo).length / 2
    scn = bpy.context.scene
    cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = rad * 2.1
    cam = bpy.data.objects.new("c", cd); scn.collection.objects.link(cam); scn.camera = cam
    d = Vector((math.sin(math.radians(30)), -math.cos(math.radians(30)), 0.55)).normalized()
    cam.location = ctr + d * rad * 4
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sd = bpy.data.lights.new("s", 'SUN'); sd.energy = 3.0; sd.angle = 0.2
    s = bpy.data.objects.new("s", sd); s.rotation_euler = (0.8, 0.2, -0.6); scn.collection.objects.link(s)
    w = bpy.data.worlds.new("w"); scn.world = w
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.78, 0.8, 1)
    scn.render.engine = 'CYCLES'; scn.cycles.samples = 16; scn.cycles.use_denoising = True
    scn.render.resolution_x = scn.render.resolution_y = 420
    scn.view_settings.view_transform = 'Standard'
    scn.render.filepath = str(out / (Path(f).stem + ".png"))
    bpy.ops.render.render(write_still=True)
