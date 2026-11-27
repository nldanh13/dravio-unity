// Read-only QA for Bright Fantasy source sprites. No runtime asset import.
function expectedSlots(c) {
  const e = new Map();
  for (const s of c.slots) for (const state of s.states) {
    const k=s.id+":"+state;
    if(e.has(k)) throw Error("duplicate contract "+k);
    e.set(k,{path:state==="normal"?s.path:s.path.replace(/\.png$/,"_"+state+".png"),w:s.sourceWidth,h:s.sourceHeight,b:s.borderPx});
  }
  for (const [kind,g] of [["icon",c.icons],["item",c.items]]) for(const id of g.ids) {
    const k=kind+":"+id+":normal";
    if(e.has(k)) throw Error("duplicate contract "+k);
    e.set(k,{path:g.pathPattern.replace("{id}",id),w:g.sourceSize,h:g.sourceSize,b:[0,0,0,0],coverage:true});
  }
  return e;
}
const key=a=>a.id+":"+a.state;
const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
function auditManifest(contract, assets, {complete=true,tree=null}={}) {
  const expected=expectedSlots(contract), seen=new Set(), paths=new Set(), errors=[];
  for(const a of assets) {
    const k=key(a),e=expected.get(k);
    if(seen.has(k)) errors.push(k+": duplicate ID/state");
    seen.add(k);
    if(paths.has(a.path)) errors.push(k+": duplicate source path");
    paths.add(a.path);
    if(!e) {errors.push(k+": unexpected slot");continue;}
    if(a.runtimeCandidate!==e.path) errors.push(k+": runtime path");
    if(a.width!==e.w||a.height!==e.h) errors.push(k+": source size");
    if(!equal(a.borderLTRB,e.b)) errors.push(k+": border");
    if(!Array.isArray(a.borderLTRB)||a.borderLTRB.length!==4||
       a.borderLTRB.some(x=>!Number.isInteger(x)||x<0)||
       a.borderLTRB[0]+a.borderLTRB[2]>=a.width||
       a.borderLTRB[1]+a.borderLTRB[3]>=a.height) errors.push(k+": invalid slice extent");
    if(!a.alpha||["transparent","partial","opaque"].some(n=>!Number.isInteger(a.alpha[n])||a.alpha[n]<0)||
       a.alpha.transparent+a.alpha.partial+a.alpha.opaque!==a.width*a.height) errors.push(k+": alpha count");
    if(!/^[a-f0-9]{40}$/.test(a.blob)||!Number.isInteger(a.bytes)||a.bytes<1) errors.push(k+": invalid blob/size");
    if(tree) {
      const t=tree.get(a.path);
      if(!t||t.sha!==a.blob||t.size!==a.bytes) errors.push(k+": Git tree mismatch");
    }
  }
  if(complete) for(const k of expected.keys()) if(!seen.has(k)) errors.push(k+": missing");
  // Partial alternative sets must supply every state of each overridden slot.
  if(!complete) for(const id of new Set(assets.map(x=>x.id))) {
    for(const k of expected.keys()) if(k.startsWith(id+":")&&!seen.has(k)) errors.push(k+": incomplete alternative");
  }
  return {expected:expected.size,actual:seen.size,errors};
}
function paeth(a,b,c) {
  const p=a+b-c,pa=Math.abs(p-a),pb=Math.abs(p-b),pc=Math.abs(p-c);
  return pa<=pb&&pa<=pc?a:pb<=pc?b:c;
}
function unfilter(raw,w,h) {
  const stride=w*4;
  if(raw.length!==h*(stride+1)) throw Error("invalid scanline size");
  const rgba=new Uint8Array(w*h*4);
  for(let y=0;y<h;y++) {
    const filter=raw[y*(stride+1)];
    if(filter>4) throw Error("unsupported PNG filter");
    for(let x=0;x<stride;x++) {
      const i=y*stride+x, a=x>=4?rgba[i-4]:0, b=y?rgba[i-stride]:0,c=y&&x>=4?rgba[i-stride-4]:0;
      const pred=[0,a,b,Math.floor((a+b)/2),paeth(a,b,c)][filter];
      rgba[i]=(raw[y*(stride+1)+1+x]+pred)&255;
    }
  }
  return rgba;
}
function inspectPixels(rgba,w,h) {
  if(rgba.length!==w*h*4) throw Error("bad RGBA size");
  const alpha=new Uint8Array(w*h),counts={transparent:0,partial:0,opaque:0};
  let xmin=w,ymin=h,xmax=-1,ymax=-1,edge=0;
  for(let i=0;i<alpha.length;i++) {
    const a=rgba[4*i+3],x=i%w,y=Math.floor(i/w);alpha[i]=a;
    counts[a===0?"transparent":a===255?"opaque":"partial"]++;
    if(a) {
      xmin=Math.min(xmin,x);xmax=Math.max(xmax,x);ymin=Math.min(ymin,y);ymax=Math.max(ymax,y);
      if(x===0||x===w-1||y===0||y===h-1) edge++;
    }
  }
  const bounds=xmax<0?null:[xmin,ymin,xmax+1,ymax+1]; // exclusive max
  const coverage=bounds?Math.max((xmax-xmin+1)/w,(ymax-ymin+1)/h):0;
  return {alpha,counts,bounds,coverage,edge};
}
function pixelChecks(a,p,w,h,{coverage=false}={}) {
  const errors=[],warnings=[];
  if(a.width!==w||a.height!==h) errors.push("decoded size");
  if(!p.bounds) errors.push("empty alpha");
  for(const n of ["transparent","partial","opaque"]) if(a.alpha?.[n]!==p.counts[n]) errors.push("decoded alpha "+n);
  if(coverage&&(p.coverage<.75||p.coverage>.85)) errors.push("icon/item coverage outside 75–85%");
  if(p.edge) warnings.push("nonzero alpha touches canvas edge: "+p.edge);
  if(a.bounds&&!equal(a.bounds,p.bounds)) warnings.push("manifest bbox differs (review bbox convention)");
  return {errors,warnings};
}
function selfTest() {
  let n=0;
  const check=(ok,msg)=>{if(!ok)throw Error("self-test: "+msg);n++;};
  const c={slots:[{id:"b",states:["normal","pressed"],path:"Assets/b.png",sourceWidth:4,sourceHeight:4,borderPx:[1,1,1,1]}],icons:{ids:[]},items:{ids:[]}};
  const a={id:"b",state:"normal",path:"ArtSource/b.png",runtimeCandidate:"Assets/b.png",width:4,height:4,borderLTRB:[1,1,1,1],alpha:{transparent:12,partial:0,opaque:4},blob:"a".repeat(40),bytes:100};
  const b={...a,state:"pressed",path:"ArtSource/b_pressed.png",runtimeCandidate:"Assets/b_pressed.png"};
  check(auditManifest(c,[a,b]).errors.length===0,"complete");
  check(auditManifest(c,[a]).errors.some(x=>x.includes("missing")),"missing");
  check(auditManifest(c,[a],{complete:false}).errors.some(x=>x.includes("incomplete")),"partial state");
  check(auditManifest(c,[a,a]).errors.some(x=>x.includes("duplicate")),"duplicate");
  check(auditManifest(c,[{...a,borderLTRB:[2,2,2,2]},b]).errors.some(x=>x.includes("extent")),"invalid border");
  check(auditManifest(c,[a,b],{tree:new Map()}).errors.some(x=>x.includes("Git tree")),"tree");
  const rgba=Uint8Array.from({length:16},(_,i)=>(i*31+17)%256);
  for(let f=0;f<=4;f++) {
    const encoded=new Uint8Array(18);
    for(let y=0;y<2;y++) {
      encoded[y*9]=f;
      for(let x=0;x<8;x++) {
        const i=y*8+x,l=x>=4?rgba[i-4]:0,u=y?rgba[i-8]:0,ul=y&&x>=4?rgba[i-12]:0;
        encoded[y*9+1+x]=(rgba[i]-[0,l,u,Math.floor((l+u)/2),paeth(l,u,ul)][f])&255;
      }
    }
    check(equal(Array.from(unfilter(encoded,2,2)),Array.from(rgba)),"filter "+f);
  }
  const pix=new Uint8Array(64);for(const i of [5,6,9,10])pix[4*i+3]=255;
  const p=inspectPixels(pix,4,4);
  check(equal(p.bounds,[1,1,3,3]),"bbox");
  check(pixelChecks(a,p,4,4).errors.length===0,"alpha");
  check(pixelChecks(a,p,4,4,{coverage:true}).errors.length===1,"coverage");
  pix[3]=1;
  check(pixelChecks(a,inspectPixels(pix,4,4),4,4).warnings.length===1,"edge");
  check(pixelChecks(a,inspectPixels(pix,4,4),4,4).errors.length===2,"changed alpha");
  return n;
}

