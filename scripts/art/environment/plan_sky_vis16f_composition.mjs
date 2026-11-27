
const scenarios=[["design",0,0,0,0,null],["notch_left",.12,0,0,0,null],["notch_both_home_bar",.15,.15,0,.055,null],["wide_ui_panel",0,0,0,0,.62],["ui_reported_narrow",0,0,0,0,.42],["tablet_like_safe_top",0,0,.08,.08,null]];
function frame(c,aspect,s){
 const [name,sl,sr,st,sb,ui]=s,dl=c.leftMarginScreenHeights,dr=c.rightPanelScreenHeights,dt=c.topMarginScreenHeights,db=c.bottomMarginScreenHeights;
 const l=Math.max(dl,sl),r=ui===null?dr+sr:Math.max(ui,sr),t=Math.max(dt,st),b=Math.max(db,sb);
 const size=c.orthographicSize*Math.max(1,(16/9-dl-dr)/Math.max(.05,aspect-l-r),(1-dt-db)/Math.max(.05,1-t-b));
 const theta=c.eulerAngles[0]*Math.PI/180;
 if(c.eulerAngles[1]!==0||c.eulerAngles[2]!==0)throw Error("camera yaw/roll unsupported");
 const up=[0,Math.cos(theta),Math.sin(theta)],a=c.anchorPosition,su=size*((b-db)-(t-dt));
 const pos=[a[0]+size*(r-l),a[1]+up[1]*su,a[2]+up[2]*su];
 return {name,aspect,size,pos,up,free:[l/aspect,b,1-r/aspect,1-t]};
}
function project(p,f){return [.5+(p[0]-f.pos[0])/(2*f.size*f.aspect),.5+((p[1]-f.pos[1])*f.up[1]+(p[2]-f.pos[2])*f.up[2])/(2*f.size)];}
function rectangle(bounds,x,z,scale){
 return [bounds.min[0]*scale+x,bounds.min[1]*scale,bounds.min[2]*scale+z,bounds.max[0]*scale+x,bounds.max[1]*scale,bounds.max[2]*scale+z];
}
function screenBox(b,f){
 const p=[];for(const x of [b[0],b[3]])for(const y of [b[1],b[4]])for(const z of [b[2],b[5]])p.push(project([x,y,z],f));
 return [Math.min(...p.map(v=>v[0])),Math.min(...p.map(v=>v[1])),Math.max(...p.map(v=>v[0])),Math.max(...p.map(v=>v[1]))];
}
function overlaps(a,b,pad=0){return a[0]<b[2]+pad&&a[2]>b[0]-pad&&a[1]<b[3]+pad&&a[3]>b[1]-pad;}
function footprint(b){return [b[0],b[2],b[3],b[5]];}
function polyBox(p){const x=p.filter((_,i)=>i%2===0),z=p.filter((_,i)=>i%2===1);return [Math.min(...x),Math.min(...z),Math.max(...x),Math.max(...z)];}


