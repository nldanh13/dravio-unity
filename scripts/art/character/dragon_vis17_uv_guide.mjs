// VIS.17 UV painting guide for all 16 pinned source GLBs. Read-only; no inferred region masks.
function parseGlb(bytes) {
  const b=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength);
  if(bytes.length<20||b.getUint32(0,true)!==0x46546c67||b.getUint32(4,true)!==2||b.getUint32(8,true)!==bytes.length)throw Error("invalid GLB header");
  const chunks=new Map();let p=12;
  while(p<bytes.length){
    if(p+8>bytes.length)throw Error("truncated chunk");
    const n=b.getUint32(p,true),t=b.getUint32(p+4,true);p+=8;
    if(n%4||p+n>bytes.length||chunks.has(t))throw Error("invalid chunk");
    chunks.set(t,bytes.subarray(p,p+n));p+=n;
  }
  if(!chunks.has(0x4e4f534a)||!chunks.has(0x004e4942))throw Error("JSON/BIN required");
  // JSON bytes decoded by host wrapper; synthetic core uses same UTF-8 API.
  const doc=JSON.parse(decodeUtf8(chunks.get(0x4e4f534a)));
  const bin=chunks.get(0x004e4942);
  if(doc.buffers?.length!==1||doc.buffers[0].uri||doc.buffers[0].byteLength>bin.length)throw Error("expected one embedded buffer");
  return {doc,bin};
}
function accessor(doc,bin,id,type,indices=false) {
  const a=doc.accessors?.[id],v=doc.bufferViews?.[a?.bufferView];
  if(!a||!v||a.type!==type||a.sparse||v.buffer!==0||!Number.isSafeInteger(a.count)||a.count<1)throw Error("unsupported accessor "+id);
  const code=a.componentType,size={5121:1,5123:2,5125:4,5126:4}[code];
  if(!size|| (indices ? ![5121,5123,5125].includes(code)||a.normalized : ![5121,5123,5126].includes(code)|| (code!==5126&&!a.normalized)))throw Error("unsupported accessor component");
  const dim=type==="SCALAR"?1:2,step=v.byteStride??size*dim,start=(v.byteOffset??0)+(a.byteOffset??0),last=start+(a.count-1)*step+size*dim;
  if(!Number.isSafeInteger(start)||!Number.isSafeInteger(step)||start<0||step<size*dim||step%size||last>(v.byteOffset??0)+v.byteLength||last>bin.length)throw Error("accessor bounds");
  const data=new DataView(bin.buffer,bin.byteOffset,bin.byteLength);
  const get=(p)=>{let x=code===5126?data.getFloat32(p,true):code===5125?data.getUint32(p,true):code===5123?data.getUint16(p,true):data.getUint8(p);if(a.normalized)x/=code===5121?255:65535;if(!Number.isFinite(x))throw Error("nonfinite accessor");return x;};
  return Array.from({length:a.count},(_,i)=>dim===1?get(start+i*step):[get(start+i*step),get(start+i*step+size)]);
}
function inventory(doc,bin) {
  const images=new Map(),materials=doc.materials??[];
  // Never edit an image reused by a data/normal/emission/extension texture.
  const nonBase=new Set(),materialRefs=[];
  const source=t=>doc.textures?.[t]?.source;
  function walk(x,path) {
    if(!x||typeof x!=="object")return;
    for(const [k,v] of Object.entries(x)){
      const next=path+"."+k;
      if(/Texture$/.test(k)&&Number.isInteger(v?.index)&&next!==".pbrMetallicRoughness.baseColorTexture"){
        const i=source(v.index);if(Number.isInteger(i))nonBase.add(i);
      }
      walk(v,next);
    }
  }
  for(const m of materials)walk(m,"");
  for(let mi=0;mi<(doc.meshes??[]).length;mi++)for(let pi=0;pi<doc.meshes[mi].primitives.length;pi++){
    const p=doc.meshes[mi].primitives[pi],mat=materials[p.material],info=mat?.pbrMetallicRoughness?.baseColorTexture;
    if(!info)continue;
    if((p.mode??4)!==4||p.extensions?.KHR_draco_mesh_compression||info.extensions?.KHR_texture_transform)throw Error("unsupported primitive/texture transform; manual review");
    const tex=doc.textures?.[info.index],i=tex?.source,img=doc.images?.[i],uvSet=info.texCoord??0;
    if(!Number.isInteger(i)||!img||!Number.isInteger(img.bufferView)||!["image/png","image/jpeg"].includes(img.mimeType)||tex.extensions)throw Error("unsupported base color image");
    if(nonBase.has(i))throw Error("base image shared with other map: "+i);
    if(p.attributes?.["TEXCOORD_"+uvSet]===undefined)throw Error("missing UV set");
    const uv=accessor(doc,bin,p.attributes["TEXCOORD_"+uvSet],"VEC2");
    const ids=p.indices===undefined?uv.map((_,j)=>j):accessor(doc,bin,p.indices,"SCALAR",true);
    if(ids.length%3||ids.some(j=>!Number.isInteger(j)||j<0||j>=uv.length))throw Error("invalid triangle indices");
    const rec=images.get(i)??{image:i,triangles:[],primitives:[],mirrored:0,degenerate:0,outside:0};
    const before=rec.triangles.length;
    for(let j=0;j<ids.length;j+=3){
      const [a,b,c]=ids.slice(j,j+3).map(k=>uv[k]);
      const d=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);
      if(Math.abs(d)<1e-12)rec.degenerate++;else if(d<0)rec.mirrored++;
      if([a,b,c].some(v=>v.some(x=>x<0||x>1)))rec.outside++;
      rec.triangles.push([a,b,c,d]);
    }
    rec.primitives.push({mesh:mi,primitive:pi,material:p.material,uvSet,triangleCount:rec.triangles.length-before,baseColorFactor:mat.pbrMetallicRoughness.baseColorFactor??[1,1,1,1]});
    images.set(i,rec);
  }
  if(!images.size)throw Error("no base color UV triangles");
  return [...images.values()];
}
function svgGuide(rec,w,h){
  if(!Number.isInteger(w)||!Number.isInteger(h)||w<1||h<1||w>8192||h>8192)throw Error("unsupported image size");
  const f=n=>Number(n.toFixed(3));
  const parts=rec.triangles.map(([a,b,c,d])=>({d:"M"+[a,b,c].map(v=>f(v[0]*w)+" "+f(v[1]*h)).join("L")+"Z",mirrored:d< -1e-12}));
  const paths=[false,true].map(m=>'<path fill="none" stroke="'+(m?"#ff9c4d":"#43d9ee")+'" stroke-width="0.8" stroke-opacity="0.75" d="'+parts.filter(x=>x.mirrored===m).map(x=>x.d).join(" ")+'"/>').join("");
  return '<svg xmlns="http://www.w3.org/2000/svg" width="'+w+'" height="'+h+'" viewBox="0 0 '+w+' '+h+'">'+paths+'</svg>\n';
}
function selfTest(){
  let n=0;const check=(ok,msg)=>{if(!ok)throw Error(msg);n++;};
  const buf=new Uint8Array(42),dv=new DataView(buf.buffer);
  [0,0,1,0,0,1,1,1].forEach((x,j)=>dv.setFloat32(j*4,x,true));
  [0,1,2,1,2,3].forEach((x,j)=>buf[32+j]=x);
  const d={buffers:[{byteLength:42}],bufferViews:[{buffer:0,byteOffset:0,byteLength:32},{buffer:0,byteOffset:32,byteLength:6},{buffer:0,byteOffset:38,byteLength:4}],accessors:[{bufferView:0,componentType:5126,count:4,type:"VEC2"},{bufferView:1,componentType:5121,count:6,type:"SCALAR"}],images:[{bufferView:2,mimeType:"image/png"}],textures:[{source:0}],materials:[{pbrMetallicRoughness:{baseColorTexture:{index:0}}}],meshes:[{primitives:[{material:0,indices:1,attributes:{TEXCOORD_0:0}}]}]};
  const rec=inventory(d,buf)[0];check(rec.triangles.length===2,"triangles");check(rec.mirrored===1,"mirrored");check(rec.outside===0,"bounds");check(svgGuide(rec,512,256).includes('M0 0L512 0L0 256Z'),"UV top-left");
  const reject=(fn,msg)=>{let failed=false;try{fn()}catch(e){failed=true}check(failed,msg)};
  const shared=JSON.parse(JSON.stringify(d));shared.materials[0].normalTexture={index:0};reject(()=>inventory(shared,buf),"shared image");
  const transformed=JSON.parse(JSON.stringify(d));transformed.materials[0].pbrMetallicRoughness.baseColorTexture.extensions={KHR_texture_transform:{offset:[.1,.1]}};reject(()=>inventory(transformed,buf),"transformed UV");
  const bad=JSON.parse(JSON.stringify(d));bad.accessors[0].count=100;reject(()=>inventory(bad,buf),"accessor overrun");
  const missing=JSON.parse(JSON.stringify(d));missing.meshes[0].primitives[0].attributes={};reject(()=>inventory(missing,buf),"missing UV");
  dv.setFloat32(0,1.2,true);check(inventory(d,buf)[0].outside===1,"outside UV");
  return n;
}

