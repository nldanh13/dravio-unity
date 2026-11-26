"""U1-CHAR.5: per-model QA reports, prefab catalog, animation report and a static performance budget from a
batch work folder (batch_import.py output). Python only (no Blender), read-only on the work folder.

  python scripts/art/character/build_character_reports.py <work_dir> <out_dir>
(before/after tables of two runs: compare_animqa.py)

Writes (Vietnamese markdown, plus JSON for tools):
  <out>/qa/<Name>.md        one page per dragon: source, evidence (SAMPLE or real), rig, skin, LODs, PBR, every clip
  <out>/prefab-catalog.md   + .json: species x stage -> Unity prefab/controller paths, clips, QA, LODs, materials
  <out>/animation-report.md clips per species against the biology clip set (dragon-archetypes.json), QA per clip
  <out>/performance-budget.md static per-scene triangle / bone / Animator estimate for 5 / 15 / 30 dragons

Evidence rule: a source whose file name starts with SAMPLE_ is a generated stand-in and is labelled so in every
table; nothing here claims a real asset was accepted (that needs Unity and the art director).
"""
import argparse
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
ARCHETYPES = REPO / 'Assets/Dravio/Character/Data/dragon-archetypes.json'
BUDGETS = REPO / 'Assets/Dravio/Character/Data/character-budgets.json'
PREFAB_FOLDER = 'Assets/Dravio/Character/Prefabs'
OVERRIDE_FOLDER = 'Assets/Dravio/Character/Animation/Overrides'
BASE_FOLDER = 'Assets/Dravio/Character/Animation'
ALL_STAGES = ('Baby', 'Juvenile', 'Adult')
LOCOMOTION_STATES = ('Idle', 'Walk', 'Run', 'Swim', 'Fly', 'Burrow', 'Sleep')
ACTION_STATES = ('Eat', 'Happy')


def _load(p):
    try:
        return json.loads(Path(p).read_text(encoding='utf-8'))
    except (OSError, ValueError):
        return None


def evidence_of(source):
    name = Path(str(source or '')).name
    return 'SAMPLE' if name.startswith('SAMPLE_') else 'thực tế (chưa nghiệm thu)'


def collect(work):
    """One record per dragon asset folder (DRG_*) of a batch work dir."""
    out = []
    for d in sorted((Path(work) / 'assets').glob('DRG_*')):
        n = d.name
        rig = _load(d / f'{n}_rig.json')
        lods = _load(d / f'{n}_lods.json')
        qa = _load(d / f'{n}_animqa.json')
        val = _load(d / f'{n}.validation.json')
        prep = _load(d / f'{n}_prepare.json')
        src = (prep or {}).get('source') or (lods or {}).get('source')
        parts = n.split('_')
        rec = {
            'name': n, 'species': rig['species'] if rig else parts[1], 'stage': rig['stage'] if rig else (parts[2] if len(parts) > 2 else 'Unstaged'),
            'source': Path(str(src)).name if src else None, 'evidence': evidence_of(src),
            'sourceUnchanged': all(x.get('source_unchanged', True) for x in (rig, lods, prep) if x),
            'rigged': rig is not None, 'rig': rig, 'lods': lods, 'qa': qa, 'validation': val, 'prepare': prep,
            'fbx': f'{n}_Rigged.fbx' if rig else f'{n}_LODs.fbx',
        }
        out.append(rec)
    return out


def unity_paths(rec):
    stem = Path(rec['fbx']).stem
    p = {'prefab': f'{PREFAB_FOLDER}/{stem}_LODGroup.prefab', 'material': f'Assets/Dravio/Character/Materials/{rec["name"]}.mat'}
    if rec['rigged']:
        arch = rec['rig']['archetype']
        p['baseController'] = f'{BASE_FOLDER}/DragonBase_{arch}.controller'
        p['overrideController'] = f'{OVERRIDE_FOLDER}/{rec["name"]}.overrideController'
    return p


