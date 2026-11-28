function flowerLOD(){
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
 oval(m,[x,h*.48,z],xyz,[.018,h*.52,.018],"648F83","A6D0A3",6,2);
 for(const sign of [-1,1]){
 const axis=norm([sign*.75,.55,.35]),u=norm(cross(axis,[0,0,1])),v=norm(cross(u,axis));
 oval(m,[x+sign*.065,h*.32,z], [u,axis,v],[.033,.10,.016],"80AB88","B6DCA5",6,2);
 }
 for(let j=0;j<5;j++){
 const a=tau*j/5,dir=[Math.cos(a),0,Math.sin(a)],side=[-Math.sin(a),0,Math.cos(a)];
 oval(m,[x+.065*dir[0],h,z+.065*dir[2]],[dir,[0,1,0],side],[.069,.022,.037],col,"FFF0EE",8,2);
 }
 oval(m,[x,h+.012,z],xyz,[.042,.026,.042],"D7AE69","FFF1B7",8,2);
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
  const name=m.name+"_LOD1";
  const g={asset:{version:"2.0",generator:"Dravio VIS.16f blossom LOD"},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name}],meshes:[{name,primitives:[{attributes:{POSITION:0,NORMAL:1,COLOR_0:2,TEXCOORD_0:3},mode:4,material:0}]}],materials:[{name:"Sky_Blossom_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.85}}],buffers:[{byteLength:offset}],bufferViews:views,accessors:[{bufferView:0,componentType:5126,count:n,type:"VEC3",...rest.report.bounds},{bufferView:1,componentType:5126,count:n,type:"VEC3"},{bufferView:2,componentType:5126,count:n,type:"VEC4"},{bufferView:3,componentType:5126,count:n,type:"VEC2"}]};
  let j=JSON.stringify(g);while(j.length%4)j+=" ";const bytes=new Uint8Array(28+j.length+offset),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;dv.setUint32(p,offset,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(const a of arrays){bytes.set(a,p);p+=a.length;}
  return {bytes,report:{...rest.report,name,bytes:bytes.length,anchors,maxAlpha,uniquePositions:byPosition.size,samplePoses:360,windStrength:WIND,maxSampleDisplacementM:worst,minFaceNormalDot:minDot,minAreaRatio,maxAreaRatio}};
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


return indexed(windVariant(blossoms()));
}
function architectureLOD(){
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
 component(m,"base",p=>lathe(p,0,0,[[0,.28,shadow],[.035,.32,ivory],[.10,.32,cream],[.135,.27,ivory],[.17,.23,shadow]],8));
 component(m,"base trim",p=>lathe(p,0,0,[[.14,.245,gold],[.152,.258,brightGold],[.175,.258,brightGold],[.19,.23,gold]],8));
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
function halo(m,center,R,r,n=16,k=6){
 const P=[],N=[],C=[];
 for(let i=0;i<n;i++){const a=tau*i/n;P[i]=[];N[i]=[];C[i]=[];for(let j=0;j<k;j++){const b=tau*j/k;
  P[i][j]=add(center,[(R+r*Math.cos(b))*Math.cos(a),(R+r*Math.cos(b))*Math.sin(a),r*Math.sin(b)]);
  N[i][j]=[Math.cos(a)*Math.cos(b),Math.sin(a)*Math.cos(b),Math.sin(b)];
  C[i][j]=mix(rgb(gold),rgb(brightGold),.4+.4*Math.sin(a)+.2*Math.sin(b));
 }}
 for(let i=0;i<n;i++)for(let j=0;j<k;j++){const a=(i+1)%n,b=(j+1)%k;quad(m,P[i][j],P[a][j],P[a][b],P[i][b],[C[i][j],C[a][j],C[a][b],C[i][b]],[N[i][j],N[a][j],N[a][b],N[i][b]]);}
}
function lantern(){
 const m=mesh("Sky_Astral_Crystal_Lantern_LOD1");plinth(m);
 component(m,"column",p=>lathe(p,0,0,[[.175,.16,shadow],[.22,.19,ivory],[.29,.13,cream],[.48,.13,ivory],[.53,.21,cream],[.57,.22,gold]],8));
 component(m,"gem",p=>gem(p,[0,.55,0],.47,.15));
 for(let i=0;i<4;i++){const a=tau*(i+.5)/4,x=.205*Math.cos(a),z=.205*Math.sin(a);
  component(m,"strut"+i,p=>lathe(p,x,z,[[.55,.026,gold],[.60,.035,brightGold],[.91,.025,ivory],[.97,.033,gold]],6));
 }
 component(m,"canopy",p=>lathe(p,0,0,[[.95,.245,gold],[.99,.25,brightGold],[1.035,.21,cream],[1.10,.115,ivory],[1.14,.045,gold]],8));
 component(m,"finial",p=>oval(p,[0,1.17,0],xyz,[.055,.055,.055],gold,brightGold,8,2));
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
 const m=mesh("Sky_Celestial_Halo_Monument_LOD1");plinth(m);
 component(m,"stem",p=>lathe(p,0,0,[[.17,.17,shadow],[.24,.19,cream],[.32,.115,ivory],[.46,.10,cream],[.54,.16,gold],[.59,.13,brightGold]],8));
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
 const g={asset:{version:"2.0",generator:"Dravio VIS.16f architecture LOD"},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name:m.name}],meshes:[{name:m.name,primitives:[{attributes:{POSITION:0,NORMAL:1,COLOR_0:2,TEXCOORD_0:3},indices:4,mode:4,material:0}]}],materials:[{name:"Sky_Architecture_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.85}}],buffers:[{byteLength:offset}],bufferViews:views,accessors:[{bufferView:0,componentType:5126,count:vertices.length,type:"VEC3",min,max},{bufferView:1,componentType:5126,count:vertices.length,type:"VEC3"},{bufferView:2,componentType:5126,count:vertices.length,type:"VEC4"},{bufferView:3,componentType:5126,count:vertices.length,type:"VEC2"},{bufferView:4,componentType:5123,count:indices.length,type:"SCALAR"}]};
 let j=JSON.stringify(g);while(j.length%4)j+=" ";const padded=(offset+3)&~3,bytes=new Uint8Array(28+j.length+padded),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let p=20+j.length;dv.setUint32(p,padded,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(const a of arrays){bytes.set(a,p);p+=a.length;}
 return {bytes,report:{name:m.name,triangles:indices.length/3,vertices:vertices.length,unindexedVertices:indices.length,closedComponents:m.components,materialSlots:1,bounds:{min,max},bytes:bytes.length}};
}
function build(){return [lantern(),monument()].map(serialize);}


return build();
}
const baseline=[{"name":"Sky_Star_Blossom_Cluster_Wind","triangles":792,"bounds":{"min":[-0.3304731845855713,5.60283597295097e-10,-0.18864977359771729],"max":[0.35047319531440735,0.5996000170707703,0.1586497724056244]}},{"name":"Sky_Astral_Crystal_Lantern","triangles":792,"bounds":{"min":[-0.3199999928474426,0,-0.3199999928474426],"max":[0.3199999928474426,1.225000023841858,0.3199999928474426]}},{"name":"Sky_Celestial_Halo_Monument","triangles":840,"bounds":{"min":[-0.36500000953674316,0,-0.3199999928474426],"max":[0.36500000953674316,1.274999976158142,0.3199999928474426]}}];

function buildAll(){return [flowerLOD(),...architectureLOD()].map((a,i)=>{
 const b=baseline[i],r=a.report;let extent=0,center=0;
 for(let k=0;k<3;k++){const w=b.bounds.max[k]-b.bounds.min[k];extent=Math.max(extent,Math.abs((r.bounds.max[k]-r.bounds.min[k])/w-1));center=Math.max(center,Math.abs((r.bounds.max[k]+r.bounds.min[k]-b.bounds.max[k]-b.bounds.min[k])/2));}
 if(extent>.10||center>.025||r.triangles>b.triangles*.85)throw Error("LOD gate "+JSON.stringify({name:r.name,extent,center,tris:r.triangles}));
 return {...a,report:{...r,baselineTriangles:b.triangles,reduction:1-r.triangles/b.triangles,maxExtentDelta:extent,maxCenterDelta:center}};
});}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_sky_vis16f_lod.mjs <new-dir>");
const a=buildAll();mkdirSync(out);for(const x of a){writeFileSync(join(out,x.report.name+".glb"),x.bytes,{flag:"wx"});console.log(JSON.stringify(x.report));}
