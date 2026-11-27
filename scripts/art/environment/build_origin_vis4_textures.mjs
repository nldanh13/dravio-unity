function makeTextures(size=512){
 const tau=2*Math.PI,clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v)),lerp=(a,b,t)=>a+(b-a)*t,ss=(a,b,t)=>{t=clamp((t-a)/(b-a));return t*t*(3-2*t)},fract=x=>x-Math.floor(x),wrap=(i,p)=>((i%p)+p)%p;
 function hash(x,y,s){let h=Math.imul(x+31,374761393)^Math.imul(y+17,668265263)^Math.imul(s+97,1274126177);h=Math.imul(h^(h>>>13),1274126177);return ((h^(h>>>16))>>>0)/4294967295}
 function noise(u,v,px,py,s){let x=u*px,y=v*py,i=Math.floor(x),j=Math.floor(y),a=fract(x),b=fract(y);a=a*a*(3-2*a);b=b*b*(3-2*b);let f=(x,y)=>hash(wrap(x,px),wrap(y,py),s);return lerp(lerp(f(i,j),f(i+1,j),a),lerp(f(i,j+1),f(i+1,j+1),a),b)}
 function fb(u,v,s){return .55*noise(u,v,3,3,s)+.28*noise(u,v,7,7,s+1)+.12*noise(u,v,15,15,s+2)+.05*noise(u,v,31,31,s+3)}
 const rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16)),mix=(a,b,t)=>a.map((x,i)=>lerp(x,b[i],t));
 function gradient(stops,t){let p=clamp(t)*(stops.length-1),i=Math.min(stops.length-2,Math.floor(p));return mix(rgb(stops[i]),rgb(stops[i+1]),p-i)}
 function voronoi(u,v,n,s){let x=u*n,y=v*n,i=Math.floor(x),j=Math.floor(y),d1=1e9,d2=1e9,bx=0,by=0,cell=0;for(let a=-1;a<=1;a++)for(let b=-1;b<=1;b++){let ix=i+a,iy=j+b,wx=wrap(ix,n),wy=wrap(iy,n),cx=ix+.18+.64*hash(wx,wy,s),cy=iy+.18+.64*hash(wx,wy,s+1),dx=x-cx,dy=y-cy,d=dx*dx+dy*dy;if(d<d1){d2=d1;d1=d;bx=dx;by=dy;cell=hash(wx,wy,s+2)}else if(d<d2)d2=d}return {d:Math.sqrt(d1),edge:Math.sqrt(d2)-Math.sqrt(d1),x:bx,y:by,cell}}
 const defs=[
 {id:"earth",name:"T_VIS4_Origin_Earth",target:"earth (tile 12); not automatic ground path override",palette:["79523A","A7794C","C69B64","DFC38B","EBD8AC"],hues:[[-7,-3,3],[0,0,0],[5,2,-3],[3,5,3]],fn(u,v){let a=fb(u,v,111),b=noise(u,v,11,9,114);let c=voronoi(u+.025*Math.sin(tau*v),v,12,115);let peb=1-ss(.09,.18,c.d);let shade=clamp(.25+.58*a+.08*b+peb*(.13-.06*ss(0,.1,c.y)));let fleck=ss(.82,.98,noise(u,v,47,47,118))*.055;return [shade+fleck,clamp(Math.floor(c.cell*4),0,3)]}},
 {id:"bark",name:"T_VIS4_Origin_Bark",target:"bark (tile 4), root (tile 5 as optional tinted derivative)",palette:["513525","795032","A57846","C79861","DBB482"],hues:[[-5,-2,0],[0,0,0],[5,1,-2],[3,4,2]],fn(u,v){let warp=.038*Math.sin(tau*v)+.016*Math.sin(tau*(v*3+u*2));let q=u+warp;let fine=.5+.5*Math.sin(tau*(q*11+.28*noise(u,v,7,3,221)));let broad=.5+.5*Math.sin(tau*(q*6+.13*Math.sin(tau*v*2)));let furrow=ss(.83,.98,fine);let rim=ss(.56,.78,fine)*(1-ss(.80,.91,fine));let s=.31+.28*fb(u,v,222)+.19*broad-.24*furrow+.12*rim;return [s,Math.floor(clamp(noise(u,v,6,4,229),0,.999)*4)]}},
 {id:"stone_warm",name:"T_VIS4_Origin_Stone_Warm",target:"stone_warm (tile 0)",palette:["8C7D69","B2A48B","CBBDA2","DED4B9","F4E8CE"],hues:[[-5,-2,4],[0,0,0],[4,2,-3],[1,4,2]],fn(u,v){let f=fb(u,v,330),m=voronoi(u+.045*Math.sin(tau*v*2),v+.035*Math.sin(tau*u),4,331);let softly=ss(.06,.34,m.edge);let light=.23+.61*f+.06*softly;let streak=.5+.5*Math.sin(tau*(u*2+v*3+.34*noise(u,v,5,5,332)));return [light+.06*streak,Math.floor(m.cell*4)]}},
 {id:"stone_cool",name:"T_VIS4_Origin_Stone_Cool",target:"stone_cool (tile 1)",palette:["647E76","859B89","A3B39D","C4CDB6","DDE1CD"],hues:[[-5,1,7],[0,0,0],[5,4,-2],[-2,5,1]],fn(u,v){let f=fb(u,v,430),m=voronoi(u+.04*Math.sin(tau*v),v+.03*Math.sin(tau*u*2),5,431);return [.22+.63*f+.08*ss(.04,.25,m.edge)+.025*noise(u,v,24,24,432),Math.floor(m.cell*4)]}},
 {id:"leaf_canopy",name:"T_VIS4_Origin_Leaf_Canopy",target:"leaf_canopy (tile 6)",palette:["32643B","4B873F","72AC45","A4CC57","D0E47D"],hues:[[-6,2,6],[0,0,0],[7,2,-4],[4,7,-1]],fn(u,v){let m=voronoi(u+.025*Math.sin(tau*v*2),v+.018*Math.sin(tau*u*3),6,551);let mass=1-ss(.20,.70,m.d);let pocket=ss(.045,.20,m.edge);let lobe=.5-.34*m.y-.12*m.x;let f=fb(u,v,554);return [.16+.31*mass+.13*pocket+.20*lobe+.16*f,Math.floor(m.cell*4)]}},
 {id:"fruit_red",name:"T_VIS4_Origin_Fruit_Red",target:"new fruit source only; requires dedicated atlas allocation/UV; never replace mushroom_cap (tile 8)",palette:["A43338","CE4C42","EA6C4D","F7975A","FFBD75"],hues:[[-3,-3,3],[0,0,0],[3,3,-2],[1,5,2]],fn(u,v){let f=fb(u,v,661),b=.5+.5*Math.cos(tau*(u+.06*Math.sin(tau*v)));let speck=ss(.84,.99,noise(u,v,31,31,665));return [.34+.43*f+.055*b+.035*speck,Math.floor(clamp(noise(u,v,4,4,666),0,.999)*4)]}},
 {id:"meadow",name:"T_VIS4_Origin_Meadow",target:"standalone T_ENV_Ground candidate; preserve path mask and UV repeat",palette:["4E813A","6C9F44","87B950","B1CD65","D5E48B"],hues:[[-4,1,5],[0,0,0],[5,3,-4],[4,5,2]],fn(u,v){let f=fb(u,v,771),m=voronoi(u+.03*Math.sin(tau*v),v,15,772);let grass=(1-ss(.13,.30,m.d))*.06;let fleck=ss(.80,.96,noise(u,v,47,47,774))*.04;let drift=.5+.5*Math.sin(tau*(u*2+v+.3*noise(u,v,4,4,775)));return [.21+.57*f+.08*drift+grass+fleck,Math.floor(m.cell*4)]}}
 ];
 const bayer=[0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5];
 return defs.map(d=>{let pal=[];for(let k=0;k<4;k++)for(let i=0;i<64;i++){let c=gradient(d.palette,i/63);pal.push(c.map((x,j)=>Math.round(clamp(x+d.hues[k][j],0,255))))}
 let pix=new Uint8Array(size*size);let lo=255,hi=0;for(let y=0;y<size;y++)for(let x=0;x<size;x++){let [t,h]=d.fn((x+.5)/size,(y+.5)/size);let level=clamp(Math.floor(clamp(t)*63+bayer[(y%4)*4+x%4]/16),0,63);let k=clamp(h,0,3)*64+level;pix[y*size+x]=k;lo=Math.min(lo,level);hi=Math.max(hi,level)}
 // Periodicity: analytical samples translated by a full repeat agree, up to floating precision.
 let err=0,hueMismatch=0;for(let i=0;i<64;i++){let u=(i+.37)/64,v=fract(i*.618+.23);let a=d.fn(u,v),b=d.fn(u+1,v),c=d.fn(u,v+1);err=Math.max(err,Math.abs(a[0]-b[0]),Math.abs(a[0]-c[0]));if(a[1]!==b[1]||a[1]!==c[1])hueMismatch++}
 if(err>1e-8||hueMismatch)throw Error("periodic "+d.id);
 const diff=(a,b)=>pal[a].reduce((s,v,i)=>s+Math.abs(v-pal[b][i]),0)/3;let edgeX=0,edgeY=0,interiorX=0,interiorY=0;for(let i=0;i<size;i++){edgeX+=diff(pix[i*size],pix[i*size+size-1]);edgeY+=diff(pix[i],pix[(size-1)*size+i]);interiorX+=diff(pix[i*size+Math.floor(size/2)],pix[i*size+Math.floor(size/2)-1]);interiorY+=diff(pix[Math.floor(size/2)*size+i],pix[(Math.floor(size/2)-1)*size+i])}
 return {id:d.id,name:d.name,target:d.target,w:size,h:size,palette:pal,pixels:pix,stats:{toneLevels:[lo,hi],uniqueColors:new Set(pix).size,periodicMaxError:err,edgeMeanAbsRGB:[edgeX/size,edgeY/size],middleMeanAbsRGB:[interiorX/size,interiorY/size]}}});
}

