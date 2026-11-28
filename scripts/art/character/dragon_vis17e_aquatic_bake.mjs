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
  const dim=type==="SCALAR"?1:type==="VEC3"?3:2,step=v.byteStride??size*dim,start=(v.byteOffset??0)+(a.byteOffset??0),last=start+(a.count-1)*step+size*dim;
  if(!Number.isSafeInteger(start)||!Number.isSafeInteger(step)||start<0||step<size*dim||step%size||last>(v.byteOffset??0)+v.byteLength||last>bin.length)throw Error("accessor bounds");
  const data=new DataView(bin.buffer,bin.byteOffset,bin.byteLength);
  const get=(p)=>{let x=code===5126?data.getFloat32(p,true):code===5125?data.getUint32(p,true):code===5123?data.getUint16(p,true):data.getUint8(p);if(a.normalized)x/=code===5121?255:65535;if(!Number.isFinite(x))throw Error("nonfinite accessor");return x;};
  return Array.from({length:a.count},(_,i)=>dim===1?get(start+i*step):Array.from({length:dim},(_,k)=>get(start+i*step+k*size)));
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

function bake(rgba,protectedMask,regions,caps){
 if(rgba.length%4||protectedMask.length!==rgba.length/4||!regions.length)throw Error("dimensions or empty region set");
 const n=protectedMask.length,out=new Uint8Array(rgba),lut=Array.from({length:256},(_,i)=>{const v=i/255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4;});
 const encode=v=>Math.round(255*(v<=.0031308?12.92*v:1.055*v**(1/2.4)-.055));
 for(const r of regions){if(r.mask.length!==n||r.tone.length!==n||!Number.isFinite(r.strength)||r.strength<=0||r.strength>caps[r.study]||!Object.hasOwn(caps,r.study))throw Error("region dimensions/strength");}
 let changed=0,covered=0,protectedCount=0,clipped=0,maxDelta=0,maxFactorDelta=0;
 for(let i=0;i<n;i++){
  let weight=0,delta=0;
  for(const r of regions){
   const w=r.mask[i],g=r.tone[i];
   if(g<84||g>172)throw Error("tone range");
   weight+=w;delta+=r.strength*(w/255)*(g-128)/128;
  }
  if(weight>255)throw Error("overlap at "+i);
  if(protectedMask[i]){protectedCount++;if(weight)throw Error("protected overlap at "+i);}
  if(!weight)continue;covered++;
  if(!rgba[i*4+3]||delta===0)continue;
  maxFactorDelta=Math.max(maxFactorDelta,Math.abs(delta));let different=false;
  for(let k=0;k<3;k++){
   const j=i*4+k,v=lut[rgba[j]]*(1+delta);
   if(v>1+1e-12||v<0){clipped++;continue;}
   const q=encode(Math.min(1,v));maxDelta=Math.max(maxDelta,Math.abs(q-rgba[j]));out[j]=q;different ||= q!==rgba[j];
  }
  if(different)changed++;
 }
 if(clipped)throw Error("would clip "+clipped+" RGB channels; reduce strength or refine masks");
 if(!protectedCount)throw Error("empty protected mask");
 if(!changed)throw Error("no visible changes");
 return {out,report:{pixels:n,coveredPixels:covered,protectedPixels:protectedCount,changedPixels:changed,maxChannelDelta:maxDelta,maxLinearMultiplierDelta:maxFactorDelta,clippedChannels:clipped}};
}
function selfTest(){
 const caps={Scale:.1,Armor:.12},src=Uint8Array.from([120,95,70,255,250,230,200,255,20,25,30,0,100,90,80,128]),protect=Uint8Array.from([0,255,0,0]);
 const region=(mask,tone,strength=.1,study="Scale")=>({mask:Uint8Array.from(mask),tone:Uint8Array.from(tone),strength,study});
 const a=region([255,0,255,0],[172,128,172,128]),r=bake(src,protect,[a],caps);let count=0;
 const check=(v,msg)=>{if(!v)throw Error(msg);count++;};
 const reject=(fn,msg)=>{let fail=false;try{fn();}catch{fail=true;}check(fail,msg);};
 check(r.report.changedPixels===1,"one changed");
 check(r.out.slice(4).every((v,i)=>v===src[i+4]),"protected/transparent/unmasked exact");
 check(r.out[3]===255&&r.out[15]===128,"alpha");
 check(bake(src,protect,[a],caps).out.every((v,i)=>v===r.out[i]),"deterministic");
 reject(()=>bake(src,protect,[a,a],caps),"overlap");
 reject(()=>bake(src,protect,[region([0,255,0,0],[128,172,128,128])],caps),"protected");
 reject(()=>bake(src,protect,[region([255,0,0,0],[173,128,128,128])],caps),"range");
 reject(()=>bake(src,protect,[region([255,0,0,0],[172,128,128,128],.11)],caps),"strength");
 reject(()=>bake(src,protect,[region([255,0,0,0],[128,128,128,128])],caps),"no change");
 reject(()=>bake(Uint8Array.from([255,255,255,255,20,20,20,255]),Uint8Array.from([0,255]),[region([255,0],[172,128])],caps),"clipping");
 reject(()=>bake(src,new Uint8Array(4),[a],caps),"empty protect");
 reject(()=>bake(src,protect,[region([255,0],[172,128])],caps),"dimensions");
 return count;
}


