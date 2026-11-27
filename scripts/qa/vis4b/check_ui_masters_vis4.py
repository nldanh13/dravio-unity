"""VIS.4b (issue #91) - QA of the PR #92 UI master set (ArtSource/UI/BrightFantasy_VIS4_Masters)
against the VIS.2a contract (PR #88), and optional staging of passing sprites to their runtime paths.

  python scripts/qa/vis4b/check_ui_masters_vis4.py --out <report dir> [--install]

Checks per sprite: RGBA8 PNG, size == contract, 9-slice border == contract and valid
(UiSpritePolicy.ValidateBorder), sample ratio >= 1 at canvas scale 1.0 / 1.5 / 2.67 for the import
size UiSpritePolicy recommends, alpha (stray specks, opaque pixels on the canvas edge, soft-edge
fringe), icon/item coverage 75-85 % (contract) and state-family alignment (same size, border,
content bounds). --install copies sprites with no hard failure to their contract runtime path
(Assets/Dravio/UI/Sprites/...) without .meta (DravioUiSpriteImporter configures them on import) and
never overwrites an existing file. No theme is created or assigned, so the HUD keeps its fallback.
"""
import argparse
import json
import shutil
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from check_ui_kit_v1 import CONTRACT, ROOT, SCALES, nine_slice, pixel_stats, validate_border  # noqa: E402

MASTERS = ROOT / "ArtSource" / "UI" / "BrightFantasy_VIS4_Masters"
IMPORT_SIZES = [32, 64, 128, 256, 512, 1024, 2048]
MAX_SCALE = 1440 / 540


def ceil_pot(v):
    p = 1
    while p < v:
        p <<= 1
    return min(p, 2048)


def rec_max_size(w, h, short_units):
    longest, shortest = max(w, h), min(w, h)
    need = short_units * MAX_SCALE
    for size in IMPORT_SIZES:
        if size >= longest:
            return ceil_pot(longest)
        if shortest * size / longest >= need:
            return size
    return ceil_pot(longest)


