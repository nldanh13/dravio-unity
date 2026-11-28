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

// Closed decorative primitives, with a position-edge orientation check per piece.
// Touching/intersecting pieces are not boolean-unioned. All colors linear RGB.
function component(m,name,fn){
 const p=mesh(name);fn(p);const edges=new Map();let volume=0;
 for(let i=0;i<p.p.length;i+=9){
  const v=[0,1,2].map(j=>p.p.slice(i+j*3,i+j*3+3).map(Math.fround));
  volume+=dot(v[0],cross(v[1],v[2]))/6;
  for(let j=0;j<3;j++){
   const a=v[j].join(","),b=v[(j+1)%3].join(","),key=a<b?a+"|"+b:b+"|"+a,sign=a<b?1:-1;
   const e=edges.get(key)??[0,0];e[0]++;e[1]+=sign;edges.set(key,e);
  }
 }
 if(volume<=0||[...edges.values()].some(e=>e[0]!==2||e[1]!==0))throw Error("closed component "+name+" "+volume);
 m.p.push(...p.p);m.n.push(...p.n);m.c.push(...p.c);m.components=(m.components??0)+1;
}
function flat(m,a,b,c,cols,expected){
 let n=norm(cross(sub(b,a),sub(c,a)));if(dot(n,expected)<0)n=mul(n,-1);
 tri(m,a,b,c,cols,[n,n,n]);
}
function lathe(m,x,z,profile,n=12){
 const P=profile.map(([y,r,col])=>Array.from({length:n},(_,i)=>[x+r*Math.cos(tau*i/n),y,z+r*Math.sin(tau*i/n)]));
 for(let j=0;j<P.length-1;j++)for(let i=0;i<n;i++){
  const k=(i+1)%n,a=P[j][i],b=P[j+1][i],c=P[j+1][k],d=P[j][k],angle=tau*(i+.5)/n;
  const expected=[Math.cos(angle)*(profile[j+1][0]-profile[j][0]),profile[j][1]-profile[j+1][1],Math.sin(angle)*(profile[j+1][0]-profile[j][0])];
  const L=rgb(profile[j][2]),H=rgb(profile[j+1][2]);
  flat(m,a,b,c,[L,H,H],expected);flat(m,a,c,d,[L,H,L],expected);
 }
 for(const [j,sign] of [[0,-1],[P.length-1,1]])for(let i=0;i<n;i++)flat(m,[x,profile[j][0],z],P[j][i],P[j][(i+1)%n],Array(3).fill(rgb(profile[j][2])),[0,sign,0]);
}
const ivory="E7E6DA",shadow="A9B8C8",cream="F9F4E2",gold="C4A166",brightGold="EED19B";
function plinth(m){
 component(m,"base",p=>lathe(p,0,0,[[0,.28,shadow],[.035,.32,ivory],[.10,.32,cream],[.135,.27,ivory],[.17,.23,shadow]],12));
 component(m,"base trim",p=>lathe(p,0,0,[[.14,.245,gold],[.152,.258,brightGold],[.175,.258,brightGold],[.19,.23,gold]],12));
}
function gem(m,c,h,r){
 const [x,y,z]=c,n=6;
 const P=[[.0,.24],[.23,.9],[.73,1],[.90,.46]].map(([t,k])=>Array.from({length:n},(_,i)=>[x+r*k*Math.cos(tau*i/n),y+h*t,z+r*k*Math.sin(tau*i/n)]));
 for(let j=0;j<P.length-1;j++)for(let i=0;i<n;i++){
  const k=(i+1)%n,a=P[j][i],b=P[j+1][i],c=P[j+1][k],d=P[j][k],angle=tau*(i+.5)/n;
  const N=[Math.cos(angle),.1,Math.sin(angle)],L=rgb(i%2?"A7B0E0":"82BDD5"),H=rgb(j===2?"F2FCF4":"C8E9EE");
  flat(m,a,b,c,[L,H,H],N);flat(m,a,c,d,[L,H,L],N);
 }
 for(let i=0;i<n;i++){const k=(i+1)%n;flat(m,[x,y+h,z],P[3][i],P[3][k],Array(3).fill(rgb("E9F9F8")),[0,1,0]);flat(m,[x,y,z],P[0][i],P[0][k],Array(3).fill(rgb("7D97C1")),[0,-1,0]);}
}
function halo(m,center,R,r,n=24,k=8){
 const P=[],N=[],C=[];
 for(let i=0;i<n;i++){const a=tau*i/n;P[i]=[];N[i]=[];C[i]=[];for(let j=0;j<k;j++){const b=tau*j/k;
  P[i][j]=add(center,[(R+r*Math.cos(b))*Math.cos(a),(R+r*Math.cos(b))*Math.sin(a),r*Math.sin(b)]);
  N[i][j]=[Math.cos(a)*Math.cos(b),Math.sin(a)*Math.cos(b),Math.sin(b)];
  C[i][j]=mix(rgb(gold),rgb(brightGold),.4+.4*Math.sin(a)+.2*Math.sin(b));
 }}
 for(let i=0;i<n;i++)for(let j=0;j<k;j++){const a=(i+1)%n,b=(j+1)%k;quad(m,P[i][j],P[a][j],P[a][b],P[i][b],[C[i][j],C[a][j],C[a][b],C[i][b]],[N[i][j],N[a][j],N[a][b],N[i][b]]);}
}
function lantern(){
 const m=mesh("Sky_Astral_Crystal_Lantern");plinth(m);
 component(m,"column",p=>lathe(p,0,0,[[.175,.16,shadow],[.22,.19,ivory],[.29,.13,cream],[.48,.13,ivory],[.53,.21,cream],[.57,.22,gold]],10));
 component(m,"gem",p=>gem(p,[0,.55,0],.47,.15));
 for(let i=0;i<4;i++){const a=tau*(i+.5)/4,x=.205*Math.cos(a),z=.205*Math.sin(a);
  component(m,"strut"+i,p=>lathe(p,x,z,[[.55,.026,gold],[.60,.035,brightGold],[.91,.025,ivory],[.97,.033,gold]],8));
 }
 component(m,"canopy",p=>lathe(p,0,0,[[.95,.245,gold],[.99,.25,brightGold],[1.035,.21,cream],[1.10,.115,ivory],[1.14,.045,gold]],12));
 component(m,"finial",p=>oval(p,[0,1.17,0],xyz,[.055,.055,.055],gold,brightGold,8,3));
 return m;
}
function star(p){
 const center=[0,.91,-.012],n=8,R=.19,r=.075,depth=.043;
 const outline=Array.from({length:n},(_,i)=>{const a=Math.PI/2+tau*i/n,s=i%2?r:R;return [s*Math.cos(a),s*Math.sin(a)];});
 const layers=[[-depth,.85],[-depth*.4,1],[depth*.4,1],[depth,.85]],P=layers.map(([z,k])=>outline.map(([x,y])=>add(center,[x*k,y*k,z])));
 for(let j=0;j<P.length-1;j++)for(let i=0;i<n;i++){const k=(i+1)%n,a=P[j][i],b=P[j][k],c=P[j+1][k],d=P[j+1][i],e=sub(b,a),N=[e[1],-e[0],0],L=rgb("93B2CF"),H=rgb("DAF2F0");flat(p,a,b,c,[L,H,H],N);flat(p,a,c,d,[L,H,L],N);}
 for(const [j,sign] of [[0,-1],[3,1]])for(let i=0;i<n;i++)flat(p,add(center,[0,0,layers[j][0]+sign*.016]),P[j][i],P[j][(i+1)%n],[rgb("F5FFF5"),rgb("B7DCE8"),rgb("B7DCE8")],[0,0,sign]);
}
function monument(){
 const m=mesh("Sky_Celestial_Halo_Monument");plinth(m);
 component(m,"stem",p=>lathe(p,0,0,[[.17,.17,shadow],[.24,.19,cream],[.32,.115,ivory],[.46,.10,cream],[.54,.16,gold],[.59,.13,brightGold]],12));
 component(m,"halo",p=>halo(p,[0,.91,0],.33,.035));
 component(m,"star support",p=>lathe(p,0,0,[[.57,.018,gold],[.74,.018,brightGold]],8));
 component(m,"star",star);
 return m;
}
function serialize(m){
 const vertices=[],indices=[],seen=new Map(),pos=[],normal=[],color=[],uv=[],min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity];
 for(let i=0;i<m.p.length/3;i++){
  const p=m.p.slice(i*3,i*3+3).map(Math.fround),n=m.n.slice(i*3,i*3+3).map(Math.fround),c=[...m.c.slice(i*3,i*3+3).map(Math.fround),0],tuple=[...p,...n,...c,.5,.5];
  if(tuple.some(v=>!Number.isFinite(v))||Math.abs(Math.hypot(...n)-1)>1e-5)throw Error("attribute");
  const key=tuple.join(",");let k=seen.get(key);
  if(k===undefined){k=vertices.length;vertices.push(tuple);seen.set(key,k);pos.push(...p);normal.push(...n);color.push(...c);uv.push(.5,.5);}
  indices.push(k);for(let d=0;d<3;d++){min[d]=Math.min(min[d],p[d]);max[d]=Math.max(max[d],p[d]);}
 }
 for(let i=0;i<indices.length;i++){const a=vertices[indices[i]],b=[...m.p.slice(i*3,i*3+3),...m.n.slice(i*3,i*3+3),...m.c.slice(i*3,i*3+3),0,.5,.5].map(Math.fround);if(a.some((x,k)=>x!==b[k]))throw Error("index reconstruction");}
 if(Math.abs(min[1])>1e-7||max[1]>1.4||indices.length/3>1600||vertices.length>=65536)throw Error("bounds/budget");
 const arrays=[new Float32Array(pos),new Float32Array(normal),new Float32Array(color),new Float32Array(uv),new Uint16Array(indices)].map(x=>new Uint8Array(x.buffer));
 let offset=0;const views=arrays.map((a,i)=>{const v={buffer:0,byteOffset:offset,byteLength:a.length,target:i===4?34963:34962};offset+=a.length;return v;});
 const g={asset:{version:"2.0",generator:"Dravio VIS.16d architecture"},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name:m.name}],meshes:[{name:m.name,primitives:[{attributes:{POSITION:0,NORMAL:1,COLOR_0:2,TEXCOORD_0:3},indices:4,mode:4,material:0}]}],materials:[{name:"Sky_Architecture_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.85}}],buffers:[{byteLength:offset}],bufferViews:views,accessors:[{bufferView:0,componentType:5126,count:vertices.length,type:"VEC3",min,max},{bufferView:1,componentType:5126,count:vertices.length,type:"VEC3"},{bufferView:2,componentType:5126,count:vertices.length,type:"VEC4"},{bufferView:3,componentType:5126,count:vertices.length,type:"VEC2"},{bufferView:4,componentType:5123,count:indices.length,type:"SCALAR"}]};
 let j=JSON.stringify(g);while(j.length%4)j+=" ";const padded=(offset+3)&~3,bytes=new Uint8Array(28+j.length+padded),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;dv.setUint32(p,padded,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(const a of arrays){bytes.set(a,p);p+=a.length;}
 return {bytes,report:{name:m.name,triangles:indices.length/3,vertices:vertices.length,unindexedVertices:indices.length,closedComponents:m.components,materialSlots:1,bounds:{min,max},bytes:bytes.length}};
}
function build(){return [lantern(),monument()].map(serialize);}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_sky_vis16d_architecture.mjs <new-dir>");
const a=build();mkdirSync(out);for(const x of a){writeFileSync(join(out,x.report.name+".glb"),x.bytes,{flag:"wx"});console.log(JSON.stringify(x.report));}