// Match source triangle edges by exact float32 position, within a single primitive.
// No approximate weld or inferred bilateral symmetry. Check color CHANGE rather
// than absolute original color, so existing seam contrast is not mistaken for a new one.
function seamEdges(positions,uv,ids){
 if(positions.length!==uv.length||ids.length%3||ids.some(i=>!Number.isInteger(i)||i<0||i>=uv.length))throw Error("edge input");
 const edges=new Map(),key=p=>p.map(Math.fround).join(",");
 for(let i=0;i<ids.length;i+=3)for(let j=0;j<3;j++){
  let a=ids[i+j],b=ids[i+(j+1)%3],ka=key(positions[a]),kb=key(positions[b]);
  if(ka===kb)continue;
  if(ka>kb){[a,b]=[b,a];[ka,kb]=[kb,ka];}
  const k=ka+"|"+kb,entry=edges.get(k)??[];entry.push([uv[a],uv[b]]);edges.set(k,entry);
 }
 const pairs=[];let nonManifold=0,boundary=0;
 for(const e of edges.values()){
  if(e.length!==2){if(e.length===1)boundary++;else nonManifold++;continue;}
  const d=Math.max(...e[0].flat().map((v,i)=>Math.abs(v-e[1].flat()[i])));
  if(d>1e-7)pairs.push(e);
 }
 return {pairs,boundary,nonManifold};
}
function seamAudit(before,after,w,h,pairs,limit=2,mode="clamp"){
 if(!["clamp","repeat"].includes(mode))throw Error("unsupported sample mode");
 if(before.length!==w*h*4||after.length!==before.length||!pairs.length)throw Error("seam coverage");
 const clamp=(x,n)=>mode==="repeat"?((x%n+n)%n):Math.min(n-1,Math.max(0,x));
 function sample(u,v,c){
  const x=u*w-.5,y=v*h-.5,i=Math.floor(x),j=Math.floor(y),a=x-i,b=y-j;
  const px=(x,y)=>{const k=(clamp(y,h)*w+clamp(x,w))*4+c;return after[k]-before[k];};
  return (1-b)*((1-a)*px(i,j)+a*px(i+1,j))+b*((1-a)*px(i,j+1)+a*px(i+1,j+1));
 }
 let max=0,count=0,over=0;const failures=[];
 for(let i=0;i<pairs.length;i++){
  const [a,b]=pairs[i];
  // At least quarter points; adapt to edge texel length, cap at 256 subdivisions.
  const segments=Math.max(4,Math.min(256,Math.ceil(Math.max(...[a,b].map(e=>Math.hypot((e[1][0]-e[0][0])*w,(e[1][1]-e[0][1])*h))))));
  let edgeMax=0;
  for(let j=0;j<=segments;j++){const t=j/segments,uv=e=>e[0].map((v,k)=>v+(e[1][k]-v)*t),p=uv(a),q=uv(b);
   for(let c=0;c<3;c++){const d=Math.abs(sample(...p,c)-sample(...q,c));max=Math.max(max,d);edgeMax=Math.max(edgeMax,d);count++;if(d>limit+1e-8)over++;}
  }
  if(edgeMax>limit+1e-8&&failures.length<10)failures.push({edge:i,maxAddedDifference:edgeMax});
 }
 if(over)throw Error("new seam contrast exceeds proposed 2 sRGB levels: "+JSON.stringify({over,max,failures}));
 return {pairedSeamEdges:pairs.length,channelSamples:count,maxAddedDifference:max,limit,overLimit:over};
}
function getSeams(doc,bin,rec){
 if(rec.primitives.length!==1)throw Error("single primitive seam audit only");
 const p=rec.primitives[0],prim=doc.meshes[p.mesh].primitives[p.primitive];
 if(doc.accessors[prim.attributes.POSITION].componentType!==5126)throw Error("float POSITION required");
 const pos=accessor(doc,bin,prim.attributes.POSITION,"VEC3"),uv=accessor(doc,bin,prim.attributes["TEXCOORD_"+p.uvSet],"VEC2"),ids=prim.indices===undefined?uv.map((_,i)=>i):accessor(doc,bin,prim.indices,"SCALAR",true);
 const e=seamEdges(pos,uv,ids);if(!e.pairs.length||e.boundary||e.nonManifold)throw Error("source exact-position seam topology requires manual review "+JSON.stringify({pairs:e.pairs.length,boundary:e.boundary,nonManifold:e.nonManifold}));
 return e;
}
function seamSelfTest(){
 let n=0;const check=(ok,msg)=>{if(!ok)throw Error(msg);n++;},reject=(f,msg)=>{let failed=false;try{f()}catch{failed=true;}check(failed,msg);};
 const p=[[0,0,0],[1,0,0],[0,1,0],[1,0,0],[0,0,0],[1,-1,0]],uv=[[.25,.25],[.25,.75],[0,.5],[.75,.75],[.75,.25],[1,.5]];
 const e=seamEdges(p,uv,[0,1,2,3,4,5]);check(e.pairs.length===1&&e.boundary===4,"edge correspondence");
 const src=new Uint8Array(4*4*4).fill(100),dst=new Uint8Array(src).fill(101);
 check(seamAudit(src,dst,4,4,e.pairs).maxAddedDifference===0,"same change");
 for(let y=0;y<4;y++)for(let x=2;x<4;x++)for(let k=0;k<3;k++)dst[(y*4+x)*4+k]=110;
 reject(()=>seamAudit(src,dst,4,4,e.pairs),"mismatch");
 reject(()=>seamAudit(src,src,4,4,[]),"no coverage");
 check(seamEdges(p,uv,[0,1,2,3,4,5,0,1,2]).nonManifold===1,"nonmanifold");
 check(seamEdges(p,[[.25,.25],[.25,.75],[0,.5],[.25,.75],[.25,.25],[1,.5]],[0,1,2,3,4,5]).pairs.length===0,"same UV");
 return n;
}


