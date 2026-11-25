"""Geometry analysis for automatic rigging (pure numpy; no Blender, so it is unit-testable).

Input: vertex positions (N x 3) in pipeline space: metres, Z up, front toward -Y, ground at z = 0,
X/Y centred. Output: landmarks (head, neck base, chest, pelvis, tail polyline, feet/hips per limb,
wing and fin roots/tips), a per-vertex region label used to restrict skin weights, a detected limb
count with confidence, and notes. Nothing here decides biology: limbs are counted from ground
contacts of THIS mesh, and the rig only uses that count when the art director accepts it
(limbCount "auto" in asset-overrides.json) or canonical data states it.

Manual landmarks (asset-overrides.json "landmarks", same space) replace any detected value.
"""
import math

import numpy as np

REGIONS = ('body', 'head', 'neck', 'tail', 'leg', 'wing', 'fin', 'eye', 'fragment')


def _pct(a, q):
    return float(np.percentile(a, q)) if len(a) else 0.0


class Analysis:
    def __init__(self, verts, slabs=48):
        V = np.asarray(verts, dtype=np.float64)
        if V.ndim != 2 or V.shape[1] != 3 or len(V) < 50:
            raise ValueError('need an N x 3 vertex array with at least 50 vertices')
        self.V = V
        self.lo = V.min(axis=0)
        self.hi = V.max(axis=0)
        self.size = self.hi - self.lo
        self.h = float(self.size[2])
        self.w = float(self.size[0])
        self.d = float(self.size[1])
        self.diag = float(np.linalg.norm(self.size))
        self.n_slabs = slabs
        self.notes = []
        self.landmarks = {}
        self.labels = np.array(['body'] * len(V), dtype=object)
        self.leg_of = np.full(len(V), -1, dtype=np.int64)

    # ---------------------------------------------------------------- ground contacts / limbs
    def ground_clusters(self, contact_rel=0.05, cell_rel=0.025, mask=None):
        V = self.V
        sel = V[:, 2] < self.lo[2] + contact_rel * self.h
        if mask is not None:
            sel &= mask
        low = np.where(sel)[0]
        if len(low) == 0:
            return []
        cell = max(cell_rel * self.diag, 1e-6)
        keys = np.floor(V[low, :2] / cell).astype(np.int64)
        cells = {}
        for i, k in zip(low, map(tuple, keys)):
            cells.setdefault(k, []).append(i)
        parent = {k: k for k in cells}

        def find(k):
            while parent[k] != k:
                parent[k] = parent[parent[k]]
                k = parent[k]
            return k
        for k in cells:
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    n = (k[0] + dx, k[1] + dy)
                    if n in cells:
                        a, b = find(k), find(n)
                        if a != b:
                            parent[a] = b
        groups = {}
        for k, idx in cells.items():
            groups.setdefault(find(k), []).extend(idx)
        out = []
        min_count = max(4, int(0.02 * len(low)))
        for idx in groups.values():
            if len(idx) < min_count:
                continue
            P = V[idx]
            out.append({'indices': np.array(idx), 'centroid': P.mean(axis=0), 'count': len(idx),
                        'xmin': float(P[:, 0].min()), 'xmax': float(P[:, 0].max()),
                        'ymin': float(P[:, 1].min()), 'ymax': float(P[:, 1].max())})
        return sorted(out, key=lambda c: (c['centroid'][1], c['centroid'][0]))

    def detect_limbs(self, mask=None):
        """Feet = ground-contact clusters off the midline; a cluster straddling the midline is belly/tail contact."""
        clusters = self.ground_clusters(mask=mask)
        feet, other = [], []
        for c in clusters:
            straddles = c['xmin'] < -0.03 * self.w and c['xmax'] > 0.03 * self.w
            if straddles or abs(c['centroid'][0]) < 0.06 * self.w:
                other.append(c)
            else:
                feet.append(c)
        left = sorted([c for c in feet if c['centroid'][0] < 0], key=lambda c: c['centroid'][1])
        right = sorted([c for c in feet if c['centroid'][0] > 0], key=lambda c: c['centroid'][1])
        paired = len(left) == len(right) and len(left) in (1, 2)
        sym = paired and all(abs(l['centroid'][1] - r['centroid'][1]) < 0.12 * self.d and
                             abs(l['centroid'][0] + r['centroid'][0]) < 0.15 * self.w for l, r in zip(left, right))
        count = len(left) + len(right)
        conf = 'high' if sym else ('low' if count else 'none')
        res = {'limbCountDetected': count, 'confidence': conf, 'groundContactsOffMidline': count,
               'groundContactsMidline': len(other)}
        named = {}
        if paired:
            parts = ['Hind'] if len(left) == 1 else ['Fore', 'Hind']
            for part, l, r in zip(parts, left, right):
                named[f'{part}_L'] = l
                named[f'{part}_R'] = r
        elif count:
            self.notes.append(f'{count} off-midline ground contacts are not left/right pairs; limbs need manual landmarks')
        self._feet_clusters = named
        res['feet'] = {k: [float(x) for x in (c['centroid'][0], c['centroid'][1], self.lo[2])] for k, c in named.items()}
        return res

    # ---------------------------------------------------------------- body profile
    def profile(self, exclude=None):
        V = self.V
        mask = np.ones(len(V), bool) if exclude is None else ~exclude
        edges = np.linspace(self.lo[1], self.hi[1], self.n_slabs + 1)
        rows = []
        for i in range(self.n_slabs):
            sel = mask & (V[:, 1] >= edges[i]) & (V[:, 1] <= edges[i + 1])
            P = V[sel]
            y = 0.5 * (edges[i] + edges[i + 1])
            if len(P) < 3:
                rows.append({'y': y, 'n': 0, 'area': 0.0, 'cx': 0.0, 'cz': self.h * 0.5, 'zmin': 0.0, 'zmax': 0.0,
                             'halfw': 0.0})
                continue
            zmin, zmax = _pct(P[:, 2], 3), _pct(P[:, 2], 97)
            halfw = _pct(np.abs(P[:, 0]), 90)
            rows.append({'y': y, 'n': int(len(P)), 'area': float(2 * halfw * (zmax - zmin)),
                         'cx': float(np.median(P[:, 0])), 'cz': float(np.median(P[:, 2])),
                         'zmin': zmin, 'zmax': zmax, 'halfw': halfw})
        return rows

    def _leg_mask(self, feet):
        """Vertices of the leg columns: near a foot in XY and below the local belly."""
        V = self.V
        mask = np.zeros(len(V), bool)
        self.leg_of[:] = -1
        prof = self.profile()
        for j, (key, c) in enumerate(sorted(feet.items())):
            cx, cy = c[0], c[1]
            slab = min(prof, key=lambda r: abs(r['y'] - cy))
            r_leg = max(0.6 * max(self._cluster_radius(key), 0.03 * self.diag), 0.04 * self.diag)
            belly = self._belly_z(cy)
            sel = (np.hypot(V[:, 0] - cx, V[:, 1] - cy) < r_leg) & (V[:, 2] < belly)
            mask |= sel
            self.leg_of[sel & (self.leg_of < 0)] = j
            del slab
        return mask

    def leg_segmentation(self, feet, mask=None, slices=28):
        """Track each leg upward from its foot through horizontal slices while its cross-section stays a
        separate XY island; it ends where it merges with the body (or another leg). Returns (leg mask,
        per-vertex leg index into sorted(feet), top z per leg)."""
        V = self.V
        main = np.ones(len(V), bool) if mask is None else mask
        keys = sorted(feet)
        top_limit = self.lo[2] + 0.75 * self.h
        dz = (top_limit - self.lo[2]) / slices
        cell = max(0.018 * self.diag, 1e-6)
        prev = {k: np.array(feet[k][:2], float) for k in keys}
        alive = set(keys)
        leg_of = np.full(len(V), -1, np.int64)
        tops = {k: self.lo[2] for k in keys}
        for i in range(slices):
            if not alive:
                break
            z0, z1 = self.lo[2] + i * dz, self.lo[2] + (i + 1) * dz
            idx = np.where(main & (V[:, 2] >= z0) & (V[:, 2] < z1))[0]
            if len(idx) == 0:
                continue
            comps = _xy_components(V[idx, :2], cell)
            owner = {}
            pick = {}
            for k in list(alive):
                d = [np.linalg.norm(c['centroid'] - prev[k]) for c in comps]
                j = int(np.argmin(d))
                c = comps[j]
                if d[j] > 0.15 * self.diag:
                    alive.discard(k)
                    continue
                pick[k] = j
                owner.setdefault(j, []).append(k)
            for k, j in pick.items():
                c = comps[j]
                straddles = c['min'][0] < 0 < c['max'][0]
                if len(owner[j]) > 1 or straddles:  # merged with the body or another leg
                    alive.discard(k)
                    continue
                members = idx[c['members']]
                leg_of[members] = keys.index(k)
                prev[k] = c['centroid']
                tops[k] = z1
        return leg_of >= 0, leg_of, tops

    def _cluster_radius(self, key):
        c = getattr(self, '_feet_clusters', {}).get(key)
        if c is None:
            return 0.0
        return 0.5 * max(c['xmax'] - c['xmin'], c['ymax'] - c['ymin'])

    def _belly_z(self, y):
        """Lowest body surface near the midline at this Y (above the ground contacts)."""
        V = self.V
        band = 0.06 * self.d
        sel = (np.abs(V[:, 1] - y) < band) & (np.abs(V[:, 0]) < 0.12 * self.w)
        P = V[sel]
        if len(P) == 0:
            return self.lo[2] + 0.35 * self.h
        z = _pct(P[:, 2], 5)
        if z < self.lo[2] + 0.06 * self.h:  # belly touches the ground (round babies): legs are short stubs
            z = self.lo[2] + 0.18 * self.h
        return z

    # ---------------------------------------------------------------- head / neck / tail
    def _area_curve(self, prof):
        a = np.array([r['area'] for r in prof])
        if len(a) >= 5:  # light smoothing
            k = np.array([1, 2, 3, 2, 1], float) / 9.0
            a = np.convolve(np.pad(a, 2, mode='edge'), k, mode='valid')
        return a

    def _slab_index(self, y):
        return int(np.clip((y - self.lo[1]) / max(self.d, 1e-9) * self.n_slabs, 0, self.n_slabs - 1))

    def head_blob(self, mask):
        """Head = the upper part of the front 15 % of the mesh (dragons carry the head at the front)."""
        V = self.V
        front = mask & (V[:, 1] < self.lo[1] + 0.15 * self.d)
        P = V[front]
        if len(P) < 10:
            P = V[mask][np.argsort(V[mask][:, 1])[:max(10, int(0.05 * mask.sum()))]]
        upper = P[P[:, 2] >= np.median(P[:, 2])]
        c = upper.mean(axis=0)
        r = max(0.6 * (upper[:, 2].max() - upper[:, 2].min()), 0.05 * self.diag)
        blob = mask & (np.linalg.norm(V - c, axis=1) < 1.6 * r)
        tip_y = float(V[blob][:, 1].min()) if blob.any() else float(self.lo[1])
        return np.array([0.0, c[1], c[2]]), np.array([0.0, tip_y, c[2]]), r

    def neck_from_profile(self, prof, lo_i, hi_i):
        """Index of a cross-section narrowing between lo_i and hi_i, or None."""
        a = self._area_curve(prof)
        if hi_i - lo_i < 3:
            return None
        j = int(np.argmin(a[lo_i:hi_i])) + lo_i
        before = float(a[:j].max()) if j > 0 else 0.0
        after = float(a[j:].max())
        return j if 0 < j < len(a) - 1 and a[j] < 0.7 * min(before, after) else None

    def tail_base_index(self, prof, from_i):
        """Scanning backward from the hips (or the body middle), the first slab whose section falls below
        55 % of the section at from_i: the tail taper."""
        a = self._area_curve(prof)
        ref = max(float(a[from_i]), 1e-12)
        for j in range(from_i, len(a)):
            if a[j] < 0.55 * ref:
                return j
        return None

    # ---------------------------------------------------------------- lateral appendages
    def lateral(self, prof, exclude):
        """Wings/fins: vertex groups well outside the core body half-width. Thickness is measured across
        the appendage's own plane (PCA), so tilted membranes still count as thin."""
        V = self.V
        core = np.array([r['halfw'] for r in prof if r['n']])
        if not len(core):
            return {}
        core_hw = _pct(core, 75)
        thresh = 1.3 * core_hw
        cand = (~exclude) & (np.abs(V[:, 0]) > thresh) & (V[:, 2] > self.lo[2] + 0.1 * self.h)
        out = {}
        for side, sign in (('L', -1.0), ('R', 1.0)):
            sel = np.where(cand & (np.sign(V[:, 0]) == sign))[0]
            if len(sel) < max(20, 0.004 * len(V)):
                continue
            P = V[sel]
            ax = np.abs(P[:, 0])
            root = P[ax <= _pct(ax, 10)].mean(axis=0)
            tip = P[ax >= _pct(ax, 98)].mean(axis=0)
            span = float(abs(tip[0]) - core_hw)
            if span < 0.4 * core_hw:  # a bulge, not an appendage
                continue
            Q = P - P.mean(axis=0)
            evals, evecs = np.linalg.eigh(np.cov(Q.T))
            thin_axis = evecs[:, 0]
            proj = Q @ thin_axis
            thickness = _pct(proj, 97) - _pct(proj, 3)
            # root on the body surface: project toward the midline at the core half-width
            root = np.array([sign * core_hw, root[1], root[2]])
            out[side] = {'indices': sel, 'root': root, 'tip': tip, 'span': span, 'thickness': float(thickness),
                         'centroid': P.mean(axis=0), 'coreHalfWidth': core_hw}
        return out

    # ---------------------------------------------------------------- full analysis
    def run(self, archetype, limb_count=None, manual=None, mask=None):
        """mask: vertices to analyse (e.g. main connected component; floating fragments excluded)."""
        manual = manual or {}
        if mask is not None:
            mask = np.asarray(mask, bool)
            self.V_all = self.V
            M = self.V[mask]
            self.lo, self.hi = M.min(axis=0), M.max(axis=0)
            self.size = self.hi - self.lo
            self.h, self.w, self.d = float(self.size[2]), float(self.size[0]), float(self.size[1])
            self.diag = float(np.linalg.norm(self.size))
            dropped = int((~mask).sum())
            if dropped:
                self.notes.append(f'{dropped} vertices in floating fragments ignored for landmark detection')
        main = np.ones(len(self.V), bool) if mask is None else mask
        limbs = self.detect_limbs(main)
        feet = dict(limbs['feet'])
        if 'feet' in manual:
            feet = {k: list(map(float, v)) for k, v in manual['feet'].items()}
        if limb_count == 0:
            feet = {}
        elif limb_count and len(feet) != limb_count:
            self.notes.append(f'limbCount {limb_count} given but {len(feet)} feet found; add feet landmarks')
            if 'feet' not in manual:
                feet = {}
        if feet:
            leg_mask, self.leg_of, self.leg_tops = self.leg_segmentation(feet, main)
        else:
            leg_mask, self.leg_tops = np.zeros(len(self.V), bool), {}
        prof = self.profile(exclude=leg_mask | ~main)
        lat = self.lateral(prof, leg_mask | ~main)
        lat_mask = np.zeros(len(self.V), bool)
        for v in lat.values():
            lat_mask[v['indices']] = True
        body_mask = main & ~leg_mask & ~lat_mask
        prof = self.profile(exclude=~body_mask)
        fore = [v for k, v in feet.items() if k.startswith('Fore')]
        hind = [v for k, v in feet.items() if k.startswith('Hind')]
        rows = [r for r in prof if r['n']]

        def body_at(y):
            r = min(rows, key=lambda r: abs(r['y'] - y))
            return np.array([0.0, float(y), r['cz']])
        head_c, head_tip, head_r = self.head_blob(body_mask)
        lm = {'headCenter': head_c, 'headTip': head_tip}
        long_body = archetype in ('serpentine', 'aquatic') or self.d > 3.0 * max(self.h, self.w)
        if fore:
            chest = body_at(float(np.mean([f[1] for f in fore])))
        else:
            chest = body_at(self.lo[1] + (0.3 if long_body else 0.35) * self.d)
        if hind:
            pelvis = body_at(float(np.mean([f[1] for f in hind])))
        else:
            pelvis = body_at(self.lo[1] + (0.6 if long_body else 0.7) * self.d)
        # neck: a narrowing between head and chest if there is one, else 35 % of the way from chest to head
        j = self.neck_from_profile(prof, self._slab_index(head_c[1]), self._slab_index(chest[1]))
        if j is not None:
            neck = body_at(prof[j]['y'])
        else:
            neck = chest + 0.35 * (head_c - chest)
            self.notes.append('no neck narrowing in the profile; neck base set between chest and head')
        lm['neckBase'], lm['chest'], lm['pelvis'] = neck, chest, pelvis
        ti = self.tail_base_index(prof, self._slab_index(pelvis[1]))
        if ti is None:
            ti = self._slab_index(self.lo[1] + 0.85 * self.d)
            self.notes.append('no tail taper found; tail base placed at 85 % of length')
        tail_base = body_at(prof[ti]['y'])
        if tail_base[1] <= pelvis[1]:
            tail_base = body_at(pelvis[1] + 0.05 * self.d)
        lm['tailBase'] = tail_base
        tail_rows = [r for r in rows if r['y'] >= tail_base[1]]
        tail_pts = [np.array([0.0, r['y'], r['cz']]) for r in tail_rows]
        tail_end = body_mask & (self.V[:, 1] > self.hi[1] - 0.03 * self.d)
        tz = float(np.median(self.V[tail_end][:, 2])) if tail_end.any() else (tail_pts[-1][2] if tail_pts else pelvis[2])
        lm['tailTip'] = np.array([0.0, float(self.hi[1]), tz])
        lm['tailPoints'] = [tail_base] + tail_pts[1:] + [lm['tailTip']]
        mid_rows = [r for r in rows if neck[1] <= r['y'] <= tail_base[1]]
        lm['bodyPoints'] = [neck] + [np.array([0.0, r['y'], r['cz']]) for r in mid_rows] + [tail_base]
        lm['feet'] = {k: np.array(v) for k, v in feet.items()}
        lm['hips'] = {}
        for k, f in lm['feet'].items():
            spine = chest if k.startswith('Fore') else pelvis
            # hip joint: inside the body a little above where this leg's cross-section joins the body
            top = self.leg_tops.get(k, self._belly_z(f[1]))
            hip_z = min(top + 0.4 * max(spine[2] - top, 0.0), spine[2])
            lm['hips'][k] = np.array([f[0] * 0.85, f[1], max(hip_z, f[2] + 0.1 * self.h)])
        lm['appendages'] = {}
        for side, v in lat.items():
            near_head = v['centroid'][1] < neck[1] + 0.25 * head_r
            thin = v['thickness'] < 0.08 * self.diag
            if archetype == 'winged' and not near_head and thin:
                kind = 'wing'
            elif near_head:
                kind = 'earfin'
            else:
                kind = 'fin'
            lm['appendages'][side] = {'kind': kind, 'root': v['root'], 'tip': v['tip'], 'span': v['span'],
                                      'thickness': v['thickness']}
        if archetype == 'winged' and any(a['kind'] == 'wing' for a in lm['appendages'].values()):
            for a in lm['appendages'].values():  # left/right membranes are one structure: classify both
                if a['kind'] == 'fin':
                    a['kind'] = 'wing'
        if archetype == 'winged' and len([a for a in lm['appendages'].values() if a['kind'] == 'wing']) < 2:
            self.notes.append('winged archetype but no left/right wing surfaces detected; add wing landmarks')
        for k, v in manual.items():
            if k in ('feet', 'limbCount', 'eyes', 'appendages', '_comment'):
                continue
            if isinstance(v, (list, tuple)) and len(v) == 3:
                lm[k] = np.array(v, float)
        if 'appendages' in manual:
            for side, a in manual['appendages'].items():
                lm['appendages'][side] = {'kind': a['kind'], 'root': np.array(a['root'], float),
                                          'tip': np.array(a['tip'], float), 'span': None, 'thickness': None}
        self.landmarks = lm
        self._label(prof, leg_mask, lat, manual.get('eyes'))
        if mask is not None:
            self.labels[~mask] = 'fragment'
        return {'limbs': limbs, 'landmarks': lm, 'notes': self.notes}

    def _label(self, prof, leg_mask, lat, eyes):
        """Region per vertex by the nearest centre-line part: head, neck, body or tail; then legs, appendages, eyes."""
        V = self.V
        lm = self.landmarks
        parts = [('head', [lm['headTip'], lm['headCenter']]),
                 ('neck', [lm['headCenter'], lm['neckBase']]),
                 ('body', [lm['neckBase'], lm['chest'], lm['pelvis'], lm['tailBase']]),
                 ('tail', lm['tailPoints'])]
        best = np.full(len(V), np.inf)
        lab = np.array(['body'] * len(V), dtype=object)
        for name, pts in parts:
            d = _dist_to_polyline(V, pts)
            better = d < best
            lab[better] = name
            best[better] = d[better]
        side = np.array([''] * len(V), dtype=object)
        for sd, a in lm['appendages'].items():
            if sd in lat:
                lab[lat[sd]['indices']] = 'wing' if a['kind'] == 'wing' else 'fin'
                side[lat[sd]['indices']] = sd
        keys = sorted(lm['feet'])
        leg_key = np.array([''] * len(V), dtype=object)
        sel = leg_mask & (lab != 'wing') & (lab != 'fin')
        lab[sel] = 'leg'
        for j, k in enumerate(keys):
            leg_key[sel & (self.leg_of == j)] = k
        self.side = side
        self.leg_key = leg_key
        if eyes:
            r = 0.05 * self.diag
            for e in eyes:
                lab[np.linalg.norm(V - np.array(e, float), axis=1) < r] = 'eye'
        self.labels = lab

    def summary(self):
        lm = self.landmarks
        conv = lambda x: [round(float(c), 4) for c in x]
        return {
            'headTip': conv(lm['headTip']), 'headCenter': conv(lm['headCenter']), 'neckBase': conv(lm['neckBase']),
            'chest': conv(lm['chest']), 'pelvis': conv(lm['pelvis']), 'tailBase': conv(lm['tailBase']),
            'tailTip': conv(lm['tailTip']), 'feet': {k: conv(v) for k, v in lm['feet'].items()},
            'hips': {k: conv(v) for k, v in lm['hips'].items()},
            'appendages': {s: {'kind': a['kind'], 'root': conv(a['root']), 'tip': conv(a['tip'])}
                           for s, a in lm['appendages'].items()},
            'regionCounts': {r: int(np.sum(self.labels == r)) for r in REGIONS},
            'notes': list(self.notes),
        }


