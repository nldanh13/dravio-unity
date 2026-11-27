function functionsArt(group){
 const groups=[["care","biology","dragons","eggs","world","quests","expedition","inventory"],["feed","pet","rest","inspect","build","research","health","settings"],["day","night","lock","done","warning","upgrade","reward"],["hunger","temperature","bond","close","back","next"]];
 const cl=(x,a=0,b=1)=>Math.max(a,Math.min(b,x)),rgb=h=>[0,2,4].map(i=>parseInt(h.slice(i,i+2),16));
 const pal={gold:["9B652B","F7D16D","FFF0AC"],green:["3D7544","87C863","DBF3A9"],blue:["326B94","78C2DB","D1F5F6"],red:["8C4540","E87A68","FFCF9E"],cream:["8D6946","E6C48C","FFF2D2"],purple:["645286","B393D9","EEE0F7"],wood:["63432F","B27A48","E7BD7F"],teal:["366E72","6CB8B1","D5F5DB"],ink:["493B32","8B7460","C8B5A0"]};
 function design(id){
 const shapes=[];
 function add(d,b,p,flat=false){const colors=Array.isArray(p)?p:pal[p];if(!colors)throw Error("palette");shapes.push({d,b,c:colors.map(rgb),flat});}
 function E(cx,cy,rx,ry,p,ang=0){let co=Math.cos(ang),si=Math.sin(ang),ex=Math.hypot(rx*co,ry*si),ey=Math.hypot(rx*si,ry*co);add((x,y)=>{let a=x-cx,b=y-cy;return (Math.hypot((a*co+b*si)/rx,(-a*si+b*co)/ry)-1)*Math.min(rx,ry)},[cx-ex,cy-ey,cx+ex,cy+ey],p)}
 function R(x,y,w,h,r,p){let cx=x+w/2,cy=y+h/2;add((x,y)=>{let a=Math.abs(x-cx)-(w/2-r),b=Math.abs(y-cy)-(h/2-r);return Math.hypot(Math.max(a,0),Math.max(b,0))+Math.min(Math.max(a,b),0)-r},[x,y,x+w,y+h],p)}
 function P(pts,p){let b=[Math.min(...pts.map(q=>q[0])),Math.min(...pts.map(q=>q[1])),Math.max(...pts.map(q=>q[0])),Math.max(...pts.map(q=>q[1]))];add((x,y)=>{let d=1e9,inside=false;for(let i=0,j=pts.length-1;i<pts.length;j=i++){let [ax,ay]=pts[j],[bx,by]=pts[i],vx=bx-ax,vy=by-ay,L=vx*vx+vy*vy;if(!L)continue;let t=cl(((x-ax)*vx+(y-ay)*vy)/L);d=Math.min(d,Math.hypot(x-ax-t*vx,y-ay-t*vy));if((ay>y)!=(by>y)&&x<(bx-ax)*(y-ay)/(by-ay)+ax)inside=!inside}return inside?-d:d},b,p)}
 function L(ax,ay,bx,by,r,p){let dx=bx-ax,dy=by-ay,len=dx*dx+dy*dy;if(!len)throw Error("zero segment");add((x,y)=>{let t=cl(((x-ax)*dx+(y-ay)*dy)/len);return Math.hypot(x-ax-t*dx,y-ay-t*dy)-r},[Math.min(ax,bx)-r,Math.min(ay,by)-r,Math.max(ax,bx)+r,Math.max(ay,by)+r],p)}
 function ring(cx,cy,r,w,p){add((x,y)=>Math.abs(Math.hypot(x-cx,y-cy)-r)-w/2,[cx-r-w/2,cy-r-w/2,cx+r+w/2,cy+r+w/2],p)}
 function star(cx,cy,r,p,n=4){let pts=[];for(let i=0;i<n*2;i++){let a=-Math.PI/2+i*Math.PI/n,s=i%2?r*.37:r;pts.push([cx+Math.cos(a)*s,cy+Math.sin(a)*s])}P(pts,p)}
 function heart(cx,cy,s,p){let pts=[];for(let i=0;i<64;i++){let t=2*Math.PI*i/64;pts.push([cx+s*16*Math.sin(t)**3/17,cy-s*(13*Math.cos(t)-5*Math.cos(2*t)-2*Math.cos(3*t)-Math.cos(4*t))/17])}P(pts,p)}
 function egg(cx,cy,rx,ry,p){add((x,y)=>{let dy=(y-cy)/ry;return (Math.hypot((x-cx)/(rx*(1+.17*cl(dy,-1,1))),dy)-1)*Math.min(rx,ry)},[cx-rx*1.17,cy-ry,cx+rx*1.17,cy+ry],p)}
 function check(cx,cy,s,p){L(cx-s*.7,cy,cx-s*.18,cy+s*.5,s*.16,p);L(cx-s*.18,cy+s*.5,cx+s*.77,cy-s*.55,s*.16,p)}
 function arrow(dir,p){let pts=[[-165,-31],[35,-31],[35,-101],[169,0],[35,101],[35,31],[-165,31]];P(pts.map(([x,y])=>[256+dir*x,256+y]),p)}
 const cream=["94774D","FFF2C8","FFFBE9"],white=["A7A08C","F2E8D1","FFFDF2"],dark=["50433B","7C6652","C3A07D"];
 switch(id){
 case "care":heart(256,237,162,"red");L(99,346,206,410,32,"cream");L(206,410,356,345,32,"cream");L(206,410,284,379,16,"cream");break;
 case "biology":for(let i=0;i<9;i++){let y=91+i*40,dx=98*Math.cos(i*Math.PI/4);L(256-dx,y,256+dx+(Math.abs(dx)<.01?1:0),y,9,"gold")}for(let i=0;i<28;i++){let y=82+i*12.4,z=y+12.4,x=100*Math.cos((y-91)*Math.PI/160),xx=100*Math.cos((z-91)*Math.PI/160);L(256+x,y,256+xx,z,22,"green");L(256-x,y,256-xx,z,22,"blue")}break;
 case "dragons":P([[113,212],[84,109],[190,170],[209,88],[267,169],[338,115],[372,229],[430,280],[380,361],[267,421],[142,374]],"green");E(246,265,144,121,"green");E(327,314,107,65,"cream");E(181,243,29,40,"ink");E(177,231,10,12,white);E(339,270,28,38,"ink");E(335,258,10,12,white);E(353,311,9,7,"ink");break;
 case "eggs":egg(328,266,104,156,"blue");egg(184,286,111,155,"cream");for(let [x,y,s] of [[153,241,15],[205,308,19],[154,355,10]])E(x,y,s,s*.7,"gold");break;
 case "world":E(256,256,170,170,"blue");P([[145,129],[221,98],[260,141],[235,191],[281,232],[246,274],[190,260],[157,202],[115,191]],"green");P([[296,269],[365,240],[414,290],[370,352],[323,392],[284,345]],"green");ring(256,256,177,14,"gold");break;
 case "quests":R(118,84,272,344,40,"wood");R(141,103,226,302,22,"cream");R(196,61,119,61,17,"gold");for(let y of [179,250,321]){L(211,y,324,y,10,"wood");check(173,y,27,"green")}break;
 case "expedition":E(256,256,181,181,"gold");E(256,256,153,153,"cream");for(let i=0;i<8;i++){let a=i*Math.PI/4;L(256+Math.cos(a)*122,256+Math.sin(a)*122,256+Math.cos(a)*138,256+Math.sin(a)*138,6,"wood")}P([[315,139],[287,286],[197,373],[225,226]],"red");P([[315,139],[287,286],[256,256]],"gold");P([[197,373],[225,226],[256,256]],"blue");E(256,256,16,16,"gold");break;
 case "inventory":R(149,107,214,75,29,"wood");R(98,159,316,261,52,"wood");R(116,167,280,154,47,"cream");R(142,294,228,100,30,"cream");R(230,272,52,57,9,"gold");L(152,193,152,278,11,"gold");L(360,193,360,278,11,"gold");break;
 case "feed":E(256,258,183,78,"wood");E(256,251,159,60,"cream");E(195,211,57,58,"red");E(302,201,57,59,"green");P([[75,257],[437,257],[402,364],[344,407],[168,407],[110,364]],"gold");E(256,272,161,38,"cream");break;
 case "pet":E(256,316,131,104,"cream");E(135,200,53,72,"cream",-.30);E(222,144,51,72,"cream",-.12);E(308,144,51,72,"cream",.12);E(389,201,53,72,"cream",.30);heart(256,311,50,"red");break;
 case "rest":R(75,240,362,164,55,"purple");R(106,259,300,117,45,"cream");add((x,y)=>Math.max(Math.hypot(x-273,y-151)-99,82-Math.hypot(x-320,y-111)),[174,52,372,250],"gold");star(385,127,30,"gold");break;
 case "inspect":L(318,315,411,410,36,"wood");E(213,206,147,147,"gold");E(213,206,115,115,"blue");L(164,140,136,189,13,white);break;
 case "build":L(190,415,289,205,30,"wood");P([[148,85],[328,69],[405,132],[385,207],[202,220],[152,178]],"ink");R(124,105,100,94,18,"gold");break;
 case "research":P([[78,124],[190,112],[256,151],[322,112],[434,124],[434,377],[322,367],[256,406],[190,367],[78,377]],"wood");P([[92,142],[190,130],[249,170],[249,382],[190,349],[92,359]],"cream");P([[263,170],[322,130],[420,142],[420,359],[322,349],[263,382]],"cream");for(let y of [204,249,294]){L(122,y,213,y+16,7,"wood");L(298,y+16,390,y,7,"wood")}star(371,157,38,"gold");break;
 case "health":heart(256,247,184,"red");R(222,151,68,194,14,white);R(159,214,194,68,14,white);break;
 case "settings":{let pts=[];for(let i=0;i<48;i++){let a=i*Math.PI/24,r=i%6===0||i%6===5?139:181;pts.push([256+r*Math.cos(a),256+r*Math.sin(a)])}P(pts,"ink");E(256,256,115,115,"gold");E(256,256,65,65,"wood");break}
 case "day":for(let i=0;i<8;i++){let a=i*Math.PI/4;L(256+Math.cos(a)*133,256+Math.sin(a)*133,256+Math.cos(a)*191,256+Math.sin(a)*191,15,"gold")}E(256,256,114,114,"gold");break;
 case "night":add((x,y)=>Math.max(Math.hypot(x-232,y-255)-167,153-Math.hypot(x-310,y-193)),[65,88,399,422],"purple");star(371,136,53,"gold");star(401,264,27,"gold");break;
 case "lock":ring(256,175,95,42,"gold");R(100,198,312,221,48,"gold");E(256,279,25,25,"wood");P([[244,282],[268,282],[282,348],[230,348]],"wood");break;
 case "done":E(256,256,180,180,"green");ring(256,256,164,12,"gold");check(256,253,113,white);break;
 case "warning":P([[256,72],[452,415],[60,415]],"gold");R(234,188,44,118,18,"wood");E(256,351,24,24,"wood");break;
 case "upgrade":P([[256,68],[423,239],[330,239],[330,410],[182,410],[182,239],[89,239]],"green");P([[256,114],[348,218],[291,218],[291,363],[221,363],[221,218],[164,218]],"cream");break;
 case "reward":R(98,215,316,201,24,"red");R(77,155,358,84,22,"red");R(230,157,52,259,8,"gold");E(193,117,74,40,"gold",.34);E(319,117,74,40,"gold",-.34);E(256,151,30,30,"gold");break;
 case "hunger":L(163,244,163,421,21,"wood");R(101,159,124,99,42,"cream");for(let x of [112,146,180,214])L(x,84,x,198,12,"cream");L(338,241,338,421,21,"wood");E(338,161,66,93,"cream");break;
 case "temperature":R(187,65,138,287,69,"cream");E(256,348,110,110,"cream");R(237,113,38,213,19,"red");E(256,348,67,67,"red");for(let y of [130,185,240])L(284,y,313,y,5,"wood");break;
 case "bond":heart(184,230,118,"red");heart(326,288,118,"purple");star(278,105,35,"gold");break;
 case "close":L(137,137,375,375,39,"cream");L(375,137,137,375,39,"cream");break;
 case "back":arrow(-1,"cream");break;
 case "next":arrow(1,"cream");break;
 default:throw Error(id);
 }
 return shapes}
 return groups[group].map(id=>{
 const shapes=design(id),bbox=[Math.min(...shapes.map(s=>s.b[0])),Math.min(...shapes.map(s=>s.b[1])),Math.max(...shapes.map(s=>s.b[2])),Math.max(...shapes.map(s=>s.b[3]))];
 function sample(x,y,scale){let p=[0,0,0,0];for(const s of shapes){let [l,t,r,b]=s.b;if(x<l-2/scale||x>r+2/scale||y<t-2/scale||y>b+2/scale)continue;let d=s.d(x,y);if(!Number.isFinite(d))throw Error(id+" nonfinite distance");let a=cl(.5-d*scale);if(!a)continue;let u=cl((x-l)/(r-l)),v=cl((y-t)/(b-t)),lit=cl(.7-.42*v-.22*u),inner=cl(.5-(d+5)*scale);let c=s.c[1].map((z,i)=>z+(s.c[2][i]-z)*lit);let edge=s.c[0];let c2=edge.map((z,i)=>z+(c[i]-z)*inner);let A=a+p[3]*(1-a);p=[...c2.map((z,i)=>(z*a+p[i]*p[3]*(1-a))/A),A]}return p}
 let b=[Infinity,Infinity,-Infinity,-Infinity];for(let y=Math.floor(bbox[1])-2;y<=bbox[3]+2;y+=4)for(let x=Math.floor(bbox[0])-2;x<=bbox[2]+2;x+=4)if(sample(x+2,y+2,1)[3]>.5){b[0]=Math.min(b[0],x);b[1]=Math.min(b[1],y);b[2]=Math.max(b[2],x+4);b[3]=Math.max(b[3],y+4)}
 let sc=409.6/Math.max(b[2]-b[0],b[3]-b[1]),cx=(b[0]+b[2])/2,cy=(b[1]+b[3])/2,w=512,h=512,pixels=new Uint8Array(w*h*4),bounds=[512,512,0,0],alpha=[0,0,0];
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){let p=sample(cx+(x+.5-256)/sc,cy+(y+.5-256)/sc,sc),i=(y*w+x)*4,a=Math.round(cl(p[3])*255);for(let k=0;k<3;k++)pixels[i+k]=Math.round(cl(a?p[k]:[152,125,88][k],0,255));pixels[i+3]=a;alpha[a===0?0:a===255?2:1]++;if(a>127){bounds[0]=Math.min(bounds[0],x);bounds[1]=Math.min(bounds[1],y);bounds[2]=Math.max(bounds[2],x);bounds[3]=Math.max(bounds[3],y)}}
 let coverage=Math.max(bounds[2]-bounds[0]+1,bounds[3]-bounds[1]+1)/512;if(coverage<.75||coverage>.85||!alpha[0]||alpha[2]<10000)throw Error(id+" coverage/alpha "+coverage+" "+alpha);
 let path="Icons/ui_icon_"+id+".png";return {id:"icon:"+id,w,h,pixels,path,runtime:"Assets/Dravio/UI/Sprites/"+path,coverage,bounds,alpha:{transparent:alpha[0],partial:alpha[1],opaque:alpha[2]}};
 })
}