def clip_rows(rec):
    rig, qa = rec['rig'] or {}, rec['qa'] or {}
    rows = []
    for c, r in (rig.get('clips') or {}).items():
        q = (qa.get('clips') or {}).get(c, {})
        de = q.get('deformation') or {}
        warns = [w for w in qa.get('warnings', []) if w.get('clip') == c]
        rows.append({
            'clip': c, 'frames': r['end'] - r['start'], 'loop': r.get('loop', False), 'groundSpeed': r.get('groundSpeed', 0.0),
            'stretch': de.get('stretchP999'), 'compress': de.get('compressP001'),
            'slide': q.get('footSlideRel'), 'penetration': q.get('groundPenetrationRel'), 'contactGap': q.get('contactGapRel'),
            'loopDeg': (q.get('loop') or {}).get('poseErrorDeg'), 'headBehind': (q.get('facing') or {}).get('headBehindChestFrames'),
            'stance': q.get('stanceTravel_m'), 'lift': ((rig.get('groundClearanceLift_m') or {}).get(c) or {}).get('lift_m', 0.0),
            'worst': ', '.join(f"{b['bone']} {round(100 * b['share'])}%" for b in (de.get('worstBones') or [])[:2]),
            'status': 'WARNING' if warns else 'PASS', 'warnings': [f"{w['check']}: {w['message']} ({w['value']} / {w['limit']})" for w in warns],
        })
    return rows


def fmt(v, n=3):
    if v is None:
        return '—'
    if isinstance(v, float):
        return f'{v:.{n}f}'
    return str(v)


def model_page(rec, limits):
    r, q, l = rec['rig'], rec['qa'] or {}, rec['lods'] or {}
    L = [f"# {rec['name']}", '',
         f"- Nguồn: `{rec['source']}` — bằng chứng: **{rec['evidence']}**. GLB nguồn không đổi: {'có' if rec['sourceUnchanged'] else '**KHÔNG**'}",
         f"- Loài / giai đoạn: {rec['species']} / {rec['stage']}",
         f"- Kiểm định pipeline: {(rec['validation'] or {}).get('status', '—')}; Animation QA: **{q.get('status', 'không có (chưa rig)')}**", '']
    if r:
        sk = r.get('skin') or {}
        L += ['## Rig', '',
              f"- Archetype `{r['archetype']}` ({r.get('archetypeSource')}); limbCount {r.get('limbCount')} ({r.get('limbCountSource')}); rig v{r.get('rigVersion')}",
              f"- Chiều cao {r.get('height_m')} m ({r.get('heightSource')}); pivot dời theo mặt đối xứng {((r.get('size') or {}).get('midlineShift_m', 0.0))} m",
              f"- {sk.get('bones')} bone ({sk.get('deform_bones')} deform), tối đa {sk.get('max_influences')} ảnh hưởng/đỉnh, {sk.get('unweighted_vertices')} đỉnh không trọng số",
              f"- IK: {len(r.get('ikChains') or [])} chuỗi chân; anchor: {', '.join(r.get('anchors') or [])}", '']
        notes = r.get('notes') or []
        if notes:
            L += ['Ghi chú rig:', ''] + [f'- {n}' for n in notes] + ['']
        L += ['## Clip', '',
              '| Clip | Frame | Loop | Tốc độ (m/s) | Giãn p99.9 | Nén p0.1 | Trượt chân | Xuyên đất | Hở tiếp xúc | Khép vòng (°) | Nâng (m) | Vùng tệ nhất | Kết quả |',
              '|---|---|---|---|---|---|---|---|---|---|---|---|---|']
        for c in clip_rows(rec):
            L.append(f"| {c['clip']} | {c['frames']} | {'có' if c['loop'] else '—'} | {fmt(c['groundSpeed'])} | {fmt(c['stretch'])} | {fmt(c['compress'])} | "
                     f"{fmt(c['slide'], 4)} | {fmt(c['penetration'], 4)} | {fmt(c['contactGap'], 4)} | {fmt(c['loopDeg'])} | {fmt(c['lift'], 4)} | {c['worst']} | {c['status']} |")
        L += ['', f"Ngưỡng (`character-budgets.json` ▸ `animationQa`, không nới): giãn ≤ {limits.get('maxStretchP999')}, nén ≥ {limits.get('minCompressP001')}, "
              f"trượt ≤ {limits.get('maxFootSlideRel')}, xuyên ≤ {limits.get('maxGroundPenetrationRel')}, hở ≤ {limits.get('maxContactGapRel')}, khép vòng ≤ {limits.get('maxLoopPoseErrorDeg')}° (tương đối theo chiều cao).", '']
        warns = q.get('warnings') or []
        L += ['## Cảnh báo QA', ''] + ([f"- {w.get('clip') or 'model'}: {w['check']} — {w['message']} ({w['value']} / {w['limit']})" for w in warns] or ['- không có']) + ['']
    else:
        L += ['## Rig', '', '- Không rig (thiếu dữ liệu sinh học, xem batch-report). Chỉ có LOD tĩnh; trong game hiện tĩnh, không giả animation.', '']
    L += ['## LOD', '', '| LOD | Tam giác | Đỉnh | Trong ngân sách |', '|---|---|---|---|']
    for x in l.get('lods') or []:
        L.append(f"| {x['level']} | {x['triangles']} | {x['vertices']} | {'có' if x['in_budget'] else '**không**'} |")
    for x in (rec['validation'] or {}).get('items') or []:
        if x.get('code') in ('LOD_SHAPE', 'LOD_SKINNING') or x.get('level') != 'pass':
            L.append(f"- {x['level']}: {x['message']}")
    p = rec['prepare'] or {}
    ts = p.get('texture_set') or {}
    L += ['', '## PBR', '', f"- Bộ texture URP: {', '.join(f'{k}=`{v}`' for k, v in ts.items()) or '—'}",
          f"- Base Color: {(p.get('base_color_source') or {}).get('kind', '—') if isinstance(p.get('base_color_source'), dict) else p.get('base_color_source', '—')}; UV chồng lấn {fmt(p.get('uv_overlap_fraction'), 4)}", '']
    up = unity_paths(rec)
    L += ['## Unity', ''] + [f'- {k}: `{v}`' for k, v in up.items()] + ['']
    return '\n'.join(L)


