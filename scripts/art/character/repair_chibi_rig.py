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
  seamWeld      (CHAR.9c) true: vertices split at UV/normal seams (same rest position, different index; FBX/Unity keep
                them split) are joined in the blend graph and get IDENTICAL weights. Without it the two sides of a
                seam can follow different bones and the skinned mesh opens cracks (rig v7 Emberling: up to 45 mm,
                5 % of the height, at Eat) that the edge-stretch QA cannot see, because no edge spans a seam
  amplitude     {clip: {"head": k, "legs": k, "knees": k, "body": k, "tail": k, "wings": k, "fins": k}}: scale
                Neck+Head / leg-chain (Upper, Lower, Foot; not Wing*) / Spine*+Chest / Tail* / Wing* / Fin_* /
                lower-leg-only rotation of that clip by k (slerp to rest; "knees" multiplies "legs" on Lower_*);
                Wake ramps from the Sleep factors to 1 so Sleep -> Wake -> Idle stays closed; groundSpeed of a clip
                with scaled legs is scaled by the same k (runtime PlaybackRate = speed / groundSpeed stays foot-locked)
  trunkAxis     (CHAR.9c) true: the trunk share (Pelvis + Spine* + Chest) of every vertex is redistributed by its
                position along the spine chain seen from above, so the lower torso and the fore-leg tops follow
                Spine/Chest instead of Pelvis on rigs whose spine runs diagonally (rig v7 gaia, mossback...);
                Neck/Head weight behind the Chest joint (spilled onto the back / wing roots) returns to the trunk
                (neckSpill, default true with trunkAxis)
  local         (CHAR.9c) {clip: [[group, frame, k, halfWidthFrames], ...]}: pull that group's rotation toward rest by
                k only around `frame` (Gaussian in time, circular on loops, product of entries); transition ends
                keep factor 1. Written by tune_repair.py --local for the frames where the mesh breaks
  pinTransitions (CHAR.9c) true: authored transitions (Sleep->Wake->Idle, TakeOff/Fly/Land, WaterEnter/Swim/
                WaterExit, DigPrep/Burrow*) stay closed. Loop clips keep one constant factor; each end of a one-shot
                clip that meets another clip takes that clip's factor if it is a loop, else 1 (authored pose), easing
                to its own factor over pinEase (default 0.25) of its length. Replaces the Wake ramp. Off = CHAR.9b.
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
SEAM_WELD = bool(CFG.get('seamWeld', False))
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
# CHAR.9c trunkAxis: rig v7 places the spine chain on a diagonal (hips low, chest high), so 3D distance gives the
# lower torso and the top of the FORE legs to Pelvis (gaia: Pelvis+ForeUpper on 650 vertices, Swim stretch 7.3).
# With trunkAxis the trunk share of every vertex (Pelvis + Spine* + Chest, whatever it was) is redistributed by the
# vertex position ALONG the chain seen from above (XY, Z ignored): 1 at each bone's midpoint, linear to the next.
# Limb, tail, neck, head, wing and fin weights are not touched.
if CFG.get('trunkAxis', False) and 'Pelvis' in names:
    chain = ['Pelvis']
    while True:
        kids = [b.name for b in arm.data.bones[chain[-1]].children if b.name in names and (b.name == 'Chest' or b.name.startswith('Spine'))]
        if not kids: break
        chain.append(kids[0])
    if len(chain) > 1:
        ti = [names.index(b) for b in chain]
        pts = [seg(chain[0])[0][:2]] + [seg(b)[1][:2] for b in chain]
        seglen = [float(np.linalg.norm(pts[i + 1] - pts[i])) for i in range(len(chain))]
        cum = np.concatenate([[0.0], np.cumsum(seglen)])
        # arclength of each vertex's closest point on the XY polyline
        best = np.full(n, np.inf); sv = np.zeros(n)
        for i in range(len(chain)):
            a2, b2 = pts[i], pts[i + 1]; ab = b2 - a2; den = max(ab @ ab, 1e-12)
            t = np.clip(((co[:, :2] - a2) @ ab) / den, 0, 1)
            d = np.linalg.norm(co[:, :2] - (a2 + t[:, None] * ab), axis=1)
            upd = d < best; best[upd] = d[upd]; sv[upd] = cum[i] + t[upd] * seglen[i]
        mids = (cum[:-1] + cum[1:]) / 2
        Wn = np.zeros((n, len(chain)))
        for i in range(len(chain)):
            lo = mids[i - 1] if i else -np.inf; hi = mids[i + 1] if i + 1 < len(chain) else np.inf
            up = np.where(sv <= mids[i], (sv - lo) / max(mids[i] - lo, 1e-9) if i else 1.0, 0)
            dn = np.where(sv > mids[i], (hi - sv) / max(hi - mids[i], 1e-9) if i + 1 < len(chain) else 1.0, 0)
            Wn[:, i] = np.clip(up + dn, 0, 1)
        Wn /= np.maximum(Wn.sum(1, keepdims=True), 1e-9)
        # Neck/Head weight that rig v7 spilled onto the back or the wing roots (aurorawing: 300+ vertices half-way
        # down the body follow the Neck in LookAround, stretch 4.4) goes back to the trunk: only vertices ahead of
        # the Chest joint (minus half the blend width) may follow the neck
        hn = [names.index(b) for b in ('Neck', 'Head') if b in names]
        if hn and CFG.get('neckSpill', True):
            back = sv < cum[-2] - W_BLEND / 2
            spill = Wt[back][:, hn].sum(1)
            print('neck/head spill to trunk', int((spill > 0.01).sum()), 'vertices, weight', round(float(spill.sum()), 1))
            Wt[np.ix_(back, [ti[0]])] += spill[:, None]
            Wt[np.ix_(back, hn)] = 0.0
        share = Wt[:, ti].sum(1)
        moved = float(np.abs(Wt[:, ti] - share[:, None] * Wn).sum() / 2)
        Wt[:, ti] = share[:, None] * Wn
        print('trunk axis', chain, 'weight moved', round(moved, 1))