import {readFileSync,realpathSync} from "node:fs";
import {resolve,relative,isAbsolute,sep} from "node:path";
import {createHash} from "node:crypto";
import {inflateSync} from "node:zlib";
import {fileURLToPath} from "node:url";
const root=fileURLToPath(new URL("../../../",import.meta.url));
const contractPath="Assets/Dravio/UI/Data/ui-sprite-slots.json";
const master="ArtSource/UI/BrightFantasy_VIS4_Masters";
const polish="ArtSource/UI/BrightFantasy_VIS5_Polish";
const readJson=p=>JSON.parse(readFileSync(resolve(root,p),"utf8"));
function within(base,p) {
  const rel=relative(base,p);return rel!==""&&!isAbsolute(rel)&&rel!==".."&&!rel.startsWith(".."+sep);
}
function readSource(a,folder) {
  const base=realpathSync(resolve(root,folder)),p=realpathSync(resolve(root,a.path));
  if(!within(base,p)||!p.endsWith(".png"))throw Error("source outside kit or not PNG");
  const raw=readFileSync(p);
  if(raw.length!==a.bytes)throw Error("byte count mismatch");
  const sha=createHash("sha1").update("blob "+raw.length+"\0").update(raw).digest("hex");
  if(sha!==a.blob)throw Error("Git blob mismatch");
  return raw;
}
function crc32(buf) {
  let crc=0xffffffff;
  for(const v of buf) {
    crc^=v;
    for(let j=0;j<8;j++)crc=(crc>>>1)^((crc&1)?0xedb88320:0);
  }
  return (crc^0xffffffff)>>>0;
}
function decodePng(b) {
  if(b.length<45||b.subarray(0,8).toString("hex")!=="89504e470d0a1a0a")throw Error("not PNG");
  let off=8,w=0,h=0,ended=false,seenHeader=false,parts=[];
  while(off<b.length) {
    if(off+12>b.length)throw Error("truncated chunk");
    const size=b.readUInt32BE(off),tag=b.toString("ascii",off+4,off+8),end=off+12+size;
    if(end>b.length)throw Error("truncated chunk data");
    const data=b.subarray(off+8,end-4);
    if(crc32(b.subarray(off+4,end-4))!==b.readUInt32BE(end-4))throw Error("CRC mismatch "+tag);
    if(!seenHeader&&tag!=="IHDR")throw Error("IHDR must be first");
    if(tag==="IHDR") {
      if(seenHeader||size!==13)throw Error("invalid IHDR");seenHeader=true;
      w=data.readUInt32BE(0);h=data.readUInt32BE(4);
      if(!w||!h||w>2048||h>2048||data[8]!==8||data[9]!==6||data[10]||data[11]||data[12])
        throw Error("expected noninterlaced RGBA8 PNG <=2048");
    } else if(tag==="IDAT")parts.push(data);
    else if(tag==="IEND") {
      if(size||end!==b.length)throw Error("invalid IEND");ended=true;break;
    } else if(tag!=="PLTE"&&tag[0]===tag[0].toUpperCase()) throw Error("unsupported critical chunk "+tag);
    off=end;
  }
  if(!ended||!parts.length)throw Error("missing PNG data");
  const limit=h*(w*4+1),raw=inflateSync(Buffer.concat(parts),{maxOutputLength:limit});
  return {w,h,rgba:unfilter(raw,w,h)};
}
function auditKit(c,manifest,folder,complete) {
  const assets=manifest.assets.filter(a=>!a.notInSlotContract);
  const report=auditManifest(c,assets,{complete}), normals=new Map(), records=[];
  for(const a of [...manifest.assets].sort((a,b)=>Number(b.state==="normal")-Number(a.state==="normal"))) {
    const k=key(a),rec={id:k};
    try {
      const {w,h,rgba}=decodePng(readSource(a,folder)),p=inspectPixels(rgba,w,h);
      const chk=pixelChecks(a,p,w,h,{coverage:/^(icon|item):/.test(a.id)});
      rec.bounds=p.bounds;rec.coverage=p.coverage;rec.edge=p.edge;rec.warnings=chk.warnings;
      report.errors.push(...chk.errors.map(x=>k+": "+x));
      const alphaHash=createHash("sha256").update(p.alpha).digest("hex");
      if(a.state==="normal")normals.set(a.id,alphaHash);
      else if(normals.get(a.id)!==alphaHash)report.errors.push(k+": alpha silhouette differs from normal");
    } catch(e) {report.errors.push(k+": "+e.message);}
    records.push(rec);
  }
  return {...report,records};
}
if(process.argv.includes("--self-test")) {
  console.log(JSON.stringify({syntheticChecks:selfTest()}));
} else {
  const c=readJson(contractPath),r={
    master:auditKit(c,readJson(master+"/manifest.json"),master,true),
    alternatives:auditKit(c,readJson(polish+"/manifest.json"),polish,false),
    scope:"source PNG audit only; not Unity, no visual/contrast or compressed atlas acceptance"
  };
  console.log(JSON.stringify(r,null,2));
  if(r.master.errors.length||r.alternatives.errors.length)process.exitCode=1;
}
