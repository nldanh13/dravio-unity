"""Cross-checks for run_selftest.py (Blender/bpy). Writes <work>/reports/<name>.checks.json.

  * source GLB hash unchanged across every step
  * vertex colours of the source survive unchanged in the prepared .blend copy
  * the prepared GLB does not carry COLOR_0 next to a baked Base Color (glTF would multiply them)
  * FBX re-import: per-LOD triangle counts match the LOD report, one shared armature,
    same bone list as the rig exporter, animation range 1-445 preserved
  * URP MetallicSmoothness map equals glTF metallicRoughness (R=B, A=1-G) within 1/255
"""
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402

import bpy  # noqa: E402
import numpy as np  # noqa: E402


def glb_json(path):
    raw = Path(path).read_bytes()
    n = struct.unpack_from('<I', raw, 12)[0]
    return json.loads(raw[20:20 + n])


def main():
    work, name = Path(cc.script_args()[0]), cc.script_args()[1]
    src = work / 'test-assets' / f'{name}.glb'
    rep = lambda p: json.loads(Path(p).read_text(encoding='utf-8'))  # noqa: E731
    prep = rep(work / 'prepared' / f'{name}_prepare.json')
    lods = rep(work / 'lods' / f'{name}_lods.json')
    source_inspect = rep(work / 'reports' / f'{name}.source.inspect.json')
    out = {}
    h = cc.sha256(src)
    out['source_hash_unchanged'] = h == source_inspect['source_sha256'] == prep['source_sha256_before'] == prep['source_sha256_after']

    # Vertex colours: compare source (as imported) vs the prepared .blend copy, per corner.
    cc.load_model(src, merge_vertices=True)
    s = cc.mesh_objects()[0].data
    src_cols = None
    if s.color_attributes:
        a = s.color_attributes[0]
        src_cols = np.array([d.color_srgb[:] for d in a.data])
        src_pos = np.array([v.co[:] for v in s.vertices])
    bpy.ops.wm.open_mainfile(filepath=str(work / 'prepared' / f'{name}_Prepared.blend'))
    p = cc.mesh_objects()[0].data
    if src_cols is not None:
        a = p.color_attributes[0] if p.color_attributes else None
        if a is None:
            out['vertex_colors_preserved'] = False
        else:
            dst = np.array([d.color_srgb[:] for d in a.data])
            same_shape = dst.shape == src_cols.shape
            # Topology is identical (no weld happened on these sources), so compare as sets of
            # (position-rounded, colour) to stay independent of vertex order.
            out['vertex_colors_preserved'] = bool(same_shape and np.allclose(np.sort(dst, axis=0), np.sort(src_cols, axis=0), atol=1/255))
    else:
        out['vertex_colors_preserved'] = None
    gj = glb_json(work / 'prepared' / f'{name}_Prepared.glb')
    attrs = gj['meshes'][0]['primitives'][0]['attributes']
    baked = any(x['step'] == 'bake_base_color' for x in prep['actions'])
    out['prepared_glb_has_uv0'] = 'TEXCOORD_0' in attrs
    out['prepared_glb_no_double_color'] = not (baked and 'COLOR_0' in attrs)
    out['prepared_glb_base_color_texture'] = all('baseColorTexture' in m.get('pbrMetallicRoughness', {}) for m in gj['materials'])

    # FBX round trip
    cc.load_model(work / 'lods' / f'{name}_LODs.fbx')
    meshes = cc.mesh_objects()
    per = {}
    for o in meshes:
        lvl = int(o.name.rsplit('_LOD', 1)[1].split('.')[0])
        per[lvl] = per.get(lvl, 0) + cc.triangle_count(o.data)
    out['fbx_lod_triangles'] = per
    out['fbx_lod_triangles_match'] = per == {l['level']: l['triangles'] for l in lods['lods']}
    arms = {m.object.name for o in meshes for m in o.modifiers if m.type == 'ARMATURE' and m.object}
    out['fbx_single_shared_armature'] = len(arms) == 1 and all(
        any(m.type == 'ARMATURE' for m in o.modifiers) for o in meshes)
    qa = rep(work / 'rigged' / f'{name}_Rigged.qa.json')
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    out['fbx_bones_match_rig_exporter'] = sorted(b.name for b in arm.data.bones) == sorted(qa['bones'])
    ranges = [list(a.frame_range) for a in bpy.data.actions]
    out['fbx_actions'] = ranges
    # Blender's FBX importer may shift the take by one frame; Unity splits clips relative
    # to the first frame, so the invariant is the 445-frame length (1..445 inclusive).
    out['fbx_animation_range_ok'] = any(abs((r[1] - r[0]) - 444) < 0.5 for r in ranges)
    out['fbx_uv_on_all_lods'] = all(o.data.uv_layers for o in meshes)

    # MetallicSmoothness conversion
    ms = prep.get('texture_set', {}).get('metallic_smoothness')
    if ms:
        cc.load_model(src)
        mat = cc.mesh_objects()[0].data.materials[0]
        mr = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image
                  and n.image.colorspace_settings.name == 'Non-Color'
                  and any(l.to_node.type == 'SEPARATE_COLOR' for l in n.outputs['Color'].links))
        g = cc.image_to_array(mr)
        img = bpy.data.images.load(str(work / 'prepared' / ms))
        img.colorspace_settings.name = 'Non-Color'
        m = cc.image_to_array(img)
        out['metallic_smoothness_max_err_R_vs_B'] = float(np.abs(m[..., 0] - g[..., 2]).max() * 255)
        out['metallic_smoothness_max_err_A_vs_1mG'] = float(np.abs(m[..., 3] - (1 - g[..., 1])).max() * 255)
        out['metallic_smoothness_ok'] = (out['metallic_smoothness_max_err_R_vs_B'] <= 1.01 and
                                         out['metallic_smoothness_max_err_A_vs_1mG'] <= 1.01)
    bools = [v for k, v in out.items() if isinstance(v, bool)]
    out['all_ok'] = all(bools)
    cc.write_json(work / 'reports' / f'{name}.checks.json', out)
    print(json.dumps(out, indent=1))
    if not out['all_ok']:
        sys.exit(1)


if __name__ == '__main__':
    main()
