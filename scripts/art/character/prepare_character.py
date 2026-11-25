"""Prepare a static source character: weld seam splits, create UV0 if missing and
bake existing colour data (vertex colour, constant or textured Base Color) into a
Base Color texture. The source file is never modified.

Usage:
  blender --factory-startup --background --python-exit-code 1 \
      --python scripts/art/character/prepare_character.py -- <source.glb> <out_dir> [options]

Outputs in <out_dir>:
  <Name>_Prepared.glb        prepared static model (UV0 + baked Base Color if applicable)
  <Name>_BaseColor.png       baked texture (only if a bake happened)
  <Name>_Prepared.blend      editable copy
  <Name>_prepare.json        what was done, colour-error metrics, source hash before/after

Decisions (all reported):
  * UV0 missing                      -> Smart UV Project (or --uv-method lightmap)
  * Base Color from vertex colour    -> bake to texture (keeps the colour attribute unless --drop-vertex-color)
  * Base Color already a texture on a non-overlapping UV0 -> nothing baked (use --force-bake to rebake)
  * Overlapping UV0 + needs bake     -> a separate non-overlapping UV is generated and becomes UV0;
                                        the original UV is kept as UV1 for any other maps.
"""
import argparse
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import char_common as cc  # noqa: E402

import bpy  # noqa: E402
import numpy as np  # noqa: E402

BAKE_UV = 'UV_Bake'


def base_color_source(mat):
    info = cc.principled_inputs(mat)
    return (info.get('base_color') or {}).get('kind'), info


def uv_overlap_fraction(mesh, uv_name, res=512):
    """Fraction of covered texels hit by more than one triangle (rasterised at res x res)."""
    mesh.calc_loop_triangles()
    uv = mesh.uv_layers[uv_name].data
    count = np.zeros((res, res), dtype=np.uint16)
    for t in mesh.loop_triangles:
        p = np.array([uv[l].uv[:] for l in t.loops]) * res
        x0, y0 = np.floor(p.min(0)).astype(int)
        x1, y1 = np.ceil(p.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, res), min(y1, res)
        if x1 <= x0 or y1 <= y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
        (ax, ay), (bx, by), (cx, cy) = p
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-12:
            continue
        l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
        l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
        l3 = 1 - l1 - l2
        inside = (l1 > 1e-3) & (l2 > 1e-3) & (l3 > 1e-3)  # strict interior: shared edges are not overlap
        count[y0:y1, x0:x1] += inside.astype(np.uint16)
    covered = (count > 0).sum()
    return float((count > 1).sum() / covered) if covered else 0.0


def generate_uv(obj, method, name):
    me = obj.data
    layer = me.uv_layers.new(name=name)
    name = layer.name  # RNA pointers to UV layers are invalidated by edit-mode toggles
    me.uv_layers.active = layer
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    if method == 'lightmap':
        bpy.ops.uv.lightmap_pack(PREF_CONTEXT='ALL_FACES', PREF_MARGIN_DIV=0.3)
    else:
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003,
                                 area_weight=1.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=0.003, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    return name


def make_uv0(me, name):
    """Move UV layer `name` to index 0 (Unity UV0) while keeping the others."""
    names = [l.name for l in me.uv_layers]
    if names and names[0] == name:
        return
    # Copy every layer's data, rebuild in the new order.
    data = {n: np.array([d.uv[:] for d in me.uv_layers[n].data]) for n in names}
    order = [name] + [n for n in names if n != name]
    for n in names:
        me.uv_layers.remove(me.uv_layers[n])
    for n in order:
        layer = me.uv_layers.new(name=n)
        layer.data.foreach_set('uv', data[n].ravel())
    me.uv_layers.active_index = 0
    me.uv_layers[0].active_render = True


def pin_texture_uvs(mat, uv_name):
    """Make every image node without an explicit vector input sample `uv_name`, so changing
    the active/render UV map for baking does not change what the source textures sample."""
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type == 'TEX_IMAGE' and not n.inputs['Vector'].is_linked:
            uvn = nt.nodes.new('ShaderNodeUVMap')
            uvn.uv_map = uv_name
            nt.links.new(uvn.outputs['UV'], n.inputs['Vector'])


