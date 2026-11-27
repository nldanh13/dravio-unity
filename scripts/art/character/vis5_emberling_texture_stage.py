"""Extract or grade EMBEDDED Emberling base-color images; never write the GLB.
Requires Python 3 and Pillow. Run extract first, paint disjoint L masks in the
original UV space, then run grade to a new staging folder. No automatic masks.
"""
import argparse
import hashlib
import io
import json
import struct
from pathlib import Path
from PIL import Image

SOURCE_BLOB = '826ebe5f6e321a045eecd50b5b0dc8fe61dee54b'
PALETTE = {'skin': 'D86B47', 'belly': 'F9D59A', 'horn': 'A83D36', 'flame': 'FFD66C'}


def git_blob(raw):
    return hashlib.sha1(f'blob {len(raw)}\0'.encode() + raw).hexdigest()


def embedded(raw):
    magic, version, length = struct.unpack_from('<4sII', raw)
    if (magic, version, length) != (b'glTF', 2, len(raw)):
        raise ValueError('Expected valid GLB 2.0 header')
    chunks = {}
    pos = 12
    while pos < len(raw):
        size, tag = struct.unpack_from('<I4s', raw, pos)
        pos += 8
        if tag in chunks or size % 4 or pos + size > len(raw):
            raise ValueError('Invalid GLB chunk')
        chunks[tag] = raw[pos:pos + size]
        pos += size
    doc = json.loads(chunks[b'JSON'])
    buf = chunks[b'BIN\0']
    found = {}
    for m, material in enumerate(doc.get('materials', [])):
        tex = material.get('pbrMetallicRoughness', {}).get('baseColorTexture')
        if tex is None:
            continue
        texture = doc['textures'][tex['index']]
        idx = texture.get('source')
        if idx is None:
            raise ValueError('Unsupported base-color texture source')
        im = doc['images'][idx]
        if 'bufferView' not in im or im.get('mimeType') not in ('image/png', 'image/jpeg'):
            raise ValueError('Only embedded PNG/JPEG supported')
        view = doc['bufferViews'][im['bufferView']]
        start = view.get('byteOffset', 0)
        size = view['byteLength']
        if view['buffer'] != 0 or start < 0 or size <= 0 or start + size > len(buf):
            raise ValueError('Invalid image view')
        rec = found.setdefault(idx, {'raw': buf[start:start + size], 'materials': [], 'mime': im['mimeType']})
        rec['materials'].append({'index': m, 'name': material.get('name'), 'texCoord': tex.get('texCoord', 0), 'textureInfo': tex, 'baseColorFactor': material.get('pbrMetallicRoughness', {}).get('baseColorFactor', [1,1,1,1])})
    if not found:
        raise ValueError('No embedded base-color images')
    return doc, found


def grade_image(image, masks, strength):
    if not 0 <= strength <= 0.35:
        raise ValueError('Strength must be 0..0.35')
    src = image.convert('RGBA')
    for name, mask in masks.items():
        if name not in PALETTE or mask.mode != 'L' or mask.size != src.size:
            raise ValueError('Invalid mask: ' + name)
    data = []
    changed = 0
    targets = {k: tuple(bytes.fromhex(v)) for k, v in PALETTE.items()}
    pixels = {k: m.getdata() for k, m in masks.items()}
    for i, (r,g,b,a) in enumerate(src.getdata()):
        weights = {k: v[i] / 255 for k,v in pixels.items()}
        if sum(weights.values()) > 1.00001:
            raise ValueError('Overlapping masks at pixel ' + str(i))
        orig = (r,g,b)
        out = list(orig)
        y = .2126*r + .7152*g + .0722*b
        for name, w in weights.items():
            if not w or not a:
                continue
            t = targets[name]
            ty = sum(c*k for c,k in zip(t, (.2126,.7152,.0722)))
            # Preserve the source's approximate sRGB luma and local painted detail.
            # Not a linear-light or physically based relighting operation.
            goal = [max(0,min(255,y+c-ty)) for c in t]
            for k in range(3):
                out[k] += strength*w*(goal[k]-orig[k])
        result = (*[round(max(0,min(255,c))) for c in out], a)
        changed += result[:3] != orig
        data.append(result)
    dst = Image.new('RGBA', src.size)
    dst.putdata(data)
    return dst, changed


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('mode', choices=['extract','grade'])
    p.add_argument('source', type=Path)
    p.add_argument('out', type=Path)
    p.add_argument('--masks', type=Path, help='Folder of image_<idx>_<region>.png (L mode)')
    p.add_argument('--strength', type=float, default=.18)
    args = p.parse_args()
    raw = args.source.read_bytes()
    if git_blob(raw) != SOURCE_BLOB:
        raise SystemExit('Source GLB differs from reviewed repository blob; stop')
    doc, imgs = embedded(raw)
    out = args.out.resolve()
    if 'Assets' in out.parts or out.exists():
        raise SystemExit('Use a new output folder outside Unity Assets')
    if args.mode == 'grade' and (not args.masks or not args.masks.is_dir()):
        raise SystemExit('Provide reviewed UV masks with --masks')
    products = []
    matched = set()
    for idx, rec in imgs.items():
        src = Image.open(io.BytesIO(rec['raw'])); src.load()
        if args.mode == 'extract':
            ext = '.png' if rec['mime'] == 'image/png' else '.jpg'
            products.append((f'image_{idx}{ext}', rec['raw'], {'materials':rec['materials'], 'size':src.size}))
        else:
            masks = {}
            for name in PALETTE:
                path = args.masks / f'image_{idx}_{name}.png'
                if path.is_file():
                    mask = Image.open(path); mask.load(); masks[name] = mask; matched.add(path.name)
            if not masks:
                continue
            dst, changed = grade_image(src, masks, args.strength)
            if not changed:
                raise ValueError(f'image {idx}: masks produced no changes')
            buf = io.BytesIO(); dst.save(buf, format='PNG')
            products.append((f'image_{idx}_graded.png', buf.getvalue(), {'materials':rec['materials'], 'size':src.size, 'changed_pixels':changed, 'masks':list(masks)}))
    if args.mode == 'grade':
        provided = {x.name for x in args.masks.glob('*.png')}
        if not products or provided != matched:
            raise SystemExit('No valid masks or unexpected PNGs in mask folder')
    out.mkdir(parents=True, exist_ok=False)
    report = {'source_blob':SOURCE_BLOB,'mode':args.mode,'strength':args.strength,'images':[]}
    for name, data, info in products:
        (out/name).write_bytes(data)
        report['images'].append({'path':name,'sha256':hashlib.sha256(data).hexdigest(),**info})
    (out/'manifest.json').write_text(json.dumps(report, indent=2), encoding='utf8')


if __name__ == '__main__':
    main()