def catalog(recs):
    rows = []
    for rec in recs:
        q = rec['qa'] or {}
        e = {'species': rec['species'], 'stage': rec['stage'], 'name': rec['name'], 'evidence': rec['evidence'], 'rigged': rec['rigged'],
             'archetype': (rec['rig'] or {}).get('archetype'), 'clips': list(((rec['rig'] or {}).get('clips') or {}).keys()),
             'animQa': q.get('status'), 'animQaWarnings': len(q.get('warnings') or []),
             'lodTriangles': [x['triangles'] for x in (rec['lods'] or {}).get('lods') or []],
             'animationData': (rec['rig'] or {}).get('animationData'),
             'bones': ((rec['rig'] or {}).get('skin') or {}).get('bones'), 'height_m': (rec['rig'] or {}).get('height_m'),
             'textures': list(((rec['prepare'] or {}).get('texture_set') or {}).values()), 'validation': (rec['validation'] or {}).get('status')}
        e.update(unity_paths(rec))
        rows.append(e)
    return rows


def catalog_md(rows, species_all):
    L = ['# Prefab catalog (DragonVisualCatalog)', '',
         'Sinh bằng `build_character_reports.py` từ thư mục work của batch. Đường dẫn Unity là nơi **Dravio ▸ Character ▸ Import Batch Manifest** sẽ tạo prefab/controller; '
         'catalog thật là `Assets/Dravio/Character/Data/DragonVisualCatalog.asset`. Thứ tự tra: đúng stage → Unstaged → stage gần nhất (có cảnh báo).', '',
         '| Loài | Stage | Bằng chứng | Rig | Archetype | Clip | Animation QA | LOD tris | Bone | Prefab |', '|---|---|---|---|---|---|---|---|---|---|']
    for e in rows:
        L.append(f"| {e['species']} | {e['stage']} | {e['evidence']} | {'có' if e['rigged'] else 'tĩnh'} | {e['archetype'] or '—'} | {', '.join(e['clips']) or '—'} | "
                 f"{e['animQa'] or '—'}{' (' + str(e['animQaWarnings']) + ')' if e['animQaWarnings'] else ''} | {'/'.join(map(str, e['lodTriangles']))} | {e['bones'] or '—'} | `{e['prefab']}` |")
    have = {(e['species'], e['stage']) for e in rows}
    unstaged = {e['species'] for e in rows if e['stage'] == 'Unstaged'}
    missing_species = [s for s in species_all if not any(e['species'] == s for e in rows)]
    gaps = [f'{s}/{st}' for s in species_all for st in ALL_STAGES if (s, st) not in have and s not in unstaged and s not in missing_species]
    L += ['', '## Còn thiếu', '', f"- Loài chưa có model ({len(missing_species)}): {', '.join(missing_species) or '—'}",
          f"- Cặp loài/stage dùng fallback stage khác ({len(gaps)}): {', '.join(gaps) or '—'}", '']
    return '\n'.join(L)


