"""CHAR.9b — repair the rig v7 skin and motion of a CHIBI dragon made from a real generated GLB (short legs, no real
neck, big head). Measured on ArtSource 01_emberling (linear skinning, as Unity): before 9/15 clips over stretch 1.6 (Run 3.44, Sleep 3.32,
Eat 2.72); after: anim_qa PASS, 0 warnings, limits unchanged.

  python scripts/art/character/repair_chibi_rig.py <Name_Rigged.blend> <Name_rig.json> --config '<json>'
  (bpy 5.3 module, or blender --background --python <this> -- <args>); writes both files IN PLACE (work folder only).

config (asset-overrides.json species.<id>.repair / files.<stem>.repair):
  blendWidth_m  geodesic half-width (metres) of the weight blend around every sharp weight transition (default 0.12)
  skinning      "linear" (Unity SkinnedMeshRenderer, FBX) or "dualQuaternion" (rig v7 default, Blender only). With
                "linear" the Armature modifier's Preserve Volume is switched off so every Blender QA number is the
                skinning Unity actually plays (rig v7 measured dual quaternion: Sleep 0.455 there, 0.424 linear)
  amplitude     {clip: {"head": k, "legs": k, "knees": k}}: scale Neck+Head / leg-chain (Upper, Lower, Foot) /
                lower-leg-only rotation of that clip by k (slerp to rest; "knees" multiplies "legs" on Lower_*);
                Wake ramps from the Sleep factors to 1 so Sleep -> Wake -> Idle stays closed; groundSpeed of a clip
                with scaled legs is scaled by the same k (runtime PlaybackRate = speed / groundSpeed stays foot-locked)
Steps:
  0. Skinning mode (above).
  1. Neck/Head share by distance to the two bone segments (generated heads were partly weighted to Neck).
  2. Seeds = edges whose weight changes faster than 2 / blendWidth per metre (hard steps AND moderate jumps across
     millimetre edges); vertices within blendWidth geodesic distance get a length-weighted Laplacian diffusion,
     restricted to bones already present nearby; max 4 influences, normalised.
  3. Anatomy-aware amplitude per clip (above).
Nothing is changed in the source GLB, the bone set, the clip frame ranges or the QA limits.
"""
import argparse
import heapq
import json
import sys

import bpy
import numpy as np
argv = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:]
ap = argparse.ArgumentParser(); ap.add_argument('blend'); ap.add_argument('rig_json'); ap.add_argument('--config', default='{}')
ap.add_argument('--out'); A = ap.parse_args(argv)
CFG = json.loads(A.config)
blend, rigj = A.blend, A.rig_json
out = A.out or A.blend
W_BLEND = float(CFG.get('blendWidth_m', 0.12))
SKINNING = CFG.get('skinning', 'dualQuaternion')
if SKINNING not in ('linear', 'dualQuaternion'):
    raise SystemExit('skinning must be "linear" or "dualQuaternion"')
r = json.load(open(rigj))
bpy.ops.wm.open_mainfile(filepath=blend)
o = [x for x in bpy.data.objects if x.type=='MESH' and x.find_armature()][0]
me = o.data; n = len(me.vertices)
co = np.array([v.co[:] for v in me.vertices])
E = np.array([e.vertices[:] for e in me.edges])
L = np.linalg.norm(co[E[:,0]]-co[E[:,1]], axis=1)
names = [g.name for g in o.vertex_groups]; G = len(names)
Wt = np.zeros((n, G))
for v in me.vertices:
    for g in v.groups: Wt[v.index, g.group] = g.weight
# CHAR.9b head/neck segmentation: generated chibi heads (horns, crest) were partly weighted to Neck, so they lag
# behind the head when it pitches (Eat, Doze). Reassign Neck/Head share by distance to the two bone segments.
arm = o.find_armature()
for m in o.modifiers:
    if m.type == 'ARMATURE':
        m.use_deform_preserve_volume = SKINNING == 'dualQuaternion'
M = o.matrix_world.inverted() @ arm.matrix_world
def seg(bn):
    b = arm.data.bones[bn]; return np.array((M @ b.head_local)[:]), np.array((M @ b.tail_local)[:])
def dseg(P, a, b):
    ab = b - a; t = np.clip(((P - a) @ ab) / max(ab @ ab, 1e-12), 0, 1); return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)
if 'Neck' in names and 'Head' in names:
    iN, iH = names.index('Neck'), names.index('Head')
    nh = Wt[:, iN] + Wt[:, iH]
    sel = nh > 0.5
    # chibi heads are much taller than the bounds-fitted Head bone: measure against a virtual Head segment from the
    # head joint to the centroid of the actual head volume (vertices above the neck top, forward of the chest joint)
    dN = dseg(co[sel], *seg('Neck')); dH = dseg(co[sel], *seg('Head'))
    tH = 1.0 / (1.0 + np.exp(-(dN - dH) / 0.015))     # soft: closer to Head segment -> Head
    Wt[sel, iH] = nh[sel] * tH; Wt[sel, iN] = nh[sel] * (1 - tH)
    print('neck/head reassigned', int(sel.sum()))
