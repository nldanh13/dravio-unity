// Original small opaque 3D flora for close-view Origin accents; distant cover stays cards.
const tau=2*Math.PI,add=(a,b)=>a.map((x,i)=>x+b[i]),sub=(a,b)=>a.map((x,i)=>x-b[i]),mul=(a,s)=>a.map(x=>x*s),dot=(a,b)=>a.reduce((s,x,i)=>s+x*b[i],0),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]],norm=a=>{const l=Math.hypot(...a);if(l<1e-10)throw Error("zero vector");return mul(a,1/l)};
const rgb=h=>[0,2,4].map(i=>{const v=parseInt(h.slice(i,i+2),16)/255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4}),mix=(a,b,t)=>a.map((x,i)=>x+(b[i]-x)*t);
function mesh(name){return {name,p:[],n:[],c:[]}}
function tri(m,a,b,c,colors,ns){
  let face=cross(sub(b,a),sub(c,a));
  if(Math.hypot(...face)<1e-8)throw Error("degenerate "+m.name);
  if(dot(face,add(add(ns[0],ns[1]),ns[2]))<0){[b,c]=[c,b];ns=[ns[0],ns[2],ns[1]];colors=[colors[0],colors[2],colors[1]];face=mul(face,-1);}
  for(let i=0;i<3;i++){if(dot(face,ns[i])< -1e-8)throw Error("opposed corner normal "+m.name);m.p.push(...[a,b,c][i]);m.n.push(...ns[i]);m.c.push(...colors[i]);}
}
function quad(m,a,b,c,d,cols,ns){tri(m,a,b,c,cols.slice(0,3),ns.slice(0,3));tri(m,a,c,d,[cols[0],cols[2],cols[3]],[ns[0],ns[2],ns[3]])}
// Closed ellipsoid with true pole vertices (not collapsed rings).
function oval(m,center,axes,radii,lo,hi,n=8,rows=4){
  const [u,v,w]=axes, [rx,ry,rz]=radii;
  const L=rgb(lo),H=rgb(hi);
  const pt=(theta,phi)=>{const q=[Math.cos(theta)*Math.cos(phi),Math.sin(theta),Math.cos(theta)*Math.sin(phi)];return {p:add(center,add(add(mul(u,q[0]*rx),mul(v,q[1]*ry)),mul(w,q[2]*rz))),n:norm(add(add(mul(u,q[0]/rx),mul(v,q[1]/ry)),mul(w,q[2]/rz))),c:mix(L,H,(q[1]+1)/2)}};
  const rings=Array.from({length:rows-1},(_,j)=>Array.from({length:n},(_,i)=>pt(-Math.PI/2+Math.PI*(j+1)/rows,tau*i/n)));
  const bottom=pt(-Math.PI/2,0),top=pt(Math.PI/2,0);
  const t=(a,b,c)=>tri(m,a.p,b.p,c.p,[a.c,b.c,c.c],[a.n,b.n,c.n]);
  for(let i=0;i<n;i++){const k=(i+1)%n;t(bottom,rings[0][i],rings[0][k]);for(let j=0;j<rings.length-1;j++){const a=rings[j][i],b=rings[j+1][i],c=rings[j+1][k],d=rings[j][k];t(a,b,c);t(a,c,d);}t(top,rings.at(-1)[k],rings.at(-1)[i]);}
}
const xyz=[[1,0,0],[0,1,0],[0,0,1]];

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


