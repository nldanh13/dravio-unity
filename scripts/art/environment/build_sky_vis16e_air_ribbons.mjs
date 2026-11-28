
// VIS16e: translucent tapered twin air ribbons with a bounded looping morph.
// Source art, not a controller. Y-up, centered about the updraft axis.
const TAU=2*Math.PI,clamp=x=>Math.max(0,Math.min(1,x)),sub=(a,b)=>a.map((v,i)=>v-b[i]),add=(a,b)=>a.map((v,i)=>v+b[i]),mul=(a,k)=>a.map(v=>v*k),dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]],norm=a=>{const l=Math.hypot(...a);if(l<1e-9)throw Error("degenerate");return mul(a,1/l)};
function build(){
 const p=[],n=[],c=[],uv=[],ix=[],delta=[[],[]],normalDelta=[[],[]];
 const steps=32;
 function point(i,j,s,phase){
  const t=i/steps,a=TAU*(.72*t+s*.5),r=.32+.12*Math.sin(Math.PI*t),y=t*1.4;
  const w=.035+.11*Math.pow(Math.sin(Math.PI*t),.7),v=(j-.5)*w;
  const drift=.042*Math.sin(Math.PI*t);
  return [(r+v)*Math.cos(a)+phase*drift*Math.sin(a+.4),y+v*.34,(r+v)*Math.sin(a)+phase*drift*Math.cos(a+.4)];
 }
 // Area-weighted normals at vertices, computed per target as well as rest.
 function geometry(phase){
  const ps=[],ns=[],ids=[];
  for(let s=0;s<2;s++){
   const start=ps.length;
   for(let i=0;i<=steps;i++)for(let j=0;j<2;j++)ps.push(point(i,j,s,phase));
   for(let i=0;i<steps;i++){const a=start+i*2,b=a+1,d=a+2,e=d+1;ids.push(a,b,e,a,e,d);}
  }
  for(let i=0;i<ps.length;i++)ns.push([0,0,0]);
  for(let i=0;i<ids.length;i+=3){const a=ids[i],b=ids[i+1],c=ids[i+2],v=cross(sub(ps[b],ps[a]),sub(ps[c],ps[a]));if(Math.hypot(...v)<1e-8)throw Error("zero face");for(const j of [a,b,c])ns[j]=add(ns[j],v);}
  return {ps,ns:ns.map(norm),ids};
 }
 const rest=geometry(0),ends=[geometry(1),geometry(-1)];
 for(let i=0;i<rest.ps.length;i++){const t=Math.floor((i%(2*(steps+1)))/2)/steps;const fade=Math.pow(Math.sin(Math.PI*t),1.6);p.push(...rest.ps[i]);n.push(...rest.ns[i]);c.push(.68+.20*t,.88+.10*t,1,.32*fade);uv.push(i%2,t);for(let k=0;k<2;k++){delta[k].push(...sub(ends[k].ps[i],rest.ps[i]));normalDelta[k].push(...sub(ends[k].ns[i],rest.ns[i]));}}
 ix.push(...rest.ids);let minDot=1,minArea=Infinity,maxArea=0,maxDisplacement=0;
 for(const end of ends)for(let j=0;j<=24;j++){
  const t=j/24,q=rest.ps.map((v,i)=>add(v,mul(sub(end.ps[i],v),t)));
  for(let i=0;i<q.length;i++)maxDisplacement=Math.max(maxDisplacement,Math.hypot(...sub(q[i],rest.ps[i])));
  for(let i=0;i<ix.length;i+=3){const [a,b,c]=ix.slice(i,i+3),v=cross(sub(rest.ps[b],rest.ps[a]),sub(rest.ps[c],rest.ps[a])),w=cross(sub(q[b],q[a]),sub(q[c],q[a])),r=Math.hypot(...w)/Math.hypot(...v);minDot=Math.min(minDot,dot(norm(v),norm(w)));minArea=Math.min(minArea,r);maxArea=Math.max(maxArea,r);}
 }
 if(minDot<.90||minArea<.75||maxArea>1.25||maxDisplacement>.05)throw Error("morph gate");
 const chunks=[],views=[],accessors=[];let offset=0;
 function acc(data,dim,type,target,bounds=false,indices=false){
  const a=indices?new Uint16Array(data):new Float32Array(data),b=new Uint8Array(a.buffer);
  if(a.some(v=>!Number.isFinite(v)))throw Error("finite");const pad=(4-offset%4)%4;if(pad){chunks.push(new Uint8Array(pad));offset+=pad;}
  const view={buffer:0,byteOffset:offset,byteLength:b.length};if(target)view.target=target;views.push(view);chunks.push(b);offset+=b.length;
  const d={bufferView:views.length-1,componentType:indices?5123:5126,count:a.length/dim,type};if(bounds){d.min=Array(dim).fill(Infinity);d.max=Array(dim).fill(-Infinity);for(let i=0;i<a.length;i++){d.min[i%dim]=Math.min(d.min[i%dim],a[i]);d.max[i%dim]=Math.max(d.max[i%dim],a[i]);}}accessors.push(d);return accessors.length-1;
 }
 const attributes={POSITION:acc(p,3,"VEC3",34962,true),NORMAL:acc(n,3,"VEC3",34962),COLOR_0:acc(c,4,"VEC4",34962),TEXCOORD_0:acc(uv,2,"VEC2",34962)},indices=acc(ix,1,"SCALAR",34963,true,true);
 const targets=delta.map((d,i)=>({POSITION:acc(d,3,"VEC3",34962,true),NORMAL:acc(normalDelta[i],3,"VEC3",34962)}));
 const time=Array.from({length:25},(_,i)=>i*3/24),weights=time.flatMap((_,i)=>{const x=i===0||i===24?0:Math.sin(TAU*i/24);return [Math.max(x,0),Math.max(-x,0)];});
 if(weights[0]!==weights.at(-2)||weights[1]!==weights.at(-1))throw Error("loop");
 const input=acc(time,1,"SCALAR",null,true),output=acc(weights,1,"SCALAR");
 const name="Sky_Updraft_Air_Ribbons";
 const g={asset:{version:"2.0",generator:"Dravio VIS.16e"},scene:0,scenes:[{nodes:[0]}],nodes:[{name,mesh:0}],meshes:[{name,weights:[0,0],extras:{targetNames:["Gust_A","Gust_B"]},primitives:[{attributes,indices,targets,material:0,mode:4}]}],materials:[{name:"Sky_Air_Translucent",alphaMode:"BLEND",doubleSided:true,pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:1}}],animations:[{name:"Air_Breathe_Loop",samplers:[{input,output,interpolation:"LINEAR"}],channels:[{sampler:0,target:{node:0,path:"weights"}}]}],buffers:[{byteLength:offset}],bufferViews:views,accessors};
 let j=JSON.stringify(g);while(j.length%4)j+=" ";const pad=(offset+3)&~3,bytes=new Uint8Array(28+j.length+pad),dv=new DataView(bytes.buffer);dv.setUint32(0,0x46546c67,true);dv.setUint32(4,2,true);dv.setUint32(8,bytes.length,true);dv.setUint32(12,j.length,true);dv.setUint32(16,0x4e4f534a,true);for(let i=0;i<j.length;i++)bytes[20+i]=j.charCodeAt(i);let k=20+j.length;dv.setUint32(k,pad,true);dv.setUint32(k+4,0x004e4942,true);k+=8;for(const a of chunks){bytes.set(a,k);k+=a.length;}
 return {bytes,report:{name,triangles:ix.length/3,vertices:p.length/3,materialSlots:1,textures:0,durationSeconds:3,keyframes:25,morphTargets:2,sampledPoses:50,minFaceNormalDot:minDot,minAreaRatio:minArea,maxAreaRatio:maxArea,maxDisplacementM:maxDisplacement,bounds:accessors[0].min.map((x,i)=>[x,accessors[0].max[i]]),maxVertexAlpha:.32,bytes:bytes.length}};
}

import {mkdirSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const out=process.argv[2];if(!out)throw Error("Usage: node build_sky_vis16e_air_ribbons.mjs <new-dir>");
const a=build();mkdirSync(out);writeFileSync(join(out,a.report.name+".glb"),a.bytes,{flag:"wx"});console.log(JSON.stringify(a.report));