def animation_md(recs, arch):
    by = {s['id']: s for s in arch['species']}
    L = ['# Báo cáo animation', '', 'Bộ clip mỗi loài lấy từ khả năng sinh học (`dragon-archetypes.json`); loài không có khả năng thì không có clip, runtime không giả (Fly trên loài không bay → Idle + cảnh báo).', '',
         '| Model | Bằng chứng | Clip theo sinh học | Clip đã tạo | Thiếu | Thừa | QA |', '|---|---|---|---|---|---|---|']
    for rec in recs:
        exp = list((by.get(rec['species']) or {}).get('clips') or [])
        got = list(((rec['rig'] or {}).get('clips') or {}).keys())
        base = [c for c in got if c not in ('TakeOff', 'Glide', 'Land')]
        missing = [c for c in exp if c not in got]
        extra = [c for c in base if c not in exp]
        L.append(f"| {rec['name']} | {rec['evidence']} | {', '.join(exp)} | {', '.join(got) or '— (tĩnh)'} | {', '.join(missing) or '—'} | {', '.join(extra) or '—'} | {(rec['qa'] or {}).get('status', '—')} |")
    L += ['', 'TakeOff/Glide/Land là clip chuyển tiếp của loài bay (archetype winged), có trong FBX và Animation Lab; state Animator hiện chỉ gồm '
          + ', '.join(LOCOMOTION_STATES) + ' + ' + ', '.join(ACTION_STATES) + ' (enum `DragonLocomotion`).', '']
    for rec in recs:
        if not rec['rigged']:
            continue
        L += [f"## {rec['name']} ({rec['evidence']})", '', '| Clip | Frame | Loop | m/s | Giãn | Nén | Trượt | Xuyên | Kết quả |', '|---|---|---|---|---|---|---|---|---|']
        for c in clip_rows(rec):
            L.append(f"| {c['clip']} | {c['frames']} | {'có' if c['loop'] else '—'} | {fmt(c['groundSpeed'])} | {fmt(c['stretch'])} | {fmt(c['compress'])} | {fmt(c['slide'], 4)} | {fmt(c['penetration'], 4)} | {c['status']} |")
        L.append('')
    return '\n'.join(L)