function encodePNG(tex){
 const u32=n=>[(n>>>24)&255,(n>>>16)&255,(n>>>8)&255,n&255];
 const table=Array.from({length:256},(_,i)=>{let c=i;for(let j=0;j<8;j++)c=(c&1)?(0xedb88320^(c>>>1)):(c>>>1);return c>>>0});
 const concat=arr=>{let out=new Uint8Array(arr.reduce((s,a)=>s+a.length,0));let k=0;for(let a of arr){out.set(a,k);k+=a.length}return out};
 const chunk=(type,data)=>{let t=Uint8Array.from(type.split("").map(c=>c.charCodeAt(0))),crc=0xffffffff;for(let a of [t,data])for(let b of a)crc=table[(crc^b)&255]^(crc>>>8);return concat([u32(data.length),t,data,u32((crc^0xffffffff)>>>0)])};
 const isIndexed=!!tex.palette,channels=isIndexed?1:3,stride=tex.w*channels;
 let raw=new Uint8Array((stride+1)*tex.h);for(let y=0;y<tex.h;y++)raw.set(tex.pixels.subarray(y*stride,(y+1)*stride),y*(stride+1)+1);
 let parts=[[0x78,0x01]],a=1,b=0;for(let x of raw){a=(a+x)%65521;b=(b+a)%65521}
 for(let p=0;p<raw.length;p+=65535){let len=Math.min(65535,raw.length-p),inv=65535-len;parts.push([p+len===raw.length?1:0,len&255,len>>8,inv&255,inv>>8],raw.subarray(p,p+len))}
 parts.push(u32(((b<<16)|a)>>>0));
 const chunks=[[137,80,78,71,13,10,26,10],chunk("IHDR",Uint8Array.from([...u32(tex.w),...u32(tex.h),8,isIndexed?3:2,0,0,0])),chunk("sRGB",Uint8Array.of(0))];
 if(isIndexed)chunks.push(chunk("PLTE",Uint8Array.from(tex.palette.flat())));
 chunks.push(chunk("IDAT",concat(parts)),chunk("IEND",new Uint8Array(0)));return concat(chunks);
}

