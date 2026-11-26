"""Classify the 16 species into rig archetypes and clip sets, from canonical data only.

  python scripts/art/character/build_archetypes.py           # write outputs
  python scripts/art/character/build_archetypes.py --check   # exit 1 if stale

Input:  Assets/Dravio/Character/Data/species-character-spec.json (itself generated from canonical data)
Output: Assets/Dravio/Character/Data/dragon-archetypes.json, Assets/Dravio/Character/DRAGON_ARCHETYPES.md

Rules (each result cites the canonical field it comes from):
  archetype   winged      <- locomotion contains "fly"
              serpentine  <- bodyStyle == Serpent (aquatic variant when locomotion lacks "walk")
              aquatic     <- locomotion lacks "walk" (and not serpentine)
              quadruped   <- otherwise, when the silhouette states "bốn chân"
              walker      <- otherwise; limb count unstated, so no leg template until it is supplied
  clips       Idle, Eat, Sleep, Happy for every species (existing care interactions);
              Walk + Run <- "walk"; Swim <- "swim"; Fly <- "fly"; Burrow <- "burrow"
              behaviour clips (U1-CHAR.6), appended after the locomotion clips so older frame ranges stay put:
                Rest, Wake, LookAround, Curious <- every species (built from its own Sleep/Idle pose and its
                  head/neck; Environment interaction hints rest / look / look_up / inspect / sniff)
                Drink, Stretch, Bask <- "walk" (a standing, legged body: drink at the pond bank, play-bow
                  stretch, lie on the warm rocks; hints drink / bask). Which dragon actually basks or drinks
                  is the Sanctuary's decision, not the clip set's.
              advanced motion clips (U1-CHAR.7), appended after the U1-CHAR.6 clips:
                Greet <- every species (head/neck/tail only: a simple social greeting)
                SwimIdle <- "swim" (hold position in water)
                SwimRest <- "swim" and respiration gill or dual (can rest under water)
                SwimSurface <- "swim" and respiration lung or dual (an air breather swims at the surface)
                WaterEnter, WaterExit <- "swim" and "walk" (goes between land and water)
                Perch <- "fly" and silhouette "chân đậu" (perching feet stated); flyers without it get none
                DigPrep, BurrowEnter, BurrowEmerge <- "burrow" (all above ground: the Sanctuary hides the
                  model while underground; nothing is moved through the ground)
  limbCount   from the silhouette text only ("bốn chân"); otherwise null and the rig step requires an
              explicit artist/GDD value in asset-overrides.json - never guessed.
  features    silhouette keywords copied verbatim as attachment/VFX hints (no geometry is invented).
"""
import argparse
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
SPEC = REPO / 'Assets/Dravio/Character/Data/species-character-spec.json'
OUT_JSON = REPO / 'Assets/Dravio/Character/Data/dragon-archetypes.json'
OUT_MD = REPO / 'Assets/Dravio/Character/DRAGON_ARCHETYPES.md'

BASE_CLIPS = ['Idle', 'Eat', 'Sleep', 'Happy']
LOCO_CLIPS = {'walk': ['Walk', 'Run'], 'swim': ['Swim'], 'fly': ['Fly'], 'burrow': ['Burrow']}
BEHAVIOUR_CLIPS = ['Rest', 'Wake', 'LookAround', 'Curious']
GROUND_BEHAVIOUR_CLIPS = {'walk': ['Drink', 'Stretch', 'Bask']}
MOTION_CLIPS = ['Greet']
# Silhouette keyword -> feature tag. Only words that literally appear in a silhouette are used.
FEATURE_WORDS = [
    ('đuôi lửa', 'flame_tail'), ('sừng', 'horns'), ('mào', 'crest'), ('mai', 'shell'),
    ('giáp', 'armor_plates'), ('vây', 'fins'), ('cánh', 'wings'), ('lông', 'feathers_or_fur'),
    ('mầm lá', 'leaf_sprout'), ('đuôi lá', 'leaf_tail'), ('đuôi tia điện', 'spark_tail'),
    ('đuôi mây', 'cloud_tail'), ('đuôi quạt', 'fan_tail'), ('đuôi cực quang', 'aurora_tail'),
    ('đuôi sao', 'star_tail'), ('màng chân', 'webbed_feet'), ('đuôi dẹt', 'flat_tail'),
    ('chân đậu', 'perching_feet'), ('vây tai', 'ear_fins'),
]
TEMPLATES = {
    'quadruped': 'Root, Pelvis, Spine, Chest, Neck, Head, Tail1-3, 4 x (Upper, Lower, Foot), VFX anchors',
    'winged': 'quadruped body (legs only if limbCount given) + 2 x (WingUpper, WingFore, WingTip)',
    'serpentine': 'Root, Head, Neck, Spine1-8, Tail1-4 (+ limb chains only if limbCount given)',
    'aquatic': 'Root, Head, Neck, Spine1-4, Tail1-3, Fin_L/R (pectoral), TailFin',
    'walker': 'no template until limbCount is supplied; limbCount=4 uses the quadruped template',
}


def has_word(text, word):
    """True if `word` occurs and is not negated ("không cánh") or a comparison ("như cánh")."""
    import re
    for m in re.finditer(r'(?<![\w])' + re.escape(word) + r'(?![\w])', text):
        before = text[:m.start()].rstrip()
        if before.endswith('không') or before.endswith('như'):
            continue
        return True
    return False