def _xy_components(P2, cell):
    """Connected components of 2-D points on a grid (8-neighbourhood)."""
    keys = np.floor(P2 / cell).astype(np.int64)
    cells = {}
    for i, k in enumerate(map(tuple, keys)):
        cells.setdefault(k, []).append(i)
    parent = {k: k for k in cells}

    def find(k):
        while parent[k] != k:
            parent[k] = parent[parent[k]]
            k = parent[k]
        return k
    for k in cells:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (k[0] + dx, k[1] + dy)
                if n in cells:
                    a, b = find(k), find(n)
                    if a != b:
                        parent[a] = b
    groups = {}
    for k, m in cells.items():
        groups.setdefault(find(k), []).extend(m)
    out = []
    for m in groups.values():
        m = np.array(m)
        Q = P2[m]
        out.append({'members': m, 'centroid': Q.mean(axis=0), 'min': Q.min(axis=0), 'max': Q.max(axis=0)})
    return out


def _dist_to_polyline(V, pts):
    P = np.asarray(pts, float)
    best = np.full(len(V), np.inf)
    for a, b in zip(P[:-1], P[1:]):
        ab = b - a
        L = float(ab @ ab)
        t = np.clip(((V - a) @ ab) / L, 0, 1) if L > 1e-12 else np.zeros(len(V))
        d = np.linalg.norm(V - (a + t[:, None] * ab), axis=1)
        best = np.minimum(best, d)
    return best


