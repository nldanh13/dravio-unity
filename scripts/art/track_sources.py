"""Deterministic source inventory; does not claim art is Unity-ready."""
import hashlib
import json
from pathlib import Path
import re
import struct
import sys

PACKS = ("Dragons", "Habitats", "NatureAndGameplay")
IMAGES = {".jpg", ".jpeg", ".png", ".webp"}
ALLOWED = IMAGES | {".glb", ".fbx", ".blend", ".obj", ".mtl", ".tga", ".exr"}
GROUPS = (
    ("Dragons/images/", "dragons", True),
    ("Habitats/concepts/", "habitat_concepts", False),
    ("Habitats/props_3d/", "sanctuary_props", True),
    ("NatureAndGameplay/", "nature_gameplay", True),
)


def glb_problem(path):
    """Basic GLB 2.0 container and JSON mesh presence check, not geometry QA."""
    try:
        size = path.stat().st_size
        with path.open("rb") as f:
            magic, version, length = struct.unpack("<4sII", f.read(12))
            if magic != b"glTF" or version != 2 or length != size:
                return "invalid GLB header, version or file length"
            first = True
            while f.tell() < size:
                n, kind = struct.unpack("<I4s", f.read(8))
                if n % 4 or f.tell() + n > size:
                    return "invalid chunk length"
                if first:
                    if kind != b"JSON" or n > 32 * 1024 * 1024:
                        return "invalid or oversized JSON chunk"
                    doc = json.loads(f.read(n))
                    if not isinstance(doc, dict) or not doc.get("meshes"):
                        return "no mesh in GLB JSON"
                    if doc.get("asset", {}).get("version") != "2.0":
                        return "invalid glTF asset version"
                    first = False
                else:
                    f.seek(n, 1)
            if first:
                return "missing JSON chunk"
    except (OSError, ValueError, struct.error, AttributeError, TypeError) as e:
        return "unreadable GLB: " + type(e).__name__
    return None


def sha256(path):
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def group_for(rel):
    for prefix, group, required in GROUPS:
        if rel.startswith(prefix):
            return group, required
    return None, False


def scan(root):
    source = root / "ArtSource"
    files, by_stem, warnings = [], {}, []
    for pack in PACKS:
        folder = source / pack
        if not folder.is_dir():
            raise ValueError("Missing pack: " + pack)
        for p in sorted(folder.rglob("*")):
            if p.is_symlink():
                raise ValueError("Symlinks not supported: " + str(p))
            if not p.is_file() or p.suffix.lower() not in ALLOWED:
                continue
            rel = p.relative_to(source).as_posix()
            if any(part.lower() in {"extras", ".git", "__pycache__"} for part in p.relative_to(source).parts):
                continue
            rec = {"path": "ArtSource/" + rel, "bytes": p.stat().st_size, "sha256": sha256(p)}
            files.append(rec)
            group, required = group_for(rel)
            if group is None or p.stem.lower().startswith("preview_"):
                continue
            if p.suffix.lower() not in IMAGES | {".glb"}:
                continue
            key = str(Path(rel).with_suffix("")).replace("\\", "/")
            if not re.fullmatch(r"[A-Za-z0-9_/-]+", key):
                warnings.append("Nonstandard asset name: " + rel)
            item = by_stem.setdefault(key, {"id": key, "group": group,
                "requiresGlb": required, "references": [], "models": []})
            if p.suffix.lower() in IMAGES:
                item["references"].append(rec["path"])
            else:
                problem = glb_problem(p)
                item["models"].append({**rec, "basicCheck": "FAIL" if problem else "PASS",
                    **({"problem": problem} if problem else {})})
    rows = []
    for key, item in sorted(by_stem.items()):
        refs, models = item["references"], item["models"]
        if len(models) > 1:
            warnings.append("Multiple GLBs for: " + key)
        if not refs:
            warnings.append("GLB without matching reference (not counted as converted): " + key)
            status = "UNMATCHED_GLB"
        elif any(m["basicCheck"] == "FAIL" for m in models):
            status = "GLB_INVALID"
        elif len(models) > 1:
            status = "AMBIGUOUS_GLB"
        elif models:
            status = "GLB_UPLOADED"
        elif item["requiresGlb"]:
            status = "IMAGE_ONLY"
        else:
            status = "CONCEPT_ONLY"
        if status in {"GLB_INVALID", "AMBIGUOUS_GLB"}:
            warnings.append(status + ": " + key)
        rows.append({**item, "sourceStatus": status})
    stats = {}
    for _, group, required in GROUPS:
        selected = [x for x in rows if x["group"] == group and x["references"]]
        stats[group] = {"references": len(selected),
            "glbUploaded": sum(x["sourceStatus"] == "GLB_UPLOADED" for x in selected),
            "requiresGlb": required}
    manifest = {"formatVersion": 1,
        "description": "Original art sources; not Unity production assets. License and visual/technical QA must be reviewed.",
        "files": sorted(files, key=lambda x: x["path"])}
    progress = {"formatVersion": 1, "scope": "Source conversion only; no production approval inferred.",
        "groups": stats, "assets": rows, "warnings": sorted(warnings)}
    return manifest, progress


def markdown(progress):
    lines = ["# DRAVIO — Asset source progress", "",
        "Generated by scripts/art/track_sources.py. Do not edit manually.",
        "GLB_UPLOADED means a matching GLB passed basic container checks, not rig/LOD/Unity QA.",
        "Habitat concepts are not required to become a single GLB.", "",
        "| Group | References | GLB uploaded | Requires conversion |",
        "|---|---:|---:|---|"]
    for group, s in progress["groups"].items():
        lines.append(f"| {group} | {s['references']} | {s['glbUploaded']} | {'Yes' if s['requiresGlb'] else 'No'} |")
    required = [s for s in progress["groups"].values() if s["requiresGlb"]]
    total = sum(s["references"] for s in required)
    done = sum(s["glbUploaded"] for s in required)
    lines += ["", f"**Conversion: {done}/{total}.**", "",
        "| Asset | Source status |", "|---|---|"]
    for item in progress["assets"]:
        name = item["id"].replace("|", r"\|").replace("\n", " ").replace("\r", " ")
        lines.append(f"| {name} | {item['sourceStatus']} |")
    lines += ["", "## Warnings", ""]
    lines += ["- " + w.replace("\n", " ").replace("\r", " ") for w in progress["warnings"]] or ["None."]
    lines += ["", "Production QA: AI 3 records inspect/rig/LOD/prefab evidence in its own reports; AI 1 records integration evidence in its PR. Source uploads never imply approval.", ""]
    return "\n".join(lines)


def main(root):
    manifest, progress = scan(root)
    source = root / "ArtSource"
    for name, data in (("manifest.json", manifest), ("progress.json", progress)):
        (source / name).write_bytes((json.dumps(data, indent=2, ensure_ascii=False) + "\n").encode("utf-8"))
    (source / "PROGRESS.md").write_bytes(markdown(progress).encode("utf-8"))
    print(markdown(progress))
    return 0


if __name__ == "__main__":
    sys.exit(main(Path(__file__).resolve().parents[2]))
