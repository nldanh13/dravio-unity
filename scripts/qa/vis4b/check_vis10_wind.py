"""Independent QA for VIS.10 wind-weighted flora GLBs (Issue #91).

Re-implements DravioWind() from DravioPainterlyLit.shader in numpy and checks:
COLOR_0 is VEC4, root (lowest) vertices have alpha 0, coincident positions share
alpha, alpha correlates with height, and deformation over sampled poses
(base 0.5/1/2.2, gust 0/1, 8 directions, 24 times, 3 world positions) keeps
faces sane. Usage: python3 check_vis10_wind.py [--strength 0.012] GLB...
Exits 1 on failure."""
import json, struct, sys, math
import numpy as np

CT = {5126: np.float32, 5123: np.uint16, 5125: np.uint32, 5121: np.uint8}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}

def load(path):
    b = open(path, "rb").read()
    jl = struct.unpack_from("<I", b, 12)[0]
    j = json.loads(b[20:20 + jl]); off = 20 + jl
    bl = struct.unpack_from("<I", b, off)[0]; bin_ = b[off + 8: off + 8 + bl]
    def acc(i):
        a = j["accessors"][i]; bv = j["bufferViews"][a["bufferView"]]
        dt = CT[a["componentType"]]; n = NC[a["type"]]
        o = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        arr = np.frombuffer(bin_, dt, a["count"] * n, o).reshape(a["count"], n)
        if a.get("normalized"): arr = arr / float(np.iinfo(dt).max)
        return arr.astype(np.float64), a["type"]
    return j, acc

def wind(p, wgt, s, base, gust, d, t, speed=1.6):
    x = base - 1.0
    w = wgt * s * max(0.0, 1.0 + x + gust * 0.8)
    ph = t * speed + p[:, 0] * 0.6 + p[:, 2] * 0.45
    q = p.copy()
    q[:, 0] += np.sin(ph) * w
    q[:, 2] += np.cos(ph * 0.83) * w * 0.6
    lean = wgt * s * gust * (0.75 + 0.25 * np.sin(ph * 0.37))
    q[:, 0] += d[0] * lean; q[:, 2] += d[1] * lean
    return q

def tri_geom(p, idx):
    a, b, c = p[idx[:, 0]], p[idx[:, 1]], p[idx[:, 2]]
    n = np.cross(b - a, c - a); ar = np.linalg.norm(n, axis=1)
    return n / np.maximum(ar, 1e-20)[:, None], ar

def check(path, s):
    j, acc = load(path); fails = []; rep = {"file": path.split("/")[-1]}
    prims = [pr for m in j["meshes"] for pr in m["primitives"]]
    rep["primitives"] = len(prims)
    for pr in prims:
        P, _ = acc(pr["attributes"]["POSITION"])
        if "COLOR_0" not in pr["attributes"]:
            fails.append("no COLOR_0"); continue
        C, ct = acc(pr["attributes"]["COLOR_0"])
        if ct != "VEC4": fails.append("COLOR_0 not VEC4"); continue
        I = (acc(pr["indices"])[0] if "indices" in pr else np.arange(len(P))[:, None]).reshape(-1, 3).astype(int)
        A = C[:, 3]
        rep.update(tris=len(I), verts=len(P), alpha_min=float(A.min()), alpha_max=float(A.max()))
        ymin = P[:, 1].min(); root = P[:, 1] <= ymin + 1e-4
        rep["root_verts"] = int(root.sum()); rep["root_alpha_max"] = float(A[root].max())
        if A[root].max() > 1e-6: fails.append("root vertices move")
        key = np.round(P, 5); _, inv = np.unique(key, axis=0, return_inverse=True)
        spread = np.zeros(inv.max() + 1); amin = np.full(inv.max() + 1, 9.0); amax = np.full(inv.max() + 1, -9.0)
        np.minimum.at(amin, inv, A); np.maximum.at(amax, inv, A)
        rep["coincident_alpha_max_diff"] = float((amax - amin).max())
        if (amax - amin).max() > 1e-3: fails.append("coincident vertices differ in alpha (seam tear)")
        rep["alpha_height_corr"] = float(np.corrcoef(P[:, 1], A)[0, 1])
        U = acc(pr["attributes"]["TEXCOORD_0"])[0] if "TEXCOORD_0" in pr["attributes"] else None
        if U is not None: rep["uv_range"] = [float(U.min()), float(U.max())]
        n0, a0 = tri_geom(P, I); ok = a0 > 1e-12
        maxd = 0.0; mind = 1.0; rmin, rmax = 9.0, 0.0; poses = 0
        for off in [(0, 0, 0), (-8, 0, 3), (7.5, 0, -5)]:
            Pw = P + np.array(off)
            for base in (0.5, 1.0, 2.2):
                for gust in (0.0, 1.0):
                    for k in range(8):
                        d = (math.cos(k * math.pi / 4), math.sin(k * math.pi / 4))
                        for t in np.linspace(0, 40, 24):
                            Q = wind(Pw, A, s, base, gust, d, t); poses += 1
                            maxd = max(maxd, float(np.linalg.norm(Q - Pw, axis=1).max()))
                            n1, a1 = tri_geom(Q, I)
                            mind = min(mind, float((n0[ok] * n1[ok]).sum(1).min()))
                            r = a1[ok] / a0[ok]; rmin = min(rmin, float(r.min())); rmax = max(rmax, float(r.max()))
        rep.update(poses=poses, max_disp_m=maxd, min_face_dot=mind, area_ratio=[rmin, rmax])
        if mind < 0.5: fails.append(f"face flips/strong shear (dot {mind:.3f})")
        if rmin < 0.5 or rmax > 2.0: fails.append("area ratio out of 0.5..2")
        bb = P.max(0) - P.min(0); rep["bounds_size"] = bb.tolist()
        rep["bounds_pad_needed_m"] = maxd
    rep["fails"] = fails
    return rep

if __name__ == "__main__":
    a = sys.argv[1:]; s = 0.012
    if a[:1] == ["--strength"]: s = float(a[1]); a = a[2:]
    bad = 0
    for f in a:
        r = check(f, s); print(json.dumps(r, indent=1)); bad += bool(r["fails"])
    sys.exit(1 if bad else 0)
