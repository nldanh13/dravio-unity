"""CHAR.9: measure skinned-mesh deformation of an animated .blend (edge length / rest length, ground penetration),
with the same definitions and limits as anim_qa.py, for exports that do not carry a rig.json (e.g. the legacy
EmberlingRefinedGLB contract). Usage: python measure_skin_deformation.py -- <file.blend> <out.json> (bpy 5.3)."""
import bpy, sys, numpy as np, json
blend, out = sys.argv[sys.argv.index('--')+1:][:2]
bpy.ops.wm.open_mainfile(filepath=blend)
o = [x for x in bpy.data.objects if x.type == 'MESH' and any(m.type == 'ARMATURE' for m in x.modifiers)][0]
E = np.array([e.vertices[:] for e in o.data.edges])
dg = bpy.context.evaluated_depsgraph_get()
def co():
    ev = o.evaluated_get(dg); m = ev.to_mesh()
    a = np.empty(len(m.vertices)*3); m.vertices.foreach_get('co', a); ev.to_mesh_clear()
    return (np.array(o.matrix_world)[:3,:3] @ a.reshape(-1,3).T).T + np.array(o.matrix_world)[:3,3]
sc = bpy.context.scene
clips = {'Idle':(1,181),'Walk':(182,217),'Eat':(218,278),'Doze':(279,399),'Happy':(400,445)}
arm = o.find_armature()
for b in arm.pose.bones: b.rotation_quaternion = (1,0,0,0); b.rotation_euler = (0,0,0); b.location = (0,0,0); b.scale = (1,1,1)
act = arm.animation_data.action; arm.animation_data.action = None
sc.frame_set(1); bpy.context.view_layer.update(); rest = co(); L0 = np.linalg.norm(rest[E[:,0]]-rest[E[:,1]], axis=1); ok = L0 > 1e-6
arm.animation_data.action = act
h = rest[:,2].max() - rest[:,2].min()
res = {}
for n,(a,b) in clips.items():
    st, cp, gp = [], [], []
    for f in range(a, b+1, 2):
        sc.frame_set(f); P = co(); L = np.linalg.norm(P[E[:,0]]-P[E[:,1]], axis=1)[ok]/L0[ok]
        st.append(np.percentile(L, 99.9)); cp.append(np.percentile(L, 0.1)); gp.append(max(0.0, -P[:,2].min())/h)
    res[n] = {'stretchP999': round(max(st),3), 'compressP001': round(min(cp),3), 'groundPenRel': round(max(gp),4)}
json.dump(res, open(out,'w'), indent=1); print(json.dumps(res))
