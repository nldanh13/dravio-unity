# Blender 4.x/5.x: generate an editable, cohesive Emberling Baby Normal prototype.
# Run via Blender --background --python scripts/art/build_emberling.py -- <output.fbx>
# No external assets. Not a finished sculpt, rig, or painterly texture.
import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = Path(args[0] if args else "art-exports/EmberlingBaby.fbx").resolve()
out.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def mat(name, rgb, roughness=.42):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*rgb,1)
    p.inputs["Roughness"].default_value = roughness
    return m

orange=mat("Body | warm terracotta",(.69,.20,.073))
belly=mat("Belly | honey cream",(.94,.66,.30))
dark=mat("Scale | red clay",(.39,.075,.035))
gold=mat("Flame | amber",(.98,.43,.045))
cream=mat("Flame core",(.99,.79,.34))
iris=mat("Iris | golden",(.49,.24,.035),.26)
black=mat("Pupil",(.025,.013,.01),.17)
white=mat("Catchlight",(.99,.94,.8),.23)

# Design coordinates x, vertical y, forward z -> Blender x, -z, y.
def xyz(p):
    return (p[0],-p[2],p[1])

def ellipsoid(name,p,r,m=None, tilt=0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=20,location=xyz(p))
    o=bpy.context.object
    o.name=name
    o.scale=(r[0],r[2],r[1])
    o.rotation_euler.x=tilt
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    if m:o.data.materials.append(m)
    for face in o.data.polygons:face.use_smooth=True
    return o

parts=[]
def body(p,r,tilt=0):
    parts.append(ellipsoid("Sculpt volume",p,r,orange,tilt))

# One fused sculpt, preserving four walking legs and the round fire tail.
body((0,.72,-.12),(.57,.54,.78))
body((0,1.08,.44),(.43,.49,.44))
body((0,1.49,.67),(.60,.55,.55))
body((0,1.24,1.10),(.42,.27,.35))
body((0,1.39,1.28),(.31,.16,.18))
for s in (-1,1):
    body((s*.37,.39,.43),(.24,.37,.25))
    body((s*.43,.17,.59),(.25,.17,.32))
    body((s*.43,.46,-.47),(.32,.37,.36))
    body((s*.48,.17,-.35),(.28,.17,.32))
    body((s*.40,1.29,1.01),(.17,.17,.21))
for i in range(29):
    t=i/28
    r=.26-.15*t
    body((.18*math.sin(t*math.pi),.57+.65*t*t,-.64-1.04*t),(r,r,r+.045))

bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
skin=bpy.context.object
skin.name="Emberling_Baby_Body"
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
remesh=skin.modifiers.new("Fuse sculpt volumes","REMESH")
remesh.mode='VOXEL'
remesh.voxel_size=.025
bpy.ops.object.modifier_apply(modifier=remesh.name)
smooth=skin.modifiers.new("Soften joined forms","SMOOTH")
smooth.factor=.7
smooth.iterations=8
bpy.ops.object.modifier_apply(modifier=smooth.name)
dec=skin.modifiers.new("Prototype topology budget","DECIMATE")
dec.ratio=.55
bpy.ops.object.modifier_apply(modifier=dec.name)
skin.data.materials.clear()
skin.data.materials.append(orange)
skin.data.materials.append(belly)
for face in skin.data.polygons:
    x,y,z=face.center
    forward=-y
    # Soft cream colour region still has a material edge; production UV paint needed.
    face.material_index=int((forward>.84 and z<1.35 and abs(x)<.36) or (forward>.28 and z<1.10 and z>.38 and abs(x)<.29))
    face.use_smooth=True

for s in (-1,1):
    ellipsoid("Eye surround",(s*.37,1.60,1.01),(.225,.27,.16),dark)
    ellipsoid("Iris",(s*.39,1.60,1.12),(.164,.21,.11),iris)
    ellipsoid("Pupil",(s*.40,1.61,1.195),(.105,.16,.055),black)
    ellipsoid("Catchlight",(s*.40-.04,1.70,1.24),(.040,.048,.024),white)
    ellipsoid("Small glint",(s*.40+.04,1.55,1.24),(.016,.020,.016),white)
    ellipsoid("Nostril",(s*.14,1.43,1.45),(.039,.029,.017),dark)
    for i in (-1,0,1):
        ellipsoid("Front claw",(s*.43+i*.10,.11,.86),(.041,.053,.071),belly)
        ellipsoid("Rear claw",(s*.48+i*.11,.11,-.075),(.041,.053,.071),belly)
for i in range(4):
    ellipsoid("Rounded back scale",(0,1.16-i*.04,-.29-i*.15),(.095,.13,.11),dark)
ellipsoid("Round flame",(0,1.30,-1.72),(.23,.31,.23),gold)
ellipsoid("Flame core",(-.035,1.46,-1.78),(.12,.19,.14),cream)

# A clean origin at ground level, Y-up in Unity via FBX export.
bpy.ops.object.select_all(action='SELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(out.with_suffix(".blend")))
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    add_leaf_bones=False,mesh_smooth_type='FACE',bake_anim=False)
print("Dravio prototype exported:",out)
