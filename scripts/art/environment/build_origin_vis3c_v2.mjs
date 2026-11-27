function buildModels(){
  const add=(a,b)=>a.map((x,i)=>x+b[i]), sub=(a,b)=>a.map((x,i)=>x-b[i]), mul=(a,s)=>a.map(x=>x*s), dot=(a,b)=>a.reduce((s,x,i)=>s+x*b[i],0), cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]], norm=a=>mul(a,1/(Math.hypot(...a)||1));
  const srgb=s=>[0,2,4].map(i=>{let x=parseInt(s.slice(i,i+2),16)/255;return x<=.04045?x/12.92:((x+.055)/1.055)**2.4});
  const mix=(a,b,t)=>a.map((x,i)=>x+(b[i]-x)*t);
  function mesh(name){return {name,p:[],n:[],c:[]}}
  function tri(m,a,b,c,color,ns=null,cs=null){
    let n=norm(cross(sub(b,a),sub(c,a)));if(Math.hypot(...cross(sub(b,a),sub(c,a)))<1e-8)throw Error("degenerate "+m.name);
    const col=typeof color==="string"?srgb(color):color;
    for(let i=0;i<3;i++){m.p.push(...[a,b,c][i]);m.n.push(...(ns?ns[i]:n));m.c.push(...(cs?cs[i]:col));}
  }
  function quad(m,a,b,c,d,col){tri(m,a,b,c,col);tri(m,a,c,d,col)}
  // Horizontal circular profile, axis Y. Each ring has radius and height.
  function lathe(m,center,rings,n,lo,hi,ell=[1,1],smooth=true){
    const c0=srgb(lo),c1=srgb(hi);const points=rings.map(([r,y])=>Array.from({length:n},(_,i)=>{let t=i*2*Math.PI/n;return add(center,[r*Math.cos(t)*ell[0],y,r*Math.sin(t)*ell[1]])}));
    const normal=(j,i)=>{const [r,y]=rings[j];const [r0,y0]=rings[Math.max(j-1,0)], [r1,y1]=rings[Math.min(j+1,rings.length-1)];const a=i*2*Math.PI/n;return norm([Math.cos(a)*(y1-y0)/ell[0],r0-r1,Math.sin(a)*(y1-y0)/ell[1]])};
    const color=j=>mix(c0,c1,j/(rings.length-1));
    for(let j=0;j<rings.length-1;j++)for(let i=0;i<n;i++){let q=(i+1)%n,a=points[j][i],b=points[j+1][i],c=points[j+1][q],d=points[j][q];const n0=normal(j,i),n1=normal(j+1,i),n2=normal(j+1,q),n3=normal(j,q);
      const ca=color(j),cb=color(j+1);
      tri(m,a,b,c,ca,smooth?[n0,n1,n2]:null,[ca,cb,cb]);tri(m,a,c,d,ca,smooth?[n0,n2,n3]:null,[ca,cb,ca]);}
    for(const [j,sign] of [[0,-1],[rings.length-1,1]]){let centerP=add(center,[0,rings[j][1],0]);for(let i=0;i<n;i++){let a=points[j][i],b=points[j][(i+1)%n];if(sign<0)tri(m,centerP,a,b,color(j));else tri(m,centerP,b,a,color(j));}}
  }
  // Beveled rounded rectangle with constant-radius corners, horizontal rings.
  function rounded(m,c,w,d,h,rad,col1,col2){
    if (!(w>0&&d>0&&h>0&&rad>0)) throw Error('invalid rounded dimensions');
    rad=Math.min(rad,w/2-1e-4,d/2-1e-4);
    const start=m.p.length,n=16,lo=srgb(col1),hi=srgb(col2);
    const outline=(w,d,r)=>{r=Math.min(r,w/2-1e-4,d/2-1e-4);let a=[];for(let k=0;k<4;k++){let cx=(k===0||k===3?1:-1)*(w/2-r),cz=(k<2?1:-1)*(d/2-r);for(let j=0;j<4;j++){let t=k*Math.PI/2+j*Math.PI/6;a.push([cx+r*Math.cos(t),0,cz+r*Math.sin(t)])}}return a};
    const bevel=Math.min(rad*.4,h*.18),rings=[[w-2*bevel,d-2*bevel,-h/2,Math.max(1e-4,rad-bevel)],[w,d,-h/2+bevel,rad],[w,d,h/2-bevel,rad],[w-2*bevel,d-2*bevel,h/2,Math.max(1e-4,rad-bevel)]];
    const ps=rings.map(([w,d,y,r])=>outline(w,d,r).map(p=>add(c,[p[0],y,p[2]])));
    for(let j=0;j<3;j++)for(let i=0;i<n;i++){let q=(i+1)%n;quad(m,ps[j][i],ps[j+1][i],ps[j+1][q],ps[j][q],mix(lo,hi,(j+.5)/3))}
    for(let i=0;i<n;i++){let q=(i+1)%n;tri(m,add(c,[0,-h/2,0]),ps[0][i],ps[0][q],lo);tri(m,add(c,[0,h/2,0]),ps[3][q],ps[3][i],hi)}
    assertConvexOutward(m,start,c);
  }
    // Intentional rounded arch in the XY plane, extruded along Z.
  function arch(m,c,w,d,h,col1,col2) {
    if (!(w>0&&d>0&&h>w/2)) throw Error("invalid arch dimensions");
    const start=m.p.length, r=w/2, bottom=-h/2, spring=h/2-r;
    const profile=[[-r,bottom],[r,bottom]];
    for(let i=0;i<=12;i++){const t=i*Math.PI/12;profile.push([r*Math.cos(t),spring+r*Math.sin(t)]);}
    const ring=z=>profile.map(([x,y])=>add(c,[x,y,z]));
    const back=ring(-d/2),front=ring(d/2),lo=srgb(col1),hi=srgb(col2);
    for(let i=0;i<profile.length;i++){
      const j=(i+1)%profile.length;
      quad(m,back[i],back[j],front[j],front[i],lo);
      tri(m,add(c,[0,0,-d/2]),back[j],back[i],lo);
      tri(m,add(c,[0,0,d/2]),front[i],front[j],hi);
    }
    assertConvexOutward(m,start,c);
  }
  // On a convex closed primitive, each outward face must point away from the interior.
  function assertConvexOutward(m,start,c){
    for(let i=start;i<m.p.length;i+=9){
      const a=m.p.slice(i,i+3),b=m.p.slice(i+3,i+6),d=m.p.slice(i+6,i+9);
      const n=cross(sub(b,a),sub(d,a));
      if(dot(n,sub(a,c))<=0)throw Error("inward convex face: "+m.name);
    }
  }
  // Oriented rectangular beam with flat normals, used only for wood trim.
  function beam(m,a,b,w,d,color){
    let t=norm(sub(b,a));let u=norm(cross(Math.abs(t[2])>.95?[0,1,0]:[0,0,1],t)),v=norm(cross(t,u));let ring=p=>[[1,1],[-1,1],[-1,-1],[1,-1]].map(([i,j])=>add(p,add(mul(u,i*w/2),mul(v,j*d/2))));let A=ring(a),B=ring(b);
    for(let i=0;i<4;i++){let q=(i+1)%4;quad(m,A[i],A[q],B[q],B[i],color)}
    quad(m,A[3],A[2],A[1],A[0],color);quad(m,B[0],B[1],B[2],B[3],color)
  }
  // Ellipsoid with capped lathe profile, rounded low-poly silhouette.
  function oval(m,c,rx,ry,rz,lo,hi,n=12){
    const rings=[];for(let i=0;i<=8;i++){let a=-Math.PI/2+.045+(Math.PI-.09)*i/8;rings.push([Math.cos(a)*rx,Math.sin(a)*ry]);}
    lathe(m,c,rings,n,lo,hi,[1,rz/rx]);
  }
  const cottage=mesh("Origin_Fairytale_Cottage");
  rounded(cottage,[0,.13,0],3.4,2.6,.26,.32,"927F67","D6C6A5");
  rounded(cottage,[0,1.13,0],2.85,2.14,1.9,.30,"D79F6B","FFF0C4");
  // Curved roof with broad sculpted ridges, 13 points across, raised ridge and curved overhangs.
  const nx=14,nz=8;
  const roof=(x,z,offset=0)=>{let u=x/1.78;return 2.10+1.22*Math.pow(Math.max(0,1-u*u),1.15)+.12*Math.pow(Math.abs(z)/1.37,3)+offset};
  let roofPts=Array.from({length:nz+1},(_,j)=>Array.from({length:nx+1},(_,i)=>{let x=-1.78+3.56*i/nx,z=-1.37+2.74*j/nz;return [x,roof(x,z)+.026*Math.cos(j*Math.PI),z]}));
  for(let j=0;j<nz;j++)for(let i=0;i<nx;i++){let a=roofPts[j][i],b=roofPts[j+1][i],c=roofPts[j+1][i+1],d=roofPts[j][i+1];let t=(Math.min(i,nx-1-i)/(nx/2))*0.55+.28;let color=mix(srgb("B87324"),srgb("FFE17E"),Math.min(1,t+(j%2)*.07));quad(cottage,a,b,c,d,color)}
  for(let j=0;j<nz;j++)for(let i of [0,nx]){let a=roofPts[j][i],b=roofPts[j+1][i];if(i===0)quad(cottage,a,add(a,[0,-.18,0]),add(b,[0,-.18,0]),b,"9B5A26");else quad(cottage,b,add(b,[0,-.18,0]),add(a,[0,-.18,0]),a,"9B5A26")}
  for(let j of [0,nz])for(let i=0;i<nx;i++){let a=roofPts[j][i],b=roofPts[j][i+1];if(j===0)quad(cottage,b,[b[0],2.04,b[2]],[a[0],2.04,a[2]],a,"C48839");else quad(cottage,a,[a[0],2.04,a[2]],[b[0],2.04,b[2]],b,"C48839");beam(cottage,a,b,.10,.13,"F4C96C")}
  rounded(cottage,[0,2.06,0],3.46,2.58,.18,.25,"8C5332","C69051");
  // Front faces +Z. Wood trim, inset door and two blue windows.
  arch(cottage,[0,.79,1.09],.86,.18,1.25,"754729","AC7547");
  arch(cottage,[0,.78,1.20],.64,.12,1.05,"A06838","D4A465");
  oval(cottage,[.19,.76,1.30],.055,.055,.035,"AC782D","FFDF81",8);
  for(let x of [-.91,.91]){arch(cottage,[x,1.35,1.12],.60,.16,.62,"805133","C89355");arch(cottage,[x,1.35,1.22],.43,.10,.45,"417F9B","9EE9E8");beam(cottage,[x-.20,1.35,1.29],[x+.20,1.35,1.29],.055,.055,"F3D9A1");beam(cottage,[x,1.13,1.29],[x,1.57,1.29],.055,.055,"F3D9A1");
    rounded(cottage,[x,1.01,1.25],.68,.29,.14,.08,"A16439","DAA264");
    for(let i=0;i<3;i++)oval(cottage,[x+(i-1)*.19,1.13,1.36],.11,.10,.10,"367A43","A1CF55",8);
    for(let i=0;i<2;i++)oval(cottage,[x+(i-.5)*.23,1.21,1.41],.075,.075,.065,"CE5C70","FFBCC4",8);
  }
  for(let x of [-1.24,1.24])beam(cottage,[x,.24,1.08],[x,2.02,1.08],.13,.13,"9D633B");
  rounded(cottage,[.90,3.00,-.55],.38,.38,.85,.07,"A97756","E9B889");rounded(cottage,[.90,3.46,-.55],.50,.49,.16,.07,"916848","D3A77B");
  // Lookout tower: stacked smooth stone bands, gold roof, windows and finial.
  const tower=mesh("Origin_Fairytale_Lookout");
  lathe(tower,[0,0,0],[[.87,.02],[.94,.12],[.89,.29],[.74,.39]],16,"A28B76","E7D5B1");
  lathe(tower,[0,0,0],[[.73,.29],[.70,.45],[.62,1.90],[.66,2.10],[.79,2.20]],16,"D4B68E","FFF0C9");
  for(let y of [.64,1.36,2.12])lathe(tower,[0,0,0],[[.72,y-.06],[.75,y],[.72,y+.06]],16,"B89464","F3D49B");
  lathe(tower,[0,0,0],[[.94,2.20],[.99,2.29],[.91,2.43],[.69,2.64],[.47,2.91],[.24,3.20],[.07,3.49]],20,"B4752F","FFE291");
  for(let j=0;j<3;j++){let y=2.39+j*.26;let rad=[.93,.70,.49][j];lathe(tower,[0,0,0],[[rad-.025,y-.035],[rad+.015,y],[rad-.025,y+.035]],20,"C58A3C","F8D77C")}
  oval(tower,[0,3.51,0],.095,.13,.095,"A97222","FFE09B",12);
  for(let x of [-.30,.30]){let z=.58;arch(tower,[x,1.62,z],.30,.16,.53,"A17750","E9BD7B");arch(tower,[x,1.63,z+.09],.19,.10,.38,"347FA5","A0D9E5")}
  arch(tower,[0,.65,.72],.44,.18,.75,"845334","C08B54");
  // Layered distant hills, designed as independent scenic group, not a gameplay asset.
  const hills=mesh("Origin_Soft_Hills");
  function hill(cx,cz,w,d,h,lo,hi){const n=24,steps=7;let rings=[];for(let j=0;j<steps;j++){let t=j/steps;let angle=t*(Math.PI/2-.10);rings.push([Math.cos(angle)*w,Math.sin(angle)*h]);}rings.push([w*.035,h*.998]);lathe(hills,[cx,0,cz],rings,n,lo,hi,[1,d/w]);}
  hill(-4.9,-1.6,3.6,2.1,2.65,"729FA2","B3DAD1");hill(.4,-2.5,4.3,2.5,3.5,"729DB4","C2DCE6");hill(5.1,-1.6,3.9,2.0,2.8,"76A8B1","C0DED8");hill(-3.8,.4,3.2,1.7,1.8,"569B67","AAD18A");hill(2.7,.6,4.1,1.9,1.95,"589D6C","B0D68D");
  // Rotate architecture 180 degrees about Y (a proper rotation, preserves winding).
  for(const m of [cottage,tower]){
    for(const a of [m.p,m.n]) for(let i=0;i<a.length;i+=3){a[i]=-a[i];a[i+2]=-a[i+2];}
  }
  // All candidates rest at Y=0 and have a centered XZ bounds pivot.
  for(const m of [cottage,tower,hills]){
    const lo=[Infinity,Infinity,Infinity],hi=[-Infinity,-Infinity,-Infinity];
    for(let i=0;i<m.p.length;i++) {const k=i%3;lo[k]=Math.min(lo[k],m.p[i]);hi[k]=Math.max(hi[k],m.p[i]);}
    const center=[(lo[0]+hi[0])/2,lo[1],(lo[2]+hi[2])/2];
    for(let i=0;i<m.p.length;i++)m.p[i]-=center[i%3];
  }
  return [cottage,tower,hills];
}

