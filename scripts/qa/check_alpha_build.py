"""Read-only source preflight for DRAVIO Android Alpha; not a Unity build."""
import argparse
import json
import re
from pathlib import Path

POLICY = "Assets/Dravio/Scripts/ConnectedWorldPolicy.cs"
SETTINGS = "ProjectSettings/ProjectSettings.asset"
BUILD_SETTINGS = "ProjectSettings/EditorBuildSettings.asset"
VERSION = "ProjectSettings/ProjectVersion.txt"
PACKAGES = "Packages/manifest.json"
BOOTSTRAP_MODELS = (
    "Assets/Dravio/Art/Models/EmberlingRefinedGLB.fbx",
    "Assets/Dravio/Art/Models/EmberlingRefined.fbx",
)


def preflight(root, release=False):
    root = Path(root)
    issues, facts = [], {}
    def issue(level, code, message):
        issues.append({"level": level, "code": code, "message": message})
    def read(rel):
        try:
            return (root / rel).read_text(encoding="utf-8-sig")
        except (OSError, UnicodeError):
            issue("BLOCK", "missing-source", rel)
            return ""
    def scalar(raw, key):
        match = re.search(r"^  " + re.escape(key) + r":[ \t]*([^\r\n]*)$", raw, re.M)
        return match.group(1).strip() if match else None
    def real_asset(rel):
        path = root / rel
        try:
            if not path.is_file() or path.stat().st_size == 0:
                return False
            with path.open("rb") as stream:
                return not stream.read(128).startswith(b"version https://git-lfs.github.com/spec/v1")
        except OSError:
            return False

    version = read(VERSION)
    match = re.search(r"^m_EditorVersion:\s*(\S+)", version, re.M)
    facts["unityVersion"] = match.group(1) if match else None
    if not match:
        issue("BLOCK", "unity-version", "Missing editor version")
    raw = read(SETTINGS)
    for key in ("productName", "bundleVersion", "AndroidMinSdkVersion", "AndroidTargetSdkVersion",
                "AndroidTargetArchitectures", "androidUseSwappy"):
        facts[key] = scalar(raw, key)
        if facts[key] is None:
            issue("BLOCK", "unknown-setting", key)
    if facts["androidUseSwappy"] != "1":
        issue("BLOCK", "frame-pacing", "Optimized Frame Pacing not enabled in source settings")
    try:
        if not int(facts["AndroidTargetArchitectures"]) & 2:
            issue("BLOCK", "arm64", "ARM64 not selected")
    except (TypeError, ValueError):
        issue("BLOCK", "arm64", "Android architecture unavailable")
    def android_value(key):
        m = re.search(r"^  " + key + r":\s*\n((?:    [^\n]*\n?)*)", raw, re.M)
        sub = re.search(r"^    Android:\s*(\S+)", m.group(1), re.M) if m else None
        return sub.group(1) if sub else None
    facts["androidPackage"] = android_value("applicationIdentifier")
    facts["androidScriptingBackend"] = android_value("scriptingBackend")
    if facts["androidScriptingBackend"] != "1":
        issue("BLOCK", "il2cpp", "Android IL2CPP is not confirmed")
    package = facts["androidPackage"]
    if not package or "template" in package.lower() or "defaultcompany" in package.lower():
        issue("BLOCK" if release else "WARN", "package-identity", "Approve final Android package ID before distribution; renaming can change save location")
    if facts["AndroidTargetSdkVersion"] == "0":
        issue("BLOCK" if release else "WARN", "target-sdk-auto", "Automatic target SDK: inspect built manifest and current store policy")
    try:
        deps = json.loads(read(PACKAGES))["dependencies"]
        facts["urpVersion"] = deps.get("com.unity.render-pipelines.universal")
        for key in ("com.unity.render-pipelines.universal", "com.unity.inputsystem", "com.unity.ugui"):
            if not deps.get(key):
                issue("BLOCK", "dependency", key)
    except (ValueError, KeyError, TypeError, AttributeError):
        issue("BLOCK", "package-manifest", "Invalid package manifest")

    policy = read(POLICY)
    expected = []
    for zone in ("Origin", "Ocean", "Sky"):
        m = re.search(r'public const string ' + zone + r'Scene\s*=\s*"([^"]+)";', policy)
        if not m:
            issue("BLOCK", "scene-policy", zone + " scene path unavailable")
            continue
        rel = m.group(1)
        if not rel.startswith("Assets/") or ".." in Path(rel).parts or not rel.endswith(".unity"):
            issue("BLOCK", "scene-path", zone + " invalid policy path")
            continue
        expected.append(rel)
    settings = read(BUILD_SETTINGS)
    block = re.search(r"^  m_Scenes:\s*\n(.*?)(?=^  m_\w+:|\Z)", settings, re.M | re.S)
    entries = []
    if block:
        for enabled, rel, guid in re.findall(
            r"^  - enabled:[ \t]*([01])[ \t]*\n    path:[ \t]*([^\r\n]+)\n    guid:[ \t]*([a-fA-F0-9]+)[ \t]*$",
            block.group(1), re.M
        ):
            entries.append((enabled == "1", rel.strip(), guid.lower()))
    if not entries:
        issue("BLOCK", "build-scene-list", "No readable global build scene entries")
    active = [rel for enabled, rel, _ in entries if enabled]
    facts["expectedScenes"] = expected
    facts["enabledGlobalScenes"] = active
    if len(expected) == 3 and active[:3] != expected:
        issue("BLOCK", "scene-order", "Global build list must begin with Origin, Ocean, Sky from ConnectedWorldPolicy")
    for rel in expected:
        if active.count(rel) != 1:
            issue("BLOCK", "scene-registration", rel + " must be enabled exactly once")
        if not real_asset(rel):
            issue("BLOCK", "scene-file", rel + " missing, empty or LFS pointer")
            continue
        meta = read(rel + ".meta")
        m = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", meta, re.M)
        if not m or not any(enabled and p == rel and guid == m.group(1).lower() for enabled, p, guid in entries):
            issue("BLOCK", "scene-guid", rel + " metadata and global build list must agree")
    facts["bootstrapFbxPresent"] = [rel for rel in BOOTSTRAP_MODELS if real_asset(rel)]
    if expected and not real_asset(expected[0]) and not facts["bootstrapFbxPresent"]:
        issue("BLOCK", "bootstrap-model", "Origin generator requires one of the two refined Emberling FBX files")
    if len(active) > 3:
        issue("WARN", "extra-scenes", "Review extra enabled scenes and active Build Profile overrides")
    return {"status": "BLOCKED" if any(i["level"] == "BLOCK" for i in issues) else "SOURCE_READY",
            "mode": "distribution" if release else "local-alpha", "facts": facts, "issues": issues,
            "limitations": ["Global source settings only; active Unity Build Profile may override them.",
                            "No Unity import, compile, scene role/component validation, actual APK manifest, signing, asset QA, or device testing.",
                            "SOURCE_READY is not build success or release approval."]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--distribution", action="store_true")
    parser.add_argument("--report-only", action="store_true", help="Report expected generated-scene blockers without failing CI")
    args = parser.parse_args()
    result = preflight(args.root, args.distribution)
    print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0 if args.report_only or result["status"] == "SOURCE_READY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
