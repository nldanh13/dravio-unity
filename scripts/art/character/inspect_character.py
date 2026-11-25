"""Read-only inspection of a character model: mesh, UV, vertex colour, PBR, skin, LOD.

Usage (Blender 5.x):
  blender --factory-startup --background --python-exit-code 1 \
      --python scripts/art/character/inspect_character.py -- <model.glb|fbx|blend> <report.json>

Also runs with the `bpy` Python module: python inspect_character.py <model> <report.json>
The source model is never written; its SHA-256 is recorded before and after.
"""
import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402

import bpy  # noqa: E402

LOD_RE = re.compile(r'_LOD(\d+)$', re.IGNORECASE)


def color_attribute_stats(mesh):
    out = []
    render_index = mesh.color_attributes.render_color_index
    for idx, attr in enumerate(mesh.color_attributes):
        values = set()
        n = len(attr.data)
        step = max(1, n // 20000)
        for i in range(0, n, step):
            c = attr.data[i].color_srgb
            values.add((round(c[0] * 255), round(c[1] * 255), round(c[2] * 255), round(c[3] * 255)))
            if len(values) > 4096:
                break
        all_white = values <= {(255, 255, 255, 255)}
        out.append({'name': attr.name, 'domain': attr.domain, 'data_type': attr.data_type,
                    'elements': n, 'distinct_colors_sampled': len(values),
                    'distinct_capped': len(values) > 4096,
                    'meaningful': len(values) > 1 or not all_white,
                    'is_render_color': idx == render_index})
    return out


def lod_level(name):
    m = LOD_RE.search(name)
    return int(m.group(1)) if m else None


def inspect(path):
    path = Path(path).resolve()
    before = cc.sha256(path)
    cc.load_model(path)
    meshes = cc.mesh_objects()
    report = {'source': str(path), 'source_sha256': before, 'blender': bpy.app.version_string,
              'meshes': [], 'images': [], 'armatures': [], 'actions': []}
    total_tris = 0
    for o in meshes:
        me = o.data
        tris = cc.triangle_count(me)
        total_tris += tris
        entry = {
            'object': o.name, 'mesh': me.name, 'lod': lod_level(o.name),
            'vertices': len(me.vertices), 'triangles': tris, 'polygons': len(me.polygons),
            'uv_layers': [{'name': uv.name, 'active_render': uv.active_render,
                           'stats': cc.uv_stats(me, uv.name)} for uv in me.uv_layers],
            'color_attributes': color_attribute_stats(me),
            'materials': [cc.principled_inputs(m) for m in me.materials],
            'topology': cc.topology_stats(me),
            'topology_welded': cc.topology_stats(me, weld=True),
            'skin': cc.skin_stats(o),
            'shape_keys': [k.name for k in me.shape_keys.key_blocks] if me.shape_keys else [],
        }
        report['meshes'].append(entry)
    for img in bpy.data.images:
        if img.type != 'IMAGE':
            continue
        w, h = img.size
        report['images'].append({
            'name': img.name, 'size': [w, h], 'colorspace': img.colorspace_settings.name,
            'power_of_two': w > 0 and h > 0 and (w & (w - 1)) == 0 and (h & (h - 1)) == 0,
            'packed': img.packed_file is not None, 'filepath': img.filepath})
    for o in bpy.context.scene.objects:
        if o.type == 'ARMATURE':
            report['armatures'].append({'name': o.name, 'bones': [b.name for b in o.data.bones]})
    for a in bpy.data.actions:
        report['actions'].append({'name': a.name, 'frame_range': list(a.frame_range)})
    lods = {}
    for m in report['meshes']:
        key = m['lod'] if m['lod'] is not None else 0
        lods[key] = lods.get(key, 0) + m['triangles']
    report['summary'] = {
        'mesh_objects': len(meshes), 'triangles_total': total_tris,
        'triangles_per_lod': {f'LOD{k}': v for k, v in sorted(lods.items())},
        'has_explicit_lods': any(m['lod'] is not None for m in report['meshes']),
        'any_uv': any(m['uv_layers'] for m in report['meshes']),
        'any_meaningful_vertex_color': any(c['meaningful'] for m in report['meshes'] for c in m['color_attributes']),
        'any_base_color_texture': any((mat.get('base_color') or {}).get('kind') == 'image'
                                      for m in report['meshes'] for mat in m['materials']),
        'skinned': any(m['skin'] for m in report['meshes']),
        'seam_split_only_boundaries': all(m['topology']['boundary_edges'] > 0 and
                                          m['topology_welded']['boundary_edges'] == 0
                                          for m in report['meshes']) if meshes else False,
    }
    after = cc.sha256(path)
    report['source_unchanged'] = before == after
    if before != after:
        raise RuntimeError("Source model changed during inspection")
    return report


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('model', type=Path)
    ap.add_argument('report', type=Path)
    ap.add_argument('--overwrite-outputs', action='store_true')
    a = ap.parse_args(cc.script_args())
    out = cc.guard_output(a.model, a.report, a.overwrite_outputs)
    report = inspect(a.model)
    cc.write_json(out, report)
    s = report['summary']
    print(f"Inspected {a.model.name}: {s['mesh_objects']} meshes, {s['triangles_total']} tris, "
          f"UV={s['any_uv']}, vertexColor={s['any_meaningful_vertex_color']}, "
          f"baseColorTex={s['any_base_color_texture']}, skinned={s['skinned']} -> {out}")


if __name__ == '__main__':
    main()
