
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
function solve(layout,locations,living,flora,water,manifest){
 if(layout.version!==2||locations.version!==2||locations.zoneId!=="origin"||living.zoneId!=="origin")throw Error("baseline mismatch");
 const fs=[16/9,19.5/9,20/9].flatMap(a=>scenarios.map(s=>frame(layout.camera,a,s)));
 const rect=locations.flatRectXZ,clear=locations.walkClearance,obs=locations.obstacles.map(o=>polyBox(o.polygonXZ));
 const points=[...locations.spawnPoints,...locations.perchPoints,...locations.transitions,...living.interactionPoints];
 // Conservative visual protection: 0.65m half width, 1.1m height above anchors;
 // transition/interaction radius may be larger. Not a complete navigation or animation envelope.
 const protectedBoxes=points.map(p=>{const r=Math.max(.65,p.radius??0);return [p.x-r,p.y,p.z-r,p.x+r,p.y+1.1,p.z+r]});
 const protection=fs.map(f=>protectedBoxes.map(b=>screenBox(b,f)));
 // Approximate existing asset boxes for visibility risk, using Blender dimensions X,Z,Y
 // and a yaw-expanded footprint. Pivot assumed centered XZ and grounded; exclude large ground/cover.
 const blockers=layout.placements.filter(p=>!p.asset.includes("Ground")&&!p.asset.includes("Cover")&&manifest.assets[p.asset]).map(p=>{
  const [w,d,h]=manifest.assets[p.asset].size_m.map(v=>v*p.scale),a=(p.yaw??0)*Math.PI/180,rx=(Math.abs(Math.cos(a))*w+Math.abs(Math.sin(a))*d)/2,rz=(Math.abs(Math.sin(a))*w+Math.abs(Math.cos(a))*d)/2;
  return {asset:p.asset,b:[p.x-rx,p.y,p.z-rz,p.x+rx,p.y+h,p.z+rz]};
 });
 const blockerScreens=fs.map(f=>blockers.map(o=>screenBox(o.b,f)));
 function inspect(b){
  const fp=footprint(b);if(overlaps(fp,rect,clear))return null;
  if(obs.some(o=>overlaps(fp,o,.15)))return null;
  let minMargin=Infinity,minPixels=Infinity,risk=0;
  for(let i=0;i<fs.length;i++){
   const f=fs[i],s=screenBox(b,f),v=f.free;
   const margin=Math.min((s[0]-v[0])*f.aspect,s[1]-v[1],(v[2]-s[2])*f.aspect,v[3]-s[3]);
   if(margin<.008||protection[i].some(q=>overlaps(s,q,.004)))return null;
   minMargin=Math.min(minMargin,margin);minPixels=Math.min(minPixels,(s[3]-s[1])*720);
   for(let j=0;j<blockers.length;j++)if(overlaps(s,blockerScreens[i][j]))risk++;
  }
  return {minFreeMarginHeights:minMargin,minProjectedHeightPxAt720:minPixels,approximateScreenBoxOverlaps:risk};
 }
 const chosen=[];
 const assets=flora.assets;
 for(const a of assets){
  const candidates=[];
  for(let x=-12;x<=12;x+=.25)for(let z=-10;z<=13;z+=.25){
   const b=rectangle(a.bounds,x,z,1),f=footprint(b);
   if(chosen.some(c=>overlaps(f,c.footprint,.65)))continue;
   const q=inspect(b);if(!q)continue;
   candidates.push({asset:a.name,x,y:0,z,yaw:0,scale:1,triangles:a.triangles,footprint:f,...q});
  }
  candidates.sort((a,b)=>a.approximateScreenBoxOverlaps-b.approximateScreenBoxOverlaps||b.minFreeMarginHeights-a.minFreeMarginHeights||a.x-b.x||a.z-b.z);
  if(candidates.length)chosen.push({...candidates[0],validGridPositions:candidates.length});
 }
 // Evaluate previous QA's large architecture locations only using actual bounds where available;
 // full water assembly has no proven terrain fit, keep unplaced rather than invent stream connections.
 return {cases:fs.length,protectedAnchors:points.length,existingObstacleBoxes:obs.length,approximateExistingBlockers:blockers.length,chosen,unplaced:["Cottage","Lookout","Soft Hills","Waterfall/Stream assembly"],baseSceneTriangles:34472,addedTriangles:chosen.reduce((s,p)=>s+p.triangles,0),assumptions:"outside flatRect + walkClearance; full AABB inside free viewport; no overlap with protected anchor rectangles; obstacle polygon AABB clearance. Existing asset projected boxes are approximate, not occlusion tests. Terrain outside flatRect not guaranteed flat or visible. No actual placement."};
}
function selfTest(){
 const f=frame({anchorPosition:[0,0,0],eulerAngles:[0,0,0],orthographicSize:6,leftMarginScreenHeights:.05,rightPanelScreenHeights:.5,topMarginScreenHeights:.06,bottomMarginScreenHeights:.05},16/9,scenarios[0]);
 if(Math.abs(f.pos[0]-2.7)>1e-9||project(f.pos,f).some(v=>v!==.5)||overlaps([0,0,1,1],[2,2,3,3])||!overlaps([0,0,1,1],[1.1,0,2,1],.2))throw Error("self test");
}

import {readFileSync,writeFileSync} from "node:fs";
import {join} from "node:path";
const root=process.argv[2]??".",out=process.argv[3];if(!out)throw Error("Usage: node plan_origin_vis13_composition.mjs <repo-root> <new-report.json>");
const p=["Assets/Dravio/Environment/Data/OriginSanctuaryLayout.json","Assets/Dravio/Environment/Data/OriginSanctuaryLocations.json","Assets/Dravio/Environment/Data/OriginSanctuaryLiving.json","ArtSource/Origin_VIS12_Flora_LOD/manifest.json","ArtSource/Origin_VIS11_Water_Orientation/manifest.json","Assets/Dravio/Environment/Data/OriginSanctuaryManifest.json"];
selfTest();const result=solve(...p.map(s=>JSON.parse(readFileSync(join(root,s),"utf8"))));writeFileSync(out,JSON.stringify(result,null,2)+"\n",{flag:"wx"});