function solve(layout,loc,living,manifest,accents,lods){
 if(loc.zoneId!=="sky"||living.zoneId!=="sky")throw Error("Sky only");
 const fs=[16/9,19.5/9,20/9].flatMap(a=>scenarios.map(s=>frame(layout.camera,a,s)));
 const anchors=[...loc.spawnPoints,...loc.perchPoints,...loc.transitions,...loc.zoneGates,...loc.areaLinks,...living.interactionPoints];
 const points=[...anchors,...living.interactionPoints.map(p=>({x:p.targetX,y:p.targetY,z:p.targetZ,radius:.3}))].filter(p=>[p.x,p.y,p.z].every(Number.isFinite));
 const protects=points.map(p=>{const r=Math.max(.65,p.radius??0);return [p.x-r,p.y-.25,p.z-r,p.x+r,p.y+1.1,p.z+r]});
 const screen=fs.map(f=>protects.map(b=>screenBox(b,f)));
 const volume=loc.volumes.map(v=>({fp:polyBox(v.polygonXZ),min:v.yMin,max:v.yMax}));
 const walks=loc.walkAreas.map(v=>polyBox(v.polygonXZ)),obstacles=loc.obstacles.map(o=>polyBox(o.polygonXZ));
 const blockers=layout.placements.filter(p=>!p.asset.includes("Cloud_Sea")&&!p.asset.includes("Cover")&&!p.asset.includes("Island_Main")&&!p.asset.includes("Islet_East")&&!p.asset.includes("Islet_Shrine")&&manifest.assets[p.asset]).map(p=>{
 const [w,d,h]=manifest.assets[p.asset].size_m.map(v=>v*p.scale),a=(p.yaw??0)*Math.PI/180,rx=(Math.abs(Math.cos(a))*w+Math.abs(Math.sin(a))*d)/2,rz=(Math.abs(Math.sin(a))*w+Math.abs(Math.cos(a))*d)/2;
 const island=p.asset.includes("Islet_Float"),y=island?p.y-h:p.y;
 return [p.x-rx,y,p.z-rz,p.x+rx,y+h,p.z+rz];
 });
 const bScreen=fs.map(f=>blockers.map(b=>screenBox(b,f)));
 const islet=accents.assets.find(a=>a.name.includes("Floating")),crystal=accents.assets.find(a=>a.name.includes("Crystal"));
 const results=[];
 // Main, east, shrine ground support not proven by their walk polygons; do not
 // assume any other surface exists. Restrict screening to known walk areas then
 // reject buffered walk areas, so grounded candidates are held without terrain.
 for(const a of [crystal,...lods.assets])results.push({asset:a.name,status:"hold-ground-support",reason:"No verified decoration surface outside buffered walk polygons; source mesh raycast and render required."});
 // A distant free-floating decorative islet need not touch ground. Conservative
 // volume and walk exclusion in XZ, plus camera and protected-anchor tests.
 const candidates=[];let examined=0,clearancePassed=0,framingPassed=0;
 for(let x=-14;x<=14;x+=.5)for(let z=-10;z<=16;z+=.5)for(const y of [-2,-1,0,1,2,3]){
 examined++;const b=rectangle(islet.bounds,x,z,1);b[1]+=y;b[4]+=y;const fp=footprint(b);
 if(volume.some(v=>overlaps(fp,v.fp,.75))||walks.some(v=>overlaps(fp,v,.75))||obstacles.some(v=>overlaps(fp,v,.25)))continue;clearancePassed++;
 let margin=Infinity,px=Infinity,risk=0,ok=true;
 for(let j=0;j<fs.length;j++){const f=fs[j],s=screenBox(b,f),v=f.free,m=Math.min((s[0]-v[0])*f.aspect,s[1]-v[1],(v[2]-s[2])*f.aspect,v[3]-s[3]);
 if(m<.008||screen[j].some(q=>overlaps(s,q,.004))){ok=false;break;}margin=Math.min(margin,m);px=Math.min(px,(s[3]-s[1])*720);risk+=bScreen[j].filter(q=>overlaps(s,q)).length;}
 if(ok){framingPassed++;candidates.push({x,y,z,minMarginScreenHeights:margin,minProjectedHeightPxAt720:px,approximatePropScreenOverlaps:risk});}
 }
 candidates.sort((a,b)=>a.approximatePropScreenOverlaps-b.approximatePropScreenOverlaps||b.minMarginScreenHeights-a.minMarginScreenHeights);
 const best=candidates[0]??null;results.push({asset:islet.name,status:best&&best.approximatePropScreenOverlaps===0?"review-only":"hold",examined,clearancePassed,framingPassed,best});
 return {cases:fs.length,anchorCount:anchors.length,protectedPoints:points.length,walkAreas:walks.length,flyVolumes:volume.length,obstacleBoxes:obstacles.length,results,airRibbon:"hold: transparent material and actual flight/occlusion review; existing interactions unchanged",limitations:"Conservative XZ clearance; camera and screen AABBs, not depth-aware occlusion. Prop sizes approximate; float island pivot assumed top. Clouds, main island, cover, actual terrain/support, full motion, Focus and Unity rendering not verified. No scene edits."};
}

import {readFileSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const root=process.argv[2]??".",out=process.argv[3];if(!out)throw Error("Usage: node plan_sky_vis16f_composition.mjs <repo> <new-report>");const read=p=>JSON.parse(readFileSync(join(root,p),"utf8"));writeFileSync(out,JSON.stringify(solve(...["Assets/Dravio/Environment/Zones/Sky/Data/SkyLayout.json","Assets/Dravio/Environment/Zones/Sky/Data/SkyLocations.json","Assets/Dravio/Environment/Zones/Sky/Data/SkyLiving.json","Assets/Dravio/Environment/Zones/Sky/Data/SkyManifest.json"].map(read),read("ArtSource/Sky_VIS16_Accents/manifest.json"),read("ArtSource/Sky_VIS16f_LOD/manifest.json")),null,2)+"\n",{flag:"wx"});
