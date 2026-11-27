function buildCards(slots,group="cards"){
 const cl=(x,a=0,b=1)=>Math.max(a,Math.min(b,x)),rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16)),mix=(a,b,t)=>a.map((v,i)=>v+(b[i]-v)*cl(t)),col=(a,b,t)=>mix(rgb(a),rgb(b),t),aa=d=>cl(.5-d);
 function rr(x,y,w,h,i,r){let a=Math.abs(x-w/2)-(w/2-i-r),b=Math.abs(y-h/2)-(h/2-i-r);return Math.hypot(Math.max(a,0),Math.max(b,0))+Math.min(Math.max(a,b),0)-r}
 function over(p,c,a){a=cl(a);let z=a+p[3]*(1-a);return z?[...c.map((v,i)=>(v*a+p[i]*p[3]*(1-a))/z),z]:[0,0,0,0]}
 function seg(x,y,ax,ay,bx,by){let dx=bx-ax,dy=by-ay,t=cl(((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy));return Math.hypot(x-ax-t*dx,y-ay-t*dy)}
 const palettes={card:["523326","895C3B","D2A56D","F5DCA6","6C4731"],slot_common:["5C3929","AB7945","E8BD7E","FFF0CA","A47649"],slot_rare:["314D68","4C8DB3","9CDBEC","D6F2F8","568EAE"],slot_epic:["4B3867","865EAD","C5A1E8","E8D8F4","9475B9"],slot_legendary:["78491E","CA8B2C","F6CB6C","FFF1B2","B68438"]};
 const out=[];
 for(const s of slots.filter(s=>group==="cards"?Object.hasOwn(palettes,s.id):["btn_close","btn_back"].includes(s.id))){
 for(const state of s.states){const w=s.sourceWidth,h=s.sourceHeight,B=s.borderPx[0],pixels=new Uint8Array(w*h*4),counts=[0,0,0];const isCard=Object.hasOwn(palettes,s.id);
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
 let X=x+.5,Y=y+.5,p=[0,0,0,0];
 if(isCard){
 const [dark,mid,light,cream,base]=palettes[s.id],top=cl((B-Y)/B),bot=cl((Y-h+B)/B),v=.5+.5*top-.5*bot;
 p=over(p,rgb(dark),aa(rr(X,Y,w,h,9,65)));
 p=over(p,col(mid,light,v),aa(rr(X,Y,w,h,13,61)));
 p=over(p,col(dark,light,.35+.5*top),aa(rr(X,Y,w,h,30,44)));
 p=over(p,col(mid,cream,.72+.17*top-.16*bot),aa(rr(X,Y,w,h,34,40)));
 p=over(p,col(base,cream,s.id==="card"?.12:.42),aa(rr(X,Y,w,h,44,30)));
 // small bevel within the inset; all corner motifs stay in the 88 px borders.
 const d=rr(X,Y,w,h,44,30);p=over(p,rgb(dark),cl(1-Math.abs(d+2)/2.5)*(.24+.22*bot)*aa(d));
 if(s.id!=="card"){
 let cx=Math.min(X,w-X),cy=Math.min(Y,h-Y),q=Math.abs(cx-48)/14+Math.abs(cy-48)/14;
 p=over(p,rgb(dark),aa((q-1)*10));
 p=over(p,col(mid,cream,cl(.85-(cy-35)/28)),aa((q-.74)*10));
 // physical corner marker: one to four round rivets, not a substitute for rarity text.
 const k=["slot_common","slot_rare","slot_epic","slot_legendary"].indexOf(s.id)+1;
 for(let j=0;j<k;j++){let px=34+j*12,py=72;let dd=Math.hypot(cx-px,cy-py)-3.5;p=over(p,rgb(dark),aa(dd));p=over(p,rgb(cream),aa(dd+1.2))}
 }
 }else{
 let close=s.id==="btn_close",C=(close?["803B32","B95A46","F19C77"]:["345C60","4D969F","A3DCDD"]).map(rgb);
 if(state==="pressed")C=C.map(c=>mix(c,rgb("3C3027"),.24));if(state==="disabled")C=C.map(c=>mix(c,rgb("ACA28F"),.72));
 let dx=X-256,dy=Y-256,R=Math.hypot(dx,dy),v=cl(.52-.42*dy/212-.10*dx/212);
 p=over(p,rgb("513329"),aa(Math.hypot(dx,dy-4)-211));
 p=over(p,col("9E6331","FFE4A1",v),aa(R-205));
 p=over(p,col("784C30","D6A361",v),aa(R-186));
 let light=cl(.56-.35*dy/173-.12*dx/173+.17*Math.sqrt(Math.max(0,1-Math.pow(R/173,2))));
 p=over(p,mix(C[0],C[2],light),aa(R-173));
 // high contrast non-text symbols, rounded stroke ends.
 let d=close?Math.min(seg(X,Y,187,187,325,325),seg(X,Y,325,187,187,325)):Math.min(seg(X,Y,175,256,333,256),seg(X,Y,175,256,246,185),seg(X,Y,175,256,246,327));
 p=over(p,rgb("573D2D"),aa(d-23)*.75);p=over(p,rgb(state==="disabled"?"E8DFC8":"FFF3D6"),aa(d-17));
 }
 let i=(y*w+x)*4,a=Math.round(cl(p[3])*255);for(let k=0;k<3;k++)pixels[i+k]=Math.round(cl(a?p[k]:[167,119,72][k],0,255));pixels[i+3]=a;counts[a===0?0:a===255?2:1]++;
 }
 let delta=0;if(isCard)for(let y=0;y<h;y++)for(let k=0;k<4;k++)delta=Math.max(delta,Math.abs(pixels[(y*w+B)*4+k]-pixels[(y*w+w-B-1)*4+k]));
 if(isCard)for(let x=B;x<w-B;x++)for(let k=0;k<4;k++)delta=Math.max(delta,Math.abs(pixels[(B*w+x)*4+k]-pixels[((h-B-1)*w+x)*4+k]));
 if(delta>1||!counts[0]||!counts[1]||!counts[2])throw Error(s.id+" alpha/slice "+delta);
 const runtime=s.path.replace(/\.png$/,state==="normal"?".png":"_"+state+".png");
 out.push({id:s.id,role:s.role,state,w,h,pixels,path:runtime.replace("Assets/Dravio/UI/Sprites/",""),runtime,border:s.borderPx,alpha:{transparent:counts[0],partial:counts[1],opaque:counts[2]},sliceEndpointDelta:delta});
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
import {mkdirSync,readFileSync,writeFileSync} from "node:fs";import {join,dirname} from "node:path";const dir=process.argv[2];if(!dir)throw Error("Usage: node build_vis4_ui_cards.mjs <new directory> [cards|controls]");mkdirSync(dir);const c=JSON.parse(readFileSync("Assets/Dravio/UI/Data/ui-sprite-slots.json","utf8"));for(const a of buildCards(c.slots,process.argv[3]||"cards")){let bytes=png(a);verify(bytes,a);let p=join(dir,a.path);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,bytes);}