def main_component_mask(n_verts, edges, min_fraction=0.02):
    """True for vertices in connected components holding at least min_fraction of all vertices
    (generated meshes often carry small floating fragments that must not steer landmark detection)."""
    parent = np.arange(n_verts)

    def find(i):
        root = i
        while parent[root] != root:
            root = parent[root]
        while parent[i] != root:
            parent[i], i = root, parent[i]
        return root
    for a, b in edges:
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb
    roots = np.array([find(i) for i in range(n_verts)])
    ids, counts = np.unique(roots, return_counts=True)
    keep = set(ids[counts >= min_fraction * n_verts]) or {ids[np.argmax(counts)]}
    return np.isin(roots, list(keep))


def resample(points, n):
    """n + 1 points evenly spaced by arc length along a polyline."""
    P = np.asarray(points, float)
    if len(P) == 1:
        return np.repeat(P, n + 1, axis=0)
    seg = np.linalg.norm(np.diff(P, axis=0), axis=1)
    s = np.concatenate([[0.0], np.cumsum(seg)])
    if s[-1] <= 1e-12:
        return np.repeat(P[:1], n + 1, axis=0)
    t = np.linspace(0, s[-1], n + 1)
    return np.stack([np.interp(t, s, P[:, k]) for k in range(3)], axis=1)


