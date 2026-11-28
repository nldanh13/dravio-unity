function png(tex){
 const u32=n=>[(n>>>24)&255,(n>>>16)&255,(n>>>8)&255,n&255];
 const table=Array.from({length:256},(_,i)=>{let c=i;for(let j=0;j<8;j++)c=(c&1)?(0xedb88320^(c>>>1)):(c>>>1);return c>>>0});
 const concat=arr=>{let out=new Uint8Array(arr.reduce((s,a)=>s+a.length,0));let k=0;for(let a of arr){out.set(a,k);k+=a.length}return out};
 const chunk=(type,data)=>{let t=Uint8Array.from(type.split("").map(c=>c.charCodeAt(0))),crc=0xffffffff;for(let a of [t,data])for(let b of a)crc=table[(crc^b)&255]^(crc>>>8);return concat([u32(data.length),t,data,u32((crc^0xffffffff)>>>0)])};
 let raw=new Uint8Array((tex.w*4+1)*tex.h);for(let y=0;y<tex.h;y++)raw.set(tex.pixels.subarray(y*tex.w*4,(y+1)*tex.w*4),y*(tex.w*4+1)+1);
 let parts=[[0x78,0x01]],a=1,b=0;for(let x of raw){a=(a+x)%65521;b=(b+a)%65521}
 for(let p=0;p<raw.length;p+=65535){let len=Math.min(65535,raw.length-p),inv=65535-len;parts.push([p+len===raw.length?1:0,len&255,len>>8,inv&255,inv>>8],raw.subarray(p,p+len))}
 parts.push(u32(((b<<16)|a)>>>0));return concat([[137,80,78,71,13,10,26,10],chunk("IHDR",Uint8Array.from([...u32(tex.w),...u32(tex.h),8,6,0,0,0])),chunk("sRGB",Uint8Array.of(0)),chunk("IDAT",concat(parts)),chunk("IEND",new Uint8Array(0))]);
}

function verifyStoredPng(bytes,tex){
  const dv=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength);let p=8,raw=null;
  while(p<bytes.length){
    const len=dv.getUint32(p),tag=String.fromCharCode(...bytes.slice(p+4,p+8));p+=8;
    if(tag==="IDAT"){
      let i=p+2,rows=[];const end=p+len-4;
      while(i<end){
        const final=bytes[i++],n=bytes[i]|bytes[i+1]<<8,inv=bytes[i+2]|bytes[i+3]<<8;i+=4;
        if((final!==0&&final!==1)||(n^inv)!==65535||i+n>end)throw Error("deflate block");
        rows.push(bytes.slice(i,i+n));i+=n;if(final&&i!==end)throw Error("deflate end");
      }
      raw=new Uint8Array(rows.reduce((s,a)=>s+a.length,0));let o=0;for(const a of rows){raw.set(a,o);o+=a.length;}
    }p+=len+4;
  }
  if(!raw||raw.length!==(tex.w*4+1)*tex.h)throw Error("PNG raw size");
  for(let y=0;y<tex.h;y++){const p=y*(tex.w*4+1);if(raw[p]!==0)throw Error("filter");for(let x=0;x<tex.w*4;x++)if(raw[p+1+x]!==tex.pixels[y*tex.w*4+x])throw Error("PNG pixel mismatch");}
}



