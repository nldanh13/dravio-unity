"""GLB source pipeline. Blender only; no procedural replacement of source mesh or PBR."""
import base64
import json
import math
import statistics
import struct
from pathlib import Path
from urllib.parse import unquote, urlsplit

import bpy
from mathutils import Matrix, Vector


def select_only(objects, active):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active


def bounds(mesh):
    if not mesh.data.vertices:
        raise ValueError("GLB mesh has no vertices")
    coords = [v.co for v in mesh.data.vertices]
    lo = Vector(tuple(min(v[i] for v in coords) for i in range(3)))
    hi = Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    if not all(math.isfinite(x) for x in (*lo, *hi)):
        raise ValueError("Non-finite GLB mesh bounds")
    return lo, hi


def import_normalized(source, yaw_degrees):
    """Import a static dragon-only GLB, keep its topology, UV channels and materials."""
    result = bpy.ops.import_scene.gltf(filepath=str(source))
    if 'FINISHED' not in result:
        raise RuntimeError("glTF import failed")
    bpy.context.view_layer.update()
    objects = list(bpy.context.scene.objects)
    meshes = [o for o in objects if o.type == 'MESH']
    if not meshes:
        raise ValueError("GLB contains no mesh objects")
    if any(o.type == 'ARMATURE' for o in objects) or any(
        o.data.shape_keys is not None or any(m.type == 'ARMATURE' for m in o.modifiers)
        for o in meshes
    ):
        raise ValueError("Expected an unrigged static GLB without shape keys; "
                         "export a static rest-pose source first to avoid losing an existing rig.")
    # Snapshot all world matrices before removing parent links; bake node transforms.
    worlds = {o: o.matrix_world.copy() for o in meshes}
    yaw = Matrix.Rotation(math.radians(yaw_degrees), 4, 'Z')
    # glTF importer uses Blender Z-up; front is assumed -Y after the supplied yaw.
    # Do not remesh, weld, decimate, project UVs, or replace material slots.
    for o in meshes:
        if o.data.users > 1:
            o.data = o.data.copy()
        o.parent = None
        o.animation_data_clear()
        o.matrix_world = yaw @ worlds[o]
        o.vertex_groups.clear()
        select_only([o], o)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    select_only(meshes, meshes[0])
    if len(meshes) > 1:
        if 'FINISHED' not in bpy.ops.object.join():
            raise RuntimeError("Failed to join GLB meshes")
    skin = bpy.context.object
    skin.name = "Emberling_Baby_Body_GLB"
    skin.data.name = "Emberling_SourceMesh"
    for o in list(bpy.context.scene.objects):
        if o != skin:
            bpy.data.objects.remove(o, do_unlink=True)
    select_only([skin], skin)
    lo, hi = bounds(skin)
    height = hi.z - lo.z
    if height <= 1e-7 or hi.x-lo.x <= 1e-7 or hi.y-lo.y <= 1e-7:
        raise ValueError("GLB must have nondegenerate 3D bounds")
    center = Vector(((lo.x+hi.x)*.5, (lo.y+hi.y)*.5, lo.z))
    factor = .90 / height
    skin.location = -center * factor
    skin.scale = (factor, factor, factor)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0
    lo, hi = bounds(skin)
    if abs(lo.z) > 1e-5 or abs(hi.z-.90) > 1e-5 or abs(lo.x+hi.x)>1e-5:
        raise RuntimeError("Normalized mesh bounds are incorrect")
    if not skin.data.uv_layers:
        print("WARNING: GLB has no UV layers. No UVs are invented.")
    bpy.ops.file.pack_all()
    print("GLB normalized:", source, "height", hi.z-lo.z, "UV sets", len(skin.data.uv_layers),
          "material slots", len(skin.data.materials))
    return skin


def percentile(values, fraction):
    values = sorted(values)
    return values[round((len(values)-1)*fraction)]


