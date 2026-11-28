function icons(){
 const clamp=(x,a=0,b=1)=>Math.max(a,Math.min(b,x)),rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16)),mix=(a,b,t)=>a.map((v,i)=>v+(b[i]-v)*clamp(t));
 function layer(p,c,a){a=clamp(a);let z=a+p[3]*(1-a);return z?[...c.map((v,i)=>(v*a+p[i]*p[3]*(1-a))/z),z]:[0,0,0,0]}
 function ell(x,y,cx,cy,rx,ry,ang=0){let dx=x-cx,dy=y-cy,u=dx*Math.cos(ang)+dy*Math.sin(ang),v=-dx*Math.sin(ang)+dy*Math.cos(ang);return (Math.hypot(u/rx,v/ry)-1)*Math.min(rx,ry)}
 function poly(x,y,pts){let d=1e9,inside=false;for(let i=0,j=pts.length-1;i<pts.length;j=i++){let [ax,ay]=pts[j],[bx,by]=pts[i],vx=bx-ax,vy=by-ay,t=clamp(((x-ax)*vx+(y-ay)*vy)/(vx*vx+vy*vy));d=Math.min(d,Math.hypot(x-ax-t*vx,y-ay-t*vy));if((ay>y)!=(by>y)&&x<(bx-ax)*(y-ay)/(by-ay)+ax)inside=!inside}return inside?-d:d}
 function shade(x,y,cx,cy,rx,ry,dark,light){let u=(x-cx)/rx,v=(y-cy)/ry,n=Math.sqrt(Math.max(0,1-u*u-v*v)),l=clamp(.50*n-.35*u-.48*v+.22);return mix(rgb(dark),rgb(light),l)}
 const water=[];for(let k=0;k<=36;k++){let t=k/36;water.push([256+161*Math.pow(Math.sin(Math.PI*t),.74)*(.67+.58*t),62+386*t])}for(let k=35;k>0;k--){let t=k/36;water.push([256-161*Math.pow(Math.sin(Math.PI*t),.74)*(.67+.58*t),62+386*t])}
 const defs=[["item","gold","Gold"],["item","egg","Egg"],["item","fruit","Fruit"],["icon","leaf","Leaf"],["icon","water","Water"],["icon","energy","Energy"]];
 return defs.map(([type,id,desc])=>{
 function sample(X,Y,scale){let p=[0,0,0,0],aa=d=>clamp(.5-d*scale);
 function form(d,c,edge){p=layer(p,rgb(edge),aa(d));p=layer(p,c,aa(d+6))}
 function oval(cx,cy,rx,ry,dark,light,edge,ang=0){form(ell(X,Y,cx,cy,rx,ry,ang),shade(X,Y,cx,cy,rx,ry,dark,light),edge)}
 function shine(cx,cy,rx,ry,ang=0,opacity=.65){p=layer(p,rgb("FFFCE4"),aa(ell(X,Y,cx,cy,rx,ry,ang))*opacity)}
 if(id==="gold"){
 oval(298,327,147,84,"A8671D","F7CE58","78411E");oval(298,300,147,77,"BB7B22","FFE58A","8E541F");
 oval(210,337,148,88,"AC6C21","F9D466","78411E");oval(210,309,148,80,"C48B2B","FFE99B","8D5421");
 oval(256,215,150,151,"D19028","FFF1A3","8F511D");
 oval(256,215,127,128,"AA691C","FFD56A","B57525");
 oval(256,215,110,111,"D99B30","FFE898","C4882C");
 let d=poly(X,Y,[[256,141],[309,215],[256,288],[203,215]]);form(d,shade(X,Y,248,198,70,80,"F1BC45","FFF7C2"),"A66D23");shine(199,139,47,12,-.6,.57);
 }else if(id==="egg"){
 let dy=(Y-263)/194,rx=150*(1+.19*dy),d=(Math.hypot((X-256)/rx,dy)-1)*135;
 form(d,shade(X,Y,256,263,165,194,"C8A36D","FFF5D1"),"8D6342");
 for(const [cx,cy,rx,ry] of [[194,181,20,13],[315,230,30,19],[222,335,25,17],[305,364,14,10],[154,272,13,10]])p=layer(p,rgb("C99D6A"),aa(ell(X,Y,cx,cy,rx,ry,-.3))*aa(d+10)*.72);
 shine(211,156,41,17,-.79,.52);
 }else if(id==="fruit"){
 let d=poly(X,Y,[[241,164],[255,72],[279,69],[270,175]]);form(d,shade(X,Y,252,110,24,58,"6B4124","BF8951"),"69432B");
 oval(314,115,74,31,"3C8647","A5D96C","38673B",-.42);
 d=Math.min(ell(X,Y,193,245,137,141,-.23),ell(X,Y,319,245,137,141,.23),ell(X,Y,256,309,166,140));
 form(d,shade(X,Y,256,280,190,181,"B53E37","FF9864"),"873831");
 p=layer(p,rgb("F6A052"),aa(d+12)*.16*Math.exp(-Math.pow((X-186)/79,2)-Math.pow((Y-208)/86,2)));
 shine(173,209,42,20,-.65,.60);shine(155,245,13,11,0,.42);
 }else if(id==="leaf"){
 let dx=X-256,dy=Y-256,u=(dx-dy)*.707,v=(dx+dy)*.707;
 let d=(Math.abs(u)/224+Math.pow(Math.abs(v)/126,1.75)-1)*100;
 form(d,shade(X,Y,256,256,185,185,"357444","B9E875"),"365D37");
 let vein=poly(X,Y,[[108,403],[126,368],[384,109],[394,119],[144,389]]);p=layer(p,rgb("D2EFA0"),aa(vein)*aa(d+8));
 for(const pts of [[[190,324],[158,249],[167,244],[202,315]],[[245,268],[217,176],[225,173],[257,260]],[[281,232],[356,251],[357,260],[271,245]]])p=layer(p,rgb("9CD16B"),aa(poly(X,Y,pts))*aa(d+9));
 shine(194,211,25,58,.45,.28);
 }else if(id==="water"){
 let d=poly(X,Y,water);form(d,shade(X,Y,256,281,158,195,"287BB0","B2F0F8"),"276A9D");
 p=layer(p,rgb("8FE6F5"),aa(d+13)*.3*Math.exp(-Math.pow((Y-386)/34,2)));shine(202,245,30,72,.29,.73);shine(178,302,13,18,0,.50);
 }else{
 let d=poly(X,Y,[[280,55],[105,276],[224,276],[176,457],[413,205],[286,205],[337,55]]);
 form(d,shade(X,Y,250,235,190,205,"E7A32F","FFF6A8"),"A36526");
 let inner=poly(X,Y,[[280,85],[134,261],[247,261],[212,390],[381,220],[265,220],[311,85]]);
 p=layer(p,shade(X,Y,239,195,160,180,"F0C048","FFFBCD"),aa(inner)*.70);shine(240,179,16,43,.65,.56);
 }
 return p}
 // Measure analytic silhouette, then render fresh at centered 80% coverage.
 let b=[512,512,0,0];for(let y=0;y<512;y+=4)for(let x=0;x<512;x+=4)if(sample(x+2,y+2,1)[3]>.5){b[0]=Math.min(b[0],x);b[1]=Math.min(b[1],y);b[2]=Math.max(b[2],x+4);b[3]=Math.max(b[3],y+4)}
 let sc=409.6/Math.max(b[2]-b[0],b[3]-b[1]),cx=(b[0]+b[2])/2,cy=(b[1]+b[3])/2;
 let w=512,h=512,pixels=new Uint8Array(w*h*4),bounds=[512,512,0,0],alpha=[0,0,0];
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){let p=sample(cx+(x+.5-256)/sc,cy+(y+.5-256)/sc,sc),i=(y*w+x)*4,a=Math.round(clamp(p[3])*255);for(let k=0;k<3;k++)pixels[i+k]=Math.round(clamp(a?p[k]:[158,127,79][k],0,255));pixels[i+3]=a;alpha[a===0?0:a===255?2:1]++;if(a>127){bounds[0]=Math.min(bounds[0],x);bounds[1]=Math.min(bounds[1],y);bounds[2]=Math.max(bounds[2],x);bounds[3]=Math.max(bounds[3],y)}}
 let coverage=Math.max(bounds[2]-bounds[0]+1,bounds[3]-bounds[1]+1)/512;if(coverage<.75||coverage>.85||!alpha[0]||!alpha[2])throw Error("coverage "+id+" "+coverage);
 let folder=type==="item"?"Items":"Icons",path=folder+"/ui_"+type+"_"+id+".png";return {id:type+":"+id,role:desc,w,h,pixels,path,runtime:"Assets/Dravio/UI/Sprites/"+path,coverage,bounds,alpha:{transparent:alpha[0],partial:alpha[1],opaque:alpha[2]}};
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
import {mkdirSync,writeFileSync} from "node:fs";import {join,dirname} from "node:path";const dir=process.argv[2];if(!dir)throw Error("Usage: node build_vis4_ui_icons.mjs <new directory>");mkdirSync(dir);for(const a of icons()){const bytes=png(a);verify(bytes,a);let p=join(dir,a.path);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,bytes);}
