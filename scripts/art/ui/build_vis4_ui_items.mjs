function items(group="materials"){
 const cl=(x,a=0,b=1)=>Math.max(a,Math.min(b,x)),rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16)),mix=(a,b,t)=>a.map((v,i)=>v+(b[i]-v)*cl(t));
 function layer(p,c,a){a=cl(a);let z=a+p[3]*(1-a);return z?[...c.map((v,i)=>(v*a+p[i]*p[3]*(1-a))/z),z]:[0,0,0,0]}
 function ell(x,y,cx,cy,rx,ry,ang=0){let dx=x-cx,dy=y-cy,u=dx*Math.cos(ang)+dy*Math.sin(ang),v=-dx*Math.sin(ang)+dy*Math.cos(ang);return (Math.hypot(u/rx,v/ry)-1)*Math.min(rx,ry)}
 function poly(x,y,pts){let d=1e9,inset=false;for(let i=0,j=pts.length-1;i<pts.length;j=i++){let [ax,ay]=pts[j],[bx,by]=pts[i],dx=bx-ax,dy=by-ay,t=cl(((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy));d=Math.min(d,Math.hypot(x-ax-t*dx,y-ay-t*dy));if((ay>y)!=(by>y)&&x<(bx-ax)*(y-ay)/(by-ay)+ax)inset=!inset}return inset?-d:d}
 function seg(x,y,ax,ay,bx,by){let dx=bx-ax,dy=by-ay,t=cl(((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy));return [Math.hypot(x-ax-t*dx,y-ay-t*dy),t]}
 function shade(x,y,cx,cy,rx,ry,dark,light){let u=(x-cx)/rx,v=(y-cy)/ry,n=Math.sqrt(Math.max(0,1-u*u-v*v)),l=cl(.46*n-.30*u-.44*v+.24);return mix(rgb(dark),rgb(light),l)}
 const ids=group==="materials"?["wood","seed","stone","crystal","coral","stardust","aquatic"]:["aether","pack_starter","pack_elemental","pack_mystic","pack_astral","blueprint"];
 return ids.map(id=>{
 function sample(X,Y,sc){let p=[0,0,0,0],aa=d=>cl(.5-d*sc);
 function fill(d,c){p=layer(p,c,aa(d))}
 function form(d,c,edge="70442C"){fill(d,rgb(edge));fill(d+5,c)}
 function oval(cx,cy,rx,ry,dark,light,edge,ang=0){form(ell(X,Y,cx,cy,rx,ry,ang),shade(X,Y,cx,cy,rx,ry,dark,light),edge)}
 function shape(pts,dark,light,edge,cx=256,cy=256,rx=190,ry=180){form(poly(X,Y,pts),shade(X,Y,cx,cy,rx,ry,dark,light),edge)}
 function stroke(ax,ay,bx,by,width,dark,light,edge){let [d,t]=seg(X,Y,ax,ay,bx,by);form(d-width,mix(rgb(dark),rgb(light),cl(.85-d/width*.62-t*.18)),edge)}
 function star(cx,cy,r,col,edge){let pts=[];for(let i=0;i<8;i++){let a=i*Math.PI/4-Math.PI/2,s=i%2?r*.27:r;pts.push([cx+s*Math.cos(a),cy+s*Math.sin(a)])}shape(pts,edge,col,edge,cx,cy,r,r)}
 if(id==="wood"){
 // two logs with cut faces and growth rings, stacked offset.
 for(const [cx,cy] of [[163,300],[231,224]]){
 stroke(cx,cy,cx+151,cy-83,65,"80502E","C28B50","6B4229");
 for(let k=0;k<3;k++)stroke(cx+19,cy-38+k*28,cx+141,cy-105+k*28,3,"745032","A77442","745032");
 oval(cx,cy,64,77,"C38B50","FFE0A1","815233",-.36);
 for(let rr of [19,38,53]){let d=ell(X,Y,cx,cy,rr,rr*1.16,-.36);p=layer(p,rgb("AA753F"),aa(Math.abs(d)-2.5)*.83)}
 }
 }else if(id==="seed"){
 oval(194,306,95,137,"8E5C32","D6AB6D","694529",.60);oval(311,265,92,139,"99642F","E8BF78","6F4729",-.59);
 for(let [cx,cy,a] of [[194,306,.60],[311,265,-.59]]){let d=ell(X,Y,cx-21,cy-32,32,72,a);p=layer(p,rgb("F6D49B"),aa(d)*.56)}
 stroke(298,165,282,110,12,"428849","8ED05E","3C6636");oval(326,109,54,26,"377A3F","A9DC6C","3C6636",-.42);
 }else if(id==="stone"){
 shape([[85,297],[141,184],[274,167],[376,219],[426,331],[362,394],[176,405]],"647F75","CAD1B6","536B62");
 shape([[141,184],[274,167],[376,219],[287,269],[170,263]],"A1B6A1","E1E1C5","9AA994",242,215,155,86);
 shape([[170,263],[287,269],[362,394],[176,405]],"7D9888","B9C7AC","829B8A",270,320,130,110);
 }else if(id==="crystal"){
 for(const [cx,cy,scale,angle] of [[166,307,.67,-.31],[338,300,.70,.32],[251,251,1,0]]){
 let base=[[-64,95],[-75,-48],[0,-179],[73,-48],[63,95],[0,144]],transform=(q)=>[cx+scale*(q[0]*Math.cos(angle)-q[1]*Math.sin(angle)),cy+scale*(q[0]*Math.sin(angle)+q[1]*Math.cos(angle))],pts=base.map(transform);
 shape(pts,"3B87AC","ABF2F0","386F91",cx,cy,100*scale,180*scale);
 shape([[-75,-48],[0,-179],[0,144],[-64,95]].map(transform),"76BDDA","D1FFF6","69ADC9",cx-25,cy,80*scale,150*scale);
 shape([[0,-179],[73,-48],[0,-5]].map(transform),"9DEAE9","E6FFF9","91D4E0",cx,cy-90*scale,70*scale,100*scale);
 }
 }else if(id==="coral"){
 for(const [a,b,w] of [[[255,386],[254,191],38],[[254,287],[150,230],27],[[150,230],[122,148],23],[[254,251],[345,175],26],[[345,175],[373,111],21],[[254,194],[248,105],27],[[335,187],[401,217],20],[[164,238],[103,264],19]])stroke(...a,...b,w,"CA5860","FFAA8E","9A4C53");
 oval(251,394,117,34,"D88D72","F9C3A1","9B6454");
 }else if(id==="stardust"){
 oval(256,345,117,83,"755AAB","D7D5F5","57447F");
 oval(256,270,129,123,"7465B7","D1E0FA","63538D");
 shape([[211,156],[211,102],[300,102],[300,156]],"A2C7D8","DDF9F3","6A92B0",250,130,75,70);
 oval(256,104,65,34,"A5733E","E4B978","7F542E");
 star(241,264,58,"FFF0A4","B99850");star(303,318,35,"E0FEFF","89BFCF");star(142,123,46,"EDE0FF","A196CE");star(372,161,30,"FFF3BD","D9B96A");
 }else if(id==="aquatic"){
 for(let k=0;k<3;k++){let cx=155+k*100,cy=268+Math.abs(k-1)*25,ang=(k-1)*.50;oval(cx,cy,53,151,"297F75","A0DB8E","356C59",ang)}
 for(const [cx,cy,r] of [[170,136,15],[344,103,21],[375,179,11]])oval(cx,cy,r,r,"58B7CF","D7FAF6","448FA6");
 stroke(255,395,255,202,7,"478E65","AAD79A","478E65");
 }else if(id==="aether"){
 oval(257,259,145,162,"668EC9","CFF5F4","577EA6");oval(257,259,114,131,"8D8BDB","E1E9FA","8791C8");
 // bright airy spiral and star, not a gameplay assertion about physical form.
 for(let k=0;k<30;k++){let t=k/29,a=t*4.4,r=95*(1-t*.78),b=(k+1)/30*4.4,r2=95*(1-(k+1)/30*.78);stroke(256+r*Math.cos(a),259+r*Math.sin(a),256+r2*Math.cos(b),259+r2*Math.sin(b),8,"B7E9EF","F6FFEA","B7E9EF")}
 star(256,249,51,"FFF5C6","C4B471");star(376,116,35,"E7FFF9","A6C9DA");
 }else if(id==="blueprint"){
 shape([[114,128],[351,95],[402,142],[385,393],[144,419],[98,364]],"4D91BF","9AD8E8","3B7197");
 for(let k=0;k<5;k++){let yy=190+k*40;stroke(148,yy,349,yy-14,2,"A1D8E7","DFF5F6","A1D8E7")}
 for(const [a,b] of [[[179,315],[179,231]],[[179,231],[250,170]],[[250,170],[326,224]],[[326,224],[326,306]],[[326,306],[179,315]],[[237,311],[237,266]],[[237,266],[271,264]],[[271,264],[271,309]]])stroke(...a,...b,4,"DBF4F6","FFFDF0","DBF4F6");
 oval(117,259,23,123,"397EAF","A1D6EC","386E97",.10);oval(383,250,21,123,"397EAF","A1D6EC","386E97",.10);
 }else{
 const pals={pack_starter:["84543A","CC9A61","F4D99A"],pack_elemental:["477E73","79BBA2","C6F0CB"],pack_mystic:["654C91","AF89D2","E7C8F6"],pack_astral:["395B96","759CD8","D1E6FA"]},[dark,mid,light]=pals[id];
 shape([[103,201],[255,122],[406,198],[406,363],[256,433],[103,363]],dark,mid,"664C39");
 shape([[103,201],[255,122],[406,198],[256,271]],mid,light,"826548",256,175,165,95);
 shape([[103,201],[256,271],[256,433],[103,363]],dark,mid,"664C39",180,300,100,150);
 shape([[256,271],[406,198],[406,363],[256,433]],dark,light,"826548",327,293,100,145);
 shape([[239,132],[270,132],[417,199],[389,214],[247,151]],"CA9140","FFE29A","A97D42",256,170,160,90);
 stroke(256,273,256,421,12,"C38A3F","FFE49C","A7793D");
 oval(256,272,41,46,"BA8439","FFE39D","98652F");
 if(id==="pack_starter")oval(256,269,18,23,"BD915A","FFF2C7","AD824A");else if(id==="pack_elemental")star(256,270,26,"D6FFDB","76AD79");else if(id==="pack_mystic")shape([[256,244],[275,269],[256,296],[237,269]],"8B5BA9","F5CAFD","87609D",256,265,25,31);else star(256,269,29,"E5F7FF","7F9FC9");
 }
 return p}
 let b=[512,512,0,0];for(let y=0;y<512;y+=4)for(let x=0;x<512;x+=4)if(sample(x+2,y+2,1)[3]>.5){b[0]=Math.min(b[0],x);b[1]=Math.min(b[1],y);b[2]=Math.max(b[2],x+4);b[3]=Math.max(b[3],y+4)}
 let sc=(id==="aether"?390:409.6)/Math.max(b[2]-b[0],b[3]-b[1]),cx=(b[0]+b[2])/2,cy=(b[1]+b[3])/2;
 let w=512,h=512,pixels=new Uint8Array(w*h*4),bounds=[512,512,0,0],alpha=[0,0,0];
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){let p=sample(cx+(x+.5-256)/sc,cy+(y+.5-256)/sc,sc),i=(y*w+x)*4,a=Math.round(cl(p[3])*255);for(let k=0;k<3;k++)pixels[i+k]=Math.round(cl(a?p[k]:[153,130,94][k],0,255));pixels[i+3]=a;alpha[a===0?0:a===255?2:1]++;if(a>127){bounds[0]=Math.min(bounds[0],x);bounds[1]=Math.min(bounds[1],y);bounds[2]=Math.max(bounds[2],x);bounds[3]=Math.max(bounds[3],y)}}
 let coverage=Math.max(bounds[2]-bounds[0]+1,bounds[3]-bounds[1]+1)/512;if(coverage<.75||coverage>.85||!alpha[0]||!alpha[2])throw Error("coverage "+id+" "+coverage);
 let path="Items/ui_item_"+id+".png";return {id:"item:"+id,w,h,pixels,path,runtime:"Assets/Dravio/UI/Sprites/"+path,coverage,bounds,alpha:{transparent:alpha[0],partial:alpha[1],opaque:alpha[2]}};
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
import {mkdirSync,writeFileSync} from "node:fs";import {join,dirname} from "node:path";const dir=process.argv[2];if(!dir)throw Error("Usage: node build_vis4_ui_items.mjs <new directory> [materials|packs]");mkdirSync(dir);for(const a of items(process.argv[3]||"materials")){const bytes=png(a);verify(bytes,a);const p=join(dir,a.path);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,bytes);}