// CPU approximation of sRGB texture mip generation, opaque source only.
// Downsample in linear RGB; quantize once per mip level. Not Unity importer,
// GPU compression, trilinear/aniso filtering or coverage of all view poses.
const MIP_LUT=Array.from({length:256},(_,i)=>{const v=i/255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4;});
function downsampleOpaque(bytes,w,h){
 if(!Number.isSafeInteger(w)||!Number.isSafeInteger(h)||w<2||h<2||w%2||h%2||bytes.length!==w*h*4)throw Error("mip dimensions");
 for(let i=3;i<bytes.length;i+=4)if(bytes[i]!==255)throw Error("opaque base color required for mip estimate");
 const nw=w/2,nh=h/2,out=new Uint8Array(nw*nh*4);
 for(let y=0;y<nh;y++)for(let x=0;x<nw;x++){
  const p=(y*nw+x)*4,a=(2*y*w+2*x)*4;
  for(let k=0;k<3;k++){
   const v=(MIP_LUT[bytes[a+k]]+MIP_LUT[bytes[a+4+k]]+MIP_LUT[bytes[a+w*4+k]]+MIP_LUT[bytes[a+w*4+4+k]])/4;
   out[p+k]=Math.round(255*(v<=.0031308?12.92*v:1.055*v**(1/2.4)-.055));
  }out[p+3]=255;
 }return {bytes:out,width:nw,height:nh};
}
function mipAudit(before,after,w,h,pairs,levels=4){
 if(!Number.isInteger(levels)||levels<1||levels>4)throw Error("mip levels");
 if(before.length!==w*h*4||after.length!==before.length)throw Error("mip length");
 for(let i=3;i<before.length;i+=4)if(before[i]!==255||after[i]!==255)throw Error("opaque mip input");
 const reports=[];
 for(let i=0;i<levels;i++){
  const report={level:i,width:w,height:h};
  // Check both boundary modes; stricter than assuming one sampler setting.
  for(const mode of ["clamp","repeat"]){
   try{report[mode]=seamAudit(before,after,w,h,pairs,2,mode);}
   catch(e){throw Error("mip "+i+" "+mode+": "+e.message);}
  }
  reports.push(report);
  if(i+1<levels){const a=downsampleOpaque(before,w,h),b=downsampleOpaque(after,w,h);before=a.bytes;after=b.bytes;w=a.width;h=a.height;}
 }
 return reports;
}
function mipSelfTest(){
 let n=0;const check=(v,m)=>{if(!v)throw Error(m);n++;},reject=(fn,m)=>{let fail=false;try{fn();}catch{fail=true;}check(fail,m);};
 const uniform=(w,h,g)=>{const a=new Uint8Array(w*h*4).fill(g);for(let i=3;i<a.length;i+=4)a[i]=255;return a;};
 const a=uniform(8,8,100),b=uniform(8,8,101),pairs=[[[1.5/8,3.5/8],[1.5/8,4.5/8]],[[5.5/8,3.5/8],[5.5/8,4.5/8]]];
 const p=[pairs];
 const d=downsampleOpaque(a,8,8);check(d.width===4&&d.height===4&&d.bytes.every((v,i)=>v===(i%4===3?255:100)),"uniform mip");
 const c=uniform(2,2,0);for(let i=0;i<2;i++)for(let k=0;k<3;k++)c[i*4+k]=255;
 check(downsampleOpaque(c,2,2).bytes[0]===188,"linear not gamma average");
 const m=mipAudit(a,b,8,8,p,4);check(m.length===4&&m.at(-1).width===1&&m.every(x=>x.clamp.maxAddedDifference===0&&x.repeat.maxAddedDifference===0),"four clean mips");
 const bleed=uniform(8,8,101);for(let y=0;y<8;y++)for(let k=0;k<3;k++)bleed[(y*8)*4+k]=126;
 check(seamAudit(a,bleed,8,8,p).maxAddedDifference===0,"hidden L0 bleed");
 reject(()=>mipAudit(a,bleed,8,8,p,4),"mip bleed rejected");
 const transparent=new Uint8Array(a);transparent[3]=128;reject(()=>downsampleOpaque(transparent,8,8),"alpha unsupported");
 reject(()=>downsampleOpaque(a,7,8),"bad dimensions");
 const edge=uniform(8,8,101);for(let y=0;y<8;y++)for(let k=0;k<3;k++)edge[(y*8+7)*4+k]=121;
 const e=[[[[0,.25],[0,.75]],[[.5,.25],[.5,.75]]]];
 check(seamAudit(a,edge,8,8,e,2,"clamp").maxAddedDifference===0,"clamp edge");
 reject(()=>seamAudit(a,edge,8,8,e,2,"repeat"),"repeat edge bleed");
 return n;
}

