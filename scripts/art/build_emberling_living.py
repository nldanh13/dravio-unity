# U1.3: separate rigged Emberling Baby Normal prototype; idle animation only.
# Run via Blender --background --python scripts/art/build_emberling.py -- <output.fbx>
# No external assets. Not a finished sculpt, rig, or painterly texture.
import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = Path(args[0] if args else "art-exports/EmberlingLiving.fbx").resolve()
polish_preview = "--polish-preview" in args[1:]
care_preview = "--care-preview" in args[1:] or polish_preview
walk_preview = "--walk-preview" in args[1:] or care_preview
if care_preview and out.stem != ("EmberlingPolished" if polish_preview else "EmberlingCare"):
    raise ValueError("Care or polish preview requires its own output filename")
if walk_preview and not care_preview and out.stem != "EmberlingWander":
    raise ValueError("Walk preview requires a separate EmberlingWander.fbx output")
out.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def mat(name, rgb, roughness=.78):
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
iris=mat("Iris | golden",(.49,.24,.035),.40)
black=mat("Pupil",(.025,.013,.01),.32)
white=mat("Catchlight",(.99,.94,.8),.40)

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

if polish_preview:
    # Continuous cylindrical body UV in the undeformed mesh. U=0 top, .5 underside;
    # V increases from tail to muzzle. Per-face unwrap avoids a seam across triangles.
    # Limbs and head use an approximate projection; final artist UVs remain future work.
    uv = skin.data.uv_layers.new(name="BodyPaint")
    skin.data.uv_layers.active = uv
    uv.active_render = True
    for face in skin.data.polygons:
        coords=[]
        for loop_index in face.loop_indices:
            p=skin.data.vertices[skin.data.loops[loop_index].vertex_index].co
            f=-p.y
            center=.72 + .60*max(0., min(1., (f-.20)/.75))
            if f<-.65:
                center=.58+.65*max(0.,min(1.,(-f-.65)/1.05))**2
            u=(math.atan2(p.x,p.z-center)/(2*math.pi))%1.
            v=max(0.,min(1.,(f+1.85)/3.4))
            coords.append((loop_index,u,v))
        seam=max(q[1] for q in coords)-min(q[1] for q in coords)>.5
        for idx,u,v in coords:
            uv.data[idx].uv=(u+1. if seam and u<.5 else u,v)
        face.material_index=0
    # One material for the entire skin; Unity supplies the continuous painted gradient.
    skin.data.materials.pop(index=1)
    
for s in (-1,1):
    ellipsoid("Eye surround",(s*.37,1.60,1.01),(.225,.27,.13 if polish_preview else .16),dark)
    ellipsoid("Iris",(s*.39,1.60,1.085 if polish_preview else 1.12),(.164,.21,.085 if polish_preview else .11),iris)
    ellipsoid("Pupil",(s*.40,1.61,1.15 if polish_preview else 1.195),(.105,.16,.045 if polish_preview else .055),black)
    ellipsoid("Catchlight",(s*.40-.04,1.70,1.185 if polish_preview else 1.24),(.040,.048,.024),white)
    ellipsoid("Small glint",(s*.40+.04,1.55,1.185 if polish_preview else 1.24),(.016,.020,.016),white)
    ellipsoid("Nostril",(s*.14,1.43,1.45),(.039,.029,.017),dark)
    for i in (-1,0,1):
        ellipsoid("Front claw",(s*.43+i*.10,.11,.86),(.041,.053,.071),belly)
        ellipsoid("Rear claw",(s*.48+i*.11,.11,-.075),(.041,.053,.071),belly)
for i in range(4):
    ellipsoid("Rounded back scale",(0,1.16-i*.04,-.29-i*.15),(.095,.13,.11),dark)
if polish_preview:
    # Rounded flame silhouette, gently tapering and leaning back at the top.
    for name,center,radii,material in [
        ("Round flame",(0,1.30,-1.72),(.23,.32,.22),gold),
        ("Flame core",(-.025,1.38,-1.73),(.135,.24,.13),cream)
    ]:
        obj=ellipsoid(name,center,radii,material)
        for vertex in obj.data.vertices:
            t=max(0.,min(1.,(vertex.co.z/radii[1]+1.)*.5))
            taper=1.-.45*t*t
            vertex.co.x*=taper
            vertex.co.y=vertex.co.y*taper+.14*t*t
else:
    ellipsoid("Round flame",(0,1.30,-1.72),(.23,.31,.23),gold)
    ellipsoid("Flame core",(-.035,1.46,-1.78),(.12,.19,.14),cream)


