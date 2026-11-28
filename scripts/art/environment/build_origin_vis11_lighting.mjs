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

const clamp=x=>Math.max(0,Math.min(1,x)),mix=(a,b,t)=>a+(b-a)*t;
function ss(a,b,x){const q=clamp((x-a)/(b-a));return q*q*(3-2*q);}
function shaft(x,y){
 // Soft taper, no hard top/bottom, broad shaft plus two faint secondary ribbons.
 const t=clamp((y-.10)/.80),center=.37+.25*t,width=.028+.13*t;
 const envelope=ss(.10,.22,y)*(1-ss(.64,.90,y));
 const band=(cx,w)=>1-ss(.20,1,Math.abs(x-cx)/w);
 return envelope*clamp(.68*band(center,width)+.18*band(center-.13,width*.53)+.15*band(center+.15,width*.44));
}
function dapple(x,y){
 // Overlapping elliptical pools of light, organic edge lobes, no baked shadow color.
 const spots=[[.29,.32,.14,.085,.57,.78],[.60,.28,.115,.066,-.37,.63],[.47,.50,.19,.105,.30,.85],[.72,.61,.11,.075,-.5,.62],[.27,.70,.095,.05,.65,.53],[.52,.75,.08,.044,-.6,.45]];
 let a=0;
 for(let i=0;i<spots.length;i++){
  const [cx,cy,rx,ry,rot,k]=spots[i],dx=x-cx,dy=y-cy;
  const u=(dx*Math.cos(rot)+dy*Math.sin(rot))/rx,v=(-dx*Math.sin(rot)+dy*Math.cos(rot))/ry;
  const theta=Math.atan2(v,u),d=Math.hypot(u,v)/(1+.08*Math.cos(3*theta+i)+.035*Math.sin(5*theta));
  const q=(1-ss(.30,1,d))*k;a=q+a*(1-q);
 }
 return a;
}
function mask(name,fn){
 const w=128,h=128,pixels=new Uint8Array(w*h*4);let gutter=128,max=0,nonzero=0;
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  const a=fn((x+.5)/w,(y+.5)/h);
  if(!Number.isFinite(a)||a<0||a>1)throw Error("mask range");
  const i=(y*w+x)*4;pixels[i]=pixels[i+1]=pixels[i+2]=255;pixels[i+3]=Math.round(a*255);
  if(pixels[i+3]){nonzero++;gutter=Math.min(gutter,x,y,w-1-x,h-1-y);max=Math.max(max,pixels[i+3]);}
 }
 if(gutter<8||max<100||nonzero<500||nonzero>w*h*.65)throw Error("mask silhouette "+JSON.stringify({name,gutter,max,nonzero}));
 const tex={name,w,h,pixels},bytes=png(tex);verifyStoredPng(bytes,tex);
 return {bytes,report:{name,width:w,height:h,format:"RGBA8, white RGB/straight alpha",maxAlpha:max,nonzeroAlpha:nonzero,minTransparentGutterPx:gutter,rgba32Level0Bytes:w*h*4,bytes:bytes.length}};
}
function weights(k){
 const intensity=clamp(k.lightIntensity/1.10),elev=k.lightElevation;
 // Art-only proposed envelopes. At all moon keys, both disabled.
 const day=k.isMoon?0:intensity;
 return {hour:k.hour,shaft:Number((day*ss(2,12,elev)*(1-ss(32,65,elev))*.32).toFixed(4)),dapple:Number((day*ss(5,24,elev)*.20).toFixed(4))};
}
function build(keys){
 const frames=keys.map(weights);let maxSlope=0;
 for(let i=0;i<frames.length;i++){
  const f=frames[i],k=keys[i];if(k.isMoon&&(f.shaft||f.dapple)||f.shaft<0||f.shaft>.32||f.dapple<0||f.dapple>.20)throw Error("invalid profile");
  if(i){const prev=frames[i-1],dt=f.hour-prev.hour;if(dt<=0)throw Error("key order");maxSlope=Math.max(maxSlope,Math.abs(f.shaft-prev.shaft)/dt,Math.abs(f.dapple-prev.dapple)/dt);if(k.isMoon!==keys[i-1].isMoon&&(f.shaft||f.dapple||prev.shaft||prev.dapple))throw Error("moon/sun discontinuity");}
 }
 return {assets:[mask("T_VIS11_Canopy_Soft_Shaft",shaft),mask("T_VIS11_Canopy_Dapple",dapple)],frames,maxSlope};
}

import {mkdirSync,readFileSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const [, ,source,out]=process.argv;
if(!source||!out)throw Error("Usage: node build_origin_vis11_lighting.mjs <living-json> <new-dir>");
const doc=JSON.parse(readFileSync(source,"utf8"));if(doc.zoneId!=="origin")throw Error("Origin profile only");
const result=build(doc.keyframes);mkdirSync(out);
for(const a of result.assets)writeFileSync(join(out,a.report.name+".png"),a.bytes,{flag:"wx"});
writeFileSync(join(out,"lighting-preview-profile.json"),JSON.stringify({status:"proposed cosmetic opacity, not runtime config",frames:result.frames},null,2)+"\n",{flag:"wx"});