def performance_md(rows, policy=None):
    """Static estimate only: what the scene draws and evaluates per frame for N dragons, from the catalog's LOD
    triangle counts and bone counts and the runtime's UpdateRatePolicy defaults. Not a device measurement."""
    policy = policy or {'nearDistance': 12.0, 'farInterval': 2, 'offscreenInterval': 8, 'cullDistance': 80.0}
    rig = [e for e in rows if e['rigged'] and len(e['lodTriangles']) == 3]
    if not rig:
        return '# Ngân sách hiệu năng\n\nKhông có model rig nào để ước tính.\n'
    avg = [sum(e['lodTriangles'][i] for e in rig) / len(rig) for i in range(3)]
    bones = sum(e['bones'] or 0 for e in rig) / len(rig)
    # distribution assumption for a sanctuary camera: 20 % near (LOD0, every frame), 50 % mid (LOD1, every 2nd frame),
    # 20 % far (LOD2, every 2nd frame), 10 % off-screen (not drawn, every 8th frame)
    mix = [('gần (LOD0, mỗi frame)', 0.2, 0, 1), ('giữa (LOD1, 1/2 frame)', 0.5, 1, policy['farInterval']),
           ('xa (LOD2, 1/2 frame)', 0.2, 2, policy['farInterval']), ('ngoài màn hình (không vẽ, 1/8 frame)', 0.1, None, policy['offscreenInterval'])]
    L = ['# Ngân sách hiệu năng (ước tính tĩnh)', '',
         f'**Không phải số đo trên thiết bị.** Tính từ LOD trung bình của {len(rig)} model rig trong catalog ({"/".join(str(round(x)) for x in avg)} tris), '
         f'{bones:.0f} bone/model, 1 material/model, và mặc định `UpdateRatePolicy` (gần ≤ {policy["nearDistance"]} m mỗi frame, xa mỗi {policy["farInterval"]} frame, '
         f'ngoài màn hình mỗi {policy["offscreenInterval"]} frame, > {policy["cullDistance"]} m không đánh giá). Phân bố giả định: 20 % gần, 50 % giữa, 20 % xa, 10 % ngoài màn hình.', '',
         '| Số rồng | Tam giác vẽ | Skinned mesh vẽ | Animator đánh giá / frame (TB) | Bone đánh giá / frame (TB) | Foot IK (gần, mặt đất) |', '|---|---|---|---|---|---|']
    for n in (5, 15, 30):
        tris = draws = evals = 0.0
        for _label, share, lod, interval in mix:
            k = n * share
            if lod is not None:
                tris += k * avg[lod]
                draws += k
            evals += k / interval
        L.append(f'| {n} | {tris:,.0f} | {draws:.0f} | {evals:.1f} | {evals * bones:,.0f} | ≤ {n * 0.2:.0f} |')
    # U1-CHAR.8 mobile: animation data and per-LOD skinning
    data = [(e['name'], e.get('animationData')) for e in rig if e.get('animationData')]
    if data:
        L += ['', '## Dữ liệu animation và skinning (U1-CHAR.8, mobile)', '',
              'Kích thước khi bake (trước khi Unity giảm keyframe), với 4 float quaternion mỗi bone và vị trí chỉ ở bone Root. '
              'Importer đặt nén `Optimal` (sai số 0,5) cho lần import đầu. Skinning: LOD0 4 bone/đỉnh, LOD1 2, LOD2 1. '
              'Shaper, head look và biểu cảm chỉ chạy ≤ 12 m; chớp mắt ≤ 6 m.', '',
              '| Model | Khung | Bone | Float/khung | MB khi bake |', '|---|---|---|---|---|']
        for name, d in data:
            L.append(f"| {name} | {d['frames']} | {d['bones']} | {d['floatsPerFrame']} | {d['bakedMB']:.3f} |")
    L += ['', 'Đo thật: mở `Scenes/DragonStress.unity` (menu **Dravio ▸ Character ▸ Create Stress Test Scene**) trên thiết bị, ghi FPS, `EvaluatedLastFrame`, '
          '`VisibleLastFrame`, `PoolCreated/Reused/Destroyed` của `DragonVisualManager` và Profiler (Animator.Update, SkinnedMesh) cho 5/15/30 rồng.', '']
    return '\n'.join(L)


MOTION_COLS = (('Ground', lambda c: any(x in c for x in ('Walk', 'Idle')) and not ('Swim' in c and 'Walk' not in c)),
               ('Water', lambda c: 'Swim' in c), ('Water rest', lambda c: 'SwimRest' in c), ('Surface', lambda c: 'SwimSurface' in c),
               ('Water entry/exit', lambda c: 'WaterEnter' in c and 'WaterExit' in c), ('Air', lambda c: 'Fly' in c),
               ('Perch', lambda c: 'Perch' in c), ('Underground', lambda c: 'Burrow' in c and 'BurrowEnter' in c), ('Greet', lambda c: 'Greet' in c))


