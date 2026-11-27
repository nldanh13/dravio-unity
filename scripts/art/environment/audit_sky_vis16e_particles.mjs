
function audit(d){
 if(d.zoneId!=="sky"||d.particleVolumes.length!==4)throw Error("Sky baseline contract");
 const channels=["fireflies","firefliesPond","dust","pondRings"];
 for(const k of channels)if(d.particleVolumes.filter(v=>v.channel===k).length!==1)throw Error("channel "+k);
 const tiers=[["Low",.35],["Medium",.65],["High",1]].map(([name,k])=>{
 const caps=d.particleVolumes.map(v=>({id:v.id,channel:v.channel,cap:Math.max(1,Math.ceil(v.maxParticles*k))}));
 const keys=d.keyframes.map(f=>{
  const counts=d.particleVolumes.map(v=>{const key=v.channel==="firefliesPond"?"fireflies":v.channel,life=(v.lifetime[0]+v.lifetime[1])/2,target=v.maxParticles*k*Math.max(0,Math.min(1,f[key]));return {id:v.id,targetAlive:target,emissionPerSecond:target<.5?0:target/Math.max(.5,life)};});
  return {hour:f.hour,counts,totalTargetAlive:counts.reduce((s,v)=>s+v.targetAlive,0),totalEmissionPerSecond:counts.reduce((s,v)=>s+v.emissionPerSecond,0)};
 });
 return {name,scale:k,caps,totalCap:caps.reduce((s,v)=>s+v.cap,0),maxKeyTargetAlive:Math.max(...keys.map(v=>v.totalTargetAlive)),maxKeyEmissionPerSecond:Math.max(...keys.map(v=>v.totalEmissionPerSecond)),keys};
 });
 return {zoneId:d.zoneId,tiers,airflowOptionalInstances:{Low:0,Medium:0,High:1},scope:"CPU estimate from authored keyframes and EnvironmentAmbientParticles formulas; not actual simultaneous count, burst budget, GPU overdraw, or device performance. Stop-emitting threshold modeled, existing particles can survive phase changes."};
}

import {readFileSync,writeFileSync} from "node:fs";
const src=process.argv[2]??"Assets/Dravio/Environment/Zones/Sky/Data/SkyLiving.json",out=process.argv[3];if(!out)throw Error("Usage: node audit_sky_vis16e_particles.mjs <SkyLiving.json> <new-report>");writeFileSync(out,JSON.stringify(audit(JSON.parse(readFileSync(src,"utf8"))),null,2)+"\n",{flag:"wx"});