dom = Wt.argmax(1)
# hard steps: an edge whose two ends are dominated by different bones with a large weight jump
jump = np.abs(Wt[E[:,0]] - Wt[E[:,1]]).max(1)
# weight gradient per metre: a transition sharper than half the blend width (hard steps, and also moderate jumps across
# millimetre edges, e.g. Neck/Head behind the chibi head) seeds the geodesic blend region
hard = (jump / np.maximum(L, 1e-6)) > (2.0 / W_BLEND)
seeds = np.unique(E[hard].ravel())
SEAM = np.zeros((0, 2), dtype=E.dtype)
if SEAM_WELD:
    # coincident vertices (UV / normal seam splits): one group id per rest position (0.01 mm grid)
    _, grp = np.unique(np.round(co / 1e-5).astype(np.int64), axis=0, return_inverse=True)
    grp = grp.ravel()
    order = np.argsort(grp, kind='stable')
    same = grp[order][1:] == grp[order][:-1]
    SEAM = np.stack([order[:-1][same], order[1:][same]], 1)
    print('seam pairs', len(SEAM))
    # seams as extra graph edges: a weight jump across a seam is a hard step too (it is one point on the surface)
    jump_s = np.abs(Wt[SEAM[:, 0]] - Wt[SEAM[:, 1]]).max(1) if len(SEAM) else np.zeros(0)
    seeds = np.unique(np.concatenate([seeds, SEAM[jump_s > 0.05].ravel()]))