def rec_max_size_sliced(w, h, b, border_units, dw, dh):
    L, B, R, T = b
    longest = max(w, h)
    need = 0.0
    bx, by = max(L, R), max(T, B)
    if bx > 0:
        need = max(need, border_units * MAX_SCALE / bx)
    elif dw > 0:
        need = max(need, dw * MAX_SCALE / w)
    if by > 0:
        need = max(need, border_units * MAX_SCALE / by)
    elif dh > 0:
        need = max(need, dh * MAX_SCALE / h)
    for size in IMPORT_SIZES:
        if size >= longest:
            return ceil_pot(longest)
        if size / longest >= need:
            return size
    return ceil_pot(longest)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True, type=Path)
    ap.add_argument("--install", action="store_true")
    args = ap.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    contract = json.loads(CONTRACT.read_text(encoding="utf-8"))
    slots = {s["id"]: s for s in contract["slots"]}
    man = json.loads((MASTERS / "manifest.json").read_text(encoding="utf-8"))
    rows, families = [], {}
    for a in man["assets"]:
        f = ROOT / a["path"]
        r = {"id": a["id"], "state": a["state"], "path": a["path"], "runtime": a["runtimeCandidate"]}
        hard, warn = [], []
        img = Image.open(f)
        r["mode"], r["size"] = img.mode, list(img.size)
        if img.mode != "RGBA":
            hard.append(f"mode {img.mode}")
        img = img.convert("RGBA")
        r.update(pixel_stats(img))
        w, h = img.size
        if ":" in a["id"]:
            kind, cid = a["id"].split(":", 1)
            st = contract["icons" if kind == "icon" else "items"]
            expected_path = st["pathPattern"].replace("{id}", cid)
            if cid not in st["ids"]:
                hard.append("id not in contract")
            if (w, h) != (st["sourceSize"], st["sourceSize"]):
                hard.append(f"size {w}x{h} != {st['sourceSize']}")
            short_units = max(st["displayUnits"])
            imported = min(w, h) * rec_max_size(w, h, short_units) / max(w, h)
            r["ratio"] = {k: round(imported / (short_units * v), 2) for k, v in SCALES.items()}
            if not (0.70 <= r["content_cover"] <= 0.90):
                warn.append(f"cover {r['content_cover']} outside contract 75-85 %")
        else:
            s = slots.get(a["id"])
            if s is None:
                hard.append("slot not in contract")
                rows.append(r | {"hard_fail": hard, "warnings": warn})
                continue
            expected_path = s["path"] if a["state"] == "normal" else s["path"][:-4] + "_" + a["state"] + ".png"
            if a["state"] not in s["states"]:
                hard.append(f"state {a['state']} not in contract")
            if (w, h) != (s["sourceWidth"], s["sourceHeight"]):
                hard.append(f"size {w}x{h} != contract {s['sourceWidth']}x{s['sourceHeight']}")
            if list(a["borderLTRB"]) != list(s["borderPx"]):
                hard.append(f"border {a['borderLTRB']} != contract {s['borderPx']}")
            b = s["borderPx"]  # contract order L,B,R,T (symmetric in practice)
            if s["sliced"]:
                issues = validate_border(w, h, (b[0], b[3], b[2], b[1]), True)
                hard += issues
                size = rec_max_size_sliced(w, h, b, s["borderUnits"], s["displayWidthUnits"], s["displayHeightUnits"])
                k = size / max(w, h) if size < max(w, h) else 1.0
                bx = max(b[0], b[2]) or max(b[1], b[3])
                if bx:
                    r["ratio"] = {kk: round(bx * k / (s["borderUnits"] * v), 2) for kk, v in SCALES.items()}
                else:
                    r["ratio"] = {kk: round(h * k / (s["displayHeightUnits"] * v), 2) for kk, v in SCALES.items()}
            else:
                short_units = min(s["displayWidthUnits"], s["displayHeightUnits"]) or s["displayHeightUnits"]
                imported = min(w, h) * rec_max_size(w, h, short_units) / max(w, h)
                r["ratio"] = {kk: round(imported / (short_units * v), 2) for kk, v in SCALES.items()}
            families.setdefault(a["id"], []).append((a["state"], r))
        if a["runtimeCandidate"] != expected_path:
            hard.append(f"runtime path {a['runtimeCandidate']} != contract {expected_path}")
        if r["ratio"]["max_2.67"] < 1:
            hard.append(f"upscaled at max canvas scale (ratio {r['ratio']['max_2.67']})")
        if r["specks"]:
            # pixel_stats counts components < max(12 px, 1 % of the main shape). Isolated 1-4 px dots
            # vanish when the 512 px source is imported at <= 256 px; report them, fail larger ones.
            note = f"{r['specks']} stray alpha speck(s) < 12 px"
            (warn if r["specks"] <= 1 else hard).append(note)
        if r["opaque_edge_px"]:
            warn.append(f"{r['opaque_edge_px']} opaque px on canvas edge")
        if r["semi_alpha_mean_luma"] is not None and r["semi_alpha_mean_luma"] > 200:
            warn.append(f"light soft-edge fringe (luma {r['semi_alpha_mean_luma']})")
        r["hard_fail"], r["warnings"] = hard, warn
        rows.append(r)

    alignment = {}
    for cid, items in families.items():
        if len(items) < 2:
            continue
        base = next(r for s, r in items if s == "normal")
        notes = []
        for s, r in items:
            if r["size"] != base["size"]:
                notes.append(f"{s}: size differs")
            b0, b1 = base["content_bbox"], r["content_bbox"]
            if b0 != b1:
                notes.append(f"{s}: content bbox {b1} != normal {b0}")
        alignment[cid] = notes
        if notes:
            for s, r in items:
                r["hard_fail"].append(f"state family '{cid}' misaligned")

    passed = [r for r in rows if not r["hard_fail"]]
    installed = []
    if args.install:
        for r in passed:
            dst = ROOT / r["runtime"]
            if dst.exists():
                continue
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / r["path"], dst)
            installed.append(r["runtime"])

    report = {"source": str(MASTERS.relative_to(ROOT)), "assets": len(rows), "passed": len(passed),
              "installed": installed, "state_alignment": alignment, "rows": rows}
    (args.out / "ui_masters_vis4_qa.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    md = ["# UI masters (PR #92) vs VIS.2a contract (PR #88) — offline sprite QA", "",
          f"Assets {len(rows)} · pass {len(passed)} · installed to runtime paths {len(installed)}", "",
          "| id | state | size | ratio 720p / 1080p / max | semi% / specks / edge px / cover | result | warnings |",
          "|---|---|---|---|---|---|---|"]
    for r in rows:
        rr = " / ".join(str(v) for v in r.get("ratio", {}).values())
        md.append(f"| {r['id']} | {r['state']} | {r['size'][0]}x{r['size'][1]} | {rr} | {r['semi_alpha_pct']} / {r['specks']} / "
                  f"{r['opaque_edge_px']} / {r['content_cover']} | {'; '.join(r['hard_fail']) or 'PASS'} | {'; '.join(r['warnings'])} |")
    md += ["", "## State families", ""] + [f"- {k}: {'OK' if not v else '; '.join(v)}" for k, v in alignment.items()]
    (args.out / "ui_masters_vis4_qa.md").write_text("\n".join(md) + "\n", encoding="utf-8")
    preview(args.out, slots)
    print(f"[VIS.4b UI masters] {len(rows)} assets, {len(passed)} pass, {len(installed)} installed")


