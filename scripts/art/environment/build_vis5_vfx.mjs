function makeVfx(){
 const cl=(v,a=0,b=1)=>Math.max(a,Math.min(b,v)),ss=(a,b,x)=>{let t=cl((x-a)/(b-a));return t*t*(3-2*t)},rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16)),mix=(a,b,t)=>a.map((v,i)=>v+(b[i]-v)*cl(t)),tau=Math.PI*2;
 const ell=(x,y,cx,cy,rx,ry,ang=0)=>{let a=x-cx,b=y-cy,c=Math.cos(ang),s=Math.sin(ang);return Math.hypot((a*c+b*s)/rx,(-a*s+b*c)/ry)};
 function over(p,c,a){a=cl(a);let A=a+p[3]*(1-a);return A?[...c.map((v,i)=>(v*a+p[i]*p[3]*(1-a))/A),A]:[...c,0]}
 function flame(u,v,phase){
 // Main tongue and two short side tongues meet near a stable lower anchor.
 let y=1-v;
 function tongue(cx,base,h,w,lean,sway){
  let t=(y-base)/h;if(t<0||t>1)return {a:0,d:2,t:0};
  let bend=(lean+sway*Math.sin(phase))*Math.pow(t,1.75)+.035*Math.sin(tau*t+phase)*t;
  let shape=Math.pow(Math.sin(Math.PI*t),.66)*(1.25-.82*t);
  let d=Math.abs(u-cx-bend)/Math.max(.0001,w*shape);
  return {a:(1-ss(.89,1.01,d))*ss(0,.045,t)*(1-ss(.97,1,t)),d,t};
 }
 let main=tongue(.51,.145,.71+.026*Math.sin(phase),.225,.035,.085);
 let left=tongue(.398,.17,.43+.035*Math.sin(phase+1.8),.135,-.08,.035);
 let right=tongue(.60,.18,.49+.04*Math.sin(phase-1.4),.125,.11,.042);
 let a=Math.max(main.a,left.a,right.a),c=mix(rgb("E76530"),rgb("FFC95D"),cl(.35+.49*main.t+.24*(1-main.d)));
 let hot=tongue(.50,.17,.47+.015*Math.sin(phase),.137,.015,.043);
 c=mix(c,rgb("FFF1B9"),hot.a*.97);
 c=mix(c,rgb("FFB54F"),Math.max(left.a,right.a)*(1-main.a)*.55);
 return [...c,a];
 }
 function raster(id,w,h,fn,blend,desc,grid=null){
 let pixels=new Uint8Array(w*h*4),alpha=[0,0,0],frames=[];
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){let p=fn((x+.5)/w,(y+.5)/h,x,y);if(!p.every(Number.isFinite))throw Error("nonfinite "+id);let i=(y*w+x)*4,a=Math.round(cl(p[3])*255);for(let k=0;k<3;k++)pixels[i+k]=Math.round(cl(p[k],0,255));pixels[i+3]=a;alpha[a===0?0:a===255?2:1]++}
 const n=grid||1,cw=w/n,ch=h/n;
 for(let f=0;f<n*n;f++){let ox=f%n*cw,oy=Math.floor(f/n)*ch,b=[cw,ch,0,0],mass=0,opaque=0;for(let j=0;j<ch;j++)for(let i=0;i<cw;i++){let a=pixels[((oy+j)*w+ox+i)*4+3];if(a){b[0]=Math.min(b[0],i);b[1]=Math.min(b[1],j);b[2]=Math.max(b[2],i);b[3]=Math.max(b[3],j);mass+=a;opaque+=a===255?1:0;}}
 let pad=Math.min(b[0],b[1],cw-1-b[2],ch-1-b[3]);if(pad<4||mass<10000)throw Error(id+" frame "+f+" "+b+" pad="+pad);frames.push({index:f,bounds:b,padding:pad,alphaMass:mass,opaque});}
 return {id,w,h,pixels,alpha,frames,blend,desc,grid};
 }
 const assets=[];
 assets.push(raster("Emberling_Flame_16",512,512,(_,__,x,y)=>{let ix=Math.floor(x/128),iy=Math.floor(y/128),f=iy*4+ix;return flame((x%128+.5)/128,(y%128+.5)/128,tau*f/16)},"Alpha","4x4 whole-sheet flame flicker, warm opaque core and soft edge",4));
 assets.push(raster("Firefly_Soft",128,128,(u,v)=>{let r=Math.hypot((u-.5)/.44,(v-.5)/.44);let a=(.65*Math.exp(-r*r*12)+.35*Math.exp(-r*r*4.2))*(1-ss(.72,1,r));return [255,255,255,a]},"Additive","Neutral white radial glow, tint by existing particle colors"));
 assets.push(raster("Magic_Spark",128,128,(u,v)=>{let x=Math.abs((u-.5)/.41),y=Math.abs((v-.5)/.41),r=Math.hypot(x,y);let cross=Math.pow(x,.47)+Math.pow(y,.47);let a=Math.max((1-ss(.88,1,cross))*.96,.26*Math.exp(-r*r*7)*(1-ss(.76,1,r)));return [255,255,255,a]},"Additive","Neutral four-point twinkle, soft halo"));
 assets.push(raster("Dust_Mote",128,128,(u,v)=>{let x=(u-.5)/.4,y=(v-.5)/.4,r=Math.hypot(x,y),ang=Math.atan2(y,x);let rr=r/(1+.07*Math.cos(ang*5)+.04*Math.sin(ang*3));return [255,255,255,.62*Math.exp(-rr*rr*3.3)*(1-ss(.68,1,rr))]},"Additive","Neutral subdued dust mote"));
 assets.push(raster("Water_Drop",128,128,(u,v)=>{let t=(v-.13)/.73;if(t<0||t>1)return [185,230,245,0];let width=.26*Math.pow(Math.sin(Math.PI*t),.78)*(.56+.62*t),d=Math.abs(u-.5)/Math.max(.0001,width),a=(1-ss(.90,1.015,d))*ss(0,.035,t)*(1-ss(.97,1,t));let c=mix(rgb("80BCD4"),rgb("DFFAFF"),cl(.22+.58*(1-d)+.18*t));let p=[...c,a*.88];p=over(p,rgb("FFFFFF"),(1-ss(.60,1,ell(u,v,.43,.48,.047,.16,.20)))*.75*a);return p},"Alpha","A translucent cyan droplet for dedicated splash emitter"));
 assets.push(raster("Pond_Ring",256,256,(u,v)=>{let x=(u-.5)/.45,y=(v-.5)/.45,r=Math.hypot(x,y);let band=Math.exp(-Math.pow((r-.72)/.036,2))*.82+Math.exp(-Math.pow((r-.60)/.022,2))*.20;return [255,255,255,band*(1-ss(.88,1,r))]},"Alpha","Neutral concentric ripple, existing horizontal billboard and color"));
 assets.push(raster("Foam_Cluster",256,256,(u,v)=>{let p=[225,250,248,0];for(const [x,y,rx,ry] of [[.30,.53,.14,.085],[.44,.48,.18,.10],[.59,.54,.15,.09],[.71,.48,.12,.07],[.46,.62,.11,.06],[.67,.66,.075,.044],[.35,.36,.064,.039],[.57,.32,.045,.035],[.77,.34,.032,.026]]){let d=ell(u,v,x,y,rx,ry);p=over(p,rgb("C4E9E8"),(1-ss(.83,1,d))*.70);let d2=ell(u,v,x-rx*.11,y-ry*.17,rx*.79,ry*.66);p=over(p,rgb("F4FFFC"),(1-ss(.81,1,d2))*.87)}return p},"Alpha","Foam patch for optional waterfall/shore review, not a new gameplay emitter"));
 return assets;
}