def classify(s):
    c = s['canonical']
    loc = c['locomotion']
    sil = c['silhouetteVi'].lower()
    limb = 4 if 'bốn chân' in sil else None
    if 'fly' in loc:
        arch, why = 'winged', 'locomotion contains "fly"'
    elif c['bodyStyle'] == 'Serpent':
        arch, why = 'serpentine', 'bodyStyle == Serpent'
    elif 'walk' not in loc:
        arch, why = 'aquatic', 'locomotion lacks "walk"'
    else:
        if limb == 4:
            arch, why = 'quadruped', 'walks; silhouette states "bốn chân"'
        else:
            arch, why = 'walker', 'walks; limb count not stated (quadruped template once limbCount=4 is supplied)'
    clips = list(BASE_CLIPS)
    for l in loc:
        clips += LOCO_CLIPS.get(l, [])
    clips += BEHAVIOUR_CLIPS
    for l in loc:
        clips += GROUND_BEHAVIOUR_CLIPS.get(l, [])
    clips += MOTION_CLIPS
    resp = c.get('respiration')
    if 'swim' in loc:
        clips.append('SwimIdle')
        if resp in ('gill', 'dual'):
            clips.append('SwimRest')
        if resp in ('lung', 'dual'):
            clips.append('SwimSurface')
        if 'walk' in loc:
            clips += ['WaterEnter', 'WaterExit']
    if 'fly' in loc and has_word(sil, 'chân đậu'):
        clips.append('Perch')
    if 'burrow' in loc:
        clips += ['DigPrep', 'BurrowEnter', 'BurrowEmerge']
    feats = [{'tag': tag, 'word': w} for w, tag in FEATURE_WORDS if has_word(sil, w)]
    needs = []
    walks = 'walk' in loc
    if walks and limb is None:
        needs.append('limbCount')  # walking limbs exist (canonical) but their number is not stated
    return {
        'id': s['id'], 'archetype': arch, 'archetypeReason': why,
        'aquaticVariant': arch == 'serpentine' and not walks,
        'locomotion': loc, 'clips': clips,
        'walkingLimbs': walks, 'flightWings': 'fly' in loc,
        'limbCount': limb, 'limbCountSource': 'silhouetteVi' if limb else None,
        'features': feats, 'requiresInput': needs,
        'template': TEMPLATES[arch],
    }


def build():
    spec = json.loads(SPEC.read_text(encoding='utf-8'))
    species = [classify(s) for s in spec['species']]
    return {
        'version': 1, 'generatedBy': 'scripts/art/character/build_archetypes.py',
        'source': 'Assets/Dravio/Character/Data/species-character-spec.json',
        'clipNames': ['Idle', 'Walk', 'Run', 'Eat', 'Sleep', 'Happy', 'Swim', 'Fly', 'Burrow',
                      'Rest', 'Wake', 'LookAround', 'Curious', 'Drink', 'Stretch', 'Bask',
                      'Greet', 'SwimIdle', 'SwimRest', 'SwimSurface', 'WaterEnter', 'WaterExit', 'Perch',
                      'DigPrep', 'BurrowEnter', 'BurrowEmerge'],
        'templates': TEMPLATES,
        'species': species,
    }


def to_md(d):
    L = ['# Archetype rig và clip của 16 loài (tự sinh – không sửa tay)', '',
         'Sinh bởi `scripts/art/character/build_archetypes.py` từ spec loài canonical. '
         '`requiresInput: limbCount` nghĩa là loài có chân đi (canonical) nhưng dữ liệu không nói số chân; '
         'bước rig sẽ dừng cho tới khi art director/GDD điền giá trị vào `asset-overrides.json`.', '',
         '| Loài | Archetype | Lý do | Clip | Số chân | Đặc điểm (từ silhouette) | Cần bổ sung |',
         '|---|---|---|---|---|---|---|']
    for s in d['species']:
        L.append(f"| `{s['id']}` | {s['archetype']}{' (dưới nước)' if s['aquaticVariant'] else ''} | {s['archetypeReason']} | "
                 f"{', '.join(s['clips'])} | {s['limbCount'] if s['limbCount'] else ('—' if not s['walkingLimbs'] else 'chưa nêu')} | "
                 f"{', '.join(f['tag'] for f in s['features']) or '—'} | {', '.join(s['requiresInput']) or '—'} |")
    L += ['', '## Template xương', '']
    for k, v in d['templates'].items():
        L.append(f'- **{k}**: {v}')
    return '\n'.join(L) + '\n'


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--check', action='store_true')
    a = ap.parse_args(argv)
    d = build()
    js = json.dumps(d, indent=2, ensure_ascii=False) + '\n'
    md = to_md(d)
    if a.check:
        stale = [p for p, c in [(OUT_JSON, js), (OUT_MD, md)] if not p.exists() or p.read_text(encoding='utf-8') != c]
        for p in stale:
            print('STALE:', p.relative_to(REPO))
        return 1 if stale else 0
    OUT_JSON.write_text(js, encoding='utf-8')
    OUT_MD.write_text(md, encoding='utf-8')
    print('Wrote', OUT_JSON.relative_to(REPO), OUT_MD.relative_to(REPO))
    return 0


if __name__ == '__main__':
    sys.exit(main())