def capability_rows(arch, recs):
    """Species x stage x capability (U1-CHAR.7). Capabilities come from the canonical clip set (the same for every
    stage of a species); the stage columns say which model exists (SAMPLE / real / none -> catalog fallback)."""
    rows = []
    for s in arch['species']:
        clips = set(s['clips'])
        caps = {name: bool(f(clips)) for name, f in MOTION_COLS}
        models = {}
        for st in ALL_STAGES + ('Unstaged',):
            r = next((x for x in recs if x['species'] == s['id'] and x['stage'] == st), None)
            models[st] = None if r is None else (r['evidence'] + ('' if r['rigged'] else ', static'))
        rows.append({'species': s['id'], 'locomotion': s['locomotion'], 'capabilities': caps, 'models': models,
                     'requiresInput': s.get('requiresInput', [])})
    return rows


def capability_md(rows):
    L = ['# Species × Stage × Capability Matrix (U1-CHAR.7)', '',
         'Khả năng lấy từ bộ clip canonical (`dragon-archetypes.json`), giống nhau ở mọi stage của một loài; runtime đọc đúng clip '
         'của model đang hiện (`DragonMotionCapabilities.From`). Cột stage cho biết model nào đang có; ô trống = catalog dùng stage khác '
         '(có cảnh báo). Model SAMPLE không phải asset thật.', '',
         '| Loài | Locomotion | ' + ' | '.join(n for n, _ in MOTION_COLS) + ' | Baby | Juvenile | Adult | Unstaged | Thiếu dữ liệu |',
         '|---|---|' + '---|' * len(MOTION_COLS) + '---|---|---|---|---|']
    for r in rows:
        L.append(f"| {r['species']} | {', '.join(r['locomotion'])} | " + ' | '.join('✓' if r['capabilities'][n] else '—' for n, _ in MOTION_COLS)
                 + ' | ' + ' | '.join(r['models'][st] or '' for st in ALL_STAGES + ('Unstaged',)) + f" | {', '.join(r['requiresInput']) or '—'} |")
    L += ['', 'Loài chỉ bơi (Frostfin, Leviathan) có cột Ground là —: mọi hành vi của chúng diễn ra trong nước.', '']
    return '\n'.join(L)


