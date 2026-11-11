// Explicitly opt-in: sends one real strategic request using server-side credentials.
import { writeFile, mkdir } from 'node:fs/promises';
const base='http://127.0.0.1:5280';
async function post(path,body){const r=await fetch(base+'/api/'+path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});const data=await r.json();if(!r.ok)throw new Error(data.error||`HTTP ${r.status}`);return data;}
let state=await post('new',{sectId:'qingyun',seed:20261111,mode:'llm',difficulty:'normal'});
state=await post('command',{revision:state.revision,type:'choose',option:'expansion'});
const before=JSON.stringify(state),started=Date.now();
try{
  state=await post('advance',{revision:state.revision});
  const result={passed:state.month===1&&state.decisions.filter(d=>d.source==='llm').length===5,elapsedMs:Date.now()-started,month:state.month,modelRequests:state.modelRequests,modelTokens:state.modelTokens,decisions:state.decisions.map(d=>({sectId:d.sectId,policy:d.policy,target:d.target,source:d.source,reason:d.reason}))};
  await mkdir('artifacts/verification',{recursive:true});await writeFile('artifacts/verification/live-model.json',JSON.stringify(result,null,2));console.log(JSON.stringify(result,null,2));if(!result.passed)process.exitCode=1;
}catch(error){
  const current=(await(await fetch(base+'/api/state')).json()).state;
  console.error(JSON.stringify({passed:false,message:error.message,elapsedMs:Date.now()-started,monthUnchanged:JSON.parse(before).month===current.month}));process.exitCode=1;
}