function serializeGLB(m){
  const count=m.p.length/3;const buffers=[new Float32Array(m.p),new Float32Array(m.n),new Float32Array(m.c)];
  const min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity];
  for(let i=0;i<count;i++)for(let k=0;k<3;k++){let x=buffers[0][i*3+k];if(!Number.isFinite(x))throw Error("nonfinite");min[k]=Math.min(min[k],x);max[k]=Math.max(max[k],x);}
  for(let i=0;i<count;i++){let n=Math.hypot(...m.n.slice(i*3,i*3+3));if(Math.abs(n-1)>1e-5)throw Error("invalid normal");}
  let offset=0;const views=buffers.map(b=>{let v={buffer:0,byteOffset:offset,byteLength:b.byteLength,target:34962};offset+=b.byteLength;return v;});
  let g={asset:{version:"2.0",generator:"Dravio VIS.3c v2 QA repair draft"},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name:m.name}],meshes:[{name:m.name,primitives:[{attributes:{POSITION:0,NORMAL:1,COLOR_0:2},material:0,mode:4}]}],materials:[{name:"VertexColor_Draft",pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],metallicFactor:0,roughnessFactor:.76},doubleSided:false}],buffers:[{byteLength:offset}],bufferViews:views,accessors:[{bufferView:0,componentType:5126,count,type:"VEC3",min,max},{bufferView:1,componentType:5126,count,type:"VEC3"},{bufferView:2,componentType:5126,count,type:"VEC3"}]};
  let raw=JSON.stringify(g);while(raw.length%4)raw+=" ";let total=12+8+raw.length+8+offset;let out=new Uint8Array(total),d=new DataView(out.buffer);d.setUint32(0,0x46546c67,true);d.setUint32(4,2,true);d.setUint32(8,total,true);d.setUint32(12,raw.length,true);d.setUint32(16,0x4e4f534a,true);for(let i=0;i<raw.length;i++)out[20+i]=raw.charCodeAt(i);let pos=20+raw.length;d.setUint32(pos,offset,true);d.setUint32(pos+4,0x004e4942,true);for(const b of buffers){out.set(new Uint8Array(b.buffer),pos+8);pos+=b.byteLength;}return {bytes:out,report:{name:m.name,triangles:count/3,vertices:count,materialSlots:1,bounds:{min,max},bytes:total}};
}

import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
const dir=process.argv[2];if(!dir)throw Error("Usage: node build_origin_vis3c_v2.mjs <new output dir>");mkdirSync(dir,{recursive:false});for(const m of buildModels()){const {bytes,report}=serializeGLB(m);writeFileSync(join(dir,m.name+".glb"),bytes);console.log(JSON.stringify(report));}
