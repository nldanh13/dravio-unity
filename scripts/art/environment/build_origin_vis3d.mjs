function buildArt(){
 const clamp=(x,a=0,b=1)=>Math.max(a,Math.min(b,x)),lerp=(a,b,t)=>a+(b-a)*t,ss=(a,b,x)=>{let t=clamp((x-a)/(b-a));return t*t*(3-2*t)},tau=2*Math.PI;
 function rgb(h){return [0,2,4].map(i=>parseInt(h.slice(i,i+2),16))}
 function mix(a,b,t){return a.map((v,i)=>lerp(v,b[i],t))}
 function over(bg,col,a){let A=a+bg[3]*(1-a);return A>0?[...col.map((v,i)=>(v*a+bg[i]*bg[3]*(1-a))/A),A]:[0,0,0,0]}
 function ellipse(x,y,cx,cy,rx,ry){return 1-ss(.87,1.04,Math.hypot((x-cx)/rx,(y-cy)/ry))}
 function segment(x,y,ax,ay,bx,by){let dx=bx-ax,dy=by-ay,t=clamp(((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy));return [Math.hypot(x-ax-t*dx,y-ay-t*dy),t]}
 function raster(name,w,h,fn){let pixels=new Uint8Array(w*h*4);let nonzero=0,clear=0;for(let y=0;y<h;y++)for(let x=0;x<w;x++){let c=fn((x+.5)/w,(y+.5)/h),i=(y*w+x)*4;for(let k=0;k<3;k++)pixels[i+k]=Math.round(clamp(c[k],0,255));pixels[i+3]=Math.round(clamp(c[3])*255);if(pixels[i+3])nonzero++;else clear++;}return {name,w,h,pixels,nonzero,clear}}
 const water=raster("T_VIS3D_Waterfall_Flow",256,256,(u,v)=>{
   const broad=.5+.5*Math.sin(tau*(u*7+ .12*Math.sin(tau*v*2)));
   const fine=.5+.5*Math.sin(tau*(u*19+ .09*Math.sin(tau*v*3+u*8)));
   const band=ss(.65,.98,broad)*(.45+.55*Math.sin(tau*v*3+u*12)**2);
   const c=mix(rgb("24A8C8"),rgb("D9FFF7"),clamp(.25*broad+.20*fine+.52*band));
   return [...c,clamp(.62+.23*band+.08*fine)];
 });
 const ripples=raster("T_VIS3D_Stream_Ripples",256,256,(u,v)=>{
   const w=.5+.5*Math.sin(tau*(v*5+.24*Math.sin(tau*u*2)+.10*Math.sin(tau*u*3)));
   const w2=.5+.5*Math.sin(tau*(v*9+.12*Math.sin(tau*u*5)));
   const line=ss(.85,.99,w)*(.35+.65*w2);
   const col=mix(rgb("28B9C2"),rgb("E7FFF5"),line*.84+.13*w2);
   return [...col,.64+.24*line];
 });
 const foam=raster("T_VIS3D_Foam_Splash",256,256,(x,y)=>{
   let p=[211,255,249,0];
   const d=Math.hypot((x-.5)/.43,(y-.55)/.27);
   let edge=(1-ss(.91,1.01,d))*ss(.46,.76,d);
   p=over(p,rgb("E9FFF9"),edge*.72);
   const bubbles=[[.23,.48,.06,.05],[.37,.41,.08,.06],[.55,.42,.09,.055],[.71,.50,.075,.045],[.43,.60,.09,.055],[.62,.63,.07,.05],[.78,.63,.045,.035],[.30,.71,.05,.035],[.5,.32,.025,.025],[.68,.28,.021,.021],[.36,.27,.026,.026],[.81,.38,.017,.017]];
   for(const [cx,cy,rx,ry] of bubbles){const a=ellipse(x,y,cx,cy,rx,ry);p=over(p,rgb("D7F9F4"),a*.92);p=over(p,rgb("FFFFFF"),ellipse(x,y,cx-rx*.19,cy-ry*.23,rx*.66,ry*.56)*.90)}
   return p;
 });
 const grass=raster("T_VIS3D_Grass_Tuft",256,256,(x,y)=>{
   let p=[0,0,0,0];
   const blades=[[.39,.94,.16,.40,.064],[.45,.95,.34,.19,.065],[.50,.96,.52,.12,.076],[.54,.94,.71,.26,.067],[.60,.95,.86,.45,.066],[.43,.95,.27,.52,.05],[.57,.96,.65,.48,.06]];
   for(let i=0;i<blades.length;i++){const [ax,ay,bx,by,w]=blades[i];let [d,t]=segment(x,y,ax,ay,bx,by);const width=w*(.96-.85*t);const a=1-ss(width*.79,width,d);const c=mix(rgb(i%2?"286C3B":"387943"),rgb(i%2?"A7DC65":"D1E77E"),t*.85+.08);p=over(p,c,a);}
   return p;
 });
 const flower=raster("T_VIS3D_Meadow_Flowers",256,256,(x,y)=>{
   let p=[0,0,0,0];const blooms=[[.29,.36,.102,"F9B2C6"],[.52,.22,.115,"FFE194"],[.73,.43,.092,"B9C4FC"]];
   for(const [cx,cy,r,col] of blooms){let [d,t]=segment(x,y,.50,.95,cx,cy);p=over(p,mix(rgb("2D7948"),rgb("8DCF60"),t),1-ss(.010,.015,d));
     for(let sign of [-1,1]){let lx=lerp(.50,cx,.32)+sign*.08,ly=lerp(.95,cy,.32);p=over(p,rgb(sign<0?"79B64F":"4F9D4F"),ellipse(x,y,lx,ly,.071,.026));}
     for(let i=0;i<5;i++){let a=tau*i/5-Math.PI/2,px=cx+Math.cos(a)*r*.62,py=cy+Math.sin(a)*r*.62;p=over(p,rgb(col),ellipse(x,y,px,py,r*.52,r*.52));p=over(p,mix(rgb(col),rgb("FFFFFF"),.55),ellipse(x,y,px-.012,py-.014,r*.30,r*.24)*.68);}
     p=over(p,rgb("E9A937"),ellipse(x,y,cx,cy,r*.31,r*.31));p=over(p,rgb("FFF1B3"),ellipse(x,y,cx-.009,cy-.011,r*.18,r*.18));
   }return p;
 });
 const mushroom=raster("T_VIS3D_Mushroom_Cluster",256,256,(x,y)=>{
   let p=[0,0,0,0];const shrooms=[[.28,.51,.18,.22,"E68A61"],[.53,.31,.24,.36,"D95555"],[.77,.60,.13,.16,"F1B56B"]];
   for(const [cx,cy,r,h,col] of shrooms){
     let stem=ellipse(x,y,cx,cy+h*.74,r*.30,h*.82);p=over(p,mix(rgb("C39776"),rgb("FFF0CE"),clamp(1-Math.abs(x-cx)/(r*.32))),stem);
     const cap=ellipse(x,y,cx,cy,r,r*.63)*(1-ss(cy+r*.26,cy+r*.37,y));
     p=over(p,mix(rgb(col),rgb("FFD8B4"),clamp((cy-y)/(r*.8))*.68),cap);
     for(const [dx,dy,s] of [[-.40,-.14,.13],[.26,-.23,.12],[.53,.04,.095],[-.08,-.42,.10]])p=over(p,rgb("FFF1CE"),ellipse(x,y,cx+dx*r,cy+dy*r,s*r,s*r)*cap);
   }return p;
 });
 // Geometry: glTF Y up, +Z front, meters; vertex colors linear.
 const sub=(a,b)=>a.map((v,i)=>v-b[i]),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]],dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0),norm=a=>{let l=Math.hypot(...a);if(l<1e-10)throw Error("zero normal");return a.map(v=>v/l)},linear=c=>rgb(c).map(v=>{let x=v/255;return x<=.04045?x/12.92:((x+.055)/1.055)**2.4});
 function mesh(name,material,texture=null){return {name,material,texture,p:[],n:[],uv:[],c:[]}}
 function tri(m,a,b,c,color,uvs=[[0,0],[0,1],[1,1]],ns=null){
   let n=norm(cross(sub(b,a),sub(c,a)));if(ns&&dot(n,ns.reduce((s,v)=>s.map((q,i)=>q+v[i]),[0,0,0]))<0){[b,c]=[c,b];uvs=[uvs[0],uvs[2],uvs[1]];ns=[ns[0],ns[2],ns[1]];n=n.map(v=>-v)}
   for(let i=0;i<3;i++){m.p.push(...[a,b,c][i]);m.n.push(...(ns?ns[i]:n));m.uv.push(...uvs[i]);m.c.push(...color)}
 }
 function stone(m,c,rx,ry,rz,col,n=14){
   let rings=[];for(let j=0;j<=6;j++){let a=-Math.PI/2+.06+(Math.PI-.12)*j/6;let y=Math.max(-ry*.65,Math.sin(a)*ry);rings.push(Array.from({length:n},(_,i)=>{let t=tau*i/n,noise=1+.045*Math.cos(t*3+j);return [c[0]+rx*Math.cos(a)*Math.cos(t)*noise,c[1]+y,c[2]+rz*Math.cos(a)*Math.sin(t)*noise]}))}
   const shade=j=>mix(linear(col),linear("C7D8A3"),j/6*.34);
   for(let j=0;j<6;j++)for(let i=0;i<n;i++){let k=(i+1)%n,a=rings[j][i],b=rings[j+1][i],d=rings[j][k],e=rings[j+1][k];let ns=[a,b,e,d].map(p=>norm([(p[0]-c[0])/(rx*rx),(p[1]-c[1])/(ry*ry),(p[2]-c[2])/(rz*rz)]));tri(m,a,b,e,shade(j),undefined,ns.slice(0,3));tri(m,a,e,d,shade(j),undefined,[ns[0],ns[2],ns[3]])}
   for(const [j,sgn] of [[0,-1],[6,1]]){let center=[c[0],rings[j][0][1],c[2]];for(let i=0;i<n;i++)tri(m,center,rings[j][i],rings[j][(i+1)%n],shade(j),undefined,Array(3).fill([0,sgn,0]));}
 }
 const rock=mesh("Origin_Waterfall_Rock_Frame",{name:"Stone_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.88}});
 // Leave the central water opening visible; smooth mossy stone lobes flank it.
 for(const [x,y,z,rx,ry,rz,col] of [[-.94,.46,-.18,.63,.68,.64,"829B81"],[.94,.46,-.15,.61,.68,.62,"879B82"],[-.87,1.19,-.38,.50,.64,.51,"93A78B"],[.87,1.22,-.38,.50,.66,.51,"94A88A"],[-.81,1.95,-.51,.47,.65,.47,"A1B28C"],[.81,1.95,-.51,.47,.65,.47,"9DAD8C"],[0,2.30,-.90,.94,.42,.48,"9BAE81"],[-1.35,.23,.37,.39,.35,.42,"7B9784"],[1.32,.22,.42,.38,.34,.41,"7D9A86"]])stone(rock,[x,y,z],rx,ry,rz,col);
 const waterMat=name=>({name,pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],baseColorTexture:{index:0},metallicFactor:0,roughnessFactor:.31},alphaMode:"BLEND",doubleSided:true});
 const falls=mesh("Origin_Waterfall_Surface",waterMat("Waterfall_Flow_Alpha"),water);
 // Top at back, falls toward +Z, curved foot. UV v=0 at top.
 const rows=18,cols=12;
 const profile=[[2.54,-.82,.83],[2.39,-.60,.84],[2.18,-.38,.78],[1.72,-.27,.70],[1.05,-.18,.68],[.39,.01,.80],[.09,.53,1.04]];
 const pts=[],uv=[];
 for(let j=0;j<=rows;j++){let t=j/rows,f=t*(profile.length-1),k=Math.min(profile.length-2,Math.floor(f)),g=f-k;let [y,z,w]=profile[k].map((v,i)=>lerp(v,profile[k+1][i],g));pts[j]=[];uv[j]=[];for(let i=0;i<=cols;i++){let u=i/cols,x=(u-.5)*w;pts[j][i]=[x,y+.016*Math.sin(u*tau*3)*Math.sin(t*Math.PI),z+.028*Math.sin(u*tau*3+t*3)];uv[j][i]=[u,t]}}
 for(let j=0;j<rows;j++)for(let i=0;i<cols;i++){let a=pts[j][i],b=pts[j+1][i],c=pts[j+1][i+1],d=pts[j][i+1];let A=uv[j][i],B=uv[j+1][i],C=uv[j+1][i+1],D=uv[j][i+1];tri(falls,a,b,c,[1,1,1],[A,B,C]);tri(falls,a,c,d,[1,1,1],[A,C,D]);}
 const stream=mesh("Origin_Stream_Surface",waterMat("Stream_Ripple_Alpha"),ripples);
 const nr=24,nc=8;function sp(j,i){let t=j/nr,u=i/nc;return [Math.sin(t*Math.PI*1.55)*.7+(u-.5)*(1.18+.30*Math.sin(t*Math.PI)),.035, t*4.5]}
 for(let j=0;j<nr;j++)for(let i=0;i<nc;i++){let a=sp(j,i),b=sp(j+1,i),c=sp(j+1,i+1),d=sp(j,i+1);let A=[i/nc,j/nr*2],B=[i/nc,(j+1)/nr*2],C=[(i+1)/nc,(j+1)/nr*2],D=[(i+1)/nc,j/nr*2];tri(stream,a,b,c,[1,1,1],[A,B,C]);tri(stream,a,c,d,[1,1,1],[A,C,D]);}
 const bank=mesh("Origin_Stream_Bank_Stone_Cluster",{name:"Bank_Stone_VertexColor",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.92}});
 for(const [x,y,z,rx,ry,rz,c] of [[-.70,.16,.42,.28,.25,.38,"819E91"],[-.79,.12,1.02,.25,.20,.31,"91A98F"],[-.56,.10,1.51,.22,.18,.26,"9EAD95"],[.78,.14,.78,.31,.22,.38,"849C88"],[.97,.09,1.40,.21,.15,.25,"A5B19B"]])stone(bank,[x,y,z],rx,ry,rz,c);
 return {textures:[water,ripples,foam,grass,flower,mushroom],meshes:[rock,falls,stream,bank]};
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