adj = [[] for _ in range(n)]
for (a,b),l in zip(E, L): adj[a].append((b,l)); adj[b].append((a,l))
for a, b in SEAM: adj[a].append((b, 0.0)); adj[b].append((a, 0.0))
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
if len(SEAM):  # seam pairs diffuse like the shortest real edge
    E = np.concatenate([E, SEAM]); wE = np.concatenate([wE, np.full(len(SEAM), wE.max())])
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
if SEAM_WELD:  # every member of a coincident group gets the group's mean weights (inside and outside the region)
    acc = np.zeros((grp.max() + 1, G)); np.add.at(acc, grp, X)
    X = acc[grp] / np.bincount(grp)[grp][:, None]
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
# authored transitions, same table as anim_qa.TRANSITIONS (from clip, frame, to clip, frame)
TRANSITION_ENDS = (('Sleep', 'start', 'Wake', 'start'), ('Wake', 'end', 'Idle', 'start'), ('Idle', 'start', 'TakeOff', 'start'),
                   ('TakeOff', 'end', 'Fly', 'start'), ('Fly', 'start', 'Land', 'start'), ('Land', 'end', 'Idle', 'start'),
                   ('Idle', 'start', 'WaterEnter', 'start'), ('WaterEnter', 'end', 'Swim', 'start'), ('Swim', 'start', 'WaterExit', 'start'),
                   ('WaterExit', 'end', 'Idle', 'start'), ('Idle', 'start', 'DigPrep', 'start'), ('DigPrep', 'end', 'BurrowEnter', 'start'),
                   ('BurrowEnter', 'end', 'BurrowEmerge', 'start'), ('BurrowEmerge', 'end', 'Idle', 'start'))
