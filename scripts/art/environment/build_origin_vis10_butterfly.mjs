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

function buildButterfly(){
 const body=mesh("Butterfly_Body"),left=mesh("Wing_Left"),right=mesh("Wing_Right");
 oval(body,[0,0,0],xyz,[.012,.012,.043],"67463C","B58E66",8,4);
 oval(body,[0,.002,.043],xyz,[.014,.013,.014],"79533F","D0A575",8,3);
 for(const [m,s] of [[left,-1],[right,1]]){
   oval(m,[s*.051,0,.020],[[s*.88,0,.475],[0,1,0],[-s*.475,0,.88]],[.060,.0048,.032],"D78842","FFE0A0",10,4);
   oval(m,[s*.038,0,-.023],[[s*.92,0,-.392],[0,1,0],[s*.392,0,.92]],[.043,.0045,.029],"D17D68","FFD2A6",10,4);
   oval(m,[s*.069,.0045,.036],xyz,[.016,.0025,.012],"ECA45D","FFF2C7",8,3);
 }
 const meshes=[body,left,right],chunks=[],views=[],accessors=[];let offset=0;
 function acc(values,dim,type,target,bounds=false){
   const f=new Float32Array(values),b=new Uint8Array(f.buffer);
   const view={buffer:0,byteOffset:offset,byteLength:b.length};if(target)view.target=target;
   views.push(view);offset+=b.length;chunks.push(b);
   const a={bufferView:views.length-1,componentType:5126,count:f.length/dim,type};
   if(f.some(v=>!Number.isFinite(v)))throw Error("finite");
   if(bounds){const min=Array(dim).fill(Infinity),max=Array(dim).fill(-Infinity);for(let i=0;i<f.length;i++){min[i%dim]=Math.min(min[i%dim],f[i]);max[i%dim]=Math.max(max[i%dim],f[i]);}a.min=min;a.max=max;}
   accessors.push(a);return accessors.length-1;
 }
 const gm=meshes.map(m=>({name:m.name,primitives:[{attributes:{POSITION:acc(m.p,3,"VEC3",34962,true),NORMAL:acc(m.n,3,"VEC3",34962),COLOR_0:acc(m.c,3,"VEC3",34962)},mode:4,material:0}]}));
 const times=Array.from({length:9},(_,i)=>i*.6/8),time=acc(times,1,"SCALAR",null,true);
 const quats=s=>times.flatMap((t,i)=>{const a=s*(.46+.80*Math.cos(2*Math.PI*i/8));return [0,0,Math.sin(a/2),Math.cos(a/2)];});
 const L=quats(-1),R=quats(1);
 for(const q of [L,R]){for(let i=0;i<q.length;i+=4)if(Math.abs(Math.hypot(...q.slice(i,i+4))-1)>1e-8)throw Error("unit quaternion");if(q.slice(0,4).some((x,i)=>Math.abs(x-q[q.length-4+i])>1e-9))throw Error("loop");}
 const l=acc(L,4,"VEC4"),rr=acc(R,4,"VEC4");
 const doc={asset:{version:"2.0",generator:"Dravio VIS.10 butterfly"},scene:0,scenes:[{nodes:[0]}],nodes:[{name:"Butterfly_Root",children:[1,2,3]},{name:"Butterfly_Body",mesh:0},{name:"Wing_Left",mesh:1,translation:[-.007,0,0],rotation:L.slice(0,4)},{name:"Wing_Right",mesh:2,translation:[.007,0,0],rotation:R.slice(0,4)}],meshes:gm,materials:[{name:"Butterfly_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.85}}],animations:[{name:"Flutter_Loop",samplers:[{input:time,output:l,interpolation:"LINEAR"},{input:time,output:rr,interpolation:"LINEAR"}],channels:[{sampler:0,target:{node:2,path:"rotation"}},{sampler:1,target:{node:3,path:"rotation"}}]}],buffers:[{byteLength:offset}],bufferViews:views,accessors};
 let j=JSON.stringify(doc);while(j.length%4)j+=" ";const bytes=new Uint8Array(28+j.length+offset),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;dv.setUint32(p,offset,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(const c of chunks){bytes.set(c,p);p+=c.length;}
 const triangles=meshes.reduce((s,m)=>s+m.p.length/9,0);if(triangles>600)throw Error("triangle cap");
 return {bytes,report:{name:"Origin_Butterfly_Amber",triangles,materialSlots:1,meshes:3,nodes:4,clip:"Flutter_Loop",durationSeconds:.6,keyframesPerWing:9,wingAngleDegrees:[-.34*180/Math.PI,1.26*180/Math.PI],textures:0,bytes:bytes.length}};
}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_origin_vis10_butterfly.mjs <new-dir>");
const a=buildButterfly();mkdirSync(out);writeFileSync(join(out,a.report.name+".glb"),a.bytes,{flag:"wx"});console.log(JSON.stringify(a.report));
