"""VIS.4b (issue #91) - offline QA of the Cute Fantasy UI Kit v1 candidates (PR #87) against the
VIS.2a sprite contract (PR #88: Assets/Dravio/UI/Data/ui-sprite-slots.json + UiSpritePolicy).

Pure Python (Pillow). Reads ArtSource only, writes a Markdown + JSON report to --out. Mirrors the
Unity-side checks of DravioUiValidator / UiSpritePolicy where they can be computed from the PNG and
adds pixel checks Unity does not do (alpha halo, stray specks, edge cropping, state alignment).

  python scripts/qa/vis4b/check_ui_kit_v1.py --out <dir>
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
KIT = ROOT / "ArtSource" / "UI" / "CuteFantasy_v1"
CONTRACT = ROOT / "Assets" / "Dravio" / "UI" / "Data" / "ui-sprite-slots.json"
SCALES = {"720p_ref_1.0": 1.0, "1080p_1.5": 1.5, "max_2.67": 1440 / 540}

# Kit id -> (contract kind, contract id, state). Only semantic matches; nothing is guessed.
STATE_SUFFIX = ("normal", "highlighted", "pressed", "disabled")


def map_asset(aid):
    for btn in ("primary", "secondary", "reward", "danger"):
        p = f"ui_btn_{btn}_"
        if aid.startswith(p):
            return ("slot", f"btn_{btn}", aid[len(p):])
    direct = {
        "ui_round_close": ("slot", "btn_close", "normal"),
        "ui_tab_normal": ("slot", "tab", "normal"),
        "ui_tab_selected": ("slot", "tab_active", "normal"),
        "ui_panel_plain": ("slot", "panel", "normal"),
        "ui_frame_card": ("slot", "card", "normal"),
        "ui_frame_portrait": ("slot", "portrait", "normal"),
        "ui_slot_common": ("slot", "slot_common", "normal"),
        "ui_slot_rare": ("slot", "slot_rare", "normal"),
        "ui_slot_epic": ("slot", "slot_epic", "normal"),
        "ui_slot_legendary": ("slot", "slot_legendary", "normal"),
        "icon_back": ("icons", "back", "normal"),
        "item_starter": ("items", "pack_starter", "normal"),
        "item_elemental": ("items", "pack_elemental", "normal"),
    }
    if aid in direct:
        return direct[aid]
    if aid.startswith("icon_"):
        return ("icons", aid[5:], "normal")
    if aid.startswith("item_"):
        return ("items", aid[5:], "normal")
    return (None, None, None)


def pixel_stats(img):
    a = img.getchannel("A")
    w, h = img.size
    px = a.load()
    rgb = img.convert("RGB").load()
    total = w * h
    transparent = semi = 0
    semi_lum = []
    for y in range(h):
        for x in range(w):
            v = px[x, y]
            if v == 0:
                transparent += 1
            elif v < 250:
                semi += 1
                r, g, b = rgb[x, y]
                semi_lum.append(0.299 * r + 0.587 * g + 0.114 * b)
    edge_opaque = sum(1 for x in range(w) for y in (0, h - 1) if px[x, y] >= 128) + \
        sum(1 for y in range(1, h - 1) for x in (0, w - 1) if px[x, y] >= 128)
    bbox = a.point(lambda v: 255 if v >= 16 else 0).getbbox()
    # Connected components of alpha >= 16 (4-neighbour); tiny ones are leftover specks.
    seen = bytearray(total)
    comps = []
    for y in range(h):
        for x in range(w):
            i = y * w + x
            if seen[i] or px[x, y] < 16:
                continue
            stack, n = [(x, y)], 0
            seen[i] = 1
            while stack:
                cx, cy = stack.pop()
                n += 1
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h:
                        j = ny * w + nx
                        if not seen[j] and px[nx, ny] >= 16:
                            seen[j] = 1
                            stack.append((nx, ny))
            comps.append(n)
    comps.sort(reverse=True)
    main = comps[0] if comps else 0
    specks = [c for c in comps[1:] if c < max(12, main * 0.01)]
    cover = 0.0
    if bbox:
        cover = max((bbox[2] - bbox[0]) / w, (bbox[3] - bbox[1]) / h)
    return {
        "transparent_pct": round(100 * transparent / total, 1),
        "semi_alpha_pct": round(100 * semi / total, 1),
        "semi_alpha_mean_luma": round(sum(semi_lum) / len(semi_lum), 1) if semi_lum else None,
        "opaque_edge_px": edge_opaque,
        "content_bbox": list(bbox) if bbox else None,
        "content_cover": round(cover, 3),
        "components": len(comps),
        "specks": len(specks),
    }


def validate_border(w, h, b, sliced):
    # Port of UiSpritePolicy.ValidateBorder; b = (L, T, R, B) as in the kit manifest.
    issues = []
    if not sliced:
        return issues
    L, T, R, B = b
    if not any(b):
        issues.append("sliced slot has no border")
    if L + R >= w:
        issues.append("left+right border >= width")
    if T + B >= h:
        issues.append("top+bottom border >= height")
    return issues


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True, type=Path)
    out = ap.parse_args().out
    out.mkdir(parents=True, exist_ok=True)
    kit = json.loads((KIT / "manifest.json").read_text(encoding="utf-8"))
    contract = json.loads(CONTRACT.read_text(encoding="utf-8"))
    slots = {s["id"]: s for s in contract["slots"]}
    rows, by_slot = [], {}
    for a in kit["assets"]:
        f = KIT / a["path"]
        r = {"id": a["id"], "path": a["path"], "type": a["type"], "exists": f.exists()}
        if not f.exists():
            r["hard_fail"] = ["file missing"]
            rows.append(r)
            continue
        data = f.read_bytes()
        r["sha256_ok"] = hashlib.sha256(data).hexdigest() == a.get("sha256")
        img = Image.open(f)
        r["mode"] = img.mode
        r["size"] = list(img.size)
        r["manifest_size_ok"] = img.size == (a["width"], a["height"])
        img = img.convert("RGBA")
        r.update(pixel_stats(img))
        kind, cid, state = map_asset(a["id"])
        r["contract"] = f"{kind}:{cid}:{state}" if kind else None
        hard, soft = [], []
        if r["mode"] != "RGBA":
            hard.append(f"PNG mode {r['mode']} (RGBA required)")
        if not r["sha256_ok"]:
            hard.append("sha256 differs from manifest")
        if r["opaque_edge_px"] > 0:
            soft.append(f"{r['opaque_edge_px']} opaque px on canvas edge (cropped / no padding)")
        if r["specks"]:
            hard.append(f"{r['specks']} stray alpha speck(s) from source board")
        if r["semi_alpha_pct"] > 12:
            soft.append(f"wide soft alpha edge {r['semi_alpha_pct']}% (halo risk)")
        if r["semi_alpha_mean_luma"] is not None and r["semi_alpha_mean_luma"] > 200:
            soft.append(f"light fringe in soft edge (luma {r['semi_alpha_mean_luma']})")
        display_short = None
        border_units = 0
        if kind == "slot":
            s = slots[cid]
            sliced = s["sliced"]
            if state not in s["states"]:
                soft.append(f"state '{state}' not in contract states {s['states']}")
            if (s["sourceWidth"], s["sourceHeight"]) != tuple(img.size):
                hard.append(f"source {img.size[0]}x{img.size[1]} != contract {s['sourceWidth']}x{s['sourceHeight']}")
            if a["type"] in ("fixed_aspect_reference",) and sliced:
                hard.append("fixed-aspect reference cannot serve a 9-slice slot")
            if sliced and a["type"] == "9slice":
                r["border_issues"] = validate_border(img.size[0], img.size[1], a["borderLTRB_px"], True)
                hard += r["border_issues"]
                border_units = s["borderUnits"]
                bpx = min(x for x in a["borderLTRB_px"] if x > 0)
                r["border_ratio"] = {k: round(bpx / (border_units * v), 2) for k, v in SCALES.items()}
            elif sliced and a["type"] != "9slice":
                hard.append(f"contract slot is 9-slice; kit asset is '{a['type']}'")
            if not sliced:
                display_short = min(s["displayWidthUnits"], s["displayHeightUnits"]) or s["displayHeightUnits"]
            by_slot.setdefault(cid, []).append((state, a, r))
        elif kind in ("icons", "items"):
            st = contract[kind]
            if cid not in st["ids"]:
                hard.append(f"no contract {kind} id '{cid}' (semantics)")
            display_short = max(st["displayUnits"])
            lo, hi = 0.75, 0.85
            if not (lo - 0.05 <= r["content_cover"] <= hi + 0.1):
                soft.append(f"content cover {r['content_cover']} outside contract 75-85%")
            if img.size[0] != img.size[1]:
                soft.append("non-square icon canvas (contract 512x512)")
            if tuple(img.size) != (st["sourceSize"], st["sourceSize"]):
                hard.append(f"source {img.size[0]}x{img.size[1]} != contract {st['sourceSize']}px")
        else:
            hard.append("no slot in the VIS.2a contract (extra art; not integrable until a slot is agreed)")
        if display_short:
            short = min(img.size)
            r["sample_ratio"] = {k: round(short / (display_short * v), 2) for k, v in SCALES.items()}
        ratios = r.get("border_ratio") or r.get("sample_ratio")
        if ratios:
            if ratios["max_2.67"] < 1:
                hard.append(f"upscaled at contract max canvas scale 2.67 (ratio {ratios['max_2.67']})")
            if ratios["720p_ref_1.0"] < 1:
                hard.append(f"upscaled even at 720p reference (ratio {ratios['720p_ref_1.0']})")
        r["hard_fail"], r["warnings"] = hard, soft
        rows.append(r)

    # State alignment: every state of one slot must share canvas size and 9-slice border, and the
    # visible content must not jump (bbox centre within 1 px; pressed may shift down <= 3 px).
    alignment = {}
    for cid, items in by_slot.items():
        if len(items) < 2:
            continue
        base = next((x for x in items if x[0] == "normal"), items[0])
        notes = []
        for state, a, r in items:
            if r["size"] != base[2]["size"]:
                notes.append(f"{state}: canvas {r['size']} != normal {base[2]['size']}")
            if a.get("borderLTRB_px") != base[1].get("borderLTRB_px"):
                notes.append(f"{state}: border {a.get('borderLTRB_px')} != normal {base[1].get('borderLTRB_px')}")
            b0, b1 = base[2]["content_bbox"], r["content_bbox"]
            if b0 and b1:
                dx = (b1[0] + b1[2] - b0[0] - b0[2]) / 2
                dy = (b1[1] + b1[3] - b0[1] - b0[3]) / 2
                dw = (b1[2] - b1[0]) - (b0[2] - b0[0])
                if abs(dx) > 1 or abs(dy) > 3 or abs(dw) > 2:
                    notes.append(f"{state}: content offset dx={dx:+.1f} dy={dy:+.1f} width {dw:+d}px")
        alignment[cid] = notes
        for state, a, r in items:
            if notes:
                r["warnings"].append(f"state set '{cid}' misaligned ({len(notes)} issue(s))")

    covered = {r["contract"] for r in rows if r.get("contract")}
    missing = {
        "slots": [s for s in slots if not any(c and c.startswith(f"slot:{s}:") for c in covered)],
        "icons": [i for i in contract["icons"]["ids"] if f"icons:{i}:normal" not in covered],
        "items": [i for i in contract["items"]["ids"] if f"items:{i}:normal" not in covered],
    }
    passed = [r["id"] for r in rows if not r["hard_fail"]]
    report = {"kit_version": kit.get("version"), "assets": len(rows), "passed": passed,
              "missing_contract_entries": missing, "state_alignment": alignment, "rows": rows}
    (out / "ui_kit_v1_qa.json").write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding="utf-8")

    md = ["# UI Kit v1 (PR #87) vs VIS.2a contract (PR #88) — offline sprite QA", "",
          f"Assets: {len(rows)} · pass (no hard failure): {len(passed)} · generated by scripts/qa/vis4b/check_ui_kit_v1.py", "",
          "| id | size | contract | ratio 720p / 1080p / max | alpha semi% / specks / edge px | hard failures | warnings |",
          "|---|---|---|---|---|---|---|"]
    for r in rows:
        ratios = r.get("border_ratio") or r.get("sample_ratio") or {}
        rr = " / ".join(str(v) for v in ratios.values()) or "—"
        md.append(f"| {r['id']} | {r.get('size', ['?'])[0]}x{r.get('size', ['?', '?'])[1]} | {r.get('contract') or '—'} | {rr} | "
                  f"{r.get('semi_alpha_pct')} / {r.get('specks')} / {r.get('opaque_edge_px')} | "
                  f"{'; '.join(r['hard_fail']) or 'PASS'} | {'; '.join(r.get('warnings', [])) or ''} |")
    md += ["", "## State alignment", ""]
    for cid, notes in alignment.items():
        md.append(f"- **{cid}**: " + ("OK" if not notes else "; ".join(notes)))
    md += ["", "## Contract entries with no kit art (fallback colour stays)", ""]
    for k, v in missing.items():
        md.append(f"- {k} ({len(v)}): {', '.join(v)}")
    (out / "ui_kit_v1_qa.md").write_text("\n".join(md) + "\n", encoding="utf-8")
    preview_sheet(out)
    print(f"[VIS.4b UI] {len(rows)} assets, {len(passed)} pass; report -> {out}")



def nine_slice(img, border_ltrb, size):
    """Unity-style 9-slice (corners fixed, edges/centre stretched) for a preview sheet."""
    L, T, R, B = border_ltrb
    w, h = img.size
    W, H = size
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    xs, Xs = [0, L, w - R, w], [0, L, W - R, W]
    ys, Ys = [0, T, h - B, h], [0, T, H - B, H]
    for i in range(3):
        for j in range(3):
            src = img.crop((xs[i], ys[j], xs[i + 1], ys[j + 1]))
            dw, dh = Xs[i + 1] - Xs[i], Ys[j + 1] - Ys[j]
            if dw > 0 and dh > 0 and src.size[0] > 0 and src.size[1] > 0:
                out.paste(src.resize((dw, dh), Image.BILINEAR), (Xs[i], Ys[j]))
    return out


def preview_sheet(out):
    """Buttons/panel/tabs 9-sliced to their contract size and icons at 64 units, each at canvas scale
    1.0 (720p reference) and 2.67 (contract maximum), with border units mapped like uGUI
    (pixelsPerUnitMultiplier = borderPx / borderUnits)."""
    kit = json.loads((KIT / "manifest.json").read_text(encoding="utf-8"))
    contract = json.loads(CONTRACT.read_text(encoding="utf-8"))
    slots = {s["id"]: s for s in contract["slots"]}
    picks = [("ui_btn_primary_normal", "btn_primary", 200), ("ui_btn_reward_pressed", "btn_reward", 200),
             ("ui_tab_selected", "tab_active", None), ("ui_panel_plain", "panel", None)]
    tiles = []
    for scale in (1.0, 1440 / 540):
        row = []
        for aid, sid, width_units in picks:
            a = next(x for x in kit["assets"] if x["id"] == aid)
            img = Image.open(KIT / a["path"]).convert("RGBA")
            s = slots[sid]
            bu = s["borderUnits"]
            k = scale * bu / max(1, min(v for v in a["borderLTRB_px"] if v > 0))
            border = [round(v * k) for v in a["borderLTRB_px"]]
            wu = width_units or s["displayWidthUnits"]
            hu = s["displayHeightUnits"] if sid != "panel" else 220
            size = (round(wu * scale), round(hu * scale))
            scaled = img.resize((max(1, round(img.size[0] * k)), max(1, round(img.size[1] * k))), Image.BILINEAR)
            row.append(nine_slice(scaled, border, size))
        for aid in ("icon_feed", "item_crystal", "icon_care"):
            a = next(x for x in kit["assets"] if x["id"] == aid)
            img = Image.open(KIT / a["path"]).convert("RGBA")
            d = round(64 * scale)
            f = d / max(img.size)
            row.append(img.resize((round(img.size[0] * f), round(img.size[1] * f)), Image.BILINEAR))
        tiles.append((scale, row))
    pad = 16
    W = max(sum(t.size[0] + pad for t in row) + pad for _, row in tiles)
    H = sum(max(t.size[1] for t in row) + 2 * pad + 20 for _, row in tiles)
    sheet = Image.new("RGBA", (W, H), (58, 92, 66, 255))
    y = 0
    from PIL import ImageDraw
    d = ImageDraw.Draw(sheet)
    for scale, row in tiles:
        d.text((pad, y + 4), f"canvas scale {scale:.2f} ({'720p reference' if scale == 1 else 'contract max: 960x540 compact on 1440p'})", fill=(255, 255, 255, 255))
        x, y = pad, y + 20 + pad
        for t in row:
            sheet.alpha_composite(t, (x, y))
            x += t.size[0] + pad
        y += max(t.size[1] for t in row) + pad
    sheet.convert("RGB").save(out / "ui_kit_v1_scale_preview.png")


if __name__ == "__main__":
    main()