import {readFileSync,writeFileSync,mkdirSync,realpathSync,existsSync} from "node:fs";
import {join,resolve,dirname,basename} from "node:path";
import {createHash} from "node:crypto";
import {spawnSync} from "node:child_process";
const decodeUtf8=b=>new TextDecoder("utf-8",{fatal:true}).decode(b);
const SOURCES={
  "80e355739c41e14694431ae9f0ebe508ecfbbd6c7da49772d94701509bc3c2ce": {
    "id": "steamdrake",
    "bytes": 4142736,
    "studies": [
      "Scale"
    ]
  },
  "05c056059d29f970aac9fb10abca7ad00aa4468f7185f8cc0667738bae41738f": {
    "id": "frostfin",
    "bytes": 3501148,
    "studies": [
      "Scale"
    ]
  },
  "d21dfe4c40fe8a82c2554e9fb94b2e9e4acf7831580e08a31af2bfbcc556bd4f": {
    "id": "leviathan",
    "bytes": 3833836,
    "studies": [
      "Scale"
    ]
  }
};
const CAPS={
  "Scale": 0.1,
  "Fur": 0.08,
  "Membrane": 0.06,
  "Horn": 0.1,
  "Armor": 0.12,
  "Crystal": 0.08
};

const sha=b=>createHash("sha256").update(b).digest("hex");
function pillow(script,bytes,args=[]){
 const p=spawnSync("python3",["-c",script,...args],{input:bytes,maxBuffer:64*1024*1024});
 if(p.error||p.status!==0)throw Error("Pillow failed: "+(p.error?.message??p.stderr.toString()));return p.stdout;
}
function decode(bytes,mode,w,h){
 const p=pillow("import io,sys,struct\nfrom PIL import Image\nim=Image.open(io.BytesIO(sys.stdin.buffer.read())); im.load()\nmode=sys.argv[1]\nif im.size!=(int(sys.argv[2]),int(sys.argv[3])): raise ValueError('dimension mismatch')\nif mode=='L' and (im.mode!='L' or im.format!='PNG'): raise ValueError('L PNG required')\nif mode=='RGBA' and im.mode not in ('RGB','RGBA'): raise ValueError('RGB/RGBA required')\nsys.stdout.buffer.write(im.convert(mode).tobytes())",bytes,[mode,String(w),String(h)]);
 if(p.length!==w*h*(mode==="L"?1:4))throw Error("decoded length");return p;
}
function encodePNG(bytes,w,h){return pillow("import sys,io\nfrom PIL import Image\nim=Image.frombytes('RGBA',(int(sys.argv[1]),int(sys.argv[2])),sys.stdin.buffer.read());im.save(sys.stdout.buffer,format='PNG')",bytes,[String(w),String(h)]);}
function input(folder,record,w,h){
 if(!record||typeof record.path!=="string"||basename(record.path)!==record.path||!record.path.endsWith(".png")||!/^[a-f0-9]{64}$/.test(record.sha256))throw Error("invalid review input");
 const path=realpathSync(join(folder,record.path));if(dirname(path)!==folder)throw Error("input outside review folder");
 const bytes=readFileSync(path);if(sha(bytes)!==record.sha256)throw Error("review image hash mismatch");return decode(bytes,"L",w,h);
}
function run(){
 const [, ,source,reviewPath,outArg]=process.argv;
 if(!source||!reviewPath||!outArg)throw Error("Usage: node dragon_vis17e_aquatic_bake.mjs <source.glb> <review.json> <new-output-dir>");
 const raw=readFileSync(source),hash=sha(raw),approved=SOURCES[hash];if(!approved||raw.length!==approved.bytes)throw Error("pinned aquatic/serpentine source required");
 const reviewRaw=readFileSync(reviewPath),review=JSON.parse(reviewRaw);
 if(review.species!==approved.id||review.sourceSha256!==hash||typeof review.reviewedBy!=="string"||!review.reviewedBy.trim()||review.approval!=="manual-uv-review"||!Array.isArray(review.images)||!review.images.length)throw Error("manual review declaration required");
 const out=resolve(realpathSync(dirname(resolve(outArg))),basename(resolve(outArg)));
 if(out.split(/[\\/]/).some(x=>x.toLowerCase()==="assets")||existsSync(out))throw Error("new output outside Unity Assets required");
 const folder=realpathSync(dirname(resolve(reviewPath))),{doc,bin}=parseGlb(raw),recs=inventory(doc,bin);
 const results=[],seen=new Set();
 for(const item of review.images){
  const rec=recs.find(x=>x.image===item.image);if(!rec||seen.has(item.image)||rec.outside||!Array.isArray(item.regions)||!item.regions.length)throw Error("invalid image review");seen.add(item.image);
  if(item.width!==2048||item.height!==2048)throw Error("pinned source expected 2048 square");
  const img=doc.images[item.image],v=doc.bufferViews[img.bufferView],start=v?.byteOffset??0,end=start+v?.byteLength;
  if(!v||v.buffer!==0||start<0||end>bin.length||end<=start)throw Error("bad embedded image view");
  const embedded=bin.subarray(start,end);if(sha(embedded)!==item.embeddedSha256)throw Error("embedded base color hash mismatch");
  const src=decode(embedded,"RGBA",item.width,item.height),protect=input(folder,item.protectedMask,item.width,item.height);
  const names=new Set(),regions=item.regions.map(r=>{
   if(typeof r.id!=="string"||!r.id||names.has(r.id)||!approved.studies.includes(r.study))throw Error("region/study not approved for species");names.add(r.id);
   return {study:r.study,strength:r.strength,mask:input(folder,r.mask,item.width,item.height),tone:input(folder,r.tone,item.width,item.height)};
  });
  if(item.bodyReview!=="manual-head-tail-fin-root-tip"||item.seamReview!=="manual-chart-padding-and-mirrored-orientation")throw Error("aquatic body review declaration required");
  const e=getSeams(doc,bin,rec),b=bake(src,protect,regions,CAPS),mips=mipAudit(src,b.out,item.width,item.height,e.pairs,4),png=encodePNG(b.out,item.width,item.height),roundtrip=decode(png,"RGBA",item.width,item.height);
  if(!roundtrip.every((v,i)=>v===b.out[i]))throw Error("PNG roundtrip mismatch");
  results.push({path:"image_"+item.image+"_tone_candidate.png",png,report:{image:item.image,width:item.width,height:item.height,sha256:sha(png),embeddedSha256:item.embeddedSha256,negativeSignedUVArea:rec.mirrored,mips,...b.report}});
 }
 if(results.length!==recs.length)throw Error("review all base color images");
 if(sha(readFileSync(source))!==hash)throw Error("source changed");
 mkdirSync(out);for(const x of results)writeFileSync(join(out,x.path),x.png,{flag:"wx"});
 writeFileSync(join(out,"manifest.json"),JSON.stringify({species:approved.id,sourceSha256:hash,reviewSha256:sha(reviewRaw),reviewedBy:review.reviewedBy,scope:"source GLB UV candidate only; no FBX UV correspondence, rig or runtime approval",images:results.map(x=>({path:x.path,...x.report}))},null,2)+"\n",{flag:"wx"});
}
if(process.argv.includes("--self-test"))console.log(JSON.stringify({syntheticChecks:selfTest()+seamSelfTest()+mipSelfTest()}));else run();