def allowed_bones(region, bones, leg_key=None, appendage_side=None, appendage_kind=None):
    """Bones a vertex of `region` may be weighted to (restricts leg bones from body, eyes, tail).
    `bones` is the set of deform bone names. Legs: own chain + its hip parent. Head/eye: head/neck only."""
    # Root is never a skin target (it only carries vertical offsets); weights go to the body chain.
    spine = {b for b in bones if b in ('Pelvis', 'Spine', 'Chest') or b.startswith('Spine')}
    tail = {b for b in bones if b.startswith('Tail')}
    head = {b for b in bones if b in ('Head', 'Neck', 'Jaw')}
    if region == 'eye':
        return {b for b in bones if b == 'Head'} or head
    if region == 'head':
        return head | {b for b in bones if b.startswith('EarFin')}
    if region == 'neck':
        return head | ({'Chest', 'Spine1'} & bones) | (spine if not ({'Chest', 'Spine1'} & bones) else set())
    if region == 'tail':
        return tail | {b for b in bones if b in ('Pelvis', 'TailRoot')} | ({max(spine)} if spine and not tail else set())
    if region == 'leg' and leg_key:
        part, side = leg_key.split('_')
        own = {f'{part}Upper_{side}', f'{part}Lower_{side}', f'{part}Foot_{side}'} & bones
        hip_parent = {'Chest'} if part == 'Fore' else {'Pelvis'}
        return own | (hip_parent & bones) | (spine if not (hip_parent & bones) else set())
    if region == 'wing' and appendage_side:
        return {b for b in bones if b.startswith('Wing') and b.endswith('_' + appendage_side)} | ({'Chest'} & bones)
    if region == 'fin' and appendage_side:
        own = {b for b in bones if 'Fin' in b and b.endswith('_' + appendage_side)}
        return own | head | spine if appendage_kind == 'earfin' else own | spine
    # body: everything except distal limb/wing/tail bones; leg Upper bones allowed only near hips (caller)
    return spine | ({'Neck'} & bones)