def bake_base_color(obj, size, margin, samples, target_uv, source_uv):
    me = obj.data
    img = bpy.data.images.new(f'{obj.name}_BaseColor', size, size, alpha=False, float_buffer=True)
    img.colorspace_settings.name = 'Linear Rec.709'
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = samples
    scene.render.bake.margin = margin
    scene.render.bake.margin_type = 'EXTEND'
    scene.render.bake.use_clear = True
    temp_nodes = []
    restore = []
    for mat in me.materials:
        if mat is None or mat.node_tree is None:
            continue
        nt = mat.node_tree
        if source_uv:
            pin_texture_uvs(mat, source_uv)
        bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
        out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output)
        old = out.inputs['Surface'].links[0].from_socket if out.inputs['Surface'].is_linked else None
        emit = nt.nodes.new('ShaderNodeEmission')
        emit.inputs['Strength'].default_value = 1.0
        bc = bsdf.inputs['Base Color']
        if bc.is_linked:
            nt.links.new(bc.links[0].from_socket, emit.inputs['Color'])
        else:
            emit.inputs['Color'].default_value = bc.default_value
        nt.links.new(emit.outputs['Emission'], out.inputs['Surface'])
        tex = nt.nodes.new('ShaderNodeTexImage')
        tex.image = img
        uvn = nt.nodes.new('ShaderNodeUVMap'); uvn.uv_map = target_uv
        nt.links.new(uvn.outputs['UV'], tex.inputs['Vector'])
        nt.nodes.active = tex
        tex.select = True
        temp_nodes.append((nt, [emit, tex, uvn]))
        restore.append((nt, out, old))
    me.uv_layers.active = me.uv_layers[target_uv]
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    r = bpy.ops.object.bake(type='EMIT', margin=margin, use_clear=True)
    if 'FINISHED' not in r:
        raise RuntimeError('Bake failed')
    for nt, out, old in restore:
        if old is not None:
            nt.links.new(old, out.inputs['Surface'])
    for nt, nodes in temp_nodes:
        for n in nodes:
            nt.nodes.remove(n)
    return img


def expected_and_baked(obj, img_arr, target_uv, source_kind, source_uv, src_tex_arr):
    """Per-triangle colour comparison at 4 interior points (centroid + 3 inset corners).
    Expected = what the source shader produced (vertex colour interpolation, constant,
    or source texture lookup); baked = bilinear lookup of the baked texture. Linear RGB."""
    me = obj.data
    me.calc_loop_triangles()
    tuv = me.uv_layers[target_uv].data
    bary = np.array([[1/3, 1/3, 1/3], [2/3, 1/6, 1/6], [1/6, 2/3, 1/6], [1/6, 1/6, 2/3]])
    tris = me.loop_triangles
    loops = np.array([t.loops[:] for t in tris])           # (n, 3)
    mats = np.array([t.material_index for t in tris])
    uv_t = np.array([d.uv[:] for d in tuv])[loops]          # (n, 3, 2)
    pts_uv = np.einsum('kj,njc->nkc', bary, uv_t).reshape(-1, 2)
    baked = cc.sample_bilinear(img_arr, pts_uv)[:, :3]
    if source_kind == 'vertex_color':
        attr = me.color_attributes[me.color_attributes.render_color_index]
        if attr.domain == 'POINT':
            vidx = np.array([me.loops[i].vertex_index for i in range(len(me.loops))])
            cols = np.array([d.color[:3] for d in attr.data])[vidx]
        else:
            cols = np.array([d.color[:3] for d in attr.data])
        expected = np.einsum('kj,njc->nkc', bary, cols[loops]).reshape(-1, 3)
    elif source_kind == 'image':
        suv = np.array([d.uv[:] for d in me.uv_layers[source_uv].data])[loops]
        spts = np.einsum('kj,njc->nkc', bary, suv).reshape(-1, 2)
        s = cc.sample_bilinear(src_tex_arr, spts)[:, :3]
        expected = cc.srgb_to_linear(s)  # byte sRGB texture -> linear
    else:  # constant per material
        consts = []
        for m in me.materials:
            v = m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value if m else (0.8,) * 4
            consts.append(v[:3])
        expected = np.repeat(np.array(consts)[mats], 4, axis=0)
    # Compare in 8-bit sRGB units, what an artist sees.
    e8 = cc.linear_to_srgb(expected) * 255
    b8 = cc.linear_to_srgb(baked) * 255
    diff = np.abs(e8 - b8).max(axis=1)
    return {'samples': int(len(diff)), 'mean_abs_srgb8': float(diff.mean()),
            'p95_abs_srgb8': float(np.percentile(diff, 95)),
            'p99_abs_srgb8': float(np.percentile(diff, 99)), 'max_abs_srgb8': float(diff.max()),
            'within_2_srgb8_pct': float((diff <= 2).mean() * 100)}