const tau=2*Math.PI,clamp=x=>Math.max(0,Math.min(1,x)),mix=(a,b,t)=>a+(b-a)*t,fract=x=>x-Math.floor(x);
function ss(a,b,x){const t=clamp((x-a)/(b-a));return t*t*(3-2*t);}
function noise(u,v,n,seed){
 const x=u*n,y=v*n,i=Math.floor(x),j=Math.floor(y),a=ss(0,1,x-i),b=ss(0,1,y-j);
 const hash=(x,y)=>fract(Math.sin(((x%n+n)%n)*127.1+((y%n+n)%n)*311.7+seed*74.7)*43758.5453);
 return mix(mix(hash(i,j),hash(i+1,j),a),mix(hash(i,j+1),hash(i+1,j+1),a),b);
}
function nearest(u,v,nx,ny){
 const x=u*nx,y=v*ny,ix=Math.floor(x),iy=Math.floor(y);let d1=Infinity,d2=Infinity,py=0;
 for(let j=-2;j<=2;j++)for(let i=-2;i<=2;i++){
  const row=iy+j,dx=x-(ix+i+.5+((row%2+2)%2)*.5),dy=y-(row+.5),d=Math.hypot(dx,dy);
  if(d<d1){d2=d1;d1=d;py=dy;}else if(d<d2)d2=d;
 }return {d1,d2,py};
}
// Scalar tonal studies, not normals, roughness, or anatomical masks.
const profiles=[
 {id:"Scale",strength:.10,description:"Rounded staggered scale cells, soft edge and upper highlight.",fn:(u,v)=>{
  const q=nearest(u+.017,v+.029,6,8),edge=1-ss(.02,.20,q.d2-q.d1),soft=1-ss(.05,.5,q.d1);
  return -.36*edge+.25*soft-.14*q.py*(1-edge)+.08*(noise(u,v,12,19)-.5);
 }},
 {id:"Fur",strength:.08,description:"Broad directional tuft rhythm.",fn:(u,v)=>{
  const a=tau*(u*7+.25*Math.sin(tau*v*2)+.12*Math.sin(tau*(u*2+v*3)));
  return .31*Math.cos(a)*(.65+.35*Math.cos(tau*v*3))+.20*(noise(u,v,5,23)-.5)+.10*Math.sin(tau*(u*14+v*2));
 }},
 {id:"Membrane",strength:.06,description:"Quiet directional folds; veins require hand placement.",fn:(u,v)=>{
  const a=tau*(u*3+.13*Math.sin(tau*v*2));return .30*Math.cos(a)+.19*(noise(u,v,4,47)-.5)+.06*Math.cos(2*a);
 }},
 {id:"Horn",strength:.10,description:"Keratin growth bands and longitudinal tone.",fn:(u,v)=>{
  const a=tau*(v*6+.13*Math.sin(tau*u*2));return .27*Math.sin(a)+.16*Math.cos(tau*u*3)+.09*(noise(u,v,8,31)-.5);
 }},
 {id:"Armor",strength:.12,description:"Rounded stone panel boundaries.",fn:(u,v)=>{
  const q=nearest(u+.031+.035*Math.sin(tau*v*2),v+.047+.025*Math.sin(tau*u*3),4,4);
  return -.39*(1-ss(.02,.17,q.d2-q.d1))+.25*(1-ss(.10,.55,q.d1))+.12*(noise(u,v,9,41)-.5);
 }},
 {id:"Crystal",strength:.08,description:"Soft geometric facet rhythm; no physical reflection or emission.",fn:(u,v)=>{
  const a=Math.sin(tau*(u*3+v*2)),b=Math.sin(tau*(u*3-v*2));
  return .28*(ss(-.18,.18,a)-.5)+.32*(ss(-.18,.18,b)-.5)+.18*(noise(u,v,4,59)-.5);
 }}
];
function raster(profile){
 const w=128,h=128,field=new Float64Array(w*h);let sum=0;
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){const q=profile.fn((x+.5)/w,(y+.5)/h);if(!Number.isFinite(q))throw Error("nonfinite");field[y*w+x]=q;sum+=q;}
 const mean=sum/field.length,peak=Math.max(...field.map(x=>Math.abs(x-mean)));if(peak<.05)throw Error("flat pattern");
 const pixels=new Uint8Array(w*h*4);let lo=255,hi=0,avg=0;
 for(let i=0;i<field.length;i++){const k=Math.round(128+44*(field[i]-mean)/peak);pixels.fill(k,i*4,i*4+3);pixels[i*4+3]=255;lo=Math.min(lo,k);hi=Math.max(hi,k);avg+=k;}
 let err=0;for(let i=0;i<256;i++){const u=fract(i*.61803398875),v=fract(i*.41421356237),a=profile.fn(u,v);err=Math.max(err,Math.abs(a-profile.fn(u+1,v)),Math.abs(a-profile.fn(u,v+1)));}
 if(err>1e-8||lo<84||hi>172||Math.abs(avg/field.length-128)>.1)throw Error("range/periodic "+profile.id);
 const edges=[0,0],interior=[0,0],counts=[0,0];
 for(let y=0;y<h;y++)for(let x=0;x<w;x++)for(let axis=0;axis<2;axis++){
 const xx=axis===0?(x+1)%w:x,yy=axis===1?(y+1)%h:y,d=Math.abs(pixels[4*(y*w+x)]-pixels[4*(yy*w+xx)]),edge=axis===0?x===w-1:y===h-1;
 if(edge)edges[axis]+=d;else{interior[axis]+=d;counts[axis]++;}
 }
 const ratios=edges.map((v,i)=>(v/(i===0?h:w))/(interior[i]/counts[i]));if(ratios.some(v=>v>2.5))throw Error("edge "+profile.id+" "+ratios);
 const name="T_VIS17B_"+profile.id+"_Tone",tex={name,w,h,pixels},bytes=png(tex);verifyStoredPng(bytes,tex);
 return {bytes,report:{name,material:profile.id,width:w,height:h,minGray:lo,maxGray:hi,meanGray:avg/field.length,periodicMaxError:err,edgeToInteriorRatio:ratios,alpha:255,bytes:bytes.length,description:profile.description,proposedMaxBlendStrength:profile.strength,maxNominalLinearMultiplierDelta:profile.strength*44/128}};
}
function build(){return profiles.map(raster);}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_vis17b_material_studies.mjs <new-dir>");
const a=build();mkdirSync(out);for(const x of a){writeFileSync(join(out,x.report.name+".png"),x.bytes,{flag:"wx"});console.log(JSON.stringify(x.report));}
