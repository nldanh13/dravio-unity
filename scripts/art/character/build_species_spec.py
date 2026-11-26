"""Generate the per-species character modelling spec from canonical repository data only.

  python scripts/art/character/build_species_spec.py            # writes the two files below
  python scripts/art/character/build_species_spec.py --check    # exit 1 if they are stale

Inputs (read-only, canonical):
  Assets/Dravio/Data/species-biology.json        (G0 biology snapshot, TS commit b33ec7a)
  Assets/Dravio/Domain/Catalog/SpeciesCatalog.cs (elements, rarity, realms, growth, BodyStyle)
Outputs:
  Assets/Dravio/Character/Data/species-character-spec.json
  Assets/Dravio/Character/SPECIES_MORPHOLOGY.md

Rules: canonical fields are copied verbatim. A modelling requirement is only emitted when it
follows mechanically from a canonical field, and each one cites that field. Anything the data
does not state (stage proportions, size, horn/wing growth per stage, limb count when the
silhouette does not say it) is left null with status "unspecified" - never invented.
"""
import argparse
import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
BIOLOGY = REPO / 'Assets/Dravio/Data/species-biology.json'
CATALOG = REPO / 'Assets/Dravio/Domain/Catalog/SpeciesCatalog.cs'
OUT_JSON = REPO / 'Assets/Dravio/Character/Data/species-character-spec.json'
OUT_MD = REPO / 'Assets/Dravio/Character/SPECIES_MORPHOLOGY.md'
STAGES = ['Baby', 'Juvenile', 'Adult']
SURFACE = {'scale': 'Vảy (scale)', 'fur': 'Lông (fur)', 'armor': 'Giáp (armor)'}

ROW_RE = re.compile(r'new Row \{(?P<body>[^\n]*?)\},?\s*$', re.M)


def parse_catalog(text):
    rows = {}
    for m in ROW_RE.finditer(text):
        b = m.group('body')

        def field(name, pattern=r'([^,}]+)'):
            f = re.search(rf'\b{name}\s*=\s*{pattern}', b)
            return f.group(1).strip() if f else None

        def array(name):
            f = re.search(rf'\b{name}\s*=\s*new\[\]\s*\{{([^}}]*)\}}', b)
            return [x.strip().strip('"') for x in f.group(1).split(',') if x.strip()] if f else []

        sid = field('Id', r'"([^"]+)"')
        elements = [{'Fire': 'fire', 'Water': 'water', 'Nature': 'nature', 'Sky': 'sky'}[e] for e in array('Elements')]
        realms = [{'Land': 'land', 'Ocean': 'ocean', 'SkyRealm': 'sky'}[r] for r in array('Realms')]
        rows[sid] = {
            'elements': elements, 'realms': realms, 'habitatTags': array('HabitatTags'),
            'rarity': field('Rarity').split('.')[-1].lower(), 'group': field('Group').split('.')[-1],
            'growthMinutes': int(field('GrowthMin')), 'bodyStyle': field('Body').split('.')[-1],
            'packEligible': field('PackEligible') != 'false',
        }
    return rows


def derived_requirements(bio, cat):
    """Only mechanical consequences of canonical fields; each item cites its source."""
    loc = bio['locomotion']
    sil = bio['silhouetteVi'].lower()
    req = []

    def add(key, value, source, note=None):
        item = {'requirement': key, 'value': value, 'source': source}
        if note:
            item['note'] = note
        req.append(item)

    add('walkingLimbs', 'walk' in loc, 'biology.locomotion',
        None if 'walk' in loc else 'Không có "walk": không dựng chân đi/đứng trên cạn.')
    add('flightWings', 'fly' in loc, 'biology.locomotion',
        'Cần cánh bay được (rig Wing_L/Wing_R có trọng số).' if 'fly' in loc else
        'Không có "fly": không làm cánh dùng để bay.')
    if 'swim' in loc:
        add('swimPoseNeeded', True, 'biology.locomotion', 'Cần tư thế/animation bơi riêng (chưa có trong rig hiện tại).')
    if 'burrow' in loc:
        add('burrowPoseNeeded', True, 'biology.locomotion', 'Cần tư thế/animation đào riêng (chưa có trong rig hiện tại).')
    add('surface', bio['protection'], 'biology.protection', SURFACE[bio['protection']])
    add('basePalette', bio['palette'], 'biology.palette',
        'Màu tham chiếu cho Base Color; biến dị di truyền (pigment/pattern/eye) xử lý riêng.')
    limb = 4 if 'bốn chân' in sil else None
    add('limbCount', limb, 'biology.silhouetteVi' if limb else None,
        None if limb else 'Silhouette không nêu số chi: để trống, cần art director xác nhận.')
    add('bodyStyle', cat['bodyStyle'], 'SpeciesCatalog.Body')
    return req