def finalize_material(obj, img, target_uv):
    for mat in obj.data.materials:
        if mat is None:
            continue
        nt = mat.node_tree
        bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
        bc = bsdf.inputs['Base Color']
        for l in list(bc.links):
            nt.links.remove(l)
        tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.name = 'Dravio_BakedBaseColor'
        uvn = nt.nodes.new('ShaderNodeUVMap'); uvn.uv_map = target_uv
        nt.links.new(uvn.outputs['UV'], tex.inputs['Vector'])
        nt.links.new(tex.outputs['Color'], bc)


def _socket_image_channel(mat, socket_name):
    """(image, channel_index) feeding a Principled socket directly or via Separate Color/RGB."""
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None or not bsdf.inputs[socket_name].is_linked:
        return None, None
    link = bsdf.inputs[socket_name].links[0]
    node = link.from_node
    if node.type == 'TEX_IMAGE' and node.image:
        return node.image, 0
    if node.type in {'SEPARATE_COLOR', 'SEPRGB'}:
        ch = {'Red': 0, 'R': 0, 'Green': 1, 'G': 1, 'Blue': 2, 'B': 2}.get(link.from_socket.name)
        inp = node.inputs[0]
        if ch is not None and inp.is_linked and inp.links[0].from_node.type == 'TEX_IMAGE':
            return inp.links[0].from_node.image, ch
    return None, None