dom = Wt.argmax(1)
# hard steps: an edge whose two ends are dominated by different bones with a large weight jump
jump = np.abs(Wt[E[:,0]] - Wt[E[:,1]]).max(1)
# weight gradient per metre: a transition sharper than half the blend width (hard steps, and also moderate jumps across
# millimetre edges, e.g. Neck/Head behind the chibi head) seeds the geodesic blend region
hard = (jump / np.maximum(L, 1e-6)) > (2.0 / W_BLEND)
seeds = np.unique(E[hard].ravel())
adj = [[] for _ in range(n)]
for (a,b),l in zip(E, L): adj[a].append((b,l)); adj[b].append((a,l))
dist = np.full(n, np.inf); pq = []
for s in seeds: dist[s] = 0.0; pq.append((0.0, s))
heapq.heapify(pq)
while pq:
    d,u = heapq.heappop(pq)
    if d > dist[u] or d > W_BLEND: continue
    for v,l in adj[u]:
        nd = d + l
        if nd < dist[v]: dist[v] = nd; heapq.heappush(pq, (nd, v))
region = dist <= W_BLEND
# diffusion restricted to the region; allowed bones per vertex = bones present in its neighbourhood (no far bones)
le = np.median(L); iters = int(max(10, (W_BLEND/le)**2 * 0.5))
wE = 1.0/np.maximum(L, 1e-6)
allowed = Wt > 1e-6
for _ in range(2):  # neighbourhood of allowed bones
    allowed[E[:,0]] |= allowed[E[:,1]] & region[E[:,0], None]; allowed[E[:,1]] |= allowed[E[:,0]] & region[E[:,1], None]
X = Wt.copy()
for _ in range(iters):
    acc = np.zeros_like(X); ws = np.zeros(n)
    np.add.at(acc, E[:,0], X[E[:,1]]*wE[:,None]); np.add.at(acc, E[:,1], X[E[:,0]]*wE[:,None])
    np.add.at(ws, E[:,0], wE); np.add.at(ws, E[:,1], wE)
    Y = 0.5*X + 0.5*acc/np.maximum(ws,1e-9)[:,None]
    Y *= allowed
    Y /= np.maximum(Y.sum(1, keepdims=True), 1e-9)
    X = np.where(region[:,None], Y, Wt)
# keep max 4, normalise
idx = np.argsort(-X, 1)[:, :4]
Z = np.zeros_like(X); np.put_along_axis(Z, idx, np.take_along_axis(X, idx, 1), 1); Z /= np.maximum(Z.sum(1, keepdims=True), 1e-9)
for g in o.vertex_groups: g.remove(list(range(n)))
for j,g in enumerate(o.vertex_groups):
    nz = np.nonzero(Z[:, j] > 1e-4)[0]
    for i in nz: g.add([int(i)], float(Z[i, j]), 'REPLACE')
print('hard edges', int(hard.sum()), 'region verts', int(region.sum()), 'iters', iters, 'edge len', round(float(le),4))
# CHAR.9b anatomy-aware amplitude (chibi: almost no neck, short legs). Per clip: {"head": k, "legs": k}.
# Wake starts in the authored Sleep pose, so it ramps from the Sleep factors to 1 at its end (keeps the
# Sleep -> Wake -> Idle chain closed).
AMP = CFG.get('amplitude', {})
if AMP:
    from mathutils import Quaternion
    sc = bpy.context.scene
    groups = {'head': [pb for pb in arm.pose.bones if pb.name in ('Neck', 'Head')],
              'legs': [pb for pb in arm.pose.bones if any(t in pb.name for t in ('Upper_', 'Lower_', 'Foot_'))],
              'knees': [pb for pb in arm.pose.bones if 'Lower_' in pb.name]}
    for pbs in groups.values():
        for pb in pbs: pb.rotation_mode = 'QUATERNION'
    plan = dict(AMP)
    if 'Sleep' in AMP and 'Wake' in r['clips'] and 'Wake' not in AMP:
        plan['Wake'] = {'ramp_from': AMP['Sleep']}
    for cname, cfg in plan.items():
        c = r['clips'][cname]; n = max(1, c['end'] - c['start'])
        for f in range(c['start'], c['end'] + 1):
            t = (f - c['start']) / n
            sc.frame_set(f)
            for g, pbs in groups.items():
                if 'ramp_from' in cfg:
                    k0 = cfg['ramp_from'].get(g, 1.0); k = k0 + (1.0 - k0) * (3 * t * t - 2 * t * t * t)
                else:
                    k = cfg.get(g, 1.0)
                if k == 1.0: continue
                for pb in pbs:
                    pb.rotation_quaternion = Quaternion().slerp(pb.rotation_quaternion.copy(), k)
                    pb.keyframe_insert('rotation_quaternion', frame=f)
    print('amplitude plan', plan)
    # stride shrinks with the leg swing: keep the runtime contract PlaybackRate = speed / groundSpeed foot-locked
    rig_out = rigj if out == A.blend else out.replace('.blend', '_rig.json')
    for cname, cfg in AMP.items():
        if 'legs' in cfg and r['clips'][cname].get('groundSpeed'):
            r['clips'][cname]['groundSpeed'] = round(r['clips'][cname]['groundSpeed'] * cfg['legs'], 4)
    r.setdefault('notes', []).append('CHAR.9b repair: geodesic weight blend %.2f m, amplitude %s' % (W_BLEND, json.dumps(AMP)))
    json.dump(r, open(rig_out, 'w'), indent=1)
if not AMP:
    r.setdefault('notes', []).append('CHAR.9b repair: geodesic weight blend %.2f m' % W_BLEND)
    json.dump(r, open(rigj if out == A.blend else out.replace('.blend', '_rig.json'), 'w'), indent=1)
r['repair'] = CFG
json.dump(r, open(rigj if out == A.blend else out.replace('.blend', '_rig.json'), 'w'), indent=1)
bpy.ops.wm.save_as_mainfile(filepath=out)