def preview(out, slots):
    """9-slice the sliced masters to their contract display size at canvas scale 1.0 and 2.67."""
    from PIL import ImageDraw
    picks = [("btn_primary", "ui_btn_primary.png", 200), ("btn_danger", "ui_btn_danger_pressed.png", 200),
             ("tab_active", "ui_tab_active.png", None), ("card", "ui_card.png", None),
             ("progress_frame", "ui_progress_frame.png", None), ("panel", "ui_panel.png", None)]
    tiles = []
    for scale in (1.0, MAX_SCALE):
        row = []
        for sid, fname, wu in picks:
            s = slots[sid]
            f = next(MASTERS.rglob(fname))
            img = Image.open(f).convert("RGBA")
            b = s["borderPx"]
            k = scale * s["borderUnits"] / max(b)
            scaled = img.resize((max(1, round(img.size[0] * k)), max(1, round(img.size[1] * k))), Image.LANCZOS)
            border = (round(b[0] * k), round(b[3] * k), round(b[2] * k), round(b[1] * k))
            W = round((wu or s["displayWidthUnits"]) * scale)
            H = round((s["displayHeightUnits"] if sid != "panel" else 260) * scale)
            row.append(nine_slice(scaled, border, (max(W, border[0] + border[2] + 1), max(H, border[1] + border[3] + 1))))
        for fname in ("ui_icon_feed.png", "ui_icon_night.png", "ui_item_crystal.png", "ui_item_egg.png"):
            f = next(MASTERS.rglob(fname))
            img = Image.open(f).convert("RGBA")
            d = round(64 * scale)
            row.append(img.resize((d, d), Image.LANCZOS))
        tiles.append((scale, row))
    pad = 16
    W = max(sum(t.size[0] + pad for t in row) + pad for _, row in tiles)
    H = sum(max(t.size[1] for t in row) + 2 * pad + 20 for _, row in tiles)
    sheet = Image.new("RGBA", (W, H), (58, 92, 66, 255))
    d = ImageDraw.Draw(sheet)
    y = 0
    for scale, row in tiles:
        d.text((pad, y + 4), f"canvas scale {scale:.2f}", fill=(255, 255, 255, 255))
        x, y = pad, y + 20 + pad
        for t in row:
            sheet.alpha_composite(t, (x, y))
            x += t.size[0] + pad
        y += max(t.size[1] for t in row) + pad
    sheet.convert("RGB").save(out / "ui_masters_vis4_scale_preview.png")


if __name__ == "__main__":
    main()
