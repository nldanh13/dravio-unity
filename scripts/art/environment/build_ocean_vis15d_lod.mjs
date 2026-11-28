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


// Three original non-interactive ocean accent clusters; Y up, all around silhouette.
// Tube sponge is a continuous hollow surface with an inner wall and closed basin.
function sponge(m,c,h,r,lo,hi){
 const n=8,L=rgb(lo),H=rgb(hi);
 // Profile from base up outer surface, across rounded lip, down interior.
 const rings=[[.64,0],[.86,.20],[.93,.70],[1,.94],[.89,1],[.66,.96],[.57,.78],[.53,.25]];
 const pts=rings.map(([rr,yy],j)=>Array.from({length:n},(_,i)=>{
  const a=tau*i/n,k=1+.06*Math.cos(a*3+.4);
  const [r0,y0]=rings[Math.max(0,j-1)],[r1,y1]=rings[Math.min(rings.length-1,j+1)];
  const radial=norm([Math.cos(a)*(y1-y0)*h,(r0-r1)*r,Math.sin(a)*(y1-y0)*h]);
  return {p:[c[0]+r*rr*k*Math.cos(a)+.08*h*yy,c[1]+h*yy,c[2]+r*rr*k*Math.sin(a)],n:radial,c:mix(L,H,j<=4?yy:yy*.38)};
 }));
 for(let j=0;j<pts.length-1;j++)for(let i=0;i<n;i++){
  const k=(i+1)%n,a=pts[j][i],b=pts[j+1][i],d=pts[j][k],e=pts[j+1][k];
  quad(m,a.p,b.p,e.p,d.p,[a.c,b.c,e.c,d.c],[a.n,b.n,e.n,d.n]);
 }
 for(const [j,sy] of [[0,-1],[pts.length-1,1]])for(let i=0;i<n;i++){
  const a=pts[j][i],b=pts[j][(i+1)%n],y=rings[j][1];
  tri(m,[c[0]+.08*h*y,c[1]+h*y,c[2]],a.p,b.p,Array(3).fill(j?mix(L,H,.1):L),Array(3).fill([0,sy,0]));
 }
}
function ribbon(m,root,angle,h,spread,width,lo,hi){
 const dir=[Math.cos(angle),0,Math.sin(angle)],side=[-Math.sin(angle),0,Math.cos(angle)],L=rgb(lo),H=rgb(hi),segments=4;
 // Closed blade cross section, smoothly tapered, root anchored. Last section
 // has nonzero width, capped separately, to avoid collapsed triangles.
 const pts=Array.from({length:segments+1},(_,j)=>{
  const t=j/segments,center=add(root,add(mul(dir,spread*t*t),[0,h*t,0]));
  const w=width*(.28+.72*Math.sin(Math.PI*t))*(1-.72*t),th=.009*(1-.65*t);
  return [[-1,-1],[1,-1],[1,1],[-1,1]].map(([a,b])=>({p:add(center,add(mul(side,w*a),mul(dir,th*b))),c:mix(L,H,t)}));
 });
 const face=(a,b,c,d)=>{const N=norm(cross(sub(b.p,a.p),sub(c.p,a.p)));quad(m,a.p,b.p,c.p,d.p,[a.c,b.c,c.c,d.c],Array(4).fill(N));};
 // use explicit outward expected directions to orient faces
 for(let j=0;j<segments;j++)for(let i=0;i<4;i++){
  const k=(i+1)%4,a=pts[j][i],b=pts[j+1][i],c=pts[j+1][k],d=pts[j][k],N=[mul(dir,-1),side,dir,mul(side,-1)][i];
  const actual=norm(cross(sub(b.p,a.p),sub(c.p,a.p))),n=dot(actual,N)<0?mul(actual,-1):actual;
  quad(m,a.p,b.p,c.p,d.p,[a.c,b.c,c.c,d.c],Array(4).fill(n));
 }
 for(const [j,sgn] of [[0,-1],[segments,1]]){const p=pts[j];quad(m,p[0].p,p[1].p,p[2].p,p[3].p,p.map(v=>v.c),Array(4).fill([0,sgn,0]));}
}
function build(){
 const grass=mesh("Ocean_Seagrass_Ribbon_Cluster");
 for(let i=0;i<7;i++){const a=tau*i/7+.3; ribbon(grass,[.065*Math.cos(a),0,.065*Math.sin(a)],a,.50+.13*Math.sin(i*1.9),.19+.08*Math.cos(i*2),.049,"246C68",i%2?"9EDBC0":"68C6A8");}
 const tubes=mesh("Ocean_Coral_Tube_Sponge");
 sponge(tubes,[-.17,0,.03],.50,.15,"AB626F","F3B298");
 sponge(tubes,[.06,0,.045],.68,.18,"BA6C87","FFD1AD");
 sponge(tubes,[.23,0,-.09],.38,.125,"C07773","F6BB99");
 const pearls=mesh("Ocean_Cool_Pearl_Anemone");
 oval(pearls,[0,.11,0],xyz,[.22,.11,.20],"416C99","93B5D5",8,3);
 for(let i=0;i<9;i++){
  const a=tau*i/9,dir=[Math.cos(a),0,Math.sin(a)],side=[-Math.sin(a),0,Math.cos(a)];
  const axis=norm(add(mul(dir,.48),[0,.88,0])),u=norm(cross(axis,side));
  oval(pearls,add(mul(dir,.135),[0,.22,0]),[u,axis,side],[.035,.13,.042],"627FAE",i%2?"B5E0E1":"C4C5F0",6,2);
  oval(pearls,add(mul(dir,.19),[0,.32,0]),xyz,[.044,.044,.044],"85A6CE","D6F0EB",6,2);
 }
 oval(pearls,[0,.23,0],xyz,[.085,.07,.085],"8DA2D2","D6EDEB",6,3);
 return [grass,tubes,pearls];
}
function verify(){
 return build().map(m=>{
  const r=serialize(m);if(r.report.triangles>800)throw Error("mesh triangle cap");
  return r;
 });
}


