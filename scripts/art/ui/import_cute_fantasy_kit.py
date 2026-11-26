"""Import the exact AI3 Cute Fantasy UI draft ZIP into ArtSource; no Unity or Git changes."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import tempfile
import zipfile

EXPECTED = "fc0b086660569dd5537c287ef553c1dc4721635a3d9f52b0da8287213dfed25b"
PREFIX = "Dravio_UI_Kit_v1"

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[3]
    dest = root / "ArtSource" / "UI" / "CuteFantasy_v1"
    if not (root / "Assets" / "Dravio").is_dir():
        raise SystemExit("Run from the Dravio repository.")
    if dest.exists():
        raise SystemExit(f"Destination already exists; will not overwrite: {dest}")
    if hashlib.sha256(args.archive.read_bytes()).hexdigest() != EXPECTED:
        raise SystemExit("Archive SHA256 mismatch; use the exact supplied v1 draft ZIP.")
    with zipfile.ZipFile(args.archive) as archive:
        if archive.testzip() is not None:
            raise SystemExit("Archive integrity check failed.")
        files = [x for x in archive.infolist() if not x.is_dir()]
        if len(files) != 94:
            raise SystemExit("Unexpected archive file count.")
        with tempfile.TemporaryDirectory() as tmp:
            stage = Path(tmp) / PREFIX
            stage.mkdir()
            seen = set()
            for info in files:
                p = PurePosixPath(info.filename)
                if p.is_absolute() or not p.parts or p.parts[0] != PREFIX or ".." in p.parts or len(p.parts) < 2 or "\\" in info.filename:
                    raise SystemExit(f"Unsafe path: {info.filename}")
                rel = PurePosixPath(*p.parts[1:])
                if rel in seen:
                    raise SystemExit(f"Duplicate archive path: {rel}")
                seen.add(rel)
                target = stage.joinpath(*rel.parts)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(archive.read(info))
            manifest = json.loads((stage / "manifest.json").read_text(encoding="utf-8"))
            if len(manifest["assets"]) != 83:
                raise SystemExit("Unexpected sprite count.")
            for asset in manifest["assets"]:
                rel = PurePosixPath(asset["path"])
                if rel not in seen:
                    raise SystemExit(f"Missing sprite: {rel}")
                image = stage.joinpath(*rel.parts)
                if hashlib.sha256(image.read_bytes()).hexdigest() != asset["sha256"]:
                    raise SystemExit(f"Sprite checksum mismatch: {rel}")
            shutil.copytree(stage, dest)
    print(f"Imported 94 source files, including 83 verified sprite candidates: {dest}")
    print("Review changes in GitHub Desktop, then commit and push your art branch.")

if __name__ == "__main__":
    main()
