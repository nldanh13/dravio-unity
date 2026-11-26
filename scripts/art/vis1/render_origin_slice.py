"""VIS.1 - Origin before/after preview renderer (Blender 5.3 EEVEE, same camera as Unity).

Renders an environment kit .blend (written by build_origin_sanctuary.py) with the
committed layout, the Unity camera pose (layout.camera_position / PITCH / ORTHO), the
13:00 lighting of a living JSON, and Emberling standing on spawn `origin_meadow.spawn_01`.

Materials approximate `Dravio/Environment/Painterly Lit` (two-tone ramp, cool shadow tint,
trilight ambient, vertex AO, path blend, rim, emission, linear fog) so that before and
after renders use the SAME renderer. These are Blender renders, NOT Unity captures.

  python scripts/art/vis1/render_origin_slice.py -- --kit <OriginSanctuaryKit.blend> \
      --data <dir with OriginSanctuaryLayout.json + OriginSanctuaryLiving.json> \
      --emberling Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx \
      --look baseline|vis1 --out <prefix> [--hour 13]

EEVEE needs a GPU backend; in a headless Linux container use Vulkan through lavapipe
(VK_ICD_FILENAMES=/opt/lavapipe/lvp_icd.json).
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "environment"))
import layout as layout_mod  # noqa: E402
import living  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    return argv[argv.index(name) + 1] if name in argv else default


KIT = arg("--kit")
DATA = Path(arg("--data"))
EMBERLING = arg("--emberling")
LOOK = arg("--look", "baseline")
OUT = arg("--out")
HOUR = float(arg("--hour", "13"))

# Material constants mirrored from the committed M_ENV_*.mat / proposed VIS.1 values.
LOOKS = {
    "baseline": {
        "shadowTint": (0.62, 0.66, 0.86), "rampThreshold": 0.05, "rampSoftness": 0.32,
        "rim": (1.0, 0.92, 0.74), "rimStrength": 0.12, "path": (0.62, 0.49, 0.33),
        "emission": 1.7, "charStylized": False, "fog": (24.0, 52.0),
    },
    "vis1": {
        "shadowTint": (0.56, 0.62, 0.90), "rampThreshold": 0.02, "rampSoftness": 0.26,
        "rim": (1.0, 0.88, 0.66), "rimStrength": 0.22, "path": (0.50, 0.38, 0.25),
        "emission": 2.0, "charStylized": True, "fog": (28.0, 64.0),
    },
}
L = LOOKS[LOOK]


def sun_vector(elev, azim):
    """Blender-space direction TOWARDS the light (Unity forward negated, axes remapped)."""
    f = living.light_forward(elev, azim)  # Unity (x, y, z)
    return Vector((f[0], f[2], -f[1]))  # Unity->Blender: (-x, -z, y); toward light = -forward


def node(nt, kind, x=0, y=0, **inputs):
    n = nt.nodes.new(kind)
    n.location = (x, y)
    for k, v in inputs.items():
        n.inputs[k].default_value = v
    return n


def painterly(name, image, state, *, clip=False, path=False, rim=True, emission=None, vertex_ao=True,
              water=False, character=False, lambert=False):
    """Painterly Lit approximation. Lighting term = Shader-to-RGB of a white diffuse lit by a
    sun of strength pi (world black for shading), i.e. max(N.L,0) * shadow."""
    m = bpy.data.materials.new(name)
    nt = m.node_tree
    nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial", 1400, 0)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = 'Linear'
    albedo = tex.outputs["Color"]
    if vertex_ao and not water:
        vc = nt.nodes.new("ShaderNodeVertexColor")
        vc.layer_name = "Color"
        mul = node(nt, "ShaderNodeMix", 200, 0)
        mul.data_type = 'RGBA'
        mul.blend_type = 'MULTIPLY'
        mul.inputs["Factor"].default_value = 1.0
        nt.links.new(albedo, mul.inputs[6])
        nt.links.new(vc.outputs["Color"], mul.inputs[7])
        albedo = mul.outputs[2]
        if path:
            bw = nt.nodes.new("ShaderNodeRGBToBW")
            nt.links.new(albedo, bw.inputs[0])
            sc = node(nt, "ShaderNodeMath", 300, -200)
            sc.operation = 'MULTIPLY_ADD'
            nt.links.new(bw.outputs[0], sc.inputs[0])
            sc.inputs[1].default_value = 0.9
            sc.inputs[2].default_value = 0.7
            pc = node(nt, "ShaderNodeVectorMath", 400, -200)
            pc.operation = 'SCALE'
            pc.inputs[0].default_value = L["path"]
            nt.links.new(sc.outputs[0], pc.inputs["Scale"])
            mix = node(nt, "ShaderNodeMix", 500, 0)
            mix.data_type = 'RGBA'
            nt.links.new(vc.outputs["Alpha"], mix.inputs["Factor"])
            nt.links.new(albedo, mix.inputs[6])
            nt.links.new(pc.outputs[0], mix.inputs[7])
            albedo = mix.outputs[2]

    # Light ramp.
    diff = node(nt, "ShaderNodeBsdfDiffuse", 0, 400, Color=(1, 1, 1, 1))
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    nt.links.new(diff.outputs[0], s2r.inputs[0])
    lum = nt.nodes.new("ShaderNodeRGBToBW")
    nt.links.new(s2r.outputs[0], lum.inputs[0])
    ramp = node(nt, "ShaderNodeMapRange", 300, 400)
    ramp.interpolation_type = 'SMOOTHSTEP'
    ramp.inputs["From Min"].default_value = L["rampThreshold"] - L["rampSoftness"]
    ramp.inputs["From Max"].default_value = L["rampThreshold"] + L["rampSoftness"]
    nt.links.new(lum.outputs[0], ramp.inputs["Value"])
    r = lum.outputs[0] if lambert else ramp.outputs[0]

    # Trilight ambient from the normal's up component.
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Normal"], sep.inputs[0])
    up = node(nt, "ShaderNodeMath", 0, 700); up.operation = 'MAXIMUM'; up.inputs[1].default_value = 0
    nt.links.new(sep.outputs["Z"], up.inputs[0])
    dn = node(nt, "ShaderNodeMath", 0, 800); dn.operation = 'MULTIPLY'; dn.inputs[1].default_value = -1
    nt.links.new(sep.outputs["Z"], dn.inputs[0])
    dn2 = node(nt, "ShaderNodeMath", 0, 850); dn2.operation = 'MAXIMUM'; dn2.inputs[1].default_value = 0
    nt.links.new(dn.outputs[0], dn2.inputs[0])
    a1 = node(nt, "ShaderNodeMix", 200, 700); a1.data_type = 'RGBA'
    nt.links.new(up.outputs[0], a1.inputs["Factor"])
    a1.inputs[6].default_value = (*state["ambientEquator"], 1)
    a1.inputs[7].default_value = (*state["ambientSky"], 1)
    amb = node(nt, "ShaderNodeMix", 400, 700); amb.data_type = 'RGBA'
    nt.links.new(dn2.outputs[0], amb.inputs["Factor"])
    nt.links.new(a1.outputs[2], amb.inputs[6])
    amb.inputs[7].default_value = (*state["ambientGround"], 1)
    floor = node(nt, "ShaderNodeMix", 500, 700); floor.data_type = 'RGBA'; floor.blend_type = 'LIGHTEN'
    floor.inputs["Factor"].default_value = 1.0
    nt.links.new(amb.outputs[2], floor.inputs[6])
    floor.inputs[7].default_value = (0.24, 0.26, 0.30, 1)
    # ambient * lerp(shadowTint, 1, ramp)
    tint = node(nt, "ShaderNodeMix", 600, 600); tint.data_type = 'RGBA'
    nt.links.new(r, tint.inputs["Factor"])
    tint.inputs[6].default_value = (1, 1, 1, 1) if lambert else (*L["shadowTint"], 1)
    tint.inputs[7].default_value = (1, 1, 1, 1)
    ambl = node(nt, "ShaderNodeVectorMath", 700, 650); ambl.operation = 'MULTIPLY'
    nt.links.new(floor.outputs[2], ambl.inputs[0])
    nt.links.new(tint.outputs[2], ambl.inputs[1])
    # light.color * intensity * ramp
    lc = Vector(state["lightColor"]) * state["lightIntensity"]
    key = node(nt, "ShaderNodeVectorMath", 700, 450); key.operation = 'SCALE'
    key.inputs[0].default_value = lc
    nt.links.new(r, key.inputs["Scale"])
    lighting = node(nt, "ShaderNodeVectorMath", 850, 500); lighting.operation = 'ADD'
    nt.links.new(key.outputs[0], lighting.inputs[0])
    nt.links.new(ambl.outputs[0], lighting.inputs[1])
    col = node(nt, "ShaderNodeVectorMath", 950, 300); col.operation = 'MULTIPLY'
    nt.links.new(albedo, col.inputs[0])
    nt.links.new(lighting.outputs[0], col.inputs[1])
    color = col.outputs[0]

    rim_strength = (0.32 if character and L["charStylized"] else L["rimStrength"]) if rim and not lambert else 0.0
    if rim_strength > 0:
        lw = node(nt, "ShaderNodeLayerWeight", 700, 200, Blend=0.5)
        p = node(nt, "ShaderNodeMath", 800, 200); p.operation = 'POWER'; p.inputs[1].default_value = 3.0
        nt.links.new(lw.outputs["Facing"], p.inputs[0])
        rr = node(nt, "ShaderNodeMath", 900, 200); rr.operation = 'MULTIPLY'
        rr.inputs[1].default_value = rim_strength
        nt.links.new(p.outputs[0], rr.inputs[0])
        rr2 = node(nt, "ShaderNodeMath", 950, 200); rr2.operation = 'MULTIPLY'
        nt.links.new(rr.outputs[0], rr2.inputs[0])
        nt.links.new(r, rr2.inputs[1])
        rc = node(nt, "ShaderNodeVectorMath", 1000, 200); rc.operation = 'SCALE'
        rc.inputs[0].default_value = L["rim"]
        nt.links.new(rr2.outputs[0], rc.inputs["Scale"])
        add = node(nt, "ShaderNodeVectorMath", 1050, 300); add.operation = 'ADD'
        nt.links.new(color, add.inputs[0]); nt.links.new(rc.outputs[0], add.inputs[1])
        color = add.outputs[0]
    if emission is not None:
        et = nt.nodes.new("ShaderNodeTexImage"); et.image = emission
        es = node(nt, "ShaderNodeVectorMath", 1000, 0); es.operation = 'SCALE'
        es.inputs["Scale"].default_value = L["emission"]
        nt.links.new(et.outputs["Color"], es.inputs[0])
        add = node(nt, "ShaderNodeVectorMath", 1100, 100); add.operation = 'ADD'
        nt.links.new(color, add.inputs[0]); nt.links.new(es.outputs[0], add.inputs[1])
        color = add.outputs[0]
    if water:
        wc = node(nt, "ShaderNodeVectorMath", 1000, -100); wc.operation = 'MULTIPLY'
        nt.links.new(color, wc.inputs[0]); wc.inputs[1].default_value = (0.8, 0.95, 0.95)
        color = wc.outputs[0]

    # Linear fog on view depth (Unity RenderSettings linear fog).
    cam = nt.nodes.new("ShaderNodeCameraData")
    fog = node(nt, "ShaderNodeMapRange", 1100, -200)
    fog.inputs["From Min"].default_value = L["fog"][0]
    fog.inputs["From Max"].default_value = L["fog"][1]
    nt.links.new(cam.outputs["View Z Depth"], fog.inputs["Value"])
    fm = node(nt, "ShaderNodeMix", 1200, 0); fm.data_type = 'RGBA'
    nt.links.new(fog.outputs[0], fm.inputs["Factor"])
    nt.links.new(color, fm.inputs[6])
    fm.inputs[7].default_value = (*state["fogColor"], 1)

    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(fm.outputs[2], em.inputs["Color"])
    shader = em.outputs[0]
    # The lighting probe must reach the output so EEVEE evaluates it; mix it in at 0 weight.
    probe = node(nt, "ShaderNodeMixShader", 1250, 100, Fac=0.0)
    nt.links.new(shader, probe.inputs[1])
    nt.links.new(diff.outputs[0], probe.inputs[2])
    probe.inputs["Fac"].default_value = 0.0
    shader = probe.outputs[0]
    if clip or water:
        trans = nt.nodes.new("ShaderNodeBsdfTransparent")
        mixs = node(nt, "ShaderNodeMixShader", 1300, 0)
        if clip:
            gt = node(nt, "ShaderNodeMath", 1200, -300); gt.operation = 'GREATER_THAN'
            gt.inputs[1].default_value = 0.45
            nt.links.new(tex.outputs["Alpha"], gt.inputs[0])
            nt.links.new(gt.outputs[0], mixs.inputs["Fac"])
        else:
            vc2 = nt.nodes.new("ShaderNodeVertexColor"); vc2.layer_name = "Color"
            wa = node(nt, "ShaderNodeMath", 1200, -300); wa.operation = 'MULTIPLY'
            wa.inputs[1].default_value = 0.8
            nt.links.new(vc2.outputs["Alpha"], wa.inputs[0])
            nt.links.new(wa.outputs[0], mixs.inputs["Fac"])
        nt.links.new(trans.outputs[0], mixs.inputs[1])
        nt.links.new(shader, mixs.inputs[2])
        shader = mixs.outputs[0]
        m.surface_render_method = 'DITHERED'
        if clip:
            m.use_backface_culling = False
    nt.links.new(shader, out.inputs["Surface"])
    return m


def find_image(stem):
    for img in bpy.data.images:
        if Path(img.filepath).stem == stem or img.name.startswith(stem):
            return img
    return None


def main():
    bpy.ops.wm.open_mainfile(filepath=KIT)
    scn = bpy.context.scene
    layout = json.loads((DATA / "OriginSanctuaryLayout.json").read_text(encoding="utf-8"))
    liv = json.loads((DATA / "OriginSanctuaryLiving.json").read_text(encoding="utf-8"))
    state = living.evaluate(HOUR, liv["keyframes"]) if "keyframes" in liv else living.evaluate(HOUR)

    tex_dir = Path(KIT).parent / "Textures"

    def img(name):
        return bpy.data.images.load(str(tex_dir / f"{name}.png"), check_existing=True)

    mats = {
        "ENV_Props": painterly("P_Props", img("T_ENV_PropsAtlas"), state, emission=img("T_ENV_PropsEmission")),
        "ENV_Foliage": painterly("P_Foliage", img("T_ENV_Foliage"), state, clip=True),
        "ENV_Ground": painterly("P_Ground", img("T_ENV_Ground"), state, path=True, rim=False),
        "ENV_Water": painterly("P_Water", img("T_ENV_Water"), state, water=True, rim=False),
    }
    kit_objects = {o.name: o for o in scn.objects if o.type == 'MESH' and o.name.startswith("SM_ENV_")}
    for o in kit_objects.values():
        for i, slot in enumerate(o.material_slots):
            base = slot.material.name.split(".")[0] if slot.material else ""
            if base in mats:
                o.data.materials[i] = mats[base]
        o.hide_render = True

    coll = bpy.data.collections.new("Layout")
    scn.collection.children.link(coll)
    for pl in layout["placements"]:
        src = kit_objects.get(pl["asset"])
        if src is None:
            continue
        inst = bpy.data.objects.new(pl["asset"] + "_inst", src.data)
        inst.location = (-pl["x"], -pl["z"], pl["y"])
        inst.rotation_euler = (0, 0, -math.radians(pl["yaw"]))
        inst.scale = (pl["scale"],) * 3
        coll.objects.link(inst)

    # Emberling on the first meadow spawn, facing the camera (Unity yaw 180 = towards -Z).
    dragon_center = Vector((0.0, -0.2, 0.45))
    if EMBERLING:
        before = set(bpy.data.objects)
        if EMBERLING.lower().endswith(".fbx"):
            bpy.ops.import_scene.fbx(filepath=EMBERLING)
        else:
            bpy.ops.import_scene.gltf(filepath=EMBERLING)
        new = [o for o in bpy.data.objects if o not in before]
        roots = [o for o in new if o.parent is None]
        bpy.context.view_layer.update()
        meshes = [o for o in new if o.type == 'MESH']
        pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
        lo = Vector([min(p[i] for p in pts) for i in range(3)])
        hi = Vector([max(p[i] for p in pts) for i in range(3)])
        s = 0.9 / max(hi.z - lo.z, 1e-6)  # prefab BoxCollider height 0.9 m
        pivot = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
        rig = bpy.data.objects.new("EmberlingRoot", None)
        scn.collection.objects.link(rig)
        for o in roots:
            o.parent = rig
            o.location -= pivot
        rig.scale = (s,) * 3
        sp = (0.0, 0.2)  # origin_meadow.spawn_01 (Unity x, z)
        rig.location = (-sp[0], -sp[1], 0)
        # Long axis of the GLB is Blender Y; turn three-quarter towards the camera.
        span_x, span_y = hi.x - lo.x, hi.y - lo.y
        base_yaw = 0.0 if span_y >= span_x else math.pi / 2
        rig.rotation_euler = (0, 0, base_yaw + math.pi + math.radians(35))
        dragon_center = Vector((-sp[0], -sp[1], 0.45))
        for o in meshes:
            src_img = None
            for slot in o.material_slots:
                if slot.material and slot.material.node_tree:
                    for n in slot.material.node_tree.nodes:
                        if n.type == 'TEX_IMAGE' and n.image is not None:
                            src_img = n.image
                            break
            if src_img is None:
                continue
            if L["charStylized"]:
                cm = painterly("P_Emberling", src_img, state, vertex_ao=False, character=True)
            else:
                # Baseline: URP/Lit diffuse with the imported base map (Lambert + ambient, no ramp, no rim).
                cm = painterly("Lit_Emberling", src_img, state, vertex_ao=False, lambert=True)
            for i in range(len(o.material_slots)):
                o.data.materials[i] = cm

    # Camera: the Unity landscape pose including the UI offset.
    cd = bpy.data.cameras.new("Cam")
    cd.type = 'ORTHO'
    cd.clip_end = 200
    cam = bpy.data.objects.new("Cam", cd)
    px, py, pz = layout_mod.camera_position()
    cam.location = (-px, -pz, py)
    p = math.radians(layout_mod.PITCH_DEG)
    cam.rotation_euler = Vector((0.0, -math.cos(p), -math.sin(p))).to_track_quat('-Z', 'Y').to_euler()
    scn.collection.objects.link(cam)
    scn.camera = cam

    # Sun of strength pi, white: Shader-to-RGB gives max(N.L, 0) * shadow.
    sd = bpy.data.lights.new("Sun", 'SUN')
    sd.energy = math.pi
    sd.color = (1, 1, 1)
    sd.angle = math.radians(3)
    sun = bpy.data.objects.new("Sun", sd)
    toward = sun_vector(state["lightElevation"], state["lightAzimuth"])
    sun.rotation_euler = (-toward).to_track_quat('-Z', 'Y').to_euler()
    scn.collection.objects.link(sun)
    world = bpy.data.worlds.new("Fog")
    scn.world = world
    bg = world.node_tree.nodes.get("Background")
    lp = world.node_tree.nodes.new("ShaderNodeLightPath")
    mix = world.node_tree.nodes.new("ShaderNodeMixShader")
    black = world.node_tree.nodes.new("ShaderNodeBackground")
    black.inputs["Strength"].default_value = 0.0
    bg.inputs["Color"].default_value = (*state["fogColor"], 1)
    wout = world.node_tree.nodes.get("World Output")
    world.node_tree.links.new(lp.outputs["Is Camera Ray"], mix.inputs["Fac"])
    world.node_tree.links.new(black.outputs[0], mix.inputs[1])
    world.node_tree.links.new(bg.outputs[0], mix.inputs[2])
    world.node_tree.links.new(mix.outputs[0], wout.inputs["Surface"])

    scn.render.engine = 'BLENDER_EEVEE'
    scn.eevee.taa_render_samples = 32
    scn.eevee.use_shadows = True
    scn.view_settings.view_transform = 'Standard'
    scn.render.film_transparent = False

    shots = [("16x9", 16 / 9, None), ("20x9", 20 / 9, None), ("focus_16x9", 16 / 9, dragon_center)]
    for label, aspect, focus in shots:
        h = 720
        scn.render.resolution_x, scn.render.resolution_y = int(round(h * aspect)), h
        if focus is None:
            cd.ortho_scale = 2 * layout_mod.ORTHO_SIZE * aspect
            cam.location = (-px, -pz, py)
        else:
            # Inspect/follow framing (U6.0.4a): dragon ~40 % of the free height, same pitch.
            cd.ortho_scale = 2 * 1.6 * aspect
            back = Vector((0.0, -math.cos(p), -math.sin(p)))
            cam.location = focus - back * layout_mod.CAM_DISTANCE
        scn.render.filepath = f"{OUT}_{label}.png"
        bpy.ops.render.render(write_still=True)
        print("[vis1] wrote", scn.render.filepath)


main()
