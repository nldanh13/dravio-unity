"""Validate existing Unity script GUIDs in AI1-owned directories; read-only."""
import re
from pathlib import Path
import sys

def errors(root):
    seen = {}
    problems = []
    for folder in ("Assets/Dravio/Scripts", "Assets/Dravio/Editor"):
        for path in sorted((root / folder).rglob("*.cs.meta")):
            raw = path.read_text(encoding="utf-8-sig")
            match = re.search(r"^guid:[ \t]*([^\r\n]+)", raw, re.M)
            guid = match.group(1).strip().lower() if match else ""
            if not re.fullmatch(r"[0-9a-f]{32}", guid):
                problems.append(f"{path.relative_to(root)}: invalid GUID {guid!r}")
            elif guid in seen:
                problems.append(f"{path.relative_to(root)}: duplicate GUID in {seen[guid]}")
            else:
                seen[guid] = path.relative_to(root)
    return problems

if __name__ == "__main__":
    root = Path(__file__).resolve().parents[2]
    problems = errors(root)
    print("\n".join(problems) if problems else "Existing Scripts/Editor script metadata GUID check PASS")
    sys.exit(bool(problems))
