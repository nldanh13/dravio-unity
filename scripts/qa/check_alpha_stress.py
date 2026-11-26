"""Screen a DRAVIO DragonStressHarness JSON report. Not device certification."""
import argparse
import json
import math
from pathlib import Path

# Provisional project targets, not Unity/Android requirements.
EXPECTED_COUNTS = (5, 15, 30)
TARGETS = {
    30: {"fpsAvg": 29, "fps1Low": 24},
    60: {"fpsAvg": 58, "fps1Low": 48},
}
# Soft investigation triggers for entire stress scene, not per-character limits.
SOFT_BUDGETS = {
    5: {"drawCallsAvg": 150, "setPassAvg": 80, "trianglesAvg": 200000},
    15: {"drawCallsAvg": 250, "setPassAvg": 120, "trianglesAvg": 350000},
    30: {"drawCallsAvg": 350, "setPassAvg": 160, "trianglesAvg": 500000},
}


def number(row, key):
    value = row.get(key)
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return None
    return value if math.isfinite(value) and value >= 0 else None


def evaluate(report, target=30):
    """Only short stress-screen results; never returns UNITY_VERIFIED or APPROVED."""
    if not isinstance(report, dict) or target not in TARGETS:
        raise ValueError("expected a report object and target 30 or 60")
    problems, warnings, rows = [], [], []
    if report.get("platform") != "Android":
        problems.append("Android device report required")
    if report.get("development") is not True:
        problems.append("Development build required for render counters")
    for key in ("device", "gpu", "unityVersion", "timestamp"):
        if not isinstance(report.get(key), str) or not report[key].strip():
            problems.append("Missing report metadata: " + key)
    source = report.get("results")
    if not isinstance(source, list):
        source = []
        problems.append("Missing results array")
    counts = {}
    for row in source:
        if not isinstance(row, dict) or type(row.get("dragons")) is not int:
            problems.append("Invalid stress row")
            continue
        n = row["dragons"]
        if n in counts:
            problems.append(f"Duplicate result: {n} dragons")
        counts[n] = row
    failures = []
    for n in EXPECTED_COUNTS:
        row = counts.get(n)
        if row is None:
            problems.append(f"Missing result: {n} dragons")
            continue
        metrics = {}
        for k in ("visibleAvg", "fpsAvg", "fps1Low", "frameMsAvg", "frameMsMax",
                  "drawCallsAvg", "setPassAvg", "trianglesAvg", "totalUsedMemoryMB"):
            value = number(row, k)
            metrics[k] = value
            if value is None or value <= 0:
                problems.append(f"{n} dragons: unavailable/zero {k}")
        if metrics["visibleAvg"] is not None and metrics["visibleAvg"] > n:
            problems.append(f"{n} dragons: visibleAvg exceeds requested count")
        elif metrics["visibleAvg"] is not None and metrics["visibleAvg"] < n * 0.5:
            problems.append(f"{n} dragons: fewer than half visible on average; workload not comparable")
        if metrics["fpsAvg"] and metrics["frameMsAvg"] and abs(metrics["fpsAvg"] * metrics["frameMsAvg"] - 1000) > 60:
            problems.append(f"{n} dragons: inconsistent FPS/frame-time pair")
        if metrics["fps1Low"] is not None and metrics["fpsAvg"] is not None and metrics["fps1Low"] > metrics["fpsAvg"] * 1.02:
            problems.append(f"{n} dragons: inconsistent 1% low")
        for key, minimum in TARGETS[target].items():
            value = metrics[key]
            if value is not None and 0 < value < minimum:
                failures.append(f"{n} dragons: {key} {value} < {minimum}")
        for key, limit in SOFT_BUDGETS[n].items():
            value = metrics[key]
            if value is not None and value > limit:
                warnings.append(f"{n} dragons: {key} {value} > review budget {limit}")
        rows.append({"dragons": n, **metrics})
    status = "INCOMPLETE" if problems else "SCREEN_FAIL" if failures else "SCREEN_PASS_WITH_WARNINGS" if warnings else "SCREEN_PASS"
    return {"status": status, "targetFps": target, "problems": problems, "failures": failures,
            "warnings": warnings, "results": rows,
            "limitations": ["Short stress scene only; no verification of actual spawn count, gameplay scenes, thermal endurance, memory peak, Android vitals or device coverage.",
                            "totalUsedMemoryMB is Unity used memory at sampling end, not Android PSS.",
                            "Report does not record requested FPS, capture duration, build commit or quality tier; record those separately.",
                            "Never substitutes for Unity/device QA or owner approval."]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    parser.add_argument("--target", type=int, choices=(30, 60), default=30)
    args = parser.parse_args()
    try:
        result = evaluate(json.loads(args.report.read_text(encoding="utf-8-sig")), args.target)
    except (OSError, ValueError) as error:
        print(json.dumps({"status": "INCOMPLETE", "error": str(error)}, indent=2))
        return 2
    print(json.dumps(result, indent=2))
    return 2 if result["status"] == "INCOMPLETE" else 1 if result["status"] == "SCREEN_FAIL" else 0


if __name__ == "__main__":
    raise SystemExit(main())