function board(art){let w=art.length*112,h=182,pixels=new Uint8Array(w*h*4);for(let i=0;i<pixels.length;i+=4)pixels.set([238,231,215,255],i);function place(t,x,y,s){for(let j=0;j<s;j++)for(let i=0;i<s;i++){let q=(Math.floor(j*t.h/s)*t.w+Math.floor(i*t.w/s))*4,k=((y+j)*w+x+i)*4,a=t.pixels[q+3]/255;for(let c=0;c<3;c++)pixels[k+c]=Math.round(pixels[k+c]*(1-a)+t.pixels[q+c]*a)}}for(let k=0;k<art.length;k++){let x=k*112;place(art[k],x+4,2,104);place(art[k],x+10,111,48);place(art[k],x+66,116,32);place(art[k],x+44,157,24)}return {w,h,pixels}}

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
import {mkdirSync,writeFileSync} from "node:fs";import {join,dirname} from "node:path";const dir=process.argv[2],group=Number(process.argv[3]||0);if(!dir)throw Error("Usage: node build_vis4_ui_functions.mjs <new directory> <group 0-3>");mkdirSync(dir);const art=functionsArt(group);for(const a of art){const bytes=png(a);verify(bytes,a);let p=join(dir,a.path);mkdirSync(dirname(p),{recursive:true});writeFileSync(p,bytes);}writeFileSync(join(dir,"preview_functions_"+(group+1)+".png"),png(board(art)));
