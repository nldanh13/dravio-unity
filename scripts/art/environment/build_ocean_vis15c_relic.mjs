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
  if(min[1]<-.025||max[1]>1.3)throw Error("plant height/pivot");
  // Normalize each source mesh to exactly ground its lowest point.
  for(let i=1;i<m.p.length;i+=3)m.p[i]-=min[1];
  arrays[0]=new Uint8Array(new Float32Array(m.p).buffer);
  max[1]=Math.max(...m.p.filter((_,i)=>i%3===1).map(Math.fround));min[1]=Math.min(...m.p.filter((_,i)=>i%3===1).map(Math.fround));
  let offset=0;const views=arrays.map(a=>{let v={buffer:0,byteOffset:offset,byteLength:a.length,target:34962};offset+=a.length;return v});
  const g={asset:{version:"2.0",generator:"Dravio VIS.15 ocean accents"},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name:m.name}],meshes:[{name:m.name,primitives:[{attributes:{POSITION:0,NORMAL:1,COLOR_0:2},mode:4,material:0}]}],materials:[{name:"Flora_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.85}}],buffers:[{byteLength:offset}],bufferViews:views,accessors:[{bufferView:0,componentType:5126,count,type:"VEC3",min,max},{bufferView:1,componentType:5126,count,type:"VEC3"},{bufferView:2,componentType:5126,count,type:"VEC3"}]};
  let j=JSON.stringify(g);while(j.length%4)j+=" ";const bytes=new Uint8Array(28+j.length+offset),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;dv.setUint32(p,offset,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(const a of arrays){bytes.set(a,p);p+=a.length;}
  return {bytes,report:{name:m.name,triangles:count/3,vertices:count,materialSlots:1,bounds:{min,max},bytes:bytes.length}};
}



function relic(){
 const m=mesh("Ocean_Tide_Relief_Fragment");
 // Rounded bevel profile, front faces -Z. Closed convex slab, asymmetric chipped crown.
 const outline=[[-.32,.09],[-.34,.52],[-.27,.72],[-.04,.82],[.10,.75],[.28,.74],[.34,.57],[.32,.07],[.23,0],[-.23,0]];
 // clockwise profile in XY; bevel offsets toward center.
 const center=[0,.39],layers=[[-.115,.87],[-.075,1],[.085,1],[.125,.85]];
 const pts=layers.map(([z,k])=>outline.map(([x,y])=>[center[0]+(x-center[0])*k,center[1]+(y-center[1])*k,z]));
 const col=rgb("80A8AA"),hi=rgb("B4CEBE"),dark=rgb("527B86");
 function face(a,b,c,d,n,cols){quad(m,a,b,c,d,cols,Array(4).fill(n));}
 for(let j=0;j<layers.length-1;j++)for(let i=0;i<outline.length;i++){const k=(i+1)%outline.length,a=pts[j][i],b=pts[j][k],c=pts[j+1][k],d=pts[j+1][i];const e=sub(b,a),n=norm([-e[1],e[0],0]);face(a,b,c,d,n,[col,col,dark,dark]);}
 for(const [j,sign] of [[0,-1],[layers.length-1,1]])for(let i=0;i<outline.length;i++)tri(m,[0,.39,layers[j][0]],pts[j][i],pts[j][(i+1)%outline.length],Array(3).fill(j?col:hi),Array(3).fill([0,0,sign]));
 // Small raised scallop motif, volumetric ridges, not text or collectible.
 for(let i=0;i<7;i++){const a=-1.05+i*.35,axis=[Math.sin(a),Math.cos(a),0],side=[Math.cos(a),-Math.sin(a),0];oval(m,[.12*Math.sin(a),.38+.12*Math.cos(a),-.123],[side,axis,[0,0,1]],[.028,.135,.024],"759D9D","D2D8B4",6,3);}
 oval(m,[0,.35,-.143],xyz,[.063,.045,.027],"B69B6B","E0D6B0",8,3);
 // Coralline nodules at the foot, subtle accents.
 for(const [x,y,z,r] of [[-.23,.07,-.095,.052],[-.15,.045,-.115,.037],[.25,.08,-.09,.045]])oval(m,[x,y,z],xyz,[r,r*.75,r*.8],"B6818A","E3B5A5",6,3);
 const r=serialize(m);if(r.report.triangles>500)throw Error("cap");return r;
}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_ocean_vis15c_relic.mjs <new-dir>");
const a=relic();mkdirSync(out);writeFileSync(join(out,a.report.name+".glb"),a.bytes,{flag:"wx"});console.log(JSON.stringify(a.report));
