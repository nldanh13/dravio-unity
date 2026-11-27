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
function serialize(m){
  const count=m.p.length/3,arrays=[m.p,m.n,m.c].map(x=>new Uint8Array(new Float32Array(x).buffer)),min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity];
  for(let i=0;i<count;i++){for(let k=0;k<3;k++){let x=Math.fround(m.p[i*3+k]);if(!Number.isFinite(x))throw Error("position");min[k]=Math.min(min[k],x);max[k]=Math.max(max[k],x);}if(Math.abs(Math.hypot(...m.n.slice(i*3,i*3+3))-1)>1e-5)throw Error("normal");}
  if(min[1]<-3||max[1]>2)throw Error("bounds gate");
  // Normalize each source mesh to exactly ground its lowest point.
  if(!m.name.includes("Floating"))for(let i=1;i<m.p.length;i+=3)m.p[i]-=min[1];
  arrays[0]=new Uint8Array(new Float32Array(m.p).buffer);
  max[1]=Math.max(...m.p.filter((_,i)=>i%3===1).map(Math.fround));min[1]=Math.min(...m.p.filter((_,i)=>i%3===1).map(Math.fround));
  let offset=0;const views=arrays.map(a=>{let v={buffer:0,byteOffset:offset,byteLength:a.length,target:34962};offset+=a.length;return v});
  const g={asset:{version:"2.0",generator:"Dravio VIS.16 sky accents"},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name:m.name}],meshes:[{name:m.name,primitives:[{attributes:{POSITION:0,NORMAL:1,COLOR_0:2},mode:4,material:0}]}],materials:[{name:"Flora_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.85}}],buffers:[{byteLength:offset}],bufferViews:views,accessors:[{bufferView:0,componentType:5126,count,type:"VEC3",min,max},{bufferView:1,componentType:5126,count,type:"VEC3"},{bufferView:2,componentType:5126,count,type:"VEC3"}]};
  let j=JSON.stringify(g);while(j.length%4)j+=" ";const bytes=new Uint8Array(28+j.length+offset),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;dv.setUint32(p,offset,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(const a of arrays){bytes.set(a,p);p+=a.length;}
  return {bytes,report:{name:m.name,triangles:count/3,vertices:count,materialSlots:1,bounds:{min,max},bytes:bytes.length}};
}



// Distant decorative island, closed layered silhouette, surface pivot Y=0.
function island(){
 const m=mesh("Sky_Floating_Garden_Islet"),n=12;
 const rings=[[.78,0],[1,-.11],[.91,-.34],[.66,-.72],[.37,-1.12],[.15,-1.46]];
 const palette=["B8D89A","91BF8D","C8C3D0","9FAEC5","8794B2","777F9E"];
 const P=rings.map(([r,y],j)=>Array.from({length:n},(_,i)=>{
 const a=tau*i/n,k=1+.075*Math.sin(3*a+.5)+.035*Math.cos(5*a);
 return [1.05*r*k*Math.cos(a)+.15*(j/(rings.length-1)),-0+y,.76*r*k*Math.sin(a)];
 }));
 function flat(a,b,c,col,expected){let normal=norm(cross(sub(b,a),sub(c,a)));if(dot(normal,expected)<0)normal=mul(normal,-1);tri(m,a,b,c,col,Array(3).fill(normal));}
 for(let j=0;j<P.length-1;j++)for(let i=0;i<n;i++){
 const k=(i+1)%n,a=P[j][i],b=P[j+1][i],c=P[j+1][k],d=P[j][k],t=tau*(i+.5)/n,N=[Math.cos(t),j===0?.4:-.1,Math.sin(t)],L=rgb(palette[j]),H=rgb(palette[j+1]);
 flat(a,b,c,[L,H,H],N);flat(a,c,d,[L,H,L],N);
 }
 for(let i=0;i<n;i++){const k=(i+1)%n;flat([0,0,0],P[0][i],P[0][k],Array(3).fill(rgb(palette[0])),[0,1,0]);flat([.15,-1.55,0],P.at(-1)[i],P.at(-1)[k],Array(3).fill(rgb(palette.at(-1))),[0,-1,0]);}
 // Deliberately no vegetation or walkable collider on the distant island.
 return m;
}
function crystal(){
 const m=mesh("Sky_Aurora_Crystal_Cluster");
 for(const [x,z,h,r,lean,lo,hi] of [[-.17,.01,.49,.11,-.09,"859ACD","C6EFF2"],[.035,0,.79,.15,.04,"919ACD","D8F2F4"],[.22,-.05,.42,.10,.11,"B0A6D9","E9D9F4"]]){
 const n=6,L=rgb(lo),H=rgb(hi),rings=[[0,.78],[.12,1],[.74,.94]];
 const p=rings.map(([t,w])=>Array.from({length:n},(_,i)=>[x+lean*t+r*w*Math.cos(tau*i/n),h*t,z+r*w*Math.sin(tau*i/n)]));
 for(let i=0;i<n;i++){
 const k=(i+1)%n,a=tau*(i+.5)/n,N=[Math.cos(a),0,Math.sin(a)];
 for(let j=0;j<p.length-1;j++){
 const v=[p[j][i],p[j+1][i],p[j+1][k],p[j][k]],normal=norm(cross(sub(v[1],v[0]),sub(v[2],v[0]))),q=dot(normal,N)<0?mul(normal,-1):normal;
 const c=Array(4).fill(mix(L,H,.2+j*.28+(i%2)*.1));quad(m,...v,c,Array(4).fill(q));
 }
 const tip=[x+lean,h,z],a0=p[2][i],b0=p[2][k],nn=norm(cross(sub(b0,a0),sub(tip,a0))),q=dot(nn,N)<0?mul(nn,-1):nn;
 tri(m,a0,b0,tip,[H,H,rgb("EFFCF4")],Array(3).fill(q));
 tri(m,[x,0,z],p[0][i],p[0][k],Array(3).fill(L),Array(3).fill([0,-1,0]));
 }
 }
 oval(m,[0,.055,0],xyz,[.36,.055,.25],"A4BACB","E0E6DB",10,3);
 return m;
}
function blossoms(){
 const m=mesh("Sky_Star_Blossom_Cluster");
 for(const [x,z,h,col] of [[-.19,.02,.39,"E4AAD0"],[.03,.04,.54,"C8B7E7"],[.21,-.07,.32,"F5D5A6"]]){
 oval(m,[x,h*.48,z],xyz,[.018,h*.52,.018],"648F83","A6D0A3",6,3);
 for(const sign of [-1,1]){
 const axis=norm([sign*.75,.55,.35]),u=norm(cross(axis,[0,0,1])),v=norm(cross(u,axis));
 oval(m,[x+sign*.065,h*.32,z], [u,axis,v],[.033,.10,.016],"80AB88","B6DCA5",6,3);
 }
 for(let j=0;j<5;j++){
 const a=tau*j/5,dir=[Math.cos(a),0,Math.sin(a)],side=[-Math.sin(a),0,Math.cos(a)];
 oval(m,[x+.065*dir[0],h,z+.065*dir[2]],[dir,[0,1,0],side],[.069,.022,.037],col,"FFF0EE",8,3);
 }
 oval(m,[x,h+.012,z],xyz,[.042,.026,.042],"D7AE69","FFF1B7",8,3);
 }
 return m;
}
const WIND=.008;
function weight(y,h){const t=Math.max(0,Math.min(1,(y/h-.045)/.955));return Math.fround(t*t*(3-2*t));}
function deform(p,a,t,base,gust,dir){
  const w=a*WIND*Math.max(0,base+gust*.8),phase=t*1.6+p[0]*.6+p[2]*.45;
  const lean=a*WIND*gust*(.75+.25*Math.sin(phase*.37));
  return [p[0]+Math.sin(phase)*w+dir[0]*lean,p[1],p[2]+Math.cos(phase*.83)*w*.6+dir[1]*lean];
}
function windVariant(m){
  const rest=serialize(m); // grounds source mesh, retains original positions, normals, RGB.
  const h=rest.report.bounds.max[1],n=m.p.length/3,alpha=[],colors=[],uv=[];
  let anchors=0,maxAlpha=0;
  const byPosition=new Map();
  for(let i=0;i<n;i++){
    const p=m.p.slice(i*3,i*3+3).map(Math.fround),a=weight(p[1],h),key=p.join(",");
    if(byPosition.has(key)&&byPosition.get(key)!==a)throw Error("seam weight mismatch");byPosition.set(key,a);
    if(a===0)anchors++;maxAlpha=Math.max(maxAlpha,a);alpha.push(a);colors.push(...m.c.slice(i*3,i*3+3),a);uv.push(.5,.5);
  }
  if(!anchors||maxAlpha<.99)throw Error("missing weight range");
  let worst=0,minDot=1,minAreaRatio=Infinity,maxAreaRatio=0;
  // Sample shader math on local rest geometry (unit scale); extreme base=2.2, gust=1.
  for(const base of [.5,1,1.6,2.2,3.2])for(const gust of [0,1])for(const dir of [[1,0],[0,1],[-.70710678,-.70710678]])for(let t=0;t<12;t++){
    const moved=[];
    for(let i=0;i<n;i++){
      const p=m.p.slice(i*3,i*3+3),q=deform(p,alpha[i],t*.37,base,gust,dir);
      const d=Math.hypot(...sub(q,p));if(alpha[i]===0&&d!==0)throw Error("anchor moved");worst=Math.max(worst,d);moved.push(q);
    }
    for(let i=0;i<n;i+=3){
      const p=[0,1,2].map(j=>m.p.slice((i+j)*3,(i+j)*3+3)),q=moved.slice(i,i+3);
      const a=cross(sub(p[1],p[0]),sub(p[2],p[0])),b=cross(sub(q[1],q[0]),sub(q[2],q[0]));
      const ratio=Math.hypot(...b)/Math.hypot(...a);
      minAreaRatio=Math.min(minAreaRatio,ratio);maxAreaRatio=Math.max(maxAreaRatio,ratio);minDot=Math.min(minDot,dot(norm(a),norm(b)));
    }
  }
  if(minDot<.8||minAreaRatio<.65||maxAreaRatio>1.35||worst>.065)throw Error("wind deformation gate "+JSON.stringify({minDot,minAreaRatio,maxAreaRatio,worst}));
  const arrays=[m.p,m.n,colors,uv].map(x=>new Uint8Array(new Float32Array(x).buffer));
  let offset=0;const views=arrays.map(a=>{const v={buffer:0,byteOffset:offset,byteLength:a.length,target:34962};offset+=a.length;return v});
  const name=m.name+"_Wind";
  const g={asset:{version:"2.0",generator:"Dravio VIS.16c Sky blossom wind"},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name}],meshes:[{name,primitives:[{attributes:{POSITION:0,NORMAL:1,COLOR_0:2,TEXCOORD_0:3},mode:4,material:0}]}],materials:[{name:"Sky_Blossom_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.85}}],buffers:[{byteLength:offset}],bufferViews:views,accessors:[{bufferView:0,componentType:5126,count:n,type:"VEC3",...rest.report.bounds},{bufferView:1,componentType:5126,count:n,type:"VEC3"},{bufferView:2,componentType:5126,count:n,type:"VEC4"},{bufferView:3,componentType:5126,count:n,type:"VEC2"}]};
  let j=JSON.stringify(g);while(j.length%4)j+=" ";const bytes=new Uint8Array(28+j.length+offset),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;dv.setUint32(p,offset,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(const a of arrays){bytes.set(a,p);p+=a.length;}
  return {bytes,report:{...rest.report,name,bytes:bytes.length,anchors,maxAlpha,uniquePositions:byPosition.size,samplePoses:360,windStrength:WIND,maxSampleDisplacementM:worst,minFaceNormalDot:minDot,minAreaRatio,maxAreaRatio}};
}

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



// Soft five-lobed cherry-blossom petal silhouette (one detached petal, not a full flower).
// White RGB preserves the existing particle tint; variation is authored in alpha only.
function petal(){
 const w=64,h=64,pixels=new Uint8Array(w*h*4);let gutter=64,alphaMax=0,nonzero=0;
 const clamp=x=>Math.max(0,Math.min(1,x)),smooth=(a,b,x)=>{const t=clamp((x-a)/(b-a));return t*t*(3-2*t)};
 function coverage(u,v){
  const t=(v-.13)/.74;if(t<=0||t>=1)return 0;
  const mid=.50+.028*Math.sin(Math.PI*t);
  const width=.266*Math.pow(Math.sin(Math.PI*t),.68)*(1.12-.44*t);
  const d=Math.abs(u-mid)/width;
  const edge=1-smooth(.88,1.04,d);
  const notch=smooth(.67,.87,v)*(1-smooth(.02,.075,Math.abs(u-mid)));
  const fold=.82+.18*Math.exp(-(((u-mid+.04)/.12)**2));
  return clamp(edge*(1-notch*.95)*fold*smooth(0,.075,t)*(1-smooth(.87,1,t)));
 }
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  let q=0;for(let j=0;j<4;j++)for(let i=0;i<4;i++)q+=coverage((x+(i+.5)/4)/w,(y+(j+.5)/4)/h)/16;
  const p=(y*w+x)*4;pixels[p]=pixels[p+1]=pixels[p+2]=255;pixels[p+3]=Math.round(255*q);
  if(pixels[p+3]){gutter=Math.min(gutter,x,y,w-1-x,h-1-y);nonzero++;alphaMax=Math.max(alphaMax,pixels[p+3]);}
 }
 if(gutter<6||alphaMax<180||nonzero<700||nonzero>1800)throw Error("petal mask "+JSON.stringify({gutter,alphaMax,nonzero}));
 const tex={name:"T_VIS16C_FX_Petal",w,h,pixels},bytes=png(tex);verifyStoredPng(bytes,tex);
 return {bytes,report:{name:tex.name,width:w,height:h,alphaMax,nonzeroAlpha:nonzero,minTransparentGutterPx:gutter,bytes:bytes.length,replacementCandidate:"Assets/Dravio/Environment/Zones/Sky/Textures/T_ENV_FX_Petal.png"}};
}
function build(){
 const b=blossoms(),original=serialize(blossoms()),a=windVariant(b);
 if(a.report.triangles!==original.report.triangles||a.report.bounds.min.some((v,i)=>Math.abs(v-original.report.bounds.min[i])>1e-7)||a.report.bounds.max.some((v,i)=>Math.abs(v-original.report.bounds.max[i])>1e-7))throw Error("rest bounds changed");
 return [a,petal()];
}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_sky_vis16c_blossom_petal.mjs <new-dir>");
const a=build();mkdirSync(out);for(const [i,x] of a.entries()){writeFileSync(join(out,x.report.name+(i?".png":".glb")),x.bytes,{flag:"wx"});console.log(JSON.stringify(x.report));}