function fish(){
 const m=mesh("Ocean_Minnow");
 oval(m,[0,0,.035],xyz,[.047,.071,.145],"328C9C","B5E6CA",12,5);
 oval(m,[0,0,-.102],xyz,[.021,.031,.055],"4B9BA2","C3E5C5",8,3);
 for(const s of [-1,1]){
  const axis=norm([0,s*.70,-.714]),side=norm(cross([1,0,0],axis));
  oval(m,[0,s*.034,-.171],[[1,0,0],axis,side],[.009,.059,.031],"C99156","FFE3A1",8,3);
  const f=norm([s*.60,-.25,-.76]),u=norm(cross(f,[0,1,0])),v=norm(cross(u,f));
  oval(m,[s*.040,-.019,.008],[u,v,f],[.022,.008,.050],"53B4BA","CDEBC7",6,3);
  oval(m,[s*.041,.020,.119],xyz,[.013,.014,.015],"E4E9BD","FFF5D9",8,3);
  oval(m,[s*.051,.021,.122],xyz,[.006,.008,.008],"253D49","43566A",6,3);
 }
 oval(m,[0,.065,.001],xyz,[.012,.039,.059],"5AAEB4","F4DCA1",8,3);
 const targets=[1,-1].map(sign=>{
  const p=[],n=[];
  for(let i=0;i<m.p.length;i+=3){
   const t=Math.max(0,Math.min(1,(.025-m.p[i+2])/.255)),d=sign*.064*t*t*(3-2*t),slope=sign*.064*(-6*t*(1-t)/.255);
   p.push(d,0,0);const old=m.n.slice(i,i+3);n.push(...sub(norm([old[0],old[1],old[2]-slope*old[0]]),old));
  }return {p,n};
 });
 const tris=m.p.length/9;if(tris>800)throw Error("triangle cap");
 let minRatio=Infinity,maxRatio=0,minDot=1;
 for(const tar of targets)for(let step=0;step<=20;step++)for(let i=0;i<m.p.length;i+=9){
   const p=[0,1,2].map(j=>m.p.slice(i+j*3,i+j*3+3)),q=p.map((v,j)=>add(v,mul(tar.p.slice(i+j*3,i+j*3+3),step/20)));
   const a=cross(sub(p[1],p[0]),sub(p[2],p[0])),b=cross(sub(q[1],q[0]),sub(q[2],q[0])),ratio=Math.hypot(...b)/Math.hypot(...a);
   minRatio=Math.min(minRatio,ratio);maxRatio=Math.max(maxRatio,ratio);minDot=Math.min(minDot,dot(norm(a),norm(b)));
 }
 if(minRatio<.6||maxRatio>1.5||minDot<.75)throw Error("deformation");
 const chunks=[],views=[],accessors=[];let offset=0;
 function acc(values,dim,type,target,bounds=false){
  const a=new Float32Array(values),b=new Uint8Array(a.buffer);if(a.some(x=>!Number.isFinite(x)))throw Error("finite");
  const v={buffer:0,byteOffset:offset,byteLength:b.length};if(target)v.target=target;views.push(v);chunks.push(b);offset+=b.length;
  const d={bufferView:views.length-1,componentType:5126,count:a.length/dim,type};
  if(bounds){d.min=Array(dim).fill(Infinity);d.max=Array(dim).fill(-Infinity);for(let i=0;i<a.length;i++){d.min[i%dim]=Math.min(d.min[i%dim],a[i]);d.max[i%dim]=Math.max(d.max[i%dim],a[i]);}}
  accessors.push(d);return accessors.length-1;
 }
 const attrs={POSITION:acc(m.p,3,"VEC3",34962,true),NORMAL:acc(m.n,3,"VEC3",34962),COLOR_0:acc(m.c,3,"VEC3",34962),TEXCOORD_0:acc(Array.from({length:m.p.length/3},()=>[.5,.5]).flat(),2,"VEC2",34962)};
 const morphs=targets.map(t=>({POSITION:acc(t.p,3,"VEC3",34962,true),NORMAL:acc(t.n,3,"VEC3",34962)}));
 const times=Array.from({length:17},(_,i)=>i*.8/16),weights=times.flatMap((_,i)=>{const v=i===0||i===16?0:Math.sin(tau*i/16);return [Math.max(0,v),Math.max(0,-v)];});
 if(weights.slice(0,2).some((v,i)=>v!==weights[weights.length-2+i])||weights.some(v=>v<0||v>1))throw Error("loop");
 const time=acc(times,1,"SCALAR",null,true),w=acc(weights,1,"SCALAR");
 const doc={asset:{version:"2.0",generator:"Dravio VIS.15c minnow"},scene:0,scenes:[{nodes:[0]}],nodes:[{name:"Ocean_Minnow",mesh:0}],meshes:[{name:m.name,weights:[0,0],extras:{targetNames:["Swim_Left","Swim_Right"]},primitives:[{attributes:attrs,targets:morphs,mode:4,material:0}]}],materials:[{name:"Ocean_Minnow_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.85}}],animations:[{name:"Swim_Loop",samplers:[{input:time,output:w,interpolation:"LINEAR"}],channels:[{sampler:0,target:{node:0,path:"weights"}}]}],buffers:[{byteLength:offset}],bufferViews:views,accessors};
 let j=JSON.stringify(doc);while(j.length%4)j+=" ";const bytes=new Uint8Array(28+j.length+offset),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;dv.setUint32(p,offset,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(const c of chunks){bytes.set(c,p);p+=c.length;}
 return {bytes,report:{name:m.name,triangles:tris,vertices:m.p.length/3,materialSlots:1,textures:0,morphTargets:2,clip:"Swim_Loop",durationSeconds:.8,keys:17,sampledPoses:42,minFaceNormalDot:minDot,minAreaRatio:minRatio,maxAreaRatio:maxRatio,bytes:bytes.length}};
}
function bubble(){
 const w=64,h=64,pixels=new Uint8Array(w*h*4);let gutter=64,alphaMax=0,nonzero=0;
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  const u=(x+.5)/w*2-1,v=(y+.5)/h*2-1,r=Math.hypot(u,v),a=Math.atan2(v,u);
  const ring=Math.exp(-(((r-.73)/.055)**2))*(.42+.34*(.5+.5*Math.cos(a+2.3)));
  const hl=.72*Math.exp(-(((u+.31)/.13)**2+((v+.38)/.085)**2)),glint=.25*Math.exp(-(((u-.30)/.09)**2+((v-.35)/.11)**2));
  const opacity=r>.88?0:Math.min(1,ring+hl+glint+.035*(1-r/.88));
  const i=(y*w+x)*4;pixels[i]=pixels[i+1]=pixels[i+2]=255;pixels[i+3]=Math.round(opacity*255);
  if(pixels[i+3]){gutter=Math.min(gutter,x,y,w-1-x,h-1-y);alphaMax=Math.max(alphaMax,pixels[i+3]);nonzero++;}
 }
 if(gutter<3||alphaMax<150)throw Error("bubble alpha");
 const tex={name:"T_VIS15C_FX_Bubble",w,h,pixels},bytes=png(tex);verifyStoredPng(bytes,tex);
 return {bytes,report:{name:tex.name,width:w,height:h,alphaMax,nonzeroAlpha:nonzero,minTransparentGutterPx:gutter,bytes:bytes.length,replacementCandidate:"Assets/Dravio/Environment/Zones/Ocean/Textures/T_ENV_FX_Bubble.png"}};
}
function build(){return [fish(),bubble()];}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_ocean_vis15c_fish_bubble.mjs <new-dir>");
const a=build();mkdirSync(out);for(const [i,x] of a.entries()){writeFileSync(join(out,x.report.name+(i?".png":".glb")),x.bytes,{flag:"wx"});console.log(JSON.stringify(x.report));}