def open_questions(sid, bio, cat):
    q = []
    loc = bio['locomotion']
    if cat['bodyStyle'] == 'Winged' and 'fly' not in loc:
        q.append('BodyStyle=Winged nhưng locomotion không có "fly" '
                 f'(silhouette: "{bio["silhouetteVi"]}"). Mô hình theo silhouette (vây dạng cánh), '
                 'không làm cánh bay; cần xác nhận.')
    if 'walk' not in loc and 'chân' in bio['silhouetteVi'].lower() and 'không chân' not in bio['silhouetteVi'].lower():
        q.append('Silhouette nhắc tới chân nhưng locomotion không có "walk"; cần xác nhận.')
    q.append('Tỉ lệ, kích thước và chi tiết riêng từng giai đoạn Baby/Juvenile/Adult chưa có trong dữ liệu gốc.')
    return q


def build():
    bio_all = json.loads(BIOLOGY.read_text(encoding='utf-8'))
    cat_all = parse_catalog(CATALOG.read_text(encoding='utf-8'))
    if set(bio_all) != set(cat_all):
        raise SystemExit(f'Species mismatch biology vs catalog: {set(bio_all) ^ set(cat_all)}')
    species = []
    for sid in cat_all:  # catalog order
        bio, cat = bio_all[sid], cat_all[sid]
        if bio['group'] != cat['group']:
            raise SystemExit(f'{sid}: breeding group differs between biology and catalog')
        growth_sec = cat['growthMinutes'] * 60
        stages = []
        for st in STAGES:
            stages.append({
                'stage': st,
                'growthWindow': {'Baby': 'growth < 35%', 'Juvenile': '35% <= growth < 100%',
                                 'Adult': 'growth = 100%'}[st],
                'startsAtGrowthSeconds': {'Baby': 0, 'Juvenile': growth_sec * 35 // 100, 'Adult': growth_sec}[st],
                'canonicalMorphology': None,
                'status': 'unspecified',
                'assetName': f'DRG_{sid}_{st}',
                'prototypeAsset': ('EmberlingRefinedGLB.fbx (0.90 m, 22-bone fitted rig) – procedural/GLB prototype only'
                                   if sid == 'emberling' and st == 'Baby' else None),
            })
        species.append({
            'id': sid,
            'canonical': {
                'silhouetteVi': bio['silhouetteVi'], 'locomotion': bio['locomotion'],
                'protection': bio['protection'], 'respiration': bio['respiration'],
                'activity': bio['activity'], 'palette': bio['palette'], 'group': bio['group'],
                'altitudeBands': bio['altitudeBands'], 'depthBands': bio['depthBands'],
                'draft': bio['draft'], **cat,
            },
            'modelingRequirements': derived_requirements(bio, cat),
            # Flat projection for Unity's JsonUtility (cannot read the mixed-type 'value' above).
            'rig': {'walkingLimbs': 'walk' in bio['locomotion'], 'flightWings': 'fly' in bio['locomotion']},
            'stages': stages,
            'openQuestions': open_questions(sid, bio, cat),
        })
    return {
        'version': 1,
        'generatedBy': 'scripts/art/character/build_species_spec.py',
        'sources': [str(BIOLOGY.relative_to(REPO)).replace('\\', '/'), str(CATALOG.relative_to(REPO)).replace('\\', '/')],
        'rules': ['Trường "canonical" chép nguyên văn từ dữ liệu gốc.',
                  'Mỗi "modelingRequirements" chỉ suy ra cơ học từ một trường gốc và ghi nguồn.',
                  'Giai đoạn: chỉ có mốc tăng trưởng (Lifecycle: Juvenile 35%, Adult 100%). '
                  'Hình thể theo giai đoạn chưa có dữ liệu gốc -> null/"unspecified".'],
        'stageThresholdsSource': 'Assets/Dravio/Domain/Biology/Lifecycle.cs (JuvenileAtMs=growth*350, AdultAtMs=growth*1000)',
        'species': species,
    }


def to_markdown(spec):
    L = ['# Đặc điểm hình thể 16 loài rồng (tự sinh – không sửa tay)', '',
         'Sinh bởi `scripts/art/character/build_species_spec.py` từ `species-biology.json` và `SpeciesCatalog.cs`. '
         'Cột "Gốc" chép nguyên văn; cột "Yêu cầu mô hình" chỉ là hệ quả trực tiếp của trường gốc. '
         'Không có dữ liệu hình thể theo giai đoạn trong nguồn, nên các ô giai đoạn ghi **chưa xác định**.', '',
         '| Loài | Silhouette (gốc) | Di chuyển | Bề mặt | Hô hấp | BodyStyle | Chân đi | Cánh bay | Số chi |',
         '|---|---|---|---|---|---|---|---|---|']
    for s in spec['species']:
        c = s['canonical']
        r = {x['requirement']: x['value'] for x in s['modelingRequirements']}
        L.append(f"| `{s['id']}` | {c['silhouetteVi']} | {', '.join(c['locomotion'])} | {c['protection']} | "
                 f"{c['respiration']} | {c['bodyStyle']} | {'có' if r['walkingLimbs'] else 'không'} | "
                 f"{'có' if r['flightWings'] else 'không'} | {r['limbCount'] if r['limbCount'] else 'chưa nêu'} |")
    L += ['', '## Ba giai đoạn', '',
          'Mốc tăng trưởng (canonical, Lifecycle.cs): Baby < 35 %, Juvenile 35–100 %, Adult = 100 % thời gian trưởng thành.', '',
          '| Loài | Thời gian trưởng thành | Juvenile từ | Baby | Juvenile | Adult |', '|---|---|---|---|---|---|']
    for s in spec['species']:
        g = s['canonical']['growthMinutes']
        st = {x['stage']: x for x in s['stages']}
        baby = 'prototype GLB/procedural (chưa phải asset cuối)' if st['Baby']['prototypeAsset'] else 'chưa xác định'
        L.append(f"| `{s['id']}` | {g} phút | {st['Juvenile']['startsAtGrowthSeconds']} s | {baby} | chưa xác định | chưa xác định |")
    L += ['', '## Câu hỏi mở cần art director / GDD trả lời', '']
    for s in spec['species']:
        for q in s['openQuestions']:
            L.append(f"- `{s['id']}`: {q}")
    L += ['', '## Bảng màu tham chiếu (gốc)', '', '| Loài | primary | secondary | belly | accent |', '|---|---|---|---|---|']
    for s in spec['species']:
        p = s['canonical']['palette']
        L.append(f"| `{s['id']}` | `{p['primary']}` | `{p['secondary']}` | `{p['belly']}` | `{p['accent']}` |")
    return '\n'.join(L) + '\n'


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--check', action='store_true')
    a = ap.parse_args(argv)
    spec = build()
    js = json.dumps(spec, indent=2, ensure_ascii=False) + '\n'
    md = to_markdown(spec)
    if a.check:
        stale = [p for p, c in [(OUT_JSON, js), (OUT_MD, md)] if not p.exists() or p.read_text(encoding='utf-8') != c]
        for p in stale:
            print('STALE:', p.relative_to(REPO))
        return 1 if stale else 0
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUT_JSON.write_text(js, encoding='utf-8')
    OUT_MD.write_text(md, encoding='utf-8')
    print(f'Wrote {len(spec["species"])} species -> {OUT_JSON.relative_to(REPO)}, {OUT_MD.relative_to(REPO)}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
