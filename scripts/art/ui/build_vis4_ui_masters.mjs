function render(slots,group="buttons"){
 const clamp=(x,a=0,b=1)=>Math.max(a,Math.min(b,x)),mix=(a,b,t)=>a.map((v,i)=>v+(b[i]-v)*clamp(t)),rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16));
 function sdf(x,y,w,h,i,r){let a=Math.abs(x-w/2)-(w/2-i-r),b=Math.abs(y-h/2)-(h/2-i-r);return Math.hypot(Math.max(a,0),Math.max(b,0))+Math.min(Math.max(a,b),0)-r}
 function over(p,c,a){a=clamp(a);let z=a+p[3]*(1-a);return z?[...c.map((v,i)=>(v*a+p[i]*p[3]*(1-a))/z),z]:[0,0,0,0]}
 const palettes={btn_primary:["27663B","4E9F51","85CB6B","D7F5A9"],btn_secondary:["8D653F","E0BB7D","FFE4B3","FFF5DB"],btn_reward:["A96523","E8A331","FFD568","FFF3B9"],btn_danger:["8C3536","D45B4F","F58E76","FFD0AB"],tab:["8D653F","CEAA72","EFD1A1","FFE9BE"],tab_active:["A86825","DEA644","F9D16B","FFF1B7"],panel:["492B22","6C412D","9E6640","EDBF76"],header:["513025","795036","AC7548","F3C883"],page_bg:["5C382B","79523A","A16B46","E8B87A"]};
 const out=[];
 for(const s of slots.filter(s=>Object.hasOwn(palettes,s.id)&&(group==="buttons"?s.id.startsWith("btn_"):!s.id.startsWith("btn_")))){
 const w=s.sourceWidth,h=s.sourceHeight,B=s.borderPx[0],surface=/^(panel|header|page_bg)$/.test(s.id);
 for(const state of s.states.filter(t=>t==="normal"||t==="pressed"||t==="disabled")){
 let colors=palettes[s.id].map(rgb);if(state==="pressed")colors=colors.map(c=>mix(c,rgb("49352B"),.20));if(state==="disabled")colors=colors.map(c=>mix(c,rgb("A69F8C"),.72));
 const pad=8,r=Math.min(B-pad-4,Math.floor(h*.32)),trim=surface?Math.round(B*.23):Math.round(B*.18);let pixels=new Uint8Array(w*h*4),alpha0=0,opaque=0,partial=0;
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
 let X=x+.5,Y=y+.5,d=sdf(X,Y,w,h,pad,r),p=[...colors[0],0];let shadow=sdf(X,Y-3,w,h,pad,r);p=over(p,rgb("422D25"),clamp(1-shadow/4)*.30);
 let top=clamp((B-Y)/B),bottom=clamp((Y-(h-B))/B);let outline=mix(rgb("684020"),rgb("F8D68C"),top*.85+(1-bottom)*.08);if(state==="disabled")outline=mix(outline,rgb("958D7C"),.72);
 p=over(p,outline,clamp(.5-d));let d1=sdf(X,Y,w,h,pad+4,Math.max(1,r-4)),metal=mix(rgb("AC7438"),rgb("F9D481"),clamp(.58+top*.38-bottom*.43));if(state==="disabled")metal=mix(metal,rgb("B0A794"),.70);
 p=over(p,metal,clamp(.5-d1));let di=sdf(X,Y,w,h,pad+trim,Math.max(1,r-trim));let c=mix(colors[1],colors[2],surface?.18:.55);c=mix(c,colors[3],top*(surface?.16:.52));c=mix(c,colors[0],bottom*(surface?.20:.47));c=mix(c,top>bottom?colors[3]:colors[0],clamp((di+5)/5)*.30);p=over(p,c,clamp(.5-di));p=over(p,colors[3],clamp(1-Math.abs(di+5)/2.8)*top*.30*clamp(.5-di));
 let i=(y*w+x)*4;for(let k=0;k<3;k++)pixels[i+k]=Math.round(clamp(p[k],0,255));pixels[i+3]=Math.round(p[3]*255);if(!pixels[i+3]){alpha0++;for(let k=0;k<3;k++)pixels[i+k]=Math.round(outline[k])}else if(pixels[i+3]===255)opaque++;else partial++;
 }
 let dx=0,dy=0;for(let y=0;y<h;y++)for(let k=0;k<4;k++)dx=Math.max(dx,Math.abs(pixels[(y*w+B)*4+k]-pixels[(y*w+w-B-1)*4+k]));for(let x=B;x<w-B;x++)for(let k=0;k<4;k++)dy=Math.max(dy,Math.abs(pixels[(B*w+x)*4+k]-pixels[((h-B-1)*w+x)*4+k]));if(dx>1||dy>1||!alpha0||!opaque)throw Error("slice alpha "+s.id);
 let runtime=s.path.replace(/\.png$/,state==="normal"?".png":"_"+state+".png");out.push({id:s.id,role:s.role,state,w,h,pixels,path:runtime.replace("Assets/Dravio/UI/Sprites/",""),runtime,border:s.borderPx,alpha:{transparent:alpha0,opaque,partial},sliceEndpointDelta:[dx,dy]})
 }}
 return out;
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
import {mkdirSync,readFileSync,writeFileSync} from "node:fs";import {join,dirname} from "node:path";const dir=process.argv[2];if(!dir)throw Error("Usage: node build_vis4_ui_masters.mjs <new dir> [buttons|surfaces]");mkdirSync(dir);const c=JSON.parse(readFileSync("Assets/Dravio/UI/Data/ui-sprite-slots.json","utf8"));for(const a of render(c.slots,process.argv[3]||"buttons")){let b=png(a);verify(b,a);let p=join(dir,a.path);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,b);}