# Generic quadruped skeleton. Coordinates use Blender space through xyz().
# Leg bones are deliberately static for idle; gait needs separate weight QA.
specs = [
    ("Root",(0,0,0),(0,.24,0),None),
    ("Pelvis",(0,.65,-.44),(0,.74,-.12),"Root"),
    ("Chest",(0,.74,-.12),(0,1.08,.44),"Pelvis"),
    ("Head",(0,1.08,.44),(0,1.61,.73),"Chest"),
    ("Tail1",(0,.58,-.60),(.15,.72,-1.02),"Pelvis"),
    ("Tail2",(.15,.72,-1.02),(0,1.23,-1.70),"Tail1"),
    ("Flame",(0,1.23,-1.70),(0,1.67,-1.75),"Tail2"),
]
for s,label in [(-1,"L"),(1,"R")]:
    specs += [
        ("Fore_"+label,(s*.37,.71,.43),(s*.43,.16,.59),"Chest"),
        ("Hind_"+label,(s*.43,.65,-.47),(s*.48,.16,-.35),"Pelvis"),
        ("Eye_"+label,(s*.38,1.60,1.09),(s*.38,1.85,1.09),"Head")
    ]

meshes=[o for o in bpy.context.scene.objects if o.type=="MESH"]
bpy.ops.object.select_all(action='DESELECT')
arm_data=bpy.data.armatures.new("EmberlingSkeleton")
arm=bpy.data.objects.new("EmberlingRig",arm_data)
bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm
arm.select_set(True)
arm.show_in_front=True
bpy.ops.object.mode_set(mode='EDIT')
for name,head,tip,parent in specs:
    b=arm_data.edit_bones.new(name)
    b.head=xyz(head)
    b.tail=xyz(tip)
    if parent:b.parent=arm_data.edit_bones[parent]
    b.use_deform=name!="Root"
bpy.ops.object.mode_set(mode='OBJECT')

def smooth01(t):
    t=max(0.,min(1.,t))
    return t*t*(3.-2.*t)

def blend(a,b,t):
    t=smooth01(t)
    return {a:1-t,b:t}

def body_weights(p):
    x,y,z=p
    f=-y
    side="L" if x<0 else "R"
    # Explicit anatomical regions; avoid nearest-bone cross-talk between legs.
    if f<-.69:
        if f<-1.04:return blend("Tail1","Tail2",(-f-1.04)/.30)
        return blend("Pelvis","Tail1",(-f-.69)/.24)
    if z<.55 and abs(x)>.18:
        leg=("Fore_" if f>.02 else "Hind_")+side
        return blend(leg,"Chest" if f>.02 else "Pelvis",(z-.23)/.32)
    if z>1.02 and f>.28:
        return blend("Chest","Head",(z-1.02)/.27)
    return blend("Pelvis","Chest",(f+.40)/.77)

def rigid_bone(o):
    name=o.name
    x,y,z=o.matrix_world.translation
    side="L" if x<0 else "R"
    if name.startswith(("Eye surround","Iris","Pupil","Catchlight","Small glint")):
        return "Eye_"+side
    if name.startswith("Front claw"):return "Fore_"+side
    if name.startswith("Rear claw"):return "Hind_"+side
    if name.startswith(("Round flame","Flame core")):return "Flame"
    if name.startswith("Rounded back scale"):return "Chest" if -y>-.40 else "Pelvis"
    return "Head"

for o in meshes:
    # Keep every detail attached to its corresponding deform bone.
    world=o.matrix_world.copy()
    o.parent=arm
    o.matrix_world=world
    modifier=o.modifiers.new("Emberling skin","ARMATURE")
    modifier.object=arm
    modifier.use_deform_preserve_volume=True
    groups={}
    def group(name):
        if name not in groups:groups[name]=o.vertex_groups.new(name=name)
        return groups[name]
    for v in o.data.vertices:
        weights=body_weights(world@v.co) if o==skin else {rigid_bone(o):1.}
        for name,weight in weights.items():
            if weight>1e-6:group(name).add([v.index],weight,'REPLACE')

# One explicit six-second action, first and last poses equal. No root locomotion.
scene=bpy.context.scene
scene.render.fps=30
scene.frame_start=1
scene.frame_end=181
pose=arm.pose.bones
for bone in pose:bone.rotation_mode='XYZ'
for frame in range(1,182):
    t=(frame-1)/180
    cycle=2*math.pi*t
    breath=math.sin(cycle*2)
    pose["Chest"].scale=(1+.014*breath,1+.008*breath,1+.014*breath)
    pose["Head"].rotation_euler=(.025*breath,0,.018*math.sin(cycle))
    pose["Tail1"].rotation_euler=(0,0,.09*math.sin(cycle))
    pose["Tail2"].rotation_euler=(.025*math.sin(cycle),0,.13*math.sin(cycle-.55))
    pose["Flame"].scale=(1+.035*math.sin(cycle*4),1+.025*math.sin(cycle*4),1+.035*math.sin(cycle*4))
    # Local Y follows the upright eye bone. Short, symmetric blink.
    blink=1-.90*max(0.,1-abs(frame-83)/4)
    for label in ("L","R"):pose["Eye_"+label].scale=(1,blink,1)
    for name,prop in [("Chest","scale"),("Head","rotation_euler"),("Tail1","rotation_euler"),("Tail2","rotation_euler"),("Flame","scale"),("Eye_L","scale"),("Eye_R","scale")]:
        pose[name].keyframe_insert(data_path=prop,frame=frame,group=name)