def fitted_specs(skin, winged=False):
    """Approximate anatomical placement from normalized bounds and vertex samples.
    Returned coordinates use the existing script's x, vertical, forward convention.
    This is an initial fitting, not anatomical inference or production retargeting.
    """
    lo, hi = bounds(skin)
    h = hi.z-lo.z
    width, depth = hi.x-lo.x, hi.y-lo.y
    verts = [v.co.copy() for v in skin.data.vertices]
    low = [v for v in verts if v.z < .20*h]
    # Approximate front/back paw clusters from low vertices on each side.
    paws = {}
    for sign, label in [(-1, 'L'), (1, 'R')]:
        side = [v for v in low if v.x*sign > .025*width]
        for part, q in [('Fore', .20), ('Hind', .80)]:
            if len(side) >= 4:
                y = percentile([v.y for v in side], q)
                near = [v for v in side if abs(v.y-y) < .09*depth] or side
                x = statistics.median(v.x for v in near)
                y = statistics.median(v.y for v in near)
            else:
                x = sign*.22*width
                y = lo.y + (.36 if part=='Fore' else .59)*depth
            paws[(part,label)] = Vector((x,y,.065*h))
    fy = sum(paws[('Fore',s)].y for s in ('L','R'))*.5
    hy = sum(paws[('Hind',s)].y for s in ('L','R'))*.5
    if hy-fy < .10*depth:
        fy,hy = lo.y+.34*depth,lo.y+.58*depth
        for (part, label), p in paws.items():
            p.y = fy if part=='Fore' else hy
    rear = [v for v in verts if v.y >= hi.y-.035*depth]
    tailtip = Vector((statistics.median(v.x for v in rear), statistics.median(v.y for v in rear),
                      statistics.median(v.z for v in rear)))
    pelvis = Vector((0,hy,.39*h))
    spine = Vector((0,fy+(hy-fy)*.53,.45*h))
    chest = Vector((0,fy,.51*h))
    neck = Vector((0,lo.y+.25*depth,.64*h))
    head = Vector((0,lo.y+.16*depth,.75*h))
    tailbase = Vector((0,min(hi.y-.08*depth,hy+.07*depth),.40*h))
    mid = tailbase.lerp(tailtip,.48)
    end = tailbase.lerp(tailtip,.87)
    specs = []
    def add(name,a,b,parent):
        a,b=Vector(a),Vector(b)
        if (b-a).length < .01*h:
            b=a+Vector((0,0,.025*h))
        def design(v):return (v.x,v.z,-v.y)
        specs.append((name,design(a),design(b),parent))
    add('Root',(0,0,0),(0,0,.12*h),None)
    add('Pelvis',pelvis,spine,'Root')
    add('Spine',spine,chest,'Pelvis')
    add('Chest',chest,neck,'Spine')
    add('Neck',neck,head,'Chest')
    add('Head',head,(0,lo.y+.045*depth,.69*h),'Neck')
    add('Tail1',tailbase,mid,'Pelvis')
    add('Tail2',mid,end,'Tail1')
    add('Flame',end,tailtip,'Tail2')
    add('TailVFX_Anchor',tailtip,tailtip+Vector((0,0,.035*h)),'Flame')
    for sign,label in [(-1,'L'),(1,'R')]:
        for part, parent in [('Fore','Chest'),('Hind','Pelvis')]:
            foot=paws[(part,label)]
            upper=Vector((foot.x*.83,foot.y,.40*h if part=='Fore' else .37*h))
            knee=Vector((foot.x,foot.y+(.04 if part=='Fore' else -.04)*depth,.22*h))
            add(part+'_'+label,upper,knee,parent)
            add(part+'Lower_'+label,knee,foot,part+'_'+label)
        ex=sign*min(.17*width,.18*h)
        add('Eye_'+label,(ex,lo.y+.12*depth,.75*h),(ex,lo.y+.12*depth,.81*h),'Head')
        wingroot=Vector((sign*min(.13*width,.17*h),fy+.05*depth,.57*h))
        upper_side=[v for v in verts if v.x*sign>.28*width and v.z>.38*h]
        tip=max(upper_side,key=lambda v:v.x*sign) if winged and upper_side else Vector(
            (sign*.43*width,fy+.14*depth,.73*h))
        add('Wing_'+label,wingroot,tip,'Chest')
    return specs


