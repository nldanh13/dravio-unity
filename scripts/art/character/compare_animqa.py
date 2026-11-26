"""Before/after comparison of animation QA reports (plain Python, no Blender).

  python scripts/art/character/compare_animqa.py <before_dir> <after_dir> <out.md> [--json out.json]
         [--before-label "rig v1"] [--after-label "rig v2"]

Both folders hold <Name>_animqa.json files written by anim_qa.py with the same character-budgets.json
limits. Models are matched by name. For every clip the table shows edge stretch p99.9 and compression p0.1
before -> after and whether each is within the limit; every warning of both runs is listed, so nothing that
got worse is hidden. A model missing on one side is reported as such (e.g. not rigged before).
"""
import argparse
import json
from pathlib import Path


def load_dir(d):
    out = {}
    for f in sorted(Path(d).glob('*_animqa.json')):
        r = json.loads(f.read_text(encoding='utf-8'))
        out[r['name']] = r
    return out


def fmt(v, ok):
    return '—' if v is None else (f'{v:.3f}' if ok else f'**{v:.3f}**')


def compare(before, after, lb, la):
    names = sorted(set(before) | set(after))
    rows, data = [], []
    L = [f'# Animation QA: {lb} → {la}', '',
         'Cùng công cụ `anim_qa.py`, cùng ngưỡng trong `character-budgets.json` (`animationQa`). '
         'Số **đậm** là vượt ngưỡng (giãn p99.9 > maxStretchP999, nén p0.1 < minCompressP001).', '',
         '| Model | ' + lb + ' | ' + la + ' | Cảnh báo ' + lb + ' | Cảnh báo ' + la + ' |', '|---|---|---|---|---|']
    for n in names:
        b, a = before.get(n), after.get(n)
        L.append(f"| {n} | {b['status'] if b else 'không có (chưa rig)'} | {a['status'] if a else 'không có'} | "
                 f"{len(b['warnings']) if b else '—'} | {len(a['warnings']) if a else '—'} |")
    for n in names:
        b, a = before.get(n), after.get(n)
        lim = (a or b)['limits']
        L += ['', f'## {n}', '', f'| Clip | giãn {lb} | giãn {la} | nén {lb} | nén {la} | vùng tệ nhất ({la}) |',
              '|---|---|---|---|---|---|']
        clips = list(dict.fromkeys(list((b or {}).get('clips', {})) + list((a or {}).get('clips', {}))))
        entry = {'name': n, 'before': b['status'] if b else None, 'after': a['status'] if a else None, 'clips': {}}
        for c in clips:
            db = (b or {}).get('clips', {}).get(c, {}).get('deformation')
            da = (a or {}).get('clips', {}).get(c, {}).get('deformation')
            sb, sa = (db or {}).get('stretchP999'), (da or {}).get('stretchP999')
            cb, ca = (db or {}).get('compressP001'), (da or {}).get('compressP001')
            okf = lambda s: s is None or s <= lim['maxStretchP999']  # noqa: E731
            okc = lambda s: s is None or s >= lim['minCompressP001']  # noqa: E731
            worst = ', '.join(f"{w['bone']} {w['share']:.0%}" for w in (da or {}).get('worstBones', [])[:2])
            L.append(f'| {c} | {fmt(sb, okf(sb))} | {fmt(sa, okf(sa))} | {fmt(cb, okc(cb))} | {fmt(ca, okc(ca))} | {worst} |')
            entry['clips'][c] = {'stretchBefore': sb, 'stretchAfter': sa, 'compressBefore': cb, 'compressAfter': ca}
        for label, r in ((lb, b), (la, a)):
            if r and r['warnings']:
                L.append('')
                L.append(f'Cảnh báo {label}:')
                for w in r['warnings']:
                    L.append(f"- {w['clip'] or 'model'}: {w['check']}: {w['message']} ({w['value']} / ngưỡng {w['limit']})")
        data.append(entry)
    return '\n'.join(L) + '\n', data


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument('before_dir', type=Path)
    ap.add_argument('after_dir', type=Path)
    ap.add_argument('out_md', type=Path)
    ap.add_argument('--json', type=Path)
    ap.add_argument('--before-label', default='trước')
    ap.add_argument('--after-label', default='sau')
    a = ap.parse_args(argv)
    md, data = compare(load_dir(a.before_dir), load_dir(a.after_dir), a.before_label, a.after_label)
    a.out_md.write_text(md, encoding='utf-8')
    if a.json:
        a.json.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding='utf-8')
    print(f'Wrote {a.out_md}')


if __name__ == '__main__':
    main()