function preview(art){
 let w=800,h=420,pixels=new Uint8Array(w*h*4);for(let y=0;y<h;y++)for(let x=0;x<w;x++){let c=y<260?[49,65,54]:[29,42,53];pixels.set([...c,255],(y*w+x)*4)}
 function place(a,x,y,ww,hh,sx=0,sy=0,sw=a.w,sh=a.h,tint=[1,1,1]){for(let j=0;j<hh;j++)for(let i=0;i<ww;i++){let q=((sy+Math.floor(j*sh/hh))*a.w+sx+Math.floor(i*sw/ww))*4,k=((y+j)*w+x+i)*4,A=a.pixels[q+3]/255;for(let c=0;c<3;c++){let v=a.pixels[q+c]*tint[c];pixels[k+c]=Math.round(a.blend==="Additive"?Math.min(255,pixels[k+c]+v*A):pixels[k+c]*(1-A)+v*A)}}}
 for(let f=0;f<16;f++)place(art[0],f%8*100+2,Math.floor(f/8)*125+13,96,96,f%4*128,Math.floor(f/4)*128,128,128);
 for(let i=1;i<art.length;i++)place(art[i],12+(i-1)*130,276,120,120,0,0,art[i].w,art[i].h,i===1?[.8,1,.45]:i===2?[.55,.95,1]:i===3?[1,.92,.72]:[1,1,1]);
 return {w,h,pixels};
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
import {mkdirSync,writeFileSync} from "node:fs";import {join} from "node:path";const dir=process.argv[2];if(!dir)throw Error("Usage: node build_vis5_vfx.mjs <new output dir>");mkdirSync(dir);const art=makeVfx();for(const a of art){const b=png(a);verify(b,a);writeFileSync(join(dir,"T_VIS5_FX_"+a.id+".png"),b);}writeFileSync(join(dir,"preview.png"),png(preview(art)));