def bind_automatic(skin, arm, inactive):
    """Bone heat bind, then retain and normalize the strongest four weights.
    Unweighted vertices are a hard failure, never silently rigid-bound.
    """
    select_only([skin,arm],arm)
    result=bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    if 'FINISHED' not in result or not any(m.type=='ARMATURE' and m.object==arm for m in skin.modifiers):
        raise RuntimeError("Automatic weights failed; inspect source topology in Blender")
    valid={b.name for b in arm.data.bones if b.use_deform}
    groups=skin.vertex_groups
    missing=[]
    for v in skin.data.vertices:
        original=[(g.group,g.weight) for g in v.groups]
        keep=sorted(((i,w) for i,w in original if groups[i].name in valid and
                     math.isfinite(w) and w>1e-8),key=lambda pair:pair[1],reverse=True)[:4]
        total=sum(w for i,w in keep)
        if total<=0:
            missing.append(v.index)
            continue
        for i,w in original:
            groups[i].remove([v.index])
        for i,w in keep:
            groups[i].add([v.index],w/total,'REPLACE')
    if missing:
        raise RuntimeError(f"Automatic weights left {len(missing)} unweighted vertices; "
                           f"examples: {missing[:20]}. Repair nonmanifold/disconnected topology or paint weights.")
    for m in skin.modifiers:
        if m.type=='ARMATURE':
            m.use_deform_preserve_volume=True
    # Retain anchor/root/placeholder bones in a deform-only FBX without letting
    # them steal weights during the heat solve. Imported textured eyes are not
    # physically blinkable unless separately rigged; no eye weights are invented.
    for name in inactive:
        arm.data.bones[name].use_deform=True
    print("Automatic weights normalized to <=4 influences:",len(skin.data.vertices))


def write_pbr_sidecar(source, out):
    """Extract original glTF image bytes and material/texture metadata losslessly.
    FBX material interchange cannot guarantee all glTF channels/extensions.
    This sidecar is a recovery/reference package, not a Unity material importer.
    """
    raw=source.read_bytes()
    magic,version,length=struct.unpack_from('<4sII',raw,0)
    if magic!=b'glTF' or version!=2 or length!=len(raw):
        raise ValueError("Expected valid GLB 2.0")
    pos=12
    doc=None
    binary=None
    while pos+8<=length:
        size,kind=struct.unpack_from('<II',raw,pos)
        pos+=8
        if pos+size>length:raise ValueError("Truncated GLB chunk")
        payload=raw[pos:pos+size]
        pos+=size
        if kind==0x4E4F534A:doc=json.loads(payload.decode('utf-8').rstrip(' \t\r\n\x00'))
        elif kind==0x004E4942:binary=payload
    if doc is None:raise ValueError("Missing GLB JSON chunk")
    folder=out.with_name(out.stem+"_PBR")
    folder.mkdir(parents=True,exist_ok=True)
    def uri_bytes(uri):
        if uri.startswith('data:'):
            header,data=uri.split(',',1)
            if ';base64' not in header:
                from urllib.parse import unquote_to_bytes
                return unquote_to_bytes(data),header[5:].split(';')[0]
            return base64.b64decode(data,validate=True),header[5:].split(';')[0]
        url=urlsplit(uri)
        if url.scheme or url.netloc:raise ValueError("Remote image/buffer URIs unsupported")
        return (source.parent/unquote(url.path)).read_bytes(),None
    def buffer_data(index):
        buf=doc['buffers'][index]
        if 'uri' in buf:return uri_bytes(buf['uri'])[0]
        if index==0 and binary is not None:return binary
        raise ValueError("Missing GLB buffer")
    exts={'image/png':'.png','image/jpeg':'.jpg','image/webp':'.webp','image/ktx2':'.ktx2'}
    image_records=[]
    for i,img in enumerate(doc.get('images',[])):
        mime=img.get('mimeType')
        if 'bufferView' in img:
            view=doc['bufferViews'][img['bufferView']]
            buf=buffer_data(view['buffer'])
            offset=view.get('byteOffset',0)
            size=view['byteLength']
            payload=buf[offset:offset+size]
            if len(payload)!=size:raise ValueError("Truncated image bufferView")
        else:
            payload,detected=uri_bytes(img['uri'])
            mime=mime or detected
        ext=exts.get(mime)
        if not ext:
            ext=('.png' if payload.startswith(b'\x89PNG\r\n\x1a\n') else
                 '.jpg' if payload.startswith(b'\xff\xd8') else
                 '.webp' if payload[:4]==b'RIFF' and payload[8:12]==b'WEBP' else '.bin')
        name=f"image_{i:03d}{ext}"
        (folder/name).write_bytes(payload)
        image_records.append({'index':i,'file':name,'name':img.get('name'),'mimeType':mime})
    manifest={'source':source.name,'images':image_records,'materials':doc.get('materials',[]),
              'textures':doc.get('textures',[]),'samplers':doc.get('samplers',[]),
              'extensionsUsed':doc.get('extensionsUsed',[]),
              'note':'glTF metallicRoughnessTexture: G=roughness, B=metallic; Unity smoothness=1-roughness. '
                     'Preserves source bytes and material indices, not automatic FBX shader conversion.'}
    (folder/'material-map.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
    return {'folder':folder.name,'images':len(image_records),'materials':len(manifest['materials'])}