if walk_preview:
    # Bake one take with disjoint ranges: Idle 1..181, Walk 182..217.
    # Unity splits the ranges explicitly; the existing idle export is unchanged.
    # A four-beat walk with small leg swings, no root motion, provisional weights.
    scene.frame_end=217
    names=("Fore_L","Hind_R","Fore_R","Hind_L")
    offsets=(0.,.25,.5,.75)
    for frame in range(1,218):
        if frame<=181:
            for name in names:
                pose[name].rotation_euler=(0,0,0)
                pose[name].keyframe_insert(data_path="rotation_euler",frame=frame,group=name)
            continue
        t=(frame-182)/35
        cycle=2*math.pi*t
        for name,offset in zip(names,offsets):
            phase=2*math.pi*(t+offset)
            # Local X pitch; low amplitude to limit hip deformation.
            pose[name].rotation_euler=(.24*math.sin(phase),0,0)
            pose[name].keyframe_insert(data_path="rotation_euler",frame=frame,group=name)
        pose["Chest"].scale=(1+.009*math.sin(cycle*2),1,1)
        pose["Head"].rotation_euler=(.035*math.cos(cycle*2),0,.025*math.sin(cycle))
        pose["Tail1"].rotation_euler=(0,0,.13*math.sin(cycle))
        pose["Tail2"].rotation_euler=(.03*math.sin(cycle),0,.15*math.sin(cycle-.55))
        pose["Flame"].scale=(1+.035*math.sin(cycle*2),1,1+.035*math.sin(cycle*2))
        for label in ("L","R"):pose["Eye_"+label].scale=(1,1,1)
        for name,prop in [("Chest","scale"),("Head","rotation_euler"),("Tail1","rotation_euler"),("Tail2","rotation_euler"),("Flame","scale"),("Eye_L","scale"),("Eye_R","scale")]:
            pose[name].keyframe_insert(data_path=prop,frame=frame,group=name)
if care_preview:
    # Additional disjoint baked clips (30 fps).
    # Eat 218..278 (2s), Doze 279..399 (4s), Happy 400..445 (1.5s).
    # Single-bone legs remain standing. Doze is not a full lying-down pose.
    scene.frame_end=445
    for frame in range(218,446):
        for bone in pose:
            bone.rotation_euler=(0,0,0)
            bone.scale=(1,1,1)
        if frame<=278:
            t=(frame-218)/60
            envelope=math.sin(math.pi*t)**2
            # Head dips and bobs; no articulated jaw on this prototype.
            pose["Head"].rotation_euler=(-.34*envelope+.035*envelope*math.sin(2*math.pi*5*t),0,0)
            pose["Chest"].rotation_euler=(-.08*envelope,0,0)
            pose["Tail2"].rotation_euler=(0,0,.11*math.sin(2*math.pi*t))
        elif frame<=399:
            t=(frame-279)/120
            breath=math.sin(4*math.pi*t)
            pose["Head"].rotation_euler=(-.19+.015*breath,0,0)
            pose["Chest"].scale=(1+.013*breath,1+.008*breath,1+.013*breath)
            pose["Tail1"].rotation_euler=(.08,0,.025*math.sin(2*math.pi*t))
            for label in ("L","R"):pose["Eye_"+label].scale=(1,.10,1)
        else:
            t=(frame-400)/45
            envelope=math.sin(math.pi*t)**2
            pose["Head"].rotation_euler=(.12*envelope,0,.08*envelope*math.sin(4*math.pi*t))
            pose["Tail1"].rotation_euler=(0,0,.20*envelope*math.sin(6*math.pi*t))
            pose["Tail2"].rotation_euler=(0,0,.25*envelope*math.sin(6*math.pi*t-.4))
            pose["Flame"].scale=(1+.10*envelope,1+.06*envelope,1+.10*envelope)
            for label in ("L","R"):pose["Eye_"+label].scale=(1,1-.45*envelope,1)
        # Key every deform channel to prevent curves bleeding across clip boundaries.
        for bone in pose:
            if bone.name=="Root":continue
            for prop in ("rotation_euler","scale"):
                bone.keyframe_insert(data_path=prop,frame=frame,group=bone.name)
action=arm.animation_data.action
action.name="Emberling_Care" if care_preview else "Emberling_Idle_Walk" if walk_preview else "Emberling_Idle"
scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.wm.save_as_mainfile(filepath=str(out.with_suffix(".blend")))
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,
    object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',
    apply_unit_scale=True,add_leaf_bones=False,mesh_smooth_type='FACE',
    bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0.0)
print("Dravio rigged idle prototype exported:",out)