def border_mask(labels, edges, rings=2, pair=None):
    """Vertices within `rings` edges of a region border (where masking leaves hard weight steps).
    With `pair=(a, b)` only the border between regions a and b counts."""
    lab = np.asarray(labels)
    E = np.asarray(edges, np.int64)
    m = np.zeros(len(lab), bool)
    if len(E) == 0:
        return m
    la, lb = lab[E[:, 0]], lab[E[:, 1]]
    diff = la != lb
    if pair is not None:
        a, b = pair
        diff &= ((la == a) & (lb == b)) | ((la == b) & (lb == a))
    m[E[diff, 0]] = True
    m[E[diff, 1]] = True
    for _ in range(rings):
        grow = m[E[:, 0]] | m[E[:, 1]]
        m[E[grow, 0]] = True
        m[E[grow, 1]] = True
    return m


def smooth_weights(W, allowed, edges, iterations=4, alpha=0.5, active=None):
    """Laplacian smoothing of per-vertex weight rows (N x B) over mesh edges, re-masked to each vertex's
    allowed bones and renormalised after every pass. Softens region borders without letting a bone leak
    into a region where it is not allowed."""
    W = np.asarray(W, float) * allowed
    E = np.asarray(edges, np.int64)
    if len(E) == 0:
        return W
    n = len(W)
    deg = np.bincount(E[:, 0], minlength=n) + np.bincount(E[:, 1], minlength=n)
    deg = np.maximum(deg, 1)[:, None]
    for _ in range(iterations):
        acc = np.zeros_like(W)
        np.add.at(acc, E[:, 0], W[E[:, 1]])
        np.add.at(acc, E[:, 1], W[E[:, 0]])
        Wn = ((1 - alpha) * W + alpha * acc / deg) * allowed
        W = Wn if active is None else np.where(np.asarray(active)[:, None], Wn, W)
        s = W.sum(axis=1, keepdims=True)
        W = np.where(s > 1e-12, W / np.maximum(s, 1e-12), W)
    return W


