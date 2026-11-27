// Read-only check of local Git objects against pinned handoff manifest.
import {readFileSync} from "node:fs";
import {execFileSync} from "node:child_process";
import {createHash} from "node:crypto";
const p=process.argv[2]??"ArtSource/Origin_VIS14_Release/manifest.json";
const m=JSON.parse(readFileSync(p,"utf8")),seen=new Set(),errors=[];
function check(ref,path,blob,bytes){
 if(!/^[a-f0-9]{40}$/.test(ref)||!/^[a-f0-9]{40}$/.test(blob)||!path.startsWith("ArtSource/")&&!path.startsWith("Assets/Dravio/UI/Sprites/"))throw Error("invalid manifest record");
 const b=execFileSync("git",["show",ref+":"+path],{maxBuffer:64*1024*1024});
 const hash=createHash("sha1").update("blob "+b.length+"\0").update(b).digest("hex");
 if(hash!==blob||b.length!==bytes)throw Error("blob/length mismatch");
}
for(const a of m.assets){
 try{const key=a.ref+":"+a.path;if(seen.has(key))throw Error("duplicate source");seen.add(key);check(a.ref,a.path,a.blob,a.bytes);if(a.runtimeVerifiedAt)check(a.runtimeVerifiedAt,a.runtimeCandidate,a.blob,a.bytes);}
 catch(e){errors.push(a.path+": "+e.message);}
}
console.log(JSON.stringify({assets:m.assets.length,errors},null,2));if(errors.length)process.exitCode=1;
