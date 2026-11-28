function polishButtons(){
 const cl=(v,a=0,b=1)=>Math.max(a,Math.min(b,v)),rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16)),mix=(a,b,t)=>a.map((v,i)=>v+(b[i]-v)*cl(t)),ss=(a,b,x)=>{let t=cl((x-a)/(b-a));return t*t*(3-2*t)};
 function rounded(x,y,w,h,inset,rad){let a=Math.abs(x-w/2)-(w/2-inset-rad),b=Math.abs(y-h/2)-(h/2-inset-rad);return Math.hypot(Math.max(a,0),Math.max(b,0))+Math.min(Math.max(a,b),0)-rad}
 function over(p,c,a){a=cl(a);let A=a+p[3]*(1-a);return A?[...c.map((v,i)=>(v*a+p[i]*p[3]*(1-a))/A),A]:[0,0,0,0]}
 function luminance(c){let v=c.map(x=>{x/=255;return x<=.04045?x/12.92:Math.pow((x+.055)/1.055,2.4)});return .2126*v[0]+.7152*v[1]+.0722*v[2]}
 function contrast(a,b){let x=luminance(a),y=luminance(b);return (Math.max(x,y)+.05)/(Math.min(x,y)+.05)}
 const out=[];
 for(const role of ["primary","danger"])for(const state of ["normal","pressed","disabled"]){
 const w=1024,h=384,B=88,pixels=new Uint8Array(w*h*4),alpha=[0,0,0],ink=rgb(role==="primary"?"49372C":"FFFFFF");
 // Intentionally keep the green face bright and the red face dark enough for existing white label.
 const pal=role==="primary"?["345F36","9BD271","C9EE9B","719F52"]:["78352D","9D3D36","C45B46","7D302C"];
 let [deep,base,light,bottom]=pal.map(rgb);
 if(state==="pressed"){base=mix(base,deep,role==="primary"?.15:.14);light=mix(light,deep,.23);bottom=mix(bottom,deep,.21)}
 if(state==="disabled"){const gray=rgb(role==="primary"?"C1BC9C":"78675F");base=mix(base,gray,.66);light=mix(light,gray,.72);bottom=mix(bottom,gray,.70);deep=mix(deep,gray,.55)}
 let minContrast=Infinity;
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
 let X=x+.5,Y=y+.5,p=[0,0,0,0],d=rounded(X,Y,w,h,9,68),aa=v=>cl(.5-v);
 const top=cl((B-Y)/B),bot=cl((Y-h+B)/B);
 // All y variation confined to fixed slice bands. The center is constant.
 let shadow=rounded(X,Y-5,w,h,9,68);p=over(p,rgb("593D26"),cl(1-shadow/4)*.33);
 p=over(p,rgb("674024"),aa(d));const z=-d;
 // Simulated rounded metal cross section with edge lighting and thin specular ridge.
 const metalT=cl(.58+top*.36-bot*.39),goldDark=rgb("96602B"),goldLight=rgb("FFE6A1");
 let gold=mix(goldDark,goldLight,metalT);
 let spec=Math.exp(-Math.pow((z-7)/2.1,2))*(.48+.32*top),ao=Math.exp(-Math.pow((z-23)/3.2,2))*.42;
 gold=mix(gold,rgb("FFF6CD"),spec);gold=mix(gold,rgb("78451F"),ao);
 if(state==="disabled")gold=mix(gold,rgb("B3A591"),.65);
 p=over(p,gold,aa(d+3));
 // Inset lip and face. No decorative texture in the stretchable center.
 p=over(p,mix(deep,rgb("71472A"),.28),aa(d+24));
 const df=rounded(X,Y,w,h,36,41),f=-df;
 let c=mix(base,light,ss(0,1,top)*.82);c=mix(c,bottom,ss(0,1,bot)*.78);
 let bevel=Math.exp(-Math.max(f,0)/7.5);c=mix(c,top>bot?light:deep,bevel*.55);
 let glow=Math.exp(-Math.pow((Y-53)/14,2))*top*.38; c=mix(c,light,glow);
 let rim=Math.exp(-Math.pow((f-6)/1.8,2))*top*.34;c=mix(c,rgb("FFF0BF"),rim);
 p=over(p,c,aa(df));
 const i=(y*w+x)*4,A=Math.round(p[3]*255);for(let k=0;k<3;k++)pixels[i+k]=Math.round(cl(A?p[k]:gold[k],0,255));pixels[i+3]=A;alpha[A===0?0:A===255?2:1]++;
 if(x>=w*.22&&x<w*.78&&y>=h*.32&&y<h*.68)minContrast=Math.min(minContrast,contrast(Array.from(pixels.subarray(i,i+3)),ink));
 }
 let delta=0;for(let y=0;y<h;y++)for(let k=0;k<4;k++)delta=Math.max(delta,Math.abs(pixels[(y*w+B)*4+k]-pixels[(y*w+w-B-1)*4+k]));for(let x=B;x<w-B;x++)for(let k=0;k<4;k++)delta=Math.max(delta,Math.abs(pixels[(B*w+x)*4+k]-pixels[((h-B-1)*w+x)*4+k]));
 if(delta>1||!alpha[0]||!alpha[2]||minContrast<4.5)throw Error(role+" "+state+" delta="+delta+" contrast="+minContrast);
 const suffix=state==="normal"?"":"_"+state,path="Buttons/ui_btn_"+role+suffix+".png";
 out.push({id:"btn_"+role,state,w,h,pixels,path,border:[B,B,B,B],alpha:{transparent:alpha[0],partial:alpha[1],opaque:alpha[2]},label:role==="primary"?"#49372C":"#FFFFFF",minSourceCenterContrast:minContrast,sliceEndpointDelta:delta});
 }
 return out;
}

function board(art){let w=768,h=312,pixels=new Uint8Array(w*h*4);for(let i=0;i<pixels.length;i+=4)pixels.set([237,228,209,255],i);for(let k=0;k<art.length;k++){let a=art[k],ox=k%3*256+8,oy=Math.floor(k/3)*150+10,ww=240,hh=100;for(let y=0;y<hh;y++)for(let x=0;x<ww;x++){let q=(Math.floor(y*a.h/hh)*a.w+Math.floor(x*a.w/ww))*4,p=((oy+y)*w+ox+x)*4,A=a.pixels[q+3]/255;for(let j=0;j<3;j++)pixels[p+j]=Math.round(pixels[p+j]*(1-A)+a.pixels[q+j]*A)}}return {w,h,pixels}}

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
import {mkdirSync,writeFileSync} from "node:fs";import {join,dirname} from "node:path";const dir=process.argv[2];if(!dir)throw Error("Usage: node build_vis5_ui_buttons.mjs <new dir>");mkdirSync(dir);const art=polishButtons();for(const a of art){const b=png(a);verify(b,a);let p=join(dir,a.path);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,b);}writeFileSync(join(dir,"preview_buttons.png"),png(board(art)));