EASE = float(CFG.get('pinEase', 0.25))
# CHAR.9c local keyframe pull-back: {clip: [[group, frame, k, halfWidthFrames], ...]}. Around `frame` the group's
# rotation is slerped toward rest by k (Gaussian in time, circular on loops); the rest of the clip keeps its
# authored motion. Every clip end that takes part in an authored transition keeps factor 1 there (pinEase).
LOCAL = CFG.get('local', {})
if AMP or LOCAL:
    from mathutils import Quaternion
    sc = bpy.context.scene
    def leg(n): return not n.startswith('Wing') and any(t in n for t in ('Upper_', 'Lower_', 'Foot_'))
    groups = {'head': [pb for pb in arm.pose.bones if pb.name in ('Neck', 'Head')],
              'legs': [pb for pb in arm.pose.bones if leg(pb.name)],
              'knees': [pb for pb in arm.pose.bones if leg(pb.name) and 'Lower_' in pb.name],
              # CHAR.9c: other archetypes (winged / serpentine / aquatic). Pelvis and Root are never scaled
              # (they carry the clip's whole-body pose and root motion)
              'body': [pb for pb in arm.pose.bones if pb.name == 'Chest' or pb.name.startswith('Spine')],
              'tail': [pb for pb in arm.pose.bones if pb.name.startswith('Tail') and not pb.name.startswith('TailVFX')],
              'wings': [pb for pb in arm.pose.bones if pb.name.startswith('Wing')],
              'fins': [pb for pb in arm.pose.bones if pb.name.startswith('Fin_')]}
    groups = {g: pbs for g, pbs in groups.items() if any(g in c for c in AMP.values() if isinstance(c, dict))
              or any(g in c.get('ramp_from', {}) for c in AMP.values() if isinstance(c, dict))
              or any(e[0] == g for es in LOCAL.values() for e in es)}
    for pbs in groups.values():
        pbs[:] = [pb for pb in pbs if pb.name not in ('Root', 'Pelvis')]
    for pbs in groups.values():
        for pb in pbs: pb.rotation_mode = 'QUATERNION'
    plan = dict(AMP)
    PIN = bool(CFG.get('pinTransitions', False))
    clips = r['clips']

    def const_k(cname, g):  # factor of a clip that is not eased (loops are the anchors of every transition)
        return float(AMP.get(cname, {}).get(g, 1.0)) if isinstance(AMP.get(cname), dict) else 1.0
    pins = {}  # (clip, 'start'|'end') -> partner clip whose factor that end must take (None = authored pose, 1)
    if PIN:
        # CHAR.9c: authored transitions (anim_qa TRANSITIONS) stay closed. A loop clip keeps one constant factor;
        # the end of a one-shot clip that meets another clip takes that clip's factor if it is a loop, else 1
        # (authored pose), easing to the clip's own factor over pinEase of its length. Replaces the Wake ramp.
        for a_c, a_f, b_c, b_f in TRANSITION_ENDS:
            if a_c in clips and b_c in clips:
                if not clips[a_c].get('loop'): pins[(a_c, a_f)] = b_c if clips[b_c].get('loop') else None
                if not clips[b_c].get('loop'): pins[(b_c, b_f)] = a_c if clips[a_c].get('loop') else None
        for (cname, _e) in pins:
            plan.setdefault(cname, {})
    elif 'Sleep' in AMP and 'Wake' in clips and 'Wake' not in AMP:
        plan['Wake'] = {'ramp_from': AMP['Sleep']}
    in_transition = {}
    for a_c, a_f, b_c, b_f in TRANSITION_ENDS:
        if a_c in clips and b_c in clips:
            for cn, e in ((a_c, a_f), (b_c, b_f)):
                in_transition.setdefault(cn, set()).update({'start', 'end'} if clips[cn].get('loop') else {e})
    for cname in LOCAL:
        plan.setdefault(cname, {})

    def local_k(cname, g, f, t):
        es = [e for e in LOCAL.get(cname, ()) if e[0] == g]
        if not es: return 1.0
        c = clips[cname]; n = max(1, c['end'] - c['start'])
        k = 1.0
        for _g, fc, kk, hw in es:
            d = abs(f - fc)
            if c.get('loop'): d = min(d, n - d)
            k *= 1.0 - (1.0 - kk) * float(np.exp(-0.5 * (d / max(hw, 1e-6)) ** 2))
        for e, u in (('start', t), ('end', 1.0 - t)):
            if e in in_transition.get(cname, ()):
                k = 1.0 + (k - 1.0) * smooth(u / EASE)
        return k

    def smooth(x):
        x = min(1.0, max(0.0, x)); return 3 * x * x - 2 * x * x * x
    for cname, cfg in plan.items():
        c = clips[cname]; n = max(1, c['end'] - c['start'])
        for f in range(c['start'], c['end'] + 1):
            t = (f - c['start']) / n
            sc.frame_set(f)
            for g, pbs in groups.items():
                if 'ramp_from' in cfg:
                    k0 = cfg['ramp_from'].get(g, 1.0); k = k0 + (1.0 - k0) * (3 * t * t - 2 * t * t * t)
                else:
                    k = cfg.get(g, 1.0)
                    for e, u in (('start', t), ('end', 1.0 - t)):
                        if (cname, e) in pins:
                            partner = pins[(cname, e)]
                            kt = const_k(partner, g) if partner else 1.0
                            k = kt + (k - kt) * smooth(u / EASE)
                    k *= local_k(cname, g, f, t)
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
    r.setdefault('notes', []).append('CHAR.9b repair: geodesic weight blend %.2f m, amplitude %s' % (W_BLEND, json.dumps(AMP))
                                     + (', local %s' % json.dumps(LOCAL) if LOCAL else ''))
    json.dump(r, open(rig_out, 'w'), indent=1)
if not AMP and not LOCAL:
    r.setdefault('notes', []).append('CHAR.9b repair: geodesic weight blend %.2f m' % W_BLEND)
    json.dump(r, open(rigj if out == A.blend else out.replace('.blend', '_rig.json'), 'w'), indent=1)
r['repair'] = CFG
json.dump(r, open(rigj if out == A.blend else out.replace('.blend', '_rig.json'), 'w'), indent=1)
bpy.ops.wm.save_as_mainfile(filepath=out)
