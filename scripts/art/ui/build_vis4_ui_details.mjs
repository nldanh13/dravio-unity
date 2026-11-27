function buildDetails(slots){
 const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v)),mix=(a,b,t)=>a.map((v,i)=>v+(b[i]-v)*clamp(t)),rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16));
 const color=(a,b,t)=>mix(rgb(a),rgb(b),t),ss=(a,b,v)=>{let t=clamp((v-a)/(b-a));return t*t*(3-2*t)};
 const rr=(x,y,w,h,inset,r)=>{let a=Math.abs(x-w/2)-(w/2-inset-r),b=Math.abs(y-h/2)-(h/2-inset-r);return Math.hypot(Math.max(a,0),Math.max(b,0))+Math.min(Math.max(a,b),0)-r};
 const layer=(p,c,a)=>{a=clamp(a);let z=a+p[3]*(1-a);return z?[...c.map((v,i)=>(v*a+p[i]*p[3]*(1-a))/z),z]:[0,0,0,0]};
 const aa=d=>clamp(.5-d);
 return ["progress_frame","progress_fill","progress_highlight","portrait","tab_indicator","divider"].map(id=>{
  const s=slots.find(a=>a.id===id),w=s.sourceWidth,h=s.sourceHeight,pixels=new Uint8Array(w*h*4);let count=[0,0,0];
  for(let y=0;y<h;y++)for(let x=0;x<w;x++){
   let X=x+.5,Y=y+.5,t=Y/h,p=[0,0,0,0];
   if(id==="progress_frame"){
    let top=clamp((56-Y)/56),bottom=clamp((Y-(h-56))/56),v=.5-.5*top+.5*bottom;
    p=layer(p,rgb("593622"),aa(rr(X,Y,w,h,8,42)));
    p=layer(p,color("F7D88F","A16C35",v),aa(rr(X,Y,w,h,11,39)));
    p=layer(p,color("704C32","BA8850",v),aa(rr(X,Y,w,h,21,29)));
    p=layer(p,color("39291F","654A33",v),aa(rr(X,Y,w,h,27,23)));
    let di=rr(X,Y,w,h,27,23);p=layer(p,rgb("F7D58E"),Math.max(0,1-Math.abs(di+1.5)/1.6)*ss(.65,.9,t)*.38);
   }else if(id==="progress_fill"){
    let d=rr(X,Y,w,h,6,30),top=clamp((40-Y)/40),bottom=clamp((Y-(h-40))/40);
    let c=color("6DAB58","B7E78B",.66);c=mix(c,rgb("E9FBC4"),top*.72);c=mix(c,rgb("38824A"),bottom*.72);
    p=layer(p,rgb("39764A"),aa(d));p=layer(p,c,aa(d+3));
   }else if(id==="progress_highlight"){
    let d=rr(X,Y,w,h,9,27);let fade=Math.exp(-Math.pow((Y-26)/9,2))*.58;
    p=layer(p,rgb("FFFBE6"),aa(d)*fade);
   }else if(id==="portrait"){
    let dx=X-w/2,dy=Y-h/2,r=Math.hypot(dx,dy),theta=Math.atan2(dy,dx);
    let a=aa(r-222)*aa(166-r),lt=clamp(.52-.39*Math.sin(theta)+.09*Math.cos(theta*2));
    let c=color("75452C","F3CD82",lt);
    if(r>210)c=color("4D3023","925A34",lt);else if(r>199)c=color("B77B37","FFF0B0",lt);else if(r>184)c=color("9A623C","F4D28F",lt);else if(r>176)c=color("BE8338","FFEAA8",lt);else c=color("513324","9D6A3C",lt);
    for(let k=0;k<8;k++){let ang=Math.PI*2*k/8-Math.PI/2,dist=Math.hypot(dx-192*Math.cos(ang),dy-192*Math.sin(ang));c=mix(c,rgb("FFDFA0"),aa(dist-7)*.88);c=mix(c,rgb("FFF6CF"),aa(Math.hypot(dx-192*Math.cos(ang)+1.6,dy-192*Math.sin(ang)+1.6)-3)*.8)}
    p=[...c,a];
   }else if(id==="tab_indicator"){
    let d=rr(X,Y,w,h,10,20);p=layer(p,rgb("945C28"),aa(d));p=layer(p,color("FFF2B2","E5A43E",t),aa(d+3));
   }else if(id==="divider"){
    p=layer(p,color("E9C77D","9F672D",t),aa(Math.abs(Y-h/2)-5)*ss(15,26,X)*ss(15,26,w-X));
    let local=Math.min(X,w-X);
    for(const [cx,cy,rx,ry,angle] of [[51,44,30,10,-.43],[77,82,32,10,.42],[111,49,24,8,-.32]]){
     let dx=local-cx,dy=Y-cy,xx=dx*Math.cos(angle)+dy*Math.sin(angle),yy=-dx*Math.sin(angle)+dy*Math.cos(angle),f=Math.hypot(xx/rx,yy/ry);
     p=layer(p,color("A47739","F6DEA2",clamp(.5-yy/(ry*2))),aa((f-1)*ry));
    }
    p=layer(p,rgb("F3D18C"),aa((Math.abs(local-32)/9+Math.abs(Y-h/2)/9-1)*8));
   }
   let i=(y*w+x)*4,a=Math.round(clamp(p[3])*255);for(let k=0;k<3;k++)pixels[i+k]=Math.round(clamp(a?p[k]:(id==="progress_fill"?[130,190,103][k]:[198,153,93][k]),0,255));pixels[i+3]=a;count[a===0?0:a===255?2:1]++;
  }
  let centerDelta=0,B=s.borderPx[0],core=0;
  if(s.sliced)for(let y=0;y<h;y++)for(let k=0;k<4;k++)centerDelta=Math.max(centerDelta,Math.abs(pixels[(y*w+B)*4+k]-pixels[(y*w+w-B-1)*4+k]));
  if(id==="portrait"){for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(Math.hypot(x+.5-w/2,y+.5-h/2)<164&&pixels[(y*w+x)*4+3])core++;if(core)throw Error("portrait alpha core")}
  if(centerDelta>1||!count[0]||!count[1])throw Error("alpha or center "+id+" "+centerDelta);
  return {id,role:s.role,state:"normal",w,h,pixels,path:s.path.replace("Assets/Dravio/UI/Sprites/",""),runtime:s.path,border:s.borderPx,alpha:{transparent:count[0],partial:count[1],opaque:count[2]},sliceHorizontalEndpointDelta:centerDelta,coreNonzero:core};
 })
}

