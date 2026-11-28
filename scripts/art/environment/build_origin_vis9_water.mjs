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

// VIS.9: directional, tileable water art and a looping foam flipbook.
// Original source textures; no runtime shader or gameplay changes.
const TAU=2*Math.PI,clamp=x=>Math.max(0,Math.min(1,x)),mix=(a,b,t)=>a+(b-a)*t;
function ss(a,b,x){const q=clamp((x-a)/(b-a));return q*q*(3-2*q)}
const wave=x=>.5+.5*Math.sin(TAU*x);
function waterfall(u,v){
  // Integral frequencies in both axes guarantee analytical periodicity.
  const warp=.075*Math.sin(TAU*(v*2+u*2))+.035*Math.sin(TAU*(v*3-u*3));
  const broad=wave(u*6+warp),narrow=wave(u*15+.16*Math.sin(TAU*(v*2+u*3)));
  const broken=wave(v*3+u*4),ribbon=ss(.70,.96,broad)*(.42+.58*broken);
  const silk=ss(.76,.98,narrow)*(.25+.75*wave(v*5-u*2));
  const t=clamp(.18+.36*broad+.24*ribbon+.12*silk);
  return [mix(35,190,t),mix(151,240,t),mix(185,246,t),255*(.68+.16*ribbon+.07*silk)];
}
function stream(u,v){
  const curve=.20*Math.sin(TAU*u*2)+.08*Math.sin(TAU*u*3);
  const a=wave(v*5+curve),b=wave(v*8-.14*Math.sin(TAU*u*4));
  const line=ss(.83,.98,a)*(.32+.68*b);
  const fine=ss(.87,.99,b)*wave(u*3+v*2);
  const t=clamp(.20+.65*line+.09*fine);
  return [mix(36,190,t),mix(147,235,t),mix(157,235,t),255*(.66+.18*line)];
}
// Fixed number of bubbles with periodic drift, no frame-to-frame random changes.
// Premultiplied composition internally, straight alpha on export.
function foam(x,y,t){
  let A=0,C=[0,0,0]; 
  function over(rgb,a){a=clamp(a);C=C.map((v,i)=>rgb[i]*a+v*(1-a));A=a+A*(1-a);}
  const phase=TAU*t;
  const rx=.315+.018*Math.sin(phase),ry=.18+.014*Math.sin(phase+.7);
  const dx=(x-.5)/rx,dy=(y-.54)/ry,angle=Math.atan2(dy,dx);
  const radius=Math.hypot(dx,dy)/(1+.045*Math.cos(5*angle+phase)+.026*Math.sin(8*angle-phase));
  const band=ss(.56,.79,radius)*(1-ss(.90,1.06,radius));
  over([184,240,238],band*.65);
  for(let i=0;i<10;i++){
    const q=TAU*i/10+.07*Math.sin(phase+i*.83),r=.72+.09*Math.sin(i*2.1+phase);
    const cx=.5+Math.cos(q)*rx*r,cy=.54+Math.sin(q)*ry*r;
    const s=.038+.012*wave(i*.31+t),d=Math.hypot((x-cx)/s,(y-cy)/(s*.77));
    const ring=ss(.43,.66,d)*(1-ss(.85,1.06,d));
    over([231,251,245],ring*.86);
    const dot=Math.hypot((x-cx+s*.24)/(s*.34),(y-cy+s*.24)/(s*.29));
    over([255,255,247],(1-ss(.72,1,dot))*.8);
  }
  return A>1e-8?[...C.map(v=>v/A),255*A]:[220,248,245,0];
}
function raster(name,w,h,fn){
  const pixels=new Uint8Array(w*h*4);
  for(let y=0;y<h;y++)for(let x=0;x<w;x++){
    const c=fn((x+.5)/w,(y+.5)/h,x,y);
    if(c.some(v=>!Number.isFinite(v)||v< -1e-6||v>255+1e-6))throw Error("invalid channel "+name);
    for(let k=0;k<4;k++)pixels[(y*w+x)*4+k]=Math.round(c[k]);
  }
  return {name,w,h,pixels};
}
function make(){
  return [raster("T_VIS9_Waterfall_Silk",256,256,waterfall),
    raster("T_VIS9_Stream_Curved_Ripples",256,256,stream),
    raster("T_VIS9_Foam_Loop_16",512,512,(_u,_v,x,y)=>{
      const frame=Math.floor(y/128)*4+Math.floor(x/128);
      return foam((x%128+.5)/128,(y%128+.5)/128,frame/16);
    })];
}
function periodic(fn,animated=false){
  let max=0;for(let i=0;i<64;i++){
    const x=(i*.61803398875)%1,y=(i*.41421356237)%1,t=i/64;
    const a=animated?fn(x,y,t):fn(x,y);
    const variants=animated?[fn(x,y,t+1)]:[fn(x+1,y),fn(x,y+1)];
    for(const b of variants)for(let k=0;k<4;k++)max=Math.max(max,Math.abs(a[k]-b[k]));
  }if(max>1e-8)throw Error("non-periodic pattern");return max;
}
function inspect(tex){
  const {w,h,pixels}=tex;let alphaMin=255,alphaMax=0,nonzero=0;
  for(let i=3;i<pixels.length;i+=4){const a=pixels[i];alphaMin=Math.min(alphaMin,a);alphaMax=Math.max(alphaMax,a);if(a)nonzero++;}
  const rep={name:tex.name,width:w,height:h,format:"RGBA8 sRGB, straight alpha",alphaMin,alphaMax,nonzeroAlpha:nonzero,rgba32Level0Bytes:w*h*4};
  if(w===512){
    let gutter=128,previous=null,first=null,maxFrameDelta=0,wrapDelta=0;
    for(let f=0;f<16;f++){
      const data=[];let count=0;
      for(let y=0;y<128;y++)for(let x=0;x<128;x++){
        const a=pixels[((Math.floor(f/4)*128+y)*w+(f%4)*128+x)*4+3];
        data.push(a);if(a){count++;gutter=Math.min(gutter,x,y,127-x,127-y);}
      }
      if(count<500)throw Error("empty foam frame");
      const delta=(a,b)=>a.reduce((s,v,i)=>s+Math.abs(v-b[i]),0)/(128*128);
      if(previous)maxFrameDelta=Math.max(maxFrameDelta,delta(previous,data));else first=data;
      previous=data;if(f===15)wrapDelta=delta(data,first);
    }
    if(gutter<12||wrapDelta>maxFrameDelta*1.5+.05)throw Error("foam atlas margin or loop");
    Object.assign(rep,{frames:16,grid:[4,4],cellSize:128,minTransparentGutterPx:gutter,maxAdjacentAlphaMeanDelta:maxFrameDelta,wrapAlphaMeanDelta:wrapDelta});
  }else{
    let xDiff=0,yDiff=0;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++)for(let k=0;k<4;k++){
      const a=pixels[(y*w+x)*4+k];xDiff+=Math.abs(a-pixels[(y*w+(x+1)%w)*4+k]);yDiff+=Math.abs(a-pixels[(((y+1)%h)*w+x)*4+k]);
    }
    rep.meanAdjacentRGBA=[xDiff/(w*h*4),yDiff/(w*h*4)];
    const ratio=rep.meanAdjacentRGBA[0]/rep.meanAdjacentRGBA[1];
    if(tex.name.includes("Waterfall")?ratio<1.5:ratio>.8)throw Error("unexpected directional pattern");
  }
  return rep;
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
function build(){
  const errors={waterfall:periodic(waterfall),stream:periodic(stream),foamTime:periodic(foam,true)};
  const assets=make().map(tex=>{const bytes=png(tex);verifyStoredPng(bytes,tex);return {bytes,report:{...inspect(tex),bytes:bytes.length}}});
  return {assets,errors};
}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_origin_vis9_water.mjs <new output dir>");
const result=build();mkdirSync(out);for(const a of result.assets)writeFileSync(join(out,a.report.name+".png"),a.bytes,{flag:"wx"});
console.log(JSON.stringify({periodicMaxError:result.errors,assets:result.assets.map(a=>a.report)},null,2));
