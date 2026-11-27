function ornaments(){
 const cl=(v,a=0,b=1)=>Math.max(a,Math.min(b,v)),rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16)),mix=(a,b,t)=>a.map((v,i)=>v+(b[i]-v)*cl(t));
 function over(p,c,a){a=cl(a);let A=a+p[3]*(1-a);return A?[...c.map((v,i)=>(v*a+p[i]*p[3]*(1-a))/A),A]:p}
 function segment(x,y,a,b){let dx=b[0]-a[0],dy=b[1]-a[1],L=dx*dx+dy*dy,t=cl(((x-a[0])*dx+(y-a[1])*dy)/L);return Math.hypot(x-a[0]-t*dx,y-a[1]-t*dy)}
 function branch(points,width){return {kind:"branch",points,width}}
 function leaf(a,b,width,bend=0){return {kind:"leaf",a,b,width,bend}}
 const corner=[branch([[35,320],[34,180],[48,103],[83,56],[149,39],[320,39]],8),branch([[35,230],[62,175],[100,142]],5),branch([[115,45],[171,78],[214,86]],5),
 leaf([39,250],[89,159],30,10),leaf([39,202],[14,117],24,-8),leaf([57,143],[148,112],36,12),leaf([83,75],[65,13],23,-6),leaf([135,43],[194,12],26,-9),leaf([173,48],[259,103],33,9),leaf([242,39],[327,12],24,-8),leaf([266,42],[332,84],21,6)];
 const crest=[branch([[24,125],[108,123],[182,143],[256,145],[330,143],[404,123],[488,125]],7),leaf([249,142],[155,53],38,-12),leaf([208,141],[94,156],29,8),leaf([170,130],[85,62],27,-9),leaf([104,124],[34,94],19,5),leaf([263,142],[357,53],38,12),leaf([304,141],[418,156],29,-8),leaf([342,130],[427,62],27,9),leaf([408,124],[478,94],19,-5),leaf([256,133],[256,22],37,0)];
 function draw(w,h,shapes,flipX=false,flipY=false){
 const pixels=new Uint8Array(w*h*4);let count=[0,0,0],b=[w,h,0,0];
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  const sc=.78;let X=((flipX?w-x-.5:x+.5)-w/2)/sc+w/2,Y=((flipY?h-y-.5:y+.5)-h/2)/sc+h/2,p=[0,0,0,0];
  for(const s of shapes){
   if(s.kind==="branch"){
    let d=Infinity;for(let j=0;j<s.points.length-1;j++)d=Math.min(d,segment(X,Y,s.points[j],s.points[j+1]));
    let a=cl(.5+(s.width-d)*sc);if(!a)continue;
    let z=cl(1-d/s.width),c=mix(rgb("8B522A"),rgb("E6B964"),Math.sqrt(z));c=mix(c,rgb("FFF0B5"),Math.exp(-Math.pow((d/s.width-.48)/.19,2))*.63);p=over(p,c,a);
   }else{
    let dx=s.b[0]-s.a[0],dy=s.b[1]-s.a[1],len=Math.hypot(dx,dy),ux=dx/len,uy=dy/len,t=((X-s.a[0])*ux+(Y-s.a[1])*uy)/len;
    if(t<0||t>1)continue;
    let bend=s.bend*Math.sin(Math.PI*t),v=(X-s.a[0])*(-uy)+(Y-s.a[1])*ux-bend,r=s.width*Math.pow(Math.sin(Math.PI*t),.78),d=Math.abs(v)-r;
    let a=cl(.5-d*sc);if(!a||r<.01)continue;
    let u=cl(v/r,-1,1),n=Math.sqrt(Math.max(0,1-u*u));let side=(flipX?-1:1)*(-uy)*u*.18+(flipY?-1:1)*ux*u*.20;
    let light=cl(.21+n*.43-side+.19*(1-t)),c=mix(rgb("32633C"),rgb("D0E99A"),light);
    c=mix(c,rgb("244B36"),Math.exp(-Math.max(-d,0)/2.4)*.48);c=mix(c,rgb("E7F6AD"),Math.exp(-Math.pow((u+.18)/.21,2))*.23);
    p=over(p,c,a);
    let vein=cl(1-Math.abs(v)/1.55)*.66*Math.sin(Math.PI*t);
    for(let k=1;k<6;k++){let q=k/7,off=t-q-Math.abs(u)*.12;vein=Math.max(vein,cl(1-Math.abs(off)/.012)*cl(1-Math.abs(u))*.27)}
    p=over(p,rgb("C9DF88"),vein*a);
   }
  }
  let i=(y*w+x)*4,A=Math.round(cl(p[3])*255);for(let k=0;k<3;k++)pixels[i+k]=Math.round(cl(A?p[k]:[111,135,65][k],0,255));pixels[i+3]=A;count[A===0?0:A===255?2:1]++;
  if(A>0){b[0]=Math.min(b[0],x);b[1]=Math.min(b[1],y);b[2]=Math.max(b[2],x);b[3]=Math.max(b[3],y)}
 }
 if(!count.every(x=>x>0)||b[0]<3||b[1]<3||b[2]>w-4||b[3]>h-4)throw Error("alpha/bounds "+b);
 return {w,h,pixels,alpha:{transparent:count[0],partial:count[1],opaque:count[2]},bounds:b};
 }
 return [["corner_tl",false,false],["corner_tr",true,false],["corner_bl",false,true],["corner_br",true,true]].map(([id,fx,fy])=>({id,...draw(384,384,corner,fx,fy),path:"Decor/ui_ornament_"+id+".png"})).concat([{id:"leaf_crest",...draw(512,192,crest),path:"Decor/ui_ornament_leaf_crest.png"}]);
}

function board(art){const w=768,h=440,pixels=new Uint8Array(w*h*4);for(let i=0;i<pixels.length;i+=4)pixels.set([238,229,211,255],i);
function place(a,x,y,ww,hh){for(let j=0;j<hh;j++)for(let k=0;k<ww;k++){let p=((y+j)*w+x+k)*4,q=(Math.floor(j*a.h/hh)*a.w+Math.floor(k*a.w/ww))*4,A=a.pixels[q+3]/255;for(let i=0;i<3;i++)pixels[p+i]=Math.round(pixels[p+i]*(1-A)+a.pixels[q+i]*A)}}
for(let i=0;i<4;i++)place(art[i],12+i*190,4,174,174);place(art[4],240,182,288,108);
for(let y=302;y<432;y++)for(let x=218;x<550;x++){let i=(y*w+x)*4;pixels.set([91,57,40,255],i)}
place(art[0],218,302,65,65);place(art[1],485,302,65,65);place(art[2],218,367,65,65);place(art[3],485,367,65,65);place(art[4],338,297,92,35);return {w,h,pixels}}

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
import {mkdirSync,writeFileSync} from "node:fs";import {join,dirname} from "node:path";const dir=process.argv[2];if(!dir)throw Error("Usage: node build_vis5_ui_ornaments.mjs <new dir>");mkdirSync(dir);const art=ornaments();for(const a of art){const b=png(a);verify(b,a);let p=join(dir,a.path);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,b);}writeFileSync(join(dir,"preview_ornaments.png"),png(board(art)));
