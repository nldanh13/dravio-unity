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
function ss(a,b,x){const t=clamp((x-a)/(b-a));return t*t*(3-2*t)}
function noise(u,v,n,seed){
 const x=u*n,y=v*n,i=Math.floor(x),j=Math.floor(y),a=ss(0,1,x-i),b=ss(0,1,y-j);
 const hash=(x,y)=>fract(Math.sin(((x%n+n)%n)*127.1+((y%n+n)%n)*311.7+seed*74.7)*43758.5453);
 return mix(mix(hash(i,j),hash(i+1,j),a),mix(hash(i,j+1),hash(i+1,j+1),a),b);
}
function meadow(u,v){
 // Broad color fields with a quiet short-grass brush rhythm; no fake shadows.
 const broad=noise(u,v,3,17)*.56+noise(u,v,6,31)*.28+noise(u,v,12,53)*.16;
 const blade=.5+.5*Math.sin(tau*(u*39+v*13+.30*Math.sin(tau*v*7)+.15*Math.sin(tau*(u*4-v*5))));
 const fine=noise(u,v,48,81),t=clamp(.17+.58*broad+.12*blade+.13*fine);
 const warm=ss(.70,.93,noise(u,v,9,47))*.10;
 return [mix(106,194,t)+warm*12,mix(158,218,t)+warm*4,mix(103,155,t)-warm*4,255];
}
function cloud(u,v){
 // Two warped periodic fields, broad luminous cloud banks plus soft fine edges.
 const U=u+.045*Math.sin(tau*v*2),V=v+.032*Math.sin(tau*u*3);
 const field=noise(U,V,3,21)*.60+noise(U,V,6,34)*.27+noise(U,V,12,89)*.13;
 const t=ss(.24,.80,field),detail=noise(U+.2,V+.1,18,59);
 const lit=clamp(.15+.72*t+.13*detail);
 return [mix(163,245,lit),mix(179,249,lit),mix(208,255,lit),255];
}
function wisp(u,v){
 // White RGB, soft alpha with three overlapping flattened cloud lobes.
 const lobes=[[.30,.55,.20,.14,.54],[.49,.46,.26,.20,.68],[.70,.55,.19,.13,.51],[.48,.62,.30,.11,.32]];
 let a=0;
 for(const [x,y,rx,ry,w] of lobes){const d=((u-x)/rx)**2+((v-y)/ry)**2,q=w*(1-ss(.08,1,d));a+=q*(1-a);}
 const noiseFactor=.80+.20*noise(u,v,8,97);
 return [255,255,255,255*clamp(a*noiseFactor)];
}
function raster(name,w,h,fn,tiled){
 const pixels=new Uint8Array(w*h*4);let gutter=Infinity,max=0,sum=0,nonzero=0,minRGB=[255,255,255],maxRGB=[0,0,0];
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){const q=fn((x+.5)/w,(y+.5)/h);if(q.some(v=>!Number.isFinite(v)||v<0||v>255))throw Error("channel "+name);const p=(y*w+x)*4;for(let k=0;k<4;k++){const b=Math.round(q[k]);pixels[p+k]=b;if(k<3){minRGB[k]=Math.min(minRGB[k],b);maxRGB[k]=Math.max(maxRGB[k],b);}}const a=pixels[p+3];max=Math.max(max,a);sum+=a;if(a){nonzero++;gutter=Math.min(gutter,x,y,w-1-x,h-1-y);}}
 let err=null,edgeRatio=null;
 if(tiled){
  err=0;for(let i=0;i<128;i++){const u=fract(i*.61803398875),v=fract(i*.41421356237),a=fn(u,v);for(const b of [fn(u+1,v),fn(u,v+1)])for(let k=0;k<4;k++)err=Math.max(err,Math.abs(a[k]-b[k]));}if(err>1e-7)throw Error("periodicity");
  const e=[0,0],mid=[0,0],cnt=[0,0];
  for(let y=0;y<h;y++)for(let x=0;x<w;x++)for(let k=0;k<3;k++){const p=(y*w+x)*4+k;for(let axis=0;axis<2;axis++){const xx=axis===0?(x+1)%w:x,yy=axis===1?(y+1)%h:y,d=Math.abs(pixels[p]-pixels[(yy*w+xx)*4+k]),edge=axis===0?x===w-1:y===h-1;if(edge)e[axis]+=d;else{mid[axis]+=d;cnt[axis]++;}}}
  edgeRatio=e.map((v,i)=>(v/(3*(i===0?h:w)))/(mid[i]/cnt[i]));if(edgeRatio.some(v=>v>2.5))throw Error("edge discontinuity "+JSON.stringify(edgeRatio));
 }else if(gutter<8||max<120||nonzero>w*h*.55)throw Error("wisp mask");
 const tex={name,w,h,pixels},bytes=png(tex);verifyStoredPng(bytes,tex);
 return {bytes,report:{name,width:w,height:h,minRGB,maxRGB,alphaMax:max,meanAlpha:sum/(w*h),nonzeroAlpha:nonzero,minTransparentGutterPx:gutter,periodicMaxError:err,edgeToInteriorMeanDifferenceRatio:edgeRatio,bytes:bytes.length,rgba32Level0Bytes:w*h*4}};
}
function shaderSample(){
 // CPU approximation of existing Painterly Water RGB multiplication in linear space.
 // Not a rendered image or final lit brightness. Existing _ScrollA/B kept unchanged.
 const lin=x=>{const t=x/255;return t<=.04045?t/12.92:((t+.055)/1.055)**2.4};
 let min=Infinity,max=0,sum=0,n=0;
 for(const t of [0,20,60,120])for(let y=0;y<32;y++)for(let x=0;x<32;x++){
  const u=x/32,v=y/32,a=cloud(u+t*.006,v+t*.002).slice(0,3).map(lin),b=cloud(u*1.37+.31-t*.004,v*1.37+.17+t*.003).slice(0,3).map(lin);
  const ripple=2*a.reduce((s,q,i)=>s+q*b[i]/3,0),factor=.75+.5*ripple;min=Math.min(min,factor);max=Math.max(max,factor);sum+=factor;n++;
 }
 if(min<.75||max>1.65)throw Error("water modulation");
 return {samples:n,minMultiplier:min,maxMultiplier:max,meanMultiplier:sum/n,scope:"linear RGB texture product only; excludes light, fog, foam, wind, glint and render"};
}
function build(){return {assets:[raster("T_VIS16B_Sky_Meadow",512,512,meadow,true),raster("T_VIS16B_Sky_CloudSea",256,256,cloud,true),raster("T_VIS16B_FX_CloudWisp",128,128,wisp,false)],shader:shaderSample()};}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_sky_vis16b_textures.mjs <new-dir>");
const a=build();mkdirSync(out);for(const x of a.assets){writeFileSync(join(out,x.report.name+".png"),x.bytes,{flag:"wx"});console.log(JSON.stringify(x.report));}console.log(JSON.stringify(a.shader));
