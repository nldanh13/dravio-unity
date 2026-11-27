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
function stem(m,base,end,r,lo="357B48",hi="9BCB68"){
  const axis=norm(sub(end,base)),u=norm(cross(Math.abs(axis[1])>.9?[1,0,0]:[0,1,0],axis)),w=norm(cross(u,axis)),n=6;
  const rings=[base,end].map((c,j)=>Array.from({length:n},(_,i)=>{let a=tau*i/n,N=add(mul(u,Math.cos(a)),mul(w,Math.sin(a)));return {p:add(c,mul(N,r*(j?.72:1))),n:N,c:rgb(j?hi:lo)}}));
  for(let i=0;i<n;i++){const k=(i+1)%n,a=rings[0][i],b=rings[1][i],c=rings[1][k],d=rings[0][k];quad(m,a.p,b.p,c.p,d.p,[a.c,b.c,c.c,d.c],[a.n,b.n,c.n,d.n]);for(const [j,sign] of [[0,-1],[1,1]])tri(m,j?end:base,rings[j][i].p,rings[j][k].p,Array(3).fill(rgb(j?hi:lo)),Array(3).fill(mul(axis,sign)));}
}
// Leaf lobes have actual thickness and a center rise rather than flat planes.
function leaf(m,base,angle,length,width,rise,lo,hi){
  const u=[Math.cos(angle),0,Math.sin(angle)],w=[-Math.sin(angle),0,Math.cos(angle)];
  oval(m,add(base,add(mul(u,length*.47),[0,rise*.48,0])),[u,[0,1,0],w],[length*.5,Math.max(.013,rise*.5),width*.5],lo,hi,8,3);
}
function flower(m,x,z,h,r,lo,hi,phase){
  const base=[x,0,z],head=[x+.04*Math.cos(phase),h,z+.04*Math.sin(phase)];
  stem(m,base,head,.016);
  leaf(m,add(base,[0,h*.30,0]),phase+.8,r*1.55,r*.7,.055,"2C7844","82C961");
  leaf(m,add(base,[0,h*.49,0]),phase+3.5,r*1.3,r*.6,.05,"3A8747","A4D36B");
  for(let i=0;i<5;i++){const a=phase+tau*i/5,u=[Math.cos(a),0,Math.sin(a)],w=[-Math.sin(a),0,Math.cos(a)];oval(m,add(head,add(mul(u,r*.60),[0,.022,0])),[u,[0,1,0],w],[r*.67,r*.22,r*.45],lo,hi,8,4);}
  oval(m,add(head,[0,.032,0]),xyz,[r*.34,r*.23,r*.34],"CA8734","FFE9A5",10,4);
}
function lathe(m,c,rings,n,lo,hi){
  const L=rgb(lo),H=rgb(hi);
  const ring=rings.map(([r,y],j)=>Array.from({length:n},(_,i)=>{const t=tau*i/n,[r0,y0]=rings[Math.max(0,j-1)],[r1,y1]=rings[Math.min(rings.length-1,j+1)];return {p:add(c,[r*Math.cos(t),y,r*Math.sin(t)]),n:norm([Math.cos(t)*(y1-y0),r0-r1,Math.sin(t)*(y1-y0)]),c:mix(L,H,j/(rings.length-1))}}));
  for(let j=0;j<ring.length-1;j++)for(let i=0;i<n;i++){let k=(i+1)%n,a=ring[j][i],b=ring[j+1][i],d=ring[j][k],e=ring[j+1][k];quad(m,a.p,b.p,e.p,d.p,[a.c,b.c,e.c,d.c],[a.n,b.n,e.n,d.n]);}
  for(const [j,sign] of [[0,-1],[ring.length-1,1]])for(let i=0;i<n;i++)tri(m,add(c,[0,rings[j][1],0]),ring[j][i].p,ring[j][(i+1)%n].p,Array(3).fill(j?H:L),Array(3).fill([0,sign,0]));
}
function shroom(m,x,z,h,r,lo,hi){
  const c=[x,0,z];
  lathe(m,c,[[r*.20,0],[r*.27,h*.12],[r*.22,h*.55],[r*.24,h*.72]],10,"C9AA79","FFF2CF");
  // Dome cap with a broad rim and a small top, no baked glow.
  lathe(m,c,[[r*.22,h*.63],[r*.87,h*.65],[r,h*.74],[r*.84,h*.94],[r*.49,h*1.10],[r*.09,h*1.16]],12,lo,hi);
}
function build(){
  const flowers=mesh("Origin_Bloom_Cluster");
  flower(flowers,-.20,-.05,.35,.12,"D87491","FFD2D8",.3);
  flower(flowers,.04,.08,.49,.14,"DCA746","FFF1B1",1.1);
  flower(flowers,.24,-.07,.29,.105,"8588CD","DCD5FF",2.1);
  const fern=mesh("Origin_Fern_Rosette");
  for(let i=0;i<7;i++){const a=tau*i/7+.2,L=.34+.05*Math.sin(i*2.1);
    const dir=[Math.cos(a),0,Math.sin(a)],start=[0,.025,0],end=add(mul(dir,L),[0,.20+.03*Math.cos(i),0]);
    stem(fern,start,end,.012,"2D7748","9BCB68");
    for(let j=1;j<=3;j++)for(const side of [-1,1]){
      const t=j/4,base=add(mul(dir,L*t),[0,.025+(end[1]-.025)*t,0]);
      leaf(fern,base,a+side*(1.05-.17*t),.16*(1-.42*t),.075*(1-.35*t),.045,"267E52",j%2?"A5D976":"7DBD6B");
    }
  }
  const fungi=mesh("Origin_Coral_Mushroom_Cluster");
  shroom(fungi,-.19,-.02,.35,.16,"A84D42","F59B76");
  shroom(fungi,.065,.07,.46,.21,"AA5147","F6AA86");
  shroom(fungi,.25,-.10,.23,.125,"BD7444","FFE0A1");
  return [flowers,fern,fungi];
}
function serialize(m){
  const count=m.p.length/3,arrays=[m.p,m.n,m.c].map(x=>new Uint8Array(new Float32Array(x).buffer)),min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity];
  for(let i=0;i<count;i++){for(let k=0;k<3;k++){let x=Math.fround(m.p[i*3+k]);if(!Number.isFinite(x))throw Error("position");min[k]=Math.min(min[k],x);max[k]=Math.max(max[k],x);}if(Math.abs(Math.hypot(...m.n.slice(i*3,i*3+3))-1)>1e-5)throw Error("normal");}
  if(min[1]<-.015||max[1]>.6)throw Error("plant height/pivot");
  // Normalize each source mesh to exactly ground its lowest point.
  for(let i=1;i<m.p.length;i+=3)m.p[i]-=min[1];
  arrays[0]=new Uint8Array(new Float32Array(m.p).buffer);
  max[1]=Math.max(...m.p.filter((_,i)=>i%3===1).map(Math.fround));min[1]=Math.min(...m.p.filter((_,i)=>i%3===1).map(Math.fround));
  let offset=0;const views=arrays.map(a=>{let v={buffer:0,byteOffset:offset,byteLength:a.length,target:34962};offset+=a.length;return v});
  const g={asset:{version:"2.0",generator:"Dravio VIS.8 original flora"},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name:m.name}],meshes:[{name:m.name,primitives:[{attributes:{POSITION:0,NORMAL:1,COLOR_0:2},mode:4,material:0}]}],materials:[{name:"Flora_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.85}}],buffers:[{byteLength:offset}],bufferViews:views,accessors:[{bufferView:0,componentType:5126,count,type:"VEC3",min,max},{bufferView:1,componentType:5126,count,type:"VEC3"},{bufferView:2,componentType:5126,count,type:"VEC3"}]};
  let j=JSON.stringify(g);while(j.length%4)j+=" ";const bytes=new Uint8Array(28+j.length+offset),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;dv.setUint32(p,offset,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(const a of arrays){bytes.set(a,p);p+=a.length;}
  return {bytes,report:{name:m.name,triangles:count/3,vertices:count,materialSlots:1,bounds:{min,max},bytes:bytes.length}};
}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const dir=process.argv[2];if(!dir)throw Error("Usage: node build_origin_vis8_flora.mjs <new output directory>");
mkdirSync(dir);for(const m of build()){const {bytes,report}=serialize(m);writeFileSync(join(dir,m.name+".glb"),bytes,{flag:"wx"});console.log(JSON.stringify(report));}