def limit_and_normalize(weights, max_inf=4):
    """weights: dict bone -> w. Keep the max_inf largest, renormalise. Empty stays empty."""
    items = sorted(((w, b) for b, w in weights.items() if w > 1e-6), reverse=True)[:max_inf]
    s = sum(w for w, _ in items)
    return {b: w / s for w, b in items} if s > 0 else {}


def foot_phases(gait, legs):
    """Normalised phase offset per leg key ('Fore_L', ...) for a gait. walk = lateral sequence."""
    if gait == 'walk':
        table = {'Hind_L': 0.0, 'Fore_L': 0.25, 'Hind_R': 0.5, 'Fore_R': 0.75}
    elif gait == 'trot':
        table = {'Fore_L': 0.0, 'Hind_R': 0.0, 'Fore_R': 0.5, 'Hind_L': 0.5}
    elif gait == 'bound':
        table = {'Fore_L': 0.0, 'Fore_R': 0.05, 'Hind_L': 0.5, 'Hind_R': 0.55}
    else:
        raise ValueError(gait)
    if len(legs) == 2:  # biped: alternate
        return {k: (0.0 if k.endswith('_L') else 0.5) for k in legs}
    return {k: table[k] for k in legs}


def foot_track(t, phase, duty, stride, lift):
    """In-place foot path for normalised cycle time t. Stance: foot moves backward (+Y) at constant speed
    = stride / (duty * T) while on the ground, i.e. the body travels forward (-Y) without sliding.
    Swing: foot returns forward along a raised arc. Returns (dy, dz) relative to the neutral foot position."""
    u = (t - phase) % 1.0
    if u < duty:
        s = u / duty
        return (-0.5 + s) * stride, 0.0
    s = (u - duty) / (1.0 - duty)
    # smooth swing (cosine ease) so velocity is continuous at lift-off and touch-down
    e = 0.5 - 0.5 * math.cos(math.pi * s)
    dy = (0.5 - e) * stride
    return dy, lift * math.sin(math.pi * s)


