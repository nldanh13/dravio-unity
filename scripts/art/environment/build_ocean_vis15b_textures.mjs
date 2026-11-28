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
const wave=x=>.5+.5*Math.cos(tau*x);
function sand(u,v){
 const warp=.18*Math.sin(tau*u*2)+.08*Math.sin(tau*(u*3+v));
 const rip=wave(v*7+warp),small=wave(u*11-v*13),broad=wave(u*2+v*3);
 const speck=ss(.86,.99,wave(u*37+v*23)*wave(u*19-v*31));
 const t=clamp(.32+.30*rip+.07*small+.12*broad-.12*speck);
 return [mix(94,177,t),mix(148,204,t),mix(145,186,t),255];
}
// Periodic cellular ridge field. Neighbor distances are evaluated with wrapped
// cell identities but unwrapped coordinates, so both axes tile continuously.
function cellular(u,v,n,seed){
 const x=u*n,y=v*n,ix=Math.floor(x),iy=Math.floor(y);let d1=Infinity,d2=Infinity;
 for(let j=-2;j<=2;j++)for(let i=-2;i<=2;i++){
  const a=ix+i,b=iy+j,A=((a%n)+n)%n,B=((b%n)+n)%n;
  const ox=.22+.56*fract(Math.sin(A*127.1+B*311.7+seed*17.3)*43758.5453);
  const oy=.22+.56*fract(Math.sin(A*269.5+B*183.3+seed*9.9)*43758.5453);
  const d=Math.hypot(x-a-ox,y-b-oy);if(d<d1){d2=d1;d1=d;}else if(d<d2)d2=d;
 }
 return d2-d1;
}
function caustic(u,v){
 const a=cellular(u+.035*Math.sin(tau*v*2),v+.035*Math.sin(tau*u*3),5,17);
 const b=cellular(u+.12,v+.17,7,31);
 const ridge=(1-ss(.025,.18,a))*(.50+.50*wave(u*3-v*2));
 const fine=(1-ss(.015,.11,b))*.24;
 const alpha=clamp(ridge*.76+fine);
 return [255,255,255,255*alpha];
}
function shaft(u,v){
 const t=clamp((v-.06)/.88),c=.46+.05*t,w=.075+.30*t;
 const vertical=ss(.04,.16,v)*(1-ss(.69,.96,v));
 const band=(x,r)=>1-ss(.1,1,Math.abs(u-x)/r);
 const a=vertical*clamp(.70*band(c,w)+.14*band(c-.15,w*.46)+.12*band(c+.13,w*.43));
 return [255,255,255,255*a];
}
function raster(name,w,h,fn,periodic){
 const pixels=new Uint8Array(w*h*4);let min=255,max=0,gutter=Infinity,nonzero=0,sum=0;
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){const a=fn((x+.5)/w,(y+.5)/h);if(a.some(v=>!Number.isFinite(v)||v<0||v>255))throw Error("channel");const p=(y*w+x)*4;for(let k=0;k<4;k++)pixels[p+k]=Math.round(a[k]);const q=pixels[p+3];min=Math.min(min,q);max=Math.max(max,q);sum+=q;if(q){nonzero++;gutter=Math.min(gutter,x,y,w-1-x,h-1-y);}}
 let err=null;if(periodic){err=0;for(let i=0;i<80;i++){const u=fract(i*.61803398875),v=fract(i*.41421356237),a=fn(u,v);for(const b of [fn(u+1,v),fn(u,v+1)])for(let k=0;k<4;k++)err=Math.max(err,Math.abs(a[k]-b[k]));}if(err>1e-7)throw Error("periodicity "+err);}
 if(name.includes("Shaft")&&(gutter<2||max<100))throw Error("shaft silhouette");
 if(name.includes("Caustics")&&(sum/(w*h)<8||sum/(w*h)>100||max<150))throw Error("caustic density");
 const tex={name,w,h,pixels},bytes=png(tex);verifyStoredPng(bytes,tex);
 return {bytes,report:{name,width:w,height:h,alphaMin:min,alphaMax:max,meanAlpha:sum/(w*h),nonzeroAlpha:nonzero,minTransparentGutterPx:gutter,periodicMaxError:err,bytes:bytes.length,rgba32Level0Bytes:w*h*4}};
}
function build(){return [raster("T_VIS15B_Ocean_Ground",512,512,sand,true),raster("T_VIS15B_FX_Caustics",256,256,caustic,true),raster("T_VIS15B_FX_LightShaft",64,256,shaft,false)];}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_ocean_vis15b_textures.mjs <new-dir>");
const a=build();mkdirSync(out);for(const x of a){writeFileSync(join(out,x.report.name+".png"),x.bytes,{flag:"wx"});console.log(JSON.stringify(x.report));}
