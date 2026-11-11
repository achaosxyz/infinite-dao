// Real end-to-end campaign: uses public game actions only. No imported fixtures or cheats.
import { writeFile,mkdir } from 'node:fs/promises';
import { power } from '../src/InfiniteDao.LegacyWeb/wwwroot/ui.js';
const base='http://127.0.0.1:5280';let state;
async function post(path,body){const r=await fetch(base+'/api/'+path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});const data=await r.json();if(!r.ok)throw new Error(data.error||`HTTP ${r.status}`);return data;}
async function command(type,target='',option=''){state=await post('command',{revision:state.revision,type,target,option});}
const player=()=>state.sects.find(s=>s.id===state.playerId),alive=()=>state.people.filter(p=>p.alive&&p.sectId===state.playerId),lands=()=>state.regions.filter(r=>r.owner===state.playerId);
const border=r=>r.neighbors.some(n=>state.regions.some(t=>t.id===n&&t.owner===state.playerId));
state=await post('new',{sectId:'qingyun',seed:8192,mode:'rules',difficulty:'normal'});
let actions=0,advances=0;
for(let i=0;i<1800&&state.status==='playing';i++){
  const p=player();
  if(state.pendingEvent){
    const e=state.pendingEvent,afford=e.choices.filter(c=>c.stones<=p.stones&&c.herbs<=p.herbs);
    const preferences=e.type==='inauguration'?['expansion']:e.type==='attack'?['careful','balanced']:e.type==='defense'?['sacrifice','balanced']:e.type==='ember'?['wander']:['accept','research','teach','ceremony','support','rescue','array','mine','preserve','buy','equal','attend','trade','keep'];
    const choice=preferences.map(id=>afford.find(c=>c.id===id)).find(Boolean)||afford[0];
    await command('choose','',choice.id);actions++;continue;
  }
  if(p.exiled){const r=state.regions.find(r=>!r.owner)||state.regions.find(r=>state.sects.find(s=>s.id===r.owner).capital!==r.id);await command('settle',r.id);actions++;continue;}
  if(state.commandsLeft>0){
    const targets=state.regions.filter(r=>r.owner!==p.id&&border(r)&&(!r.owner||(p.truces[r.owner]||0)<=state.month)).map(r=>({r,def:r.owner?power(state,r.owner)*(.52+r.fort*.12):35+r.fort*12})).sort((a,b)=>a.def-b.def);
    const wounded=alive().reduce((n,c)=>n+c.injury,0)/Math.max(1,alive().length);
    if(wounded>18&&p.herbs>=90&&p.stones>=200){await command('heal');actions++;continue;}
    if(p.intel<5&&p.stones>600){await command('scout');actions++;continue;}
    if(targets.length&&p.stones>=130&&p.herbs>=20&&power(state,p.id)*(1+p.intel*.04)*.79>targets[0].def){await command('attack',targets[0].r.id);actions++;continue;}
    if(p.training<10&&p.stones>160+p.training*60+200&&p.herbs>65){await command('train');actions++;continue;}
    if(alive().length<35&&p.stones>500){await command('recruit');actions++;continue;}
    if(p.doctrine<8&&p.stones>220+p.doctrine*80+400&&p.herbs>140){await command('research');actions++;continue;}
    const develop=lands().filter(r=>r.development<5).sort((a,b)=>a.development-b.development)[0];
    if(develop&&p.stones>develop.development*100+500){await command('develop',develop.id);actions++;continue;}
  }
  state=await post('advance',{revision:state.revision});advances++;
}
const report={passed:state.status==='victory',status:state.status,months:state.month,actions,advances,survivors:alive().length,territories:lands().length,extinguished:state.sects.filter(s=>s.eliminated).length,rebirths:state.rebirths,events:state.history.length,seed:state.seed,mode:state.mode,difficulty:state.difficulty};
await mkdir('artifacts/verification',{recursive:true});await writeFile('artifacts/verification/campaign.json',JSON.stringify(report,null,2));await writeFile('artifacts/verification/campaign-ending.json',JSON.stringify(state));console.log(JSON.stringify(report,null,2));if(!report.passed)process.exitCode=1;