import {readFileSync,mkdirSync,writeFileSync,existsSync,realpathSync} from "node:fs";
import {resolve,dirname,basename,join} from "node:path";
import {createHash} from "node:crypto";
const decodeUtf8=b=>new TextDecoder("utf-8",{fatal:true}).decode(b);
const SOURCES={
  "42bffef7b055d6841e595fcc89840efa83bf3d8553a7edd0b90293f22b7a546f": {
    "id": "emberling",
    "filename": "01_emberling.glb",
    "bytes": 3942888
  },
  "1834c2c0f7b2a07849409fb76a256abb93e6c39a8206133ca1b196be12bb8954": {
    "id": "droplet",
    "filename": "02_droplet.glb",
    "bytes": 3953936
  },
  "8392e83e31874cd12a953d1fae4797b2b181376275a1221ed60f73bffbc07646": {
    "id": "sprout",
    "filename": "03_sprout.glb",
    "bytes": 4184268
  },
  "804c04f8d3a5bffd2ac48ef16658a73b803c6f6db193356959c4fe1b2e116095": {
    "id": "zephyr",
    "filename": "04_zephyr.glb",
    "bytes": 4524668
  },
  "80e355739c41e14694431ae9f0ebe508ecfbbd6c7da49772d94701509bc3c2ce": {
    "id": "steamdrake",
    "filename": "05_steamdrake.glb",
    "bytes": 4142736
  },
  "0736cace7072f5a4e0a0c6730411fd71403c3ab1cef37617c116eaf83d59036f": {
    "id": "magma",
    "filename": "06_magma.glb",
    "bytes": 3648020
  },
  "a0458fd23246199b0a85ea6b3b91324c4890d7edfc60cfe2a85531bd0f9158ee": {
    "id": "sparkwing",
    "filename": "07_sparkwing.glb",
    "bytes": 3627404
  },
  "05c056059d29f970aac9fb10abca7ad00aa4468f7185f8cc0667738bae41738f": {
    "id": "frostfin",
    "filename": "08_frostfin.glb",
    "bytes": 3501148
  },
  "56a58cc389829ee6d5797bc3f1854d44966f07c5c84dc7031e470f16d4aa8621": {
    "id": "mossback",
    "filename": "09_mossback.glb",
    "bytes": 3560776
  },
  "627fd6cce53d1db64674ea8c726717625e8bde615b9f68308f0b0bc1aa9c4279": {
    "id": "blossom",
    "filename": "10_blossom.glb",
    "bytes": 3900628
  },
  "f1cfc468579bc62d247c1ce126461fdfbcbfa55504e1e7985a0efe94bfcbb273": {
    "id": "solaris",
    "filename": "11_solaris.glb",
    "bytes": 3502444
  },
  "d21dfe4c40fe8a82c2554e9fb94b2e9e4acf7831580e08a31af2bfbcc556bd4f": {
    "id": "leviathan",
    "filename": "12_leviathan.glb",
    "bytes": 3833836
  },
  "f7052958535f6578f0352b44c268061601c35bc8351fbeeb49468234f4e147f9": {
    "id": "amberhorn",
    "filename": "13_amberhorn.glb",
    "bytes": 4277448
  },
  "a2113560571a419ffdd711e04f6a2ab877ccfac8495219cd49781af3656f29cd": {
    "id": "aurorawing",
    "filename": "14_aurorawing.glb",
    "bytes": 3376524
  },
  "c88f8f7e239b2bde5ae5b5f0aeb00ebc7d51242d88d83abbc9e0163b4385f040": {
    "id": "celestia",
    "filename": "15_celestia.glb",
    "bytes": 3508848
  },
  "70c5d8622a2b7c5e0b1e8035605b4ad0422f3be8f9d4414a31958ad694e5a78e": {
    "id": "gaia",
    "filename": "16_gaia.glb",
    "bytes": 4717212
  }
};
function imageDimensions(doc,bin,id){
  const img=doc.images[id],v=doc.bufferViews[img.bufferView],start=v?.byteOffset??0,end=start+v?.byteLength;
  if(!v||v.buffer!==0||start<0||end>bin.length||end<=start)throw Error("invalid image view");
  const data=Buffer.from(bin.subarray(start,end));
  if(img.mimeType==="image/png"){
    if(data.length<24||data.subarray(0,8).toString("hex")!=="89504e470d0a1a0a"||data.toString("ascii",12,16)!=="IHDR")throw Error("invalid PNG header");
    return [data.readUInt32BE(16),data.readUInt32BE(20)];
  }
  if(data.length<4||data.readUInt16BE(0)!==0xffd8)throw Error("invalid JPEG header");
  let p=2;
  while(p+4<=data.length){
    if(data[p++]!==255)throw Error("invalid JPEG marker");
    while(data[p]===255)p++;
    const marker=data[p++];if(marker===0xd9||marker===0xda)break;
    if(marker===0x01||(marker>=0xd0&&marker<=0xd7))continue;
    if(p+2>data.length)throw Error("truncated JPEG marker");
    const len=data.readUInt16BE(p);if(len<2||p+len>data.length)throw Error("invalid JPEG segment");
    if([0xc0,0xc1,0xc2,0xc3,0xc5,0xc6,0xc7,0xc9,0xca,0xcb,0xcd,0xce,0xcf].includes(marker)){
      if(len<8)throw Error("invalid JPEG SOF");return [data.readUInt16BE(p+5),data.readUInt16BE(p+3)];
    }
    p+=len;
  }
  throw Error("JPEG dimensions missing");
}
if(process.argv.includes("--self-test"))console.log(JSON.stringify({syntheticChecks:selfTest()}));
else {
  const [, ,source,outArg]=process.argv;
  if(!source||!outArg)throw Error("Usage: node emberling_uv_guide.mjs <pinned-species.glb> <new-output-directory>");
  const raw=readFileSync(source),hash=createHash("sha256").update(raw).digest("hex");
  const approved=SOURCES[hash];
  if(!approved||raw.length!==approved.bytes)throw Error("source hash/length mismatch: only pinned VIS17 sources supported");
  const out=resolve(realpathSync(dirname(resolve(outArg))),basename(resolve(outArg)));
  if(out.split(/[\\/]/).some(x=>x.toLowerCase()==="assets")||existsSync(out))throw Error("new output folder outside Unity Assets required");
  const {doc,bin}=parseGlb(raw);
  const images=inventory(doc,bin).map(rec=>{
    const [w,h]=imageDimensions(doc,bin,rec.image);
    const svg=svgGuide(rec,w,h);
    return {path:"image_"+rec.image+"_uv.svg",svg,report:{image:rec.image,width:w,height:h,triangles:rec.triangles.length,negativeSignedUVArea:rec.mirrored,degenerateUV:rec.degenerate,outsideUnitUV:rec.outside,primitives:rec.primitives}};
  });
  mkdirSync(out);
  for(const im of images)writeFileSync(join(out,im.path),im.svg,{flag:"wx"});
  writeFileSync(join(out,"manifest.json"),JSON.stringify({species:approved.id,sourceFilename:approved.filename,sourceSha256:hash,scope:"UV guide only; negative area is orientation not proof of overlap; no region masks or texture modification",images:images.map(im=>({...im.report,path:im.path}))},null,2)+"\n",{flag:"wx"});
}