function verifyPNG(bytes,t){
 const dv=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength);if(dv.getUint32(16)!==t.w||dv.getUint32(20)!==t.h||bytes[25]!==3)throw Error("PNG header");
 let pos=8,idat=null,pal=null;while(pos<bytes.length){let n=dv.getUint32(pos),tag=String.fromCharCode(...bytes.subarray(pos+4,pos+8));let v=bytes.subarray(pos+8,pos+8+n);if(tag==="IDAT")idat=v;if(tag==="PLTE")pal=v;pos+=12+n}
 if(!idat||!pal||pal.length!==768||pos!==bytes.length)throw Error("chunks");
 const want=t.palette.flat();if(want.some((x,i)=>x!==pal[i]))throw Error("palette");
 let k=2,raw=[],end=false;while(!end){end=!!(idat[k++]&1);let n=idat[k]+256*idat[k+1],inv=idat[k+2]+256*idat[k+3];k+=4;if((n^inv)!==65535)throw Error("deflate block");for(let i=0;i<n;i++)raw.push(idat[k++])}
 if(raw.length!==(t.w+1)*t.h)throw Error("scanlines");
 let a=1,b=0;for(let x of raw){a=(a+x)%65521;b=(b+a)%65521}if(new DataView(idat.buffer,idat.byteOffset,idat.byteLength).getUint32(k)!==(((b<<16)|a)>>>0))throw Error("adler");
 for(let y=0;y<t.h;y++){if(raw[y*(t.w+1)]!==0)throw Error("filter");for(let x=0;x<t.w;x++)if(raw[y*(t.w+1)+x+1]!==t.pixels[y*t.w+x])throw Error("pixel mismatch")}
}

function preview(texs){const tile=128,gap=8,w=4*(tile+gap)+gap,h=2*(tile+gap)+gap,pixels=new Uint8Array(w*h*3);pixels.fill(226);for(let k=0;k<texs.length;k++){let t=texs[k],ox=gap+k%4*(tile+gap),oy=gap+Math.floor(k/4)*(tile+gap);for(let y=0;y<tile;y++)for(let x=0;x<tile;x++){let c=t.palette[t.pixels[Math.floor(y*t.h/tile)*t.w+Math.floor(x*t.w/tile)]];pixels.set(c,((oy+y)*w+ox+x)*3)}}return {w,h,pixels}}
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
const dir=process.argv[2];if(!dir)throw Error("Usage: node build_origin_vis4_textures.mjs <new output dir>");mkdirSync(dir);mkdirSync(join(dir,"Textures"));const textures=makeTextures();for(const t of textures){const bytes=encodePNG(t);verifyPNG(bytes,t);writeFileSync(join(dir,"Textures",t.name+".png"),bytes);console.log(t.id,t.stats)}writeFileSync(join(dir,"preview.png"),encodePNG(preview(textures)));