function png(t){
 const u32=n=>[(n>>>24)&255,(n>>>16)&255,(n>>>8)&255,n&255],cat=a=>{let o=new Uint8Array(a.reduce((s,b)=>s+b.length,0)),i=0;for(let b of a){o.set(b,i);i+=b.length}return o};
 const tab=Array.from({length:256},(_,i)=>{let c=i;for(let j=0;j<8;j++)c=c&1?(0xedb88320^(c>>>1)):(c>>>1);return c>>>0});const chunk=(n,a)=>{let k=Uint8Array.from(n.split("").map(c=>c.charCodeAt(0))),c=0xffffffff;for(let b of [k,a])for(let x of b)c=tab[(c^x)&255]^(c>>>8);return cat([u32(a.length),k,a,u32((c^0xffffffff)>>>0)])};
 let stride=t.w*4,raw=new Uint8Array((stride+1)*t.h);for(let y=0;y<t.h;y++){let i=y*(stride+1),s=y*stride;raw[i]=1;for(let x=0;x<stride;x++)raw[i+x+1]=(t.pixels[s+x]-(x>=4?t.pixels[s+x-4]:0))&255}
 let buf=[],bb=0,bits=0;function write(v,n){bb|=v<<bits;bits+=n;while(bits>=8){buf.push(bb&255);bb>>>=8;bits-=8}}function rev(v,n){let r=0;for(let i=0;i<n;i++)r=r*2+((v>>i)&1);return r}function sym(v){let n=v<144?8:v<256?9:v<280?7:8,c=v<144?48+v:v<256?400+v-144:v<280?v-256:192+v-280;write(rev(c,n),n)}
 const lb=[3,4,5,6,7,8,9,10,11,13,15,17,19,23,27,31,35,43,51,59,67,83,99,115,131,163,195,227,258],le=[0,0,0,0,0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3,4,4,4,4,5,5,5,5,0],db=[1,2,3,4,5,7,9,13,17,25,33,49,65,97,129,193,257,385,513,769,1025,1537,2049,3073,4097,6145,8193,12289,16385,24577],de=[0,0,0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11,12,12,13,13];
 let last=new Int32Array(65536);last.fill(-1);let hash=i=>((raw[i]*251+raw[i+1])*251+raw[i+2])&65535;write(1,1);write(1,2);
 for(let i=0;i<raw.length;){let l=0,d=0;if(i+2<raw.length){let k=hash(i),p=last[k];if(p>=0&&i-p<=32768&&raw[p]===raw[i]&&raw[p+1]===raw[i+1]&&raw[p+2]===raw[i+2]){d=i-p;while(l<258&&i+l<raw.length&&raw[i+l]===raw[i+l-d])l++}last[k]=i}if(l>=3){let a=lb.length-1;while(lb[a]>l)a--;sym(257+a);write(l-lb[a],le[a]);let b=db.length-1;while(db[b]>d)b--;write(rev(b,5),5);write(d-db[b],de[b]);for(let j=1;j<l&&i+j+2<raw.length;j++)last[hash(i+j)]=i+j;i+=l}else{sym(raw[i]);i++}}sym(256);if(bits)buf.push(bb&255);let a=1,b=0;for(let x of raw){a=(a+x)%65521;b=(b+a)%65521}
 return cat([[137,80,78,71,13,10,26,10],chunk("IHDR",Uint8Array.from([...u32(t.w),...u32(t.h),8,6,0,0,0])),chunk("sRGB",Uint8Array.of(0)),chunk("IDAT",cat([[0x78,0x01],buf,u32(((b<<16)|a)>>>0)])),chunk("IEND",new Uint8Array(0))])
}