const WIND=.010;
function weight(y,h){const t=Math.max(0,Math.min(1,(y/h-.045)/.955));return Math.fround(t*t*(3-2*t));}
function deform(p,a,t,base,gust,dir){
  const w=a*WIND*Math.max(0,base+gust*.8),phase=t*1.6+p[0]*.6+p[2]*.45;
  const lean=a*WIND*gust*(.75+.25*Math.sin(phase*.37));
  return [p[0]+Math.sin(phase)*w+dir[0]*lean,p[1],p[2]+Math.cos(phase*.83)*w*.6+dir[1]*lean];
}
function windVariant(m){
  const rest=serialize(m); // grounds source mesh, retains original positions, normals, RGB.
  const h=rest.report.bounds.max[1],n=m.p.length/3,alpha=[],colors=[],uv=[],isStatic=!m.name.includes('Seagrass');
  let anchors=0,maxAlpha=0;
  const byPosition=new Map();
  for(let i=0;i<n;i++){
    const p=m.p.slice(i*3,i*3+3).map(Math.fround),a=isStatic?0:weight(p[1],h),key=p.join(",");
    if(byPosition.has(key)&&byPosition.get(key)!==a)throw Error("seam weight mismatch");byPosition.set(key,a);
    if(a===0)anchors++;maxAlpha=Math.max(maxAlpha,a);alpha.push(a);colors.push(...m.c.slice(i*3,i*3+3),a);uv.push(.5,.5);
  }
  if(!anchors||(!isStatic&&maxAlpha<.99))throw Error("missing weight range");
  let worst=0,minDot=1,minAreaRatio=Infinity,maxAreaRatio=0;
  // Sample shader math on local rest geometry (unit scale); extreme base=2.2, gust=1.
  for(const base of [.5,1,2.2])for(const gust of [0,1])for(const dir of [[1,0],[0,1],[-.70710678,-.70710678]])for(let t=0;t<12;t++){
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
  if(minDot<.8||minAreaRatio<.65||maxAreaRatio>1.35||worst>.06)throw Error("wind deformation gate "+JSON.stringify({minDot,minAreaRatio,maxAreaRatio,worst}));
  const arrays=[m.p,m.n,colors,uv].map(x=>new Uint8Array(new Float32Array(x).buffer));
  let offset=0;const views=arrays.map(a=>{const v={buffer:0,byteOffset:offset,byteLength:a.length,target:34962};offset+=a.length;return v});
  const name=m.name+"_LOD1";
  const g={asset:{version:"2.0",generator:"Dravio VIS.15d Ocean LOD"},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name}],meshes:[{name,primitives:[{attributes:{POSITION:0,NORMAL:1,COLOR_0:2,TEXCOORD_0:3},mode:4,material:0}]}],materials:[{name:isStatic?"Ocean_Static_VertexColor":"Ocean_Seagrass_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.85}}],buffers:[{byteLength:offset}],bufferViews:views,accessors:[{bufferView:0,componentType:5126,count:n,type:"VEC3",...rest.report.bounds},{bufferView:1,componentType:5126,count:n,type:"VEC3"},{bufferView:2,componentType:5126,count:n,type:"VEC4"},{bufferView:3,componentType:5126,count:n,type:"VEC2"}]};
  let j=JSON.stringify(g);while(j.length%4)j+=" ";const bytes=new Uint8Array(28+j.length+offset),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;dv.setUint32(p,offset,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(const a of arrays){bytes.set(a,p);p+=a.length;}
  return {bytes,report:{...rest.report,name,bytes:bytes.length,anchors,maxAlpha,uniquePositions:byPosition.size,samplePoses:216,windStrength:isStatic?0:WIND,maxSampleDisplacementM:worst,minFaceNormalDot:minDot,minAreaRatio,maxAreaRatio}};
}

function indexed(source){
 const raw=source.bytes,dv=new DataView(raw.buffer,raw.byteOffset,raw.byteLength),jlen=dv.getUint32(12,true);
 const doc=JSON.parse(String.fromCharCode(...raw.subarray(20,20+jlen))),binStart=28+jlen;
 const attributes=doc.meshes[0].primitives[0].attributes,oldCount=doc.accessors[0].count,values=[],ids=[],seen=new Map();
 for(let i=0;i<oldCount;i++){
  const tuple=[];
  for(const id of Object.values(attributes)){
   const a=doc.accessors[id],v=doc.bufferViews[a.bufferView],dim=Number(a.type.slice(3));
   for(let k=0;k<dim;k++)tuple.push(dv.getFloat32(binStart+v.byteOffset+(i*dim+k)*4,true));
  }
  const key=tuple.join(","),existing=seen.get(key);
  if(existing===undefined){const id=values.length;seen.set(key,id);values.push(tuple);ids.push(id);}else ids.push(existing);
 }
 if(values.length>=65536||ids.length%3)throw Error("index limit");
 const chunks=[],views=[],acc=[];let offset=0,component=0;
 for(const id of Object.values(attributes)){
  const old=doc.accessors[id],dim=Number(old.type.slice(3));
  const f=new Float32Array(values.flatMap(v=>v.slice(component,component+dim))),b=new Uint8Array(f.buffer);
  chunks.push(b);views.push({buffer:0,byteOffset:offset,byteLength:b.length,target:34962});offset+=b.length;
  acc.push({...old,bufferView:views.length-1,count:values.length});component+=dim;
 }
 const ind=new Uint16Array(ids),b=new Uint8Array(ind.buffer),padded=new Uint8Array(Math.ceil(b.length/4)*4);padded.set(b);
 views.push({buffer:0,byteOffset:offset,byteLength:b.length,target:34963});offset+=padded.length;chunks.push(padded);
 acc.push({bufferView:views.length-1,componentType:5123,count:ids.length,type:"SCALAR",min:[0],max:[values.length-1]});
 doc.meshes[0].primitives[0].indices=acc.length-1;doc.bufferViews=views;doc.accessors=acc;doc.buffers[0].byteLength=offset;
 // Verify every reconstructed tuple equals original float32 attributes.
 for(let i=0;i<oldCount;i++){let q=0;for(const id of Object.values(attributes)){
  const a=sourceAccessor(id),dim=Number(a.type.slice(3)),v=a.view;
  for(let k=0;k<dim;k++)if(values[ids[i]][q++]!==dv.getFloat32(binStart+v.byteOffset+(i*dim+k)*4,true))throw Error("attribute changed");
 }}
 function sourceAccessor(id){const old=JSON.parse(String.fromCharCode(...raw.subarray(20,20+jlen)));const a=old.accessors[id];return {...a,view:old.bufferViews[a.bufferView]};}
 let j=JSON.stringify(doc);while(j.length%4)j+=" ";const bytes=new Uint8Array(28+j.length+offset),out=new DataView(bytes.buffer);
 out.setUint32(0,0x46546c67,true);out.setUint32(4,2,true);out.setUint32(8,bytes.length,true);out.setUint32(12,j.length,true);out.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;out.setUint32(p,offset,true);out.setUint32(p+4,0x004e4942,true);p+=8;for(const c of chunks){bytes.set(c,p);p+=c.length;}
 return {bytes,report:{...source.report,vertices:values.length,unindexedVertices:oldCount,bytes:bytes.length}};
}

const baseline=[{"name":"Ocean_Seagrass_Ribbon_Cluster","triangles":364,"bounds":{"min":[-0.33183175325393677,0,-0.1907687932252884],"max":[0.32418230175971985,0.6258295774459839,0.21078097820281982]}},{"name":"Ocean_Coral_Tube_Sponge","triangles":576,"bounds":{"min":[-0.28387099504470825,0,-0.21207936108112335],"max":[0.39048394560813904,0.6800000071525574,0.22920571267604828]}},{"name":"Ocean_Cool_Pearl_Anemone","triangles":552,"bounds":{"min":[-0.2199999988079071,0,-0.22011347115039825],"max":[0.2281051129102707,0.36399999260902405,0.22011347115039825]}}];

function makeLod(){return build().map(windVariant).map(indexed).map((a,i)=>{
 const r=a.report,b=baseline[i];let extent=0,center=0;
 for(let k=0;k<3;k++){const w=b.bounds.max[k]-b.bounds.min[k];extent=Math.max(extent,Math.abs((r.bounds.max[k]-r.bounds.min[k])/w-1));center=Math.max(center,Math.abs((r.bounds.max[k]+r.bounds.min[k]-b.bounds.max[k]-b.bounds.min[k])/2));}
 if(extent>.12||center>.04||r.triangles>b.triangles*.8)throw Error("LOD gate "+JSON.stringify({name:r.name,extent,center,tris:r.triangles}));
 return {...a,report:{...r,baselineTriangles:b.triangles,reduction:1-r.triangles/b.triangles,maxExtentDelta:extent,maxCenterDelta:center}};
});}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_ocean_vis15d_lod.mjs <new-dir>");mkdirSync(out);for(const a of makeLod()){writeFileSync(join(out,a.report.name+".glb"),a.bytes,{flag:"wx"});console.log(JSON.stringify(a.report));}