def motion_md(recs):
    L = ['# Motion metadata và chuyển tiếp (U1-CHAR.7, 7b)', '',
         'Cho Environment/Sanctuary: kích thước thân, tốc độ authored, độ lệch root khi cất/hạ cánh, khoảng không cần cho từng môi trường, '
         'và kết quả kiểm chuyển tiếp (ngưỡng khép vòng: ≤ 0,5°).', '',
         '| Model | Bằng chứng | Thân D×R×C (m) | Swim / Fly m/s | TakeOff / Land root (m) | Nước: cao × rộng (m) | Bay: sải (m) | Chuyển tiếp (max °) |',
         '|---|---|---|---|---|---|---|---|']
    for rec in recs:
        if not rec['rigged']:
            continue
        m = (rec['rig'] or {}).get('motion') or {}
        b = m.get('body', {})
        off = m.get('rootOffsets_m', {})
        cl = (rec['rig'] or {}).get('clips') or {}
        env = (rec['qa'] or {}).get('motionEnvelope') or {}
        tr = (rec['qa'] or {}).get('transitions') or []
        sp = lambda c: fmt(cl[c]['groundSpeed'], 2) if c in cl and cl[c].get('groundSpeed') else '—'  # noqa: E731
        L.append(f"| {rec['name']} | {rec['evidence']} | {fmt(b.get('length_m'), 2)}×{fmt(b.get('width_m'), 2)}×{fmt(b.get('height_m'), 2)} | "
                 f"{sp('Swim')} / {sp('Fly')} | {fmt(off.get('TakeOff.end'), 3) if off.get('TakeOff.end') is not None else '—'} / "
                 f"{fmt(off.get('Land.start'), 3) if off.get('Land.start') is not None else '—'} | "
                 f"{fmt(env.get('swimVerticalExtent_m'), 2)} × {fmt(env.get('swimWidth_m'), 2)} | {fmt(env.get('flightSpan_m'), 2)} | "
                 f"{len(tr)} ({fmt(max((x['poseErrorDeg'] for x in tr), default=None))}) |")
    L += ['', '## Sinh cơ học (U1-CHAR.7b)', '',
          'Froude theo Alexander (λ/h = 2,3·Fr^0,3); Strouhal St = f·A/U (hành trình 0,2–0,4); A/L = biên độ đỉnh-đỉnh '
          'chóp đuôi/cánh trên chiều dài thân (cá 0,1–0,3); tỉ lệ vận tốc lúc rời đất (chim 0,8–0,95). '
          'Nguồn: `Assets/Dravio/Character/BIOMECHANICS_RESEARCH.md`.', '',
          '| Model | Walk: khung / Fr | Run: khung / Fr | Swim: St / A/L | Fly: St / A/L | Quay trên không °/s | Rời đất | Ghi chú |',
          '|---|---|---|---|---|---|---|---|']
    for rec in recs:
        if not rec['rigged']:
            continue
        m = (rec['rig'] or {}).get('motion') or {}
        bm = m.get('biomechanics') or {}
        if not bm:
            continue
        g = lambda k: f"{bm[k]['frames']} / {fmt(bm[k]['froude'], 3)}" if k in bm else '—'  # noqa: E731
        w = lambda c: f"{fmt(bm[c].get('strouhal'), 2)} / {fmt(bm[c].get('tipAmplitudeL'), 2)}" if c in bm else '—'  # noqa: E731
        notes = [c + ': A/L ngoài dải cá, giữ sải mặc định' for c in ('Swim', 'SwimSurface') if bm.get(c, {}).get('amplitudeOutsideBand')]
        L.append(f"| {rec['name']} | {g('walk')} | {g('run')} | {w('Swim')} | {w('Fly')} | "
                 f"{fmt((m.get('maxTurnRateDegPerSec') or {}).get('air'), 0)} | {fmt(bm.get('TakeOff', {}).get('liftOffVelocityShare'), 2)} | "
                 f"{'; '.join(notes) or '—'} |")
    L += ['', 'Tốc độ Swim/Fly: clip tại chỗ. Tốc độ danh nghĩa theo Strouhal 0,3 của nhịp đo trên model. Nếu không đo được hoặc biên độ '
          'đuôi ngoài dải thì dùng số thân mỗi chu kỳ của `rig-profiles.json` ▸ `motionDefaults`. '
          '**Chờ art director xác nhận.** Giới hạn quay: Ground/Water từ `motionDefaults`, Air tính từ vòng nghiêng.', '']
    return '\n'.join(L)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument('work_dir', type=Path)
    ap.add_argument('out_dir', type=Path)
    a = ap.parse_args(argv)
    if not (a.work_dir / 'assets').is_dir():
        raise SystemExit(f'{a.work_dir}: no assets/ folder (run batch_import.py first)')
    if a.out_dir.resolve() == a.work_dir.resolve():
        raise SystemExit('out_dir must differ from work_dir')
    recs = collect(a.work_dir)
    arch = _load(ARCHETYPES) or {'species': []}
    limits = (_load(BUDGETS) or {}).get('animationQa', {})
    (a.out_dir / 'qa').mkdir(parents=True, exist_ok=True)
    for rec in recs:
        (a.out_dir / 'qa' / f"{rec['name']}.md").write_text(model_page(rec, limits), encoding='utf-8')
    rows = catalog(recs)
    (a.out_dir / 'prefab-catalog.json').write_text(json.dumps(rows, indent=2, ensure_ascii=False), encoding='utf-8')
    (a.out_dir / 'prefab-catalog.md').write_text(catalog_md(rows, [s['id'] for s in arch['species']]), encoding='utf-8')
    (a.out_dir / 'animation-report.md').write_text(animation_md(recs, arch), encoding='utf-8')
    caps = capability_rows(arch, recs)
    (a.out_dir / 'capability-matrix.json').write_text(json.dumps(caps, indent=2, ensure_ascii=False), encoding='utf-8')
    (a.out_dir / 'capability-matrix.md').write_text(capability_md(caps), encoding='utf-8')
    (a.out_dir / 'motion-report.md').write_text(motion_md(recs), encoding='utf-8')
    (a.out_dir / 'performance-budget.md').write_text(performance_md(rows), encoding='utf-8')
    print(f'{len(recs)} dragon(s): {sum(r["evidence"] == "SAMPLE" for r in recs)} SAMPLE, {sum(r["evidence"] != "SAMPLE" for r in recs)} real -> {a.out_dir}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