function glb(m,texBytes){
 const count=m.p.length/3,min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity];let mismatched=0;
 for(let i=0;i<count;i++){for(let j=0;j<3;j++){let x=m.p[i*3+j];if(!Number.isFinite(x))throw Error("position");min[j]=Math.min(min[j],Math.fround(x));max[j]=Math.max(max[j],Math.fround(x));}if(Math.abs(Math.hypot(...m.n.slice(i*3,i*3+3))-1)>1e-5)throw Error("normal");}
 for(let i=0;i<count;i+=3){let [a,b,c]=[0,1,2].map(j=>m.p.slice((i+j)*3,(i+j+1)*3));let d=b.map((v,j)=>v-a[j]),e=c.map((v,j)=>v-a[j]);let n=[d[1]*e[2]-d[2]*e[1],d[2]*e[0]-d[0]*e[2],d[0]*e[1]-d[1]*e[0]];if(Math.hypot(...n)<1e-8)throw Error("degenerate "+m.name);for(let k=0;k<3;k++)if(n.reduce((s,v,j)=>s+v*m.n[(i+k)*3+j],0)<-1e-7)mismatched++}
 let arrays=[m.p,m.n,m.uv,m.c].map(a=>new Uint8Array(new Float32Array(a).buffer));let views=[],offset=0;for(let a of arrays){views.push({buffer:0,byteOffset:offset,byteLength:a.length,target:34962});offset+=a.length}
 if(texBytes){views.push({buffer:0,byteOffset:offset,byteLength:texBytes.length});let a=new Uint8Array(Math.ceil(texBytes.length/4)*4);a.set(texBytes);arrays.push(a);offset+=a.length}
 let j={asset:{version:"2.0",generator:"Dravio VIS.3d procedural art"},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name:m.name}],meshes:[{name:m.name,primitives:[{attributes:{POSITION:0,NORMAL:1,TEXCOORD_0:2,COLOR_0:3},material:0,mode:4}]}],materials:[m.material],buffers:[{byteLength:offset}],bufferViews:views,accessors:[{bufferView:0,componentType:5126,count,type:"VEC3",min,max},{bufferView:1,componentType:5126,count,type:"VEC3"},{bufferView:2,componentType:5126,count,type:"VEC2"},{bufferView:3,componentType:5126,count,type:"VEC3"}]};
 if(texBytes){j.images=[{bufferView:4,mimeType:"image/png"}];j.textures=[{source:0,sampler:0}];j.samplers=[{magFilter:9729,minFilter:9987,wrapS:10497,wrapT:10497}]}
 let raw=JSON.stringify(j);while(raw.length%4)raw+=" ";let out=new Uint8Array(28+raw.length+offset),dv=new DataView(out.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,out.length,true);dv.setUint32(12,raw.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<raw.length;i++)out[i+20]=raw.charCodeAt(i);let p=20+raw.length;dv.setUint32(p,offset,true);dv.setUint32(p+4,0x004e4942,true);p+=8;for(let a of arrays){out.set(a,p);p+=a.length}
 return {bytes:out,report:{name:m.name,triangles:count/3,vertices:count,materials:1,bounds:{min,max},opposedCornerNormals:mismatched,bytes:out.length,embeddedTexture:m.texture?.name??null}};
}
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
const dir=process.argv[2];if(!dir)throw Error("Usage: node build_origin_vis3d.mjs <new output directory>");mkdirSync(dir);mkdirSync(join(dir,"Models"));mkdirSync(join(dir,"Textures"));const a=buildArt(),p=new Map();for(const t of a.textures){const b=png(t);p.set(t.name,b);writeFileSync(join(dir,"Textures",t.name+".png"),b);}for(const m of a.meshes){const {bytes,report}=glb(m,m.texture?p.get(m.texture.name):null);writeFileSync(join(dir,"Models",m.name+".glb"),bytes);console.log(JSON.stringify(report));}