def chain_weights(points, joints, names, parent=None, power=6.0, parent_blend=0.3, parent_max=0.5):
    """Weights along a bone chain (legs, tails) from distances to the chain segments.

    `joints` are the chain joint positions (len(names)+1), `names` the bone per segment.
    w_i ~ (d_min / d_i)^power, so a vertex deep inside one segment is ~rigid to it and vertices
    around a joint blend smoothly over a width that follows the limb thickness (no hard step
    where the closest segment switches). Vertices whose projection lies within `parent_blend`
    of the first segment start blend into `parent` (Chest/Pelvis), up to `parent_max`.
    Returns {bone: weight} dicts summing to 1, at most 3 bones each.
    """
    P = np.asarray(points, float)
    J = np.asarray(joints, float)
    n = len(names)
    seg = J[1:] - J[:-1]
    L2 = np.maximum((seg ** 2).sum(axis=1), 1e-18)
    D = np.zeros((len(P), n))
    t0 = np.zeros(len(P))
    for i in range(n):
        t = np.clip(((P - J[i]) @ seg[i]) / L2[i], 0.0, 1.0)
        D[:, i] = np.linalg.norm(P - (J[i] + t[:, None] * seg[i]), axis=1)
        if i == 0:
            t0 = t
    D = np.maximum(D, 1e-6)
    W = (D.min(axis=1, keepdims=True) / D) ** power
    out = []
    for r in range(len(P)):
        order = np.argsort(-W[r])[:3]
        w = {names[j]: float(W[r, j]) for j in order if W[r, j] > 1e-3}
        s = sum(w.values())
        w = {k: x / s for k, x in w.items()}
        if parent and names[0] in w and t0[r] < parent_blend:
            k = parent_max * (1 - t0[r] / parent_blend) * w[names[0]]
            w = {b: x * (1 - k) for b, x in w.items()}
            w[parent] = w.get(parent, 0.0) + k
        out.append(w)
    return out