def _write_png(path, arr, colorspace):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(path.stem, w, h, alpha=True)
    img.colorspace_settings.name = colorspace
    img.pixels.foreach_set(np.ascontiguousarray(arr, dtype=np.float32).ravel())
    img.filepath_raw = str(path)
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def export_texture_set(obj, out_dir, name, overwrite, source, baked):
    """Unity/URP-ready maps next to the GLB, independent of FBX material conversion:
    <Name>_Normal.png (copied, OpenGL/+Y as in glTF) and <Name>_MetallicSmoothness.png
    (R = metallic, A = smoothness = 1 - roughness; URP Lit 'Metallic Map' layout), built from
    the glTF metallicRoughness texture (G = roughness, B = metallic) or constants."""
    written = {}
    mats = [m for m in obj.data.materials if m and m.node_tree]
    if not mats:
        return written
    if len(mats) > 1:
        return {'skipped': 'more than one material; export per-material maps manually'}
    mat = mats[0]
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    rimg, rch = _socket_image_channel(mat, 'Roughness')
    mimg, mch = _socket_image_channel(mat, 'Metallic')
    if rimg or mimg:
        ref = rimg or mimg
        w, h = ref.size
        def channel(img, ch, const):
            if img is None:
                return np.full((h, w), const, dtype=np.float32)
            arr = cc.image_to_array(img)
            if arr.shape[:2] != (h, w):
                raise SystemExit('Roughness and metallic maps differ in size; resample manually')
            return arr[..., ch]
        rough = channel(rimg, rch, bsdf.inputs['Roughness'].default_value)
        metal = channel(mimg, mch, bsdf.inputs['Metallic'].default_value)
        out = np.zeros((h, w, 4), dtype=np.float32)
        out[..., 0] = metal
        out[..., 3] = 1.0 - rough
        p = cc.guard_output(source, out_dir / f'{name}_MetallicSmoothness.png', overwrite)
        _write_png(p, out, 'Non-Color')
        written['metallic_smoothness'] = p.name
    nimg = None
    if bsdf.inputs['Normal'].is_linked:
        nm = bsdf.inputs['Normal'].links[0].from_node
        if nm.type == 'NORMAL_MAP' and nm.inputs['Color'].is_linked:
            src = nm.inputs['Color'].links[0].from_node
            nimg = src.image if src.type == 'TEX_IMAGE' else None
    if nimg is not None:
        p = cc.guard_output(source, out_dir / f'{name}_Normal.png', overwrite)
        _write_png(p, cc.image_to_array(nimg), 'Non-Color')
        written['normal'] = p.name
    bimg, _ = _socket_image_channel(mat, 'Base Color')
    if bimg is not None and not baked:
        p = cc.guard_output(source, out_dir / f'{name}_BaseColor.png', overwrite)
        _write_png(p, cc.image_to_array(bimg), 'sRGB')
        written['base_color'] = p.name
    return written


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('source', type=Path)
    ap.add_argument('out_dir', type=Path)
    ap.add_argument('--name', help='Output base name (default: source stem)')
    ap.add_argument('--texture-size', type=int, default=1024, choices=[256, 512, 1024, 2048])
    ap.add_argument('--margin', type=int, default=16, help='Bake bleed margin in pixels (mip safety)')
    ap.add_argument('--samples', type=int, default=4)
    ap.add_argument('--uv-method', choices=['smart', 'lightmap'], default='smart')
    ap.add_argument('--force-bake', action='store_true')
    ap.add_argument('--drop-vertex-color', action='store_true',
                    help='Also remove colour attributes from the editable .blend copy')
    ap.add_argument('--export-vertex-color', action='store_true',
                    help='Write COLOR_0 into the prepared GLB. Off by default: glTF multiplies COLOR_0 '
                         'into Base Color, so keeping it next to a baked texture would apply the colour twice')
    ap.add_argument('--no-weld', action='store_true')
    ap.add_argument('--overwrite-outputs', action='store_true')
    a = ap.parse_args(cc.script_args())
    src = a.source.resolve()
    if src.suffix.lower() not in {'.glb', '.gltf'}:
        raise SystemExit('prepare_character expects a static .glb/.gltf source')
    name = a.name or src.stem
    outs = {k: cc.guard_output(src, a.out_dir / f'{name}{suffix}', a.overwrite_outputs)
            for k, suffix in [('glb', '_Prepared.glb'), ('png', '_BaseColor.png'),
                              ('blend', '_Prepared.blend'), ('json', '_prepare.json')]}
    before = cc.sha256(src)
    cc.load_model(src, merge_vertices=True)
    meshes = cc.mesh_objects()
    if any(o.type == 'ARMATURE' for o in bpy.context.scene.objects) or any(o.data.shape_keys for o in meshes):
        raise SystemExit('Expected a static, unrigged source without shape keys (rig after preparation).')
    # One object keeps the bake simple; the existing rig pipeline joins meshes anyway.
    for o in meshes:
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=False)
    me = obj.data
    report = {'source': str(src), 'source_sha256_before': before, 'name': name,
              'input': {'objects_joined': len(meshes), 'triangles': cc.triangle_count(me),
                        'uv_layers': [l.name for l in me.uv_layers],
                        'color_attributes': [c.name for c in me.color_attributes],
                        'materials': [cc.principled_inputs(m) for m in me.materials]},
              'actions': []}
    report['input']['topology_after_gltf_merge'] = cc.topology_stats(me)
    if not a.no_weld:
        welded = cc.weld_coincident_boundaries(obj)
        report['actions'].append({'step': 'weld_coincident_boundaries', 'vertices_welded': welded})
    report['topology'] = cc.topology_stats(me)
    if not me.materials:
        mat = bpy.data.materials.new(f'{name}_Default')
        me.materials.append(mat)
        report['actions'].append({'step': 'added_default_material'})

    kinds = set()
    for m in me.materials:
        k, _ = base_color_source(m)
        kinds.add(k)
    kinds.discard(None)
    vc_meaningful = any(len({tuple(round(x, 3) for x in c.data[i].color)
                             for i in range(0, len(c.data), max(1, len(c.data) // 3000))}) > 1
                        for c in me.color_attributes)
    if 'vertex_color' in kinds:
        source_kind = 'vertex_color'
    elif 'image' in kinds:
        source_kind = 'image'
    else:
        source_kind = 'constant'
    if source_kind == 'constant' and vc_meaningful:
        # Colour attribute present but the material ignores it (common after glTF import of
        # files whose material does not reference COLOR_0). Treat it as the colour source,
        # and say so: an artist must confirm that the vertex colour is the intended albedo.
        source_kind = 'vertex_color'
        report['actions'].append({'step': 'assume_vertex_color_is_albedo',
                                  'note': 'Material did not reference the colour attribute; '
                                          'vertex colour used as Base Color. Confirm visually.'})
        for m in me.materials:
            nt = m.node_tree
            vc = nt.nodes.new('ShaderNodeVertexColor')
            vc.layer_name = me.color_attributes[me.color_attributes.render_color_index].name
            nt.links.new(vc.outputs['Color'], nt.nodes['Principled BSDF'].inputs['Base Color'])
    report['base_color_source'] = source_kind

    had_uv = bool(me.uv_layers)
    source_uv = me.uv_layers[0].name if had_uv else None
    needs_bake = source_kind == 'vertex_color' or (a.force_bake and source_kind == 'image')
    target_uv = source_uv
    if not had_uv:
        target_uv = generate_uv(obj, a.uv_method, 'UVMap')
        report['actions'].append({'step': 'generate_uv', 'method': a.uv_method, 'uv': target_uv})
    elif needs_bake:
        overlap = uv_overlap_fraction(me, source_uv)
        report['source_uv_overlap_fraction'] = overlap
        if overlap > 0.002:
            target_uv = generate_uv(obj, a.uv_method, BAKE_UV)
            report['actions'].append({'step': 'generate_bake_uv', 'reason': f'source UV overlap {overlap:.3%}',
                                      'uv': target_uv, 'kept_original_uv_as': 'UV1'})
    if target_uv:
        report['uv_stats'] = cc.uv_stats(me, target_uv)
        report['uv_overlap_fraction'] = uv_overlap_fraction(me, target_uv)

    if needs_bake:
        src_tex_arr = None
        if source_kind == 'image':
            img_node = next(n for m in me.materials for n in m.node_tree.nodes
                            if n.type == 'TEX_IMAGE' and n.image)
            src_tex_arr = cc.image_to_array(img_node.image)
        img = bake_base_color(obj, a.texture_size, a.margin, a.samples, target_uv,
                              source_uv if source_kind == 'image' else None)
        report['color_error_float_bake'] = expected_and_baked(
            obj, cc.image_to_array(img), target_uv, source_kind, source_uv, src_tex_arr)
        # Save as 8-bit sRGB PNG (what Unity imports) and verify the saved file, not the buffer.
        img.filepath_raw = str(outs['png'])
        img.file_format = 'PNG'
        scene = bpy.context.scene
        scene.render.image_settings.file_format = 'PNG'
        scene.render.image_settings.color_depth = '8'
        # save_render applies the scene view transform; AgX/Filmic would shift albedo.
        scene.view_settings.view_transform = 'Standard'
        scene.view_settings.look = 'None'
        scene.view_settings.exposure = 0.0
        scene.view_settings.gamma = 1.0
        scene.display_settings.display_device = 'sRGB'
        scene.sequencer_colorspace_settings.name = 'sRGB'
        img.save_render(str(outs['png']), scene=scene)
        saved = bpy.data.images.load(str(outs['png']))
        saved.colorspace_settings.name = 'sRGB'
        arr = cc.image_to_array(saved).copy()
        arr[..., :3] = cc.srgb_to_linear(arr[..., :3])  # byte image pixels are sRGB encoded
        report['color_error_png'] = expected_and_baked(obj, arr, target_uv, source_kind, source_uv, src_tex_arr)
        finalize_material(obj, saved, target_uv)
        bpy.data.images.remove(img)
        saved.pack()
        report['actions'].append({'step': 'bake_base_color', 'from': source_kind, 'size': a.texture_size,
                                  'margin_px': a.margin, 'uv': target_uv, 'png': outs['png'].name})
    else:
        report['actions'].append({'step': 'no_bake', 'reason': f'Base Color source is {source_kind}'})
    if target_uv and me.uv_layers[0].name != target_uv:
        make_uv0(me, target_uv)
        report['actions'].append({'step': 'set_uv0', 'uv': target_uv})
    if a.drop_vertex_color:
        for c in list(me.color_attributes):
            me.color_attributes.remove(c)
        report['actions'].append({'step': 'drop_vertex_color'})
    obj.name = name
    report['texture_set'] = export_texture_set(obj, a.out_dir, name, a.overwrite_outputs, src, needs_bake)
    bpy.ops.wm.save_as_mainfile(filepath=str(outs['blend']), copy=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    r = bpy.ops.export_scene.gltf(filepath=str(outs['glb']), use_selection=True, export_format='GLB',
                                  export_yup=True, export_apply=True,
                                  export_vertex_color='ACTIVE' if a.export_vertex_color and not a.drop_vertex_color
                                  else 'NONE')
    if 'FINISHED' not in r:
        raise RuntimeError('GLB export failed')
    report['output'] = {'glb': outs['glb'].name, 'triangles': cc.triangle_count(me),
                        'uv_layers': [l.name for l in me.uv_layers],
                        'color_attributes': [c.name for c in me.color_attributes]}
    after = cc.sha256(src)
    report['source_sha256_after'] = after
    report['source_unchanged'] = before == after
    if before != after:
        raise RuntimeError('Source changed during preparation')
    cc.write_json(outs['json'], report)
    err = report.get('color_error_png')
    print(f"Prepared {src.name} -> {outs['glb'].name}; base colour source={source_kind}; "
          + (f"bake error p95={err['p95_abs_srgb8']:.2f}/255 max={err['max_abs_srgb8']:.1f}" if err else 'no bake'))


if __name__ == '__main__':
    main()