function verify(bytes,t){
 let dv=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength);if(dv.getUint32(16)!==t.w||dv.getUint32(20)!==t.h||bytes[25]!==6)throw Error("header");let pos=8,id;while(pos<bytes.length){let n=dv.getUint32(pos);if(String.fromCharCode(...bytes.subarray(pos+4,pos+8))==="IDAT")id=bytes.subarray(pos+8,pos+8+n);pos+=n+12}let bit=16,read=n=>{let v=0;for(let j=0;j<n;j++,bit++)v|=((id[bit>>3]>>(bit&7))&1)<<j;return v};if(read(1)!==1||read(2)!==1)throw Error("block");
 function symbol(){let c=0;for(let n=1;n<=9;n++){c=c*2+read(1);if(n===7&&c<=23)return c+256;if(n===8&&c>=48&&c<=191)return c-48;if(n===8&&c>=192&&c<=199)return c-192+280;if(n===9&&c>=400&&c<=511)return c-400+144}throw Error("symbol")}
 const lb=[3,4,5,6,7,8,9,10,11,13,15,17,19,23,27,31,35,43,51,59,67,83,99,115,131,163,195,227,258],le=[0,0,0,0,0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3,4,4,4,4,5,5,5,5,0],db=[1,2,3,4,5,7,9,13,17,25,33,49,65,97,129,193,257,385,513,769,1025,1537,2049,3073,4097,6145,8193,12289,16385,24577],de=[0,0,0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11,12,12,13,13];
 let stride=t.w*4,raw=new Uint8Array((stride+1)*t.h),i=0;while(true){let s=symbol();if(s===256)break;if(s<256)raw[i++]=s;else{let l=lb[s-257]+read(le[s-257]),k=0;for(let j=0;j<5;j++)k=k*2+read(1);let d=db[k]+read(de[k]);if(d>i||i+l>raw.length)throw Error("distance");for(let j=0;j<l;j++,i++)raw[i]=raw[i-d]}if(i>raw.length)throw Error("length")}if(i!==raw.length)throw Error("length");
 for(let y=0;y<t.h;y++){let off=y*(stride+1);if(raw[off]!==1)throw Error("filter");for(let x=0;x<stride;x++)if(((raw[off+1+x]+(x>=4?t.pixels[y*stride+x-4]:0))&255)!==t.pixels[y*stride+x])throw Error("pixel")}
}
import {mkdirSync,readFileSync,writeFileSync} from "node:fs";import {join,dirname} from "node:path";const dir=process.argv[2];if(!dir)throw Error("Usage: node build_vis4_ui_details.mjs <new directory>");mkdirSync(dir);const c=JSON.parse(readFileSync("Assets/Dravio/UI/Data/ui-sprite-slots.json","utf8"));for(const a of buildDetails(c.slots)){const bytes=png(a);verify(bytes,a);let p=join(dir,a.path);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,bytes);}
