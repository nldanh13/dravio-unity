"""VIS.4b (issue #91) - static integration checks for the VIS.3a grade (PR #89) without Unity.

  python scripts/qa/vis4b/check_vis4b_integration.py

1. Every float/colour/texture a M_ENV_*.mat sets exists in its shader's Properties block, and every
   Properties entry of the Painterly shaders is in UnityPerMaterial (SRP Batcher compatibility).
2. OriginSanctuaryLiving.asset keyframes == OriginSanctuaryLiving.json (the runtime reads the .asset).
3. The static 13:00 lighting (environment builder C#, committed scene RenderSettings/sun) equals the
   13:00 keyframe of the living JSON.
4. The QA branch does not touch gameplay/data/save/collider/location/animation paths vs the base.
Exit code 1 on any failure.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ENV = ROOT / "Assets" / "Dravio" / "Environment"
fails = []


def check(cond, msg):
    print(("PASS " if cond else "FAIL ") + msg)
    if not cond:
        fails.append(msg)


def shader_props(path):
    src = path.read_text(encoding="utf-8")
    block = src[src.index("Properties"):src.index("SubShader")]
    props = set(re.findall(r"^\s*(?:\[[^\]]*\]\s*)*(_\w+)\s*\(", block, re.M))
    cbuf = src[src.index("CBUFFER_START(UnityPerMaterial)"):src.index("CBUFFER_END")]
    cb = set(re.findall(r"\b(_\w+)\s*;", cbuf))
    textures = set(re.findall(r"TEXTURE2D\((_\w+)\)", src))
    return props, cb, textures


shaders = {
    "Dravio/Environment/Painterly Lit": ENV / "Shaders" / "DravioPainterlyLit.shader",
    "Dravio/Environment/Painterly Water": ENV / "Shaders" / "DravioPainterlyWater.shader",
}
guid_to_shader = {}
for name, p in shaders.items():
    meta = (p.parent / (p.name + ".meta")).read_text()
    guid_to_shader[re.search(r"guid: (\w+)", meta).group(1)] = name
    props, cb, tex = shader_props(p)
    # Textures (and their _ST) and toggles (keywords/render state) are not CBUFFER members.
    numeric = {x for x in props if x not in tex and x not in ("_AlphaClip", "_Cull")}
    missing = sorted(x for x in numeric if x not in cb)
    check(not missing, f"{p.name}: all numeric properties in UnityPerMaterial {missing or ''}")

for mat in sorted((ENV / "Materials").glob("M_ENV_*.mat")):
    txt = mat.read_text()
    g = re.search(r"m_Shader: \{fileID: \d+, guid: (\w+)", txt)
    if not g or g.group(1) not in guid_to_shader:
        continue
    props, _, _ = shader_props(shaders[guid_to_shader[g.group(1)]])
    used = set(re.findall(r"^\s*- (_\w+):", txt, re.M))
    unknown = sorted(u for u in used if u not in props and not u.endswith("_ST"))
    check(not unknown, f"{mat.name}: material properties exist in shader {unknown or ''}")

# 2. living .asset == json
liv = json.loads((ENV / "Data" / "OriginSanctuaryLiving.json").read_text())
asset = (ENV / "Data" / "OriginSanctuaryLiving.asset").read_text()
kf_block = asset[asset.index("  keyframes:"):]
asset_frames = re.split(r"\n  - hour: ", kf_block)[1:]
check(len(asset_frames) == len(liv["keyframes"]), f"living .asset keyframes {len(asset_frames)} == json {len(liv['keyframes'])}")


def yaml_list(fr, key):
    m = re.search(rf"\n    {key}:\n    - ([-\d.e]+)\n    - ([-\d.e]+)\n    - ([-\d.e]+)", fr)
    return [float(m.group(i)) for i in (1, 2, 3)] if m else None


def yaml_color(fr, key):
    m = re.search(rf"\b{key}: \{{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+)", fr)
    return [float(m.group(i)) for i in (1, 2, 3)] if m else None


def yaml_float(fr, key):
    m = re.search(rf"\b{key}: ([-\d.e]+)", fr)
    return float(m.group(1)) if m else None


bad = []
for fr, k in zip(asset_frames, liv["keyframes"]):
    hour = float(fr.split("\n", 1)[0])
    if abs(hour - k["hour"]) > 1e-4:
        bad.append(f"hour {hour} != {k['hour']}")
        continue
    for key in ("lightColor", "ambientSky", "ambientEquator", "ambientGround", "fogColor"):
        c = yaml_list(fr, key)
        if c is None or any(abs(a - b) > 1e-4 for a, b in zip(c, k[key])):
            bad.append(f"{hour}h {key} asset {c} != json {k[key]}")
    for key in ("lightIntensity", "shadowStrength", "lightElevation", "lightAzimuth"):
        v = yaml_float(fr, key)
        if v is None or abs(v - k[key]) > 1e-4:
            bad.append(f"{hour}h {key} asset {v} != json {k[key]}")
check(not bad, "living .asset matches json for light/ambient/fog keys " + ("; ".join(bad[:6]) if bad else ""))

# 3. 13:00 static lighting consistency
k13 = next(k for k in liv["keyframes"] if abs(k["hour"] - 13.0) < 1e-6)
cs = (ENV / "Editor" / "OriginSanctuaryEnvironmentBuilder.cs").read_text()
def cs_color(pattern):
    m = re.search(pattern + r"\s*=?\s*new Color\(([-\d.]+)f, ([-\d.]+)f, ([-\d.]+)f", cs)
    return [float(m.group(i)) for i in (1, 2, 3)] if m else None
pairs = {
    "ambientSky": cs_color(r"RenderSettings\.ambientSkyColor"),
    "ambientEquator": cs_color(r"RenderSettings\.ambientEquatorColor"),
    "ambientGround": cs_color(r"RenderSettings\.ambientGroundColor"),
    "fogColor": cs_color(r"Color sky"),
    "lightColor": cs_color(r"sun\.color"),
}
for key, v in pairs.items():
    check(v is not None and all(abs(a - b) < 1e-4 for a, b in zip(v, k13[key])), f"builder 13:00 {key} {v} == living {k13[key]}")
scene = (ENV / "Scenes" / "OriginLongTermGameplay_Environment.unity").read_text()
for key, yk in (("ambientSky", "m_AmbientSkyColor"), ("ambientEquator", "m_AmbientEquatorColor"),
                ("ambientGround", "m_AmbientGroundColor"), ("fogColor", "m_FogColor")):
    v = yaml_color(scene, yk)
    check(v is not None and all(abs(a - b) < 1e-4 for a, b in zip(v, k13[key])), f"scene {yk} {v} == living 13:00 {k13[key]}")

# 4. protected paths unchanged vs base
base = sys.argv[1] if len(sys.argv) > 1 else "origin/feat/u5-6-player-journey"
try:
    changed = subprocess.run(["git", "diff", "--name-only", base, "HEAD"], cwd=ROOT, capture_output=True,
                             text=True, check=True).stdout.split()
    protected = re.compile(r"Assets/Dravio/(Domain|Persistence|Data|Platform|Character|Scripts)/|"
                           r"OriginSanctuary(Colliders|Locations|Layout)\.json|\.anim$|\.controller$|ProjectSettings/")
    hit = [c for c in changed if protected.search(c)]
    check(not hit, f"no gameplay/data/save/collider/location/animation change vs {base} {hit or ''}")
except subprocess.CalledProcessError as e:
    check(False, f"git diff vs {base}: {e.stderr.strip()}")

print(f"\n{len(fails)} failure(s)")
sys.exit(1 if fails else 0)
