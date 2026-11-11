import {esc,icon,num,date,shortDate,realms,lifespan,policyName,portrait,sigil,button,members,lands,power,income,upkeep,landing,shell,policyCards,peopleView,historyView,mapSvg} from './ui.js';

const app=document.querySelector('#app'), modalRoot=document.querySelector('#modal-root');
let state=null,catalog=null,model=null,page='world',selected='qingyun',hasSave=false,landingMode=true,busy=false,running=false,speed=1,timer=null,zoom=1;
let dismissedEvent=0,endingShown=false,personFilter='own',personSearch='',personSort='realm',historyFilter='all',lastFocus=null;
let soundOn=localStorage.getItem('dao-sound')==='on', audioContext=null,toastTimer=null;
const tips={recruit:['山门纳新','招募 4 名炼气弟子，消耗 140 灵石。新门徒会增加供养开支，也会成长为宗门未来。'],train:['演武传功','军阵等级 +1，全体修为 +8。军阵等级永久保留，即使山门失守也能带走。'],research:['推演道藏','道藏等级 +1，持续提高修炼效率。传承在流亡中仍会保留。'],heal:['开炉济伤','消耗 50 灵石与 50 灵材，全体伤势降低 45，宗门安定 +6。'],explore:['探索秘境','消耗 120 灵石与 10 灵材。可能寻得灵石、灵材或镇宗法宝；也可能遭遇妖兽，造成弟子受伤。'],scout:['布设情报','消耗 70 灵石，提高情报等级。每级使本宗出征战力提高 4%，最高 5 级。']};

async function api(path,body,method='POST'){
  const response=await fetch('/api/'+path,{method:body===undefined?'GET':method,headers:body===undefined?{}:{'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
  const result=await response.json();
  if(!response.ok){const error=new Error(result.error||'操作未完成，请重试。');error.modelFailure=result.modelFailure;throw error;}
  return result;
}
function toast(message){clearTimeout(toastTimer);const el=document.querySelector('#toast');el.textContent=message;el.classList.add('show');toastTimer=setTimeout(()=>el.classList.remove('show'),5000);}
function render(){
  const y=window.scrollY;
  app.innerHTML=landingMode?landing(catalog,selected,hasSave):shell(state,page,running,speed,model,zoom);
  if(!landingMode&&page==='people')document.querySelector('#world-content').innerHTML=peopleView(state,personFilter,personSearch,personSort);
  if(!landingMode&&page==='history')document.querySelector('#world-content').innerHTML=historyView(state,historyFilter);
  if(busy){const bar=document.createElement('div');bar.className='busy-bar';bar.id='busy-bar';app.append(bar);if(!landingMode&&state?.mode==='llm'&&state.month%3===0)document.querySelector('#world-content')?.insertAdjacentHTML('afterbegin','<div class="model-wait"><span class="spinner"></span>诸宗正在议事 · HIGH 思考 · 世界尚未推进，请勿重复请求。可暂停后续演进。</div>');}
  window.scrollTo({top:y,behavior:'instant'});
}
function stop(){running=false;clearTimeout(timer);timer=null;if(catalog)render();}
function tickSound(kind='click'){
  if(!soundOn)return;
  try{audioContext??=new(window.AudioContext||window.webkitAudioContext)();audioContext.resume();const notes=kind==='event'?[293.66,440,587.33]:kind==='success'?[261.63,329.63,392]:[523.25];notes.forEach((freq,i)=>{const o=audioContext.createOscillator(),g=audioContext.createGain();o.type='sine';o.frequency.value=freq;g.gain.setValueAtTime(0,audioContext.currentTime+i*.12);g.gain.linearRampToValueAtTime(.035,audioContext.currentTime+i*.12+.015);g.gain.exponentialRampToValueAtTime(.001,audioContext.currentTime+i*.12+.65);o.connect(g);g.connect(audioContext.destination);o.start(audioContext.currentTime+i*.12);o.stop(audioContext.currentTime+i*.12+.7);});}catch{}
}
function modal(content,size='',label='宗门议事'){
  if(!modalRoot.firstElementChild)lastFocus=document.activeElement;
  modalRoot.innerHTML=`<div class="modal-backdrop"><section class="modal ${size}" role="dialog" aria-modal="true" aria-label="${esc(label)}" tabindex="-1">${content}</section></div>`;
  modalRoot.querySelector('.modal').focus();
}
function closeModal(){modalRoot.innerHTML='';lastFocus?.focus?.();}
const modalHeader=(title,eyebrow='宗门纪 · 已暂停')=>`<header class="modal-top"><div><div class="eyebrow">${eyebrow}</div><h2>${esc(title)}</h2></div><button class="icon-button" data-action="close" aria-label="关闭">${icon('close')}</button></header>`;
async function transact(work,{message='',autoEvent=true}={}){
  if(busy)return;
  busy=true;render();
  modalRoot.querySelectorAll('button[data-action]:not([data-action="close"])').forEach(el=>el.disabled=true);
  try{const next=await work();if(next?.sects)state=next;hasSave=true;closeModal();if(message)toast(message);tickSound('success');}
  catch(error){running=false;clearTimeout(timer);toast(error.message);if(error.modelFailure)showModelError(error.message);else if(modalRoot.firstElementChild){closeModal();}}
  finally{busy=false;render();}
  if(autoEvent)checkInterrupts();
}
function checkInterrupts(){
  if(!state||landingMode)return;
  if(state.pendingEvent){running=false;clearTimeout(timer);render();if(dismissedEvent!==state.pendingEvent.id){dismissedEvent=state.pendingEvent.id;showEvent();}}
  if(state.player?.exiled){running=false;clearTimeout(timer);}
  if(state.status==='victory'&&!endingShown){endingShown=true;running=false;render();showEnding();}
}
async function advance(){
  if(!state||busy)return;
  if(state.pendingEvent){stop();showEvent();return;}
  if(state.sects.find(s=>s.id===state.playerId).exiled){stop();toast('请先点击地图上的落脚地域重建山门。');return;}
  if(state.status!=='playing'){stop();showEnding();return;}
  const waiting=state.mode==='llm'&&state.month%3===0;
  if(waiting){document.querySelector('#world-content')?.insertAdjacentHTML('afterbegin','<div class="model-wait"><span class="spinner"></span>诸宗正在议事 · HIGH 思考 · 世界尚未推进；请勿重复请求。</div>');}
  await transact(()=>api('advance',{revision:state.revision}));
  if(running&&!state.pendingEvent)timer=setTimeout(advance,Math.max(450,2300/speed));
}
function play(){if(busy)return;if(state.pendingEvent){showEvent();return;}if(state.status!=='playing'){showEnding();return;}running=true;render();advance();}
async function command(type,target='',option=''){stop();await transact(()=>api('command',{revision:state.revision,type,target:String(target),option}));}

function showEvent(){
  if(!state?.pendingEvent)return;stop();tickSound('event');
  const e=state.pendingEvent,s=state.sects.find(x=>x.id===state.playerId);
  modal(`<div class="council-layout"><aside class="council-art">${sigil(s)}<h3>${e.type==='ember'?'道统不绝':'一念山河'}</h3><p>${s.name}</p></aside><div class="council-main"><div class="between"><span class="eyebrow">${esc(e.eyebrow)}</span><button class="icon-button" data-action="close" aria-label="暂不决策，查看世界">${icon('close')}</button></div><h2>${esc(e.title)}</h2><p class="story">${esc(e.text)}</p><div class="choices">${e.choices.map((c,i)=>`<button class="choice" data-action="choose" data-id="${esc(c.id)}" ${s.stones<c.stones||s.herbs<c.herbs?'disabled':''}><span class="choice-number">${['壹','贰','叁','肆'][i]}</span><span><strong>${esc(c.label)}</strong><p>${esc(c.description)}</p>${c.stones||c.herbs?`<span class="cost">${c.stones?`${c.stones} 灵石`:''}${c.stones&&c.herbs?' · ':''}${c.herbs?`${c.herbs} 灵材`:''}${s.stones<c.stones||s.herbs<c.herbs?' · 资源不足':''}</span>`:''}</span></button>`).join('')}</div><div class="council-close"><span>可暂时关闭查看世界，议事将一直保留。</span></div></div></div>`,'','关键节点选择');
}

function showRegion(id){
  stop();const r=state.regions.find(r=>r.id===id);if(!r)return;
  const owner=state.sects.find(s=>s.id===r.owner),s=state.sects.find(s=>s.id===state.playerId),own=r.owner===s.id;
  const adjacent=r.neighbors.some(n=>state.regions.some(x=>x.id===n&&x.owner===s.id));
  const blocked=!!state.pendingEvent||state.commandsLeft<1||state.status!=='playing';
  const truce=owner&&(s.truces[owner.id]||0)>state.month;
  const canBuy=s.exiled&&state.regions.every(r=>r.owner!=='')&&owner?.capital!==r.id;
  modal(`${modalHeader(r.name,'青岚十六境 · '+r.terrain)}<div class="region-hero"><div class="row">${owner?sigil(owner):'<span class="sigil">野</span>'}<span class="badge ${own?'':'gold'}">${owner?.name||'无主之地'}${owner?.capital===r.id?' · 宗门山门':''}</span></div><h2>${r.name}</h2><p>${esc(r.description)}</p><div class="region-metrics"><div><small>地域发展</small><strong>${r.development}<small> 级</small></strong></div><div><small>护山阵</small><strong>${r.fort}<small> 级</small></strong></div><div><small>灵矿 / 灵材</small><strong>${r.mine}<small> / </small>${r.herbYield}</strong></div><div><small>凡人人口</small><strong>${num(r.population)}</strong></div></div></div><div class="modal-content"><h3 style="margin-bottom:15px">${s.exiled?'薪火落脚之地':own?'领地经营':'边界决策'}</h3>${s.exiled?`<p class="hint" style="margin-bottom:17px">${!owner?'在这里重立山门，消耗 150 灵石。原有弟子、道藏、军阵与法宝都将延续。':canBuy?'天下已无空地，可依山门旧约支付 350 灵石向此宗赎买非首府地域。':'优先选择无主之地；若所有地域已有主人，可赎买非首府地域。'}</p>${button('在此重建山门','settle-confirm',r.id,'primary wide',blocked||!!owner&&!canBuy||s.stones<(owner?350:150))}`:own?`<div class="action-grid"><button class="action-card" data-action="build-confirm" data-id="${r.id}:develop" ${blocked||r.development>=5?'disabled':''}><strong>兴建坊市与灵田</strong><p>地域发展 +1，每月灵石与灵材产出提高。</p><small>${r.development*100+80} 灵石 · 一道宗令</small></button><button class="action-card" data-action="build-confirm" data-id="${r.id}:fortify" ${blocked||r.fort>=5?'disabled':''}><strong>加固护山大阵</strong><p>护阵 +1，提高入侵者的战力门槛。</p><small>${r.fort*100+60} 灵石 · 15 灵材 · 一道宗令</small></button></div>`:`<p class="hint" style="margin-bottom:17px">${truce?'盟约尚未到期，不能背盟出征。':adjacent?`此地与本宗接壤。出征预计战力 ${num(power(state,s.id)*(1+s.intel*.04))}；守方约 ${num(owner?power(state,owner.id)*(.52+r.fort*.12):35+r.fort*12)}。随机裁决与伤亡会影响实际结果。`:'此地尚未与本宗接壤，需要先拓展相邻领地。'}</p>${button(icon('sword')+(owner?' 商议出征':' 开拓此境'),'attack',r.id,'gold wide',blocked||!adjacent||truce)}`}
  <div class="divider"></div><p class="hint">相邻地域：${r.neighbors.map(n=>{const x=state.regions.find(r=>r.id===n);return `<button class="text-btn" data-action="region" data-id="${n}">${x.name}</button>`;}).join(' · ')}</p>${owner?`<p style="margin-top:10px">${button('查看'+owner.name,'sect-detail',owner.id,'small')}</p>`:''}</div>`,'','地域详情');
}

function showPerson(id){
  stop();const p=state.people.find(p=>p.id===Number(id));if(!p)return;const s=state.sects.find(s=>s.id===p.sectId);const player=state.sects.find(s=>s.id===state.playerId);
  const ready=p.alive&&s?.id===player.id&&p.progress>=70&&p.realm<5&&p.injury<=40&&state.commandsLeft>0&&!state.pendingEvent&&!player.exiled;
  modal(`${modalHeader(p.name,'人物志 · '+(s?.name||'散修'))}<div class="detail-hero">${portrait(p.portrait,'',p.name)}<div><span class="badge ${p.alive?'':'coral'}">${p.alive?p.role:'已陨落'}</span><h2>${esc(p.name)}</h2><p>${realms[p.realm]} · ${p.temperament} · ${Math.floor(p.ageMonths/12)} 岁</p><p>${esc(p.technique)}</p></div></div><div class="modal-content"><div class="metric-grid" style="padding:0"><div><small>修行资质</small><b>${p.potential}</b></div><div><small>忠诚</small><b>${p.loyalty}</b></div><div><small>伤势</small><b class="${p.injury>40?'coral':''}">${p.injury}</b></div><div><small>当前修为</small><b>${p.progress}<small> / 110</small></b></div><div><small>寿元上限</small><b>${lifespan[p.realm]}<small> 年</small></b></div><div><small>状态</small><b style="font:16px var(--serif);margin-top:12px">${!p.alive?'仙路已终':p.injury>40?'伤重静养':'修炼中'}</b></div></div><p class="hint" style="margin:20px 0">${esc(p.biography)}</p>${s?.id===state.playerId&&p.alive?`<div class="strategy-note">修为达到 110 时会自动尝试突破（需资源充足）。达到 70 时，掌门可投入更多资源护持，提高突破概率。失败会受伤并损失修为，但不会凭空死亡。</div><p class="hint" style="margin:14px 0">护持消耗 ${100+p.realm*100} 灵石、${45+p.realm*30} 灵材、一道宗令。</p>${button('护持突破','breakthrough-confirm',p.id,'gold wide',!ready)}`:''}</div>`,'small','修士详情');
}

function showSect(id){
  stop();const s=state.sects.find(s=>s.id===id);if(!s)return;const p=state.people.find(p=>p.id===s.leaderId);const people=members(state,id);
  modal(`${modalHeader(s.name,'天下宗门 · '+(s.eliminated?'道统断绝':s.exiled?'流亡中':'存续'))}<div class="detail-hero">${portrait(s.portrait,'',s.name)}<div><h2 style="color:${s.color}">${s.name}</h2><p>${s.motto}</p><p>${esc(s.personality)}</p></div></div><div class="modal-content"><div class="metric-grid" style="padding:0"><div><small>宗门战力</small><b>${num(power(state,id))}</b></div><div><small>领地</small><b>${lands(state,id).length}</b></div><div><small>修士</small><b>${people.length}</b></div><div><small>安定</small><b>${s.stability}</b></div><div><small>道藏 / 军阵</small><b>${s.doctrine}<small> / </small>${s.training}</b></div><div><small>策略</small><b style="font:16px var(--serif);margin-top:12px">${policyName(s.policy)}</b></div></div><div class="divider"></div><h3>当前掌门</h3><p class="hint" style="margin-top:10px">${s.eliminated?'旧宗已散，幸存弟子成为散修。':`${esc(p?.name)} · ${realms[p?.realm||0]} · ${esc(p?.biography)}`}</p><div class="strategy-note">${esc(s.intent)}</div><div class="divider"></div><h3 style="margin-bottom:12px">主要修士</h3>${people.sort((a,b)=>b.realm-a.realm).slice(0,6).map(p=>`<button class="rank-row" data-action="person" data-id="${p.id}"><span class="rank-name">${esc(p.name)}<small style="margin-left:10px">${p.role}</small></span><span class="realm-chip">${realms[p.realm]}</span>${icon('arrow')}</button>`).join('')||'<p class="hint">原门人已散入天下，可在人物名录中查阅。</p>'}<div class="divider"></div><p class="hint">领地：${lands(state,id).map(r=>r.name).join('、')||'无'}</p></div>`,'','宗门概况');
}

function showHistory(id){stop();const e=state.history.find(e=>e.id===Number(id));if(!e)return;modal(`${modalHeader(e.title,'青岚编年 · #'+e.id)}<div class="modal-content"><p class="eyebrow" style="margin-bottom:18px">${date(e.month)}</p><p style="font-family:var(--serif);line-height:2.1;font-size:15px">${esc(e.text)}</p>${e.delta?`<div class="strategy-note jade">${esc(e.delta)}</div>`:''}<div class="divider"></div><p class="hint">相关宗门：${e.sects.map(id=>state.sects.find(s=>s.id===id)?.name).filter(Boolean).join('、')}</p><p class="hint" style="margin-top:12px">${e.causes.length?'因果依据：'+e.causes.map(id=>`<button class="text-btn" data-action="history-detail" data-id="${id}">事件 #${id}</button>`).join('、'):'此记录来自当时的世界规则或掌门决策，没有引用更早的事实事件。'}</p></div>`,'small','历史详情');}

function confirm(title,text,action,id,style='primary'){stop();modal(`${modalHeader(title)}<div class="modal-content"><p style="line-height:2;color:#bfd2c8">${esc(text)}</p>${state?`<p class="hint" style="margin-top:17px">本月剩余 ${state.commandsLeft} 道宗令。操作结果将写入历史并自动存档。</p>`:''}</div><div class="modal-footer">${button('再想一想','close','','small')}${button('确认'+title,action,id,style)}</div>`,'small',title);}

function showPolicies(){stop();const s=state.sects.find(x=>x.id===state.playerId);modal(`${modalHeader('发展策略')}<div class="modal-content">${policyCards(s)}<p class="hint" style="margin-top:18px">可随时调整，不消耗宗令。策略影响自然发展，关键征战仍由你决定。</p></div>`,'large','发展策略');}

async function showSaves(){
  stop();try{const saves=await api('saves');modal(`${modalHeader('山河存续','存档 · 仅保存在本机')}<div class="modal-content"><p class="hint" style="margin-bottom:18px">每次宗令和月度结算后都会自动保存。手动槽位可保留不同的人生岔路。读取会替换当前进度，请先保存需要的分支。</p>${['auto','one','two','three'].map((slot,i)=>{const save=saves.find(x=>x.slot===slot);return `<div class="save-row"><div><h4>${i===0?'自动存档':'手动存档 · '+['','壹','贰','叁'][i]}</h4><p>${save?`${esc(save.sect)} · ${date(save.month)} · ${new Date(save.updatedUtc).toLocaleString('zh-CN')}`:'空白书卷'}</p></div><div class="row">${slot!=='auto'?button(save?'覆盖':'保存','save-slot',slot,'small',!state||landingMode):''}${button('读取','load-confirm',slot,'small gold',!save||save.status==='invalid')}</div></div>`;}).join('')}<div class="divider"></div><div class="row" style="flex-wrap:wrap">${button(icon('download')+' 导出当前存档','export','','small',!state)}${button('导入存档文件','import','','small')}<input type="file" id="import-file" accept="application/json,.json" hidden></div></div>`,'','存档管理');}catch(error){toast(error.message);}
}

async function showSettings(){
  stop();try{model=(await api('state')).model;}catch{}
  modal(`${modalHeader('游戏设置','山河之间 · 掌门偏好')}<div class="modal-content"><div class="setting-row"><div><h4>界面琴音</h4><p>决策与议事时播放简短的五声音阶，不在后台持续播放。</p></div><button class="toggle ${soundOn?'on':''}" data-action="sound" role="switch" aria-checked="${soundOn}" aria-label="界面琴音"></button></div><div class="setting-row"><div><h4>世界决策模式</h4><p>${state?.mode==='llm'?'当前智能世界：其他宗门每季度由模型提出策略。':'当前离线世界：不调用网络，使用可复现的本地策略。'}切换会记入历史，不会改变既有事实。</p></div>${button(state?.mode==='llm'?'切换离线':'启用智能','switch-mode','','small gold',!state||busy)}</div><div class="setting-row"><div><h4>模型思考 · ${esc(model?.effort||'high').toUpperCase()}</h4><p>采用本机 config/llm.local.json。密钥从不发送到浏览器。单次请求失败不自动重试。</p></div><span class="badge ${model?.configured?'':'coral'}">${model?.configured?'已配置':'未配置'}</span></div><div class="setting-row"><div><h4>本批调用额度 ${model?.callsUsed||0} / ${model?.callLimit||0}</h4><p>按服务启动批次限制调用，避免无限消耗。明确授权后可开始下一批；这可能产生真实 API 费用。</p></div>${button('授权下一批','renew-confirm','','small',!model?.configured)}</div><div class="setting-row"><div><h4>本局智能调用</h4><p>请求 ${state?.modelRequests||0} 次，成功响应总计 ${num(state?.modelTokens||0)} tokens。统计包含模型返回的输入、输出与缓存用量。</p></div></div><div class="setting-row"><div><h4>返回山门选择</h4><p>不会删除任何存档。可选择继续前缘，或另起一段宗门史。</p></div>${button('返回标题','title-confirm','','small')}</div></div><footer class="modal-footer"><span class="hint">空格暂停 · Esc 关闭窗口 · S 打开存档</span>${button('完成','close','','small primary')}</footer>`,'','游戏设置');
}

function showHelp(){stop();modal(`${modalHeader('如何执掌一宗','无限修仙 · 宗门纪')}<div class="modal-content"><p class="story" style="font:16px/2 var(--serif);color:#c3d4c9;margin-bottom:20px">你不是一位孤身的修士，而是一段道统的掌舵者。宗门兴衰由你的选择、人物成长与诸宗决策共同写成。</p><div class="tutorial-grid">${[['sect','下宗令，定长远','每月两道宗令，可招募、传功、建设、探索和外交。灵石用于供养与建设，灵材用于修炼和突破。休养生息可提高灵石产出。'],['pause','让世界行进，也可随时停下','点击推进一月，或播放键自动演进。空格暂停；打开详情和关键议事会自动暂停。查看地图、宗门、人物不消耗时间。'],['sword','攻守有据，关键抉择','只能向相邻地域出征。先查看双方战力，再选择稳进、强攻或谨慎战术。护山阵、伤势、情报与军阵都真实影响胜负。'],['book','修士会成长，也会离去','修为积满可自动突破。人物面板可提前护持突破。每位修士有资质、忠诚、伤势和寿元。补足灵材，别让强者无力修炼。'],['leaf','山门可破，道统不绝','最后山门失守时，部分门徒会带出道藏、法宝与余资。处理流亡议事，选择落脚地重建。在同一个世界中继续你的复兴。'],['crown','问鼎山河，万宗归一','消灭其他五宗的全部山门，使其不再作为独立势力存续，即达成统一。战略选择由模型或离线规则提出，世界事实只由规则结算。']].map(([ico,title,text])=>`<article class="tutorial-step">${icon(ico)}<h3>${title}</h3><p>${text}</p></article>`).join('')}</div></div><footer class="modal-footer"><span class="hint">新手建议：先兴建坊市、招募弟子，待修炼成长后再逐步出征。</span>${button('我已明白','close','','primary')}</footer>`,'large','游玩指南');}

function showModelError(message){modal(`${modalHeader('诸宗议事暂缓','模型请求未完成 · 世界保持暂停')}<div class="modal-content"><p class="story" style="line-height:2;color:#d6caba">${esc(message)}</p><div class="strategy-note">不会自动重试，也不会偷偷改用规则世界。你可检查本地配置后再次推进，或明确切换离线模式，不丢失进度。</div></div><div class="modal-footer">${button('查看设置','settings','','small')}${button('切换离线继续','go-offline','','small gold')}${button('暂不重试','close','','small')}</div>`,'small','模型请求暂停');}

function showEnding(){
  stop();const s=state.sects.find(s=>s.id===state.playerId);modal(`<div class="victory-scene"><div class="eyebrow">一段宗门史，写到山河尽头</div>${sigil(s,'large')}<h1>万宗归一</h1><p>昔日六宗并立，如今山河共主。<br>${s.name}的道统，终于传遍青岚。</p><div class="ending-stats"><div><strong>${state.month}</strong><small>月的征途</small></div><div><strong>${state.rebirths}</strong><small>次薪火重建</small></div><div><strong>${members(state,s.id).length}</strong><small>位存续门徒</small></div></div><p style="font-size:12px">这不是一段凭空写成的传说。<br>每一位逝者、每一场战役，都留在你的宗门史中。</p><div class="row" style="justify-content:center;margin-top:25px">${button('查阅宗门史','ending-history','','gold')}${button('导出此局存档','export','','primary')}</div></div>`,'','统一终局');
}

document.addEventListener('click',async event=>{
  const el=event.target.closest('[data-action]');if(!el||el.disabled)return;
  const action=el.dataset.action,id=el.dataset.id||'';
  if(busy&&!['pause','close','page','region','person','sect-detail','history-detail','help','zoom'].includes(action)){toast('当前决策正在结算，请稍候。');return;}
  tickSound();
  try{
    switch(action){
      case 'select-sect':{const values=['new-mode','new-difficulty','new-seed'].map(id=>document.getElementById(id)?.value);selected=id;render();['new-mode','new-difficulty','new-seed'].forEach((id,i)=>{if(values[i]!=null)document.getElementById(id).value=values[i];});break;}
      case 'new-game':{
        const mode=document.querySelector('#new-mode').value,difficulty=document.querySelector('#new-difficulty').value,seed=Number(document.querySelector('#new-seed').value);
        if(!Number.isInteger(seed)||seed<1||seed>2147483647){toast('请输入 1～2147483647 之间的世界种子。');break;}
        if(hasSave){modal(`${modalHeader('开启新的宗门史')}<div class="modal-content"><p class="hint">新局会替换自动存档。手动槽位不会改变，若要保留当前分支，请先返回并存入手动槽位。</p></div><div class="modal-footer">${button('取消','close','','small')}${button('确认接掌宗门','start-confirm',JSON.stringify({sectId:selected,mode,difficulty,seed}),'primary')}</div>`,'small','新局确认');}else await startNew({sectId:selected,mode,difficulty,seed});break;
      }
      case 'start-confirm':await startNew(JSON.parse(id));break;
      case 'continue':await loadSlot('auto');break;
      case 'close':closeModal();break;
      case 'page':stop();page=id;closeModal();render();window.scrollTo(0,0);break;
      case 'pause':stop();break;
      case 'play':play();break;
      case 'speed':speed=Number(id);render();break;
      case 'advance':await advance();break;
      case 'event':showEvent();break;
      case 'choose':await command('choose','',id);break;
      case 'region':showRegion(id);break;
      case 'person':showPerson(id);break;
      case 'sect-detail':showSect(id);break;
      case 'history-detail':showHistory(id);break;
      case 'policy':showPolicies();break;
      case 'set-policy':await command('policy','',id);toast('发展策略已调整：'+policyName(id));break;
      case 'command-confirm':{const [title,text]=tips[id];const s=state.sects.find(s=>s.id===state.playerId);const cost=id==='train'?` 本次消耗 ${160+s.training*60} 灵石与 30 灵材。`:id==='research'?` 本次消耗 ${220+s.doctrine*80} 灵石与 60 灵材。`:'';confirm(title,text+cost,'command',id);break;}
      case 'command':await command(id);break;
      case 'build-confirm':{const [rid,type]=id.split(':');const r=state.regions.find(x=>x.id===rid);confirm(type==='develop'?'兴建坊市与灵田':'加固护山大阵',`${r.name}：${type==='develop'?`消耗 ${r.development*100+80} 灵石，地域发展 +1。`:`消耗 ${r.fort*100+60} 灵石与 15 灵材，护阵 +1。`}`,'build',id);break;}
      case 'build':{const [rid,type]=id.split(':');await command(type,rid);break;}
      case 'attack':await command('attack',id);dismissedEvent=0;showEvent();break;
      case 'settle-confirm':confirm('重建山门','保留门徒、传承、军阵与法宝，在此重新开始经营。无主之地消耗 150 灵石，赎买他宗非首府消耗 350 灵石。','settle',id);break;
      case 'settle':await command('settle',id);break;
      case 'breakthrough-confirm':{const p=state.people.find(p=>p.id===Number(id));confirm('护持突破',`${p.name}将尝试晋入${realms[p.realm+1]}。消耗 ${100+p.realm*100} 灵石与 ${45+p.realm*30} 灵材；失败会损失修为并受伤。`,'breakthrough',id);break;}
      case 'breakthrough':await command('breakthrough',id);break;
      case 'diplomacy-action':{const [sid,type]=id.split(':');confirm(type==='gift'?'馈赠灵石':'议定盟约',type==='gift'?'消耗 120 灵石，使对方实际获得 100 灵石，关系改善 25。':'消耗 180 灵石，双方缔结 18 个月互不侵犯盟约。','diplomacy-commit',id);break;}
      case 'diplomacy-commit':{const [sid,type]=id.split(':');await command(type,sid);break;}
      case 'zoom':zoom=id==='reset'?1:Math.min(1.6,Math.max(.8,zoom+(id==='in'?.2:-.2)));document.querySelector('.map-canvas').outerHTML=mapSvg(state,zoom);break;
      case 'saves':await showSaves();break;
      case 'save-slot':await transact(()=>api('save/'+id,{revision:state.revision}),{message:'宗门史已保存。',autoEvent:false});await showSaves();break;
      case 'load-confirm':confirm('读取存档','当前进度将被所选存档替换，未存入手动槽位的分支可能丢失。','load-slot',id);break;
      case 'load-slot':await loadSlot(id);break;
      case 'export':{if(!state){toast('尚无可导出的世界。');break;}const a=document.createElement('a');a.href='/api/export';a.download='无限修仙-宗门存档.json';a.click();toast('已开始导出当前宗门史。');break;}
      case 'import':document.querySelector('#import-file')?.click();break;
      case 'settings':await showSettings();break;
      case 'sound':soundOn=!soundOn;localStorage.setItem('dao-sound',soundOn?'on':'off');tickSound('event');await showSettings();break;
      case 'switch-mode':confirm('切换世界模式',state.mode==='llm'?'明确切换为离线策略，后续不再调用模型。当前世界和存档不会丢失。':'启用你配置的模型，每个季度发送一次诸宗状态与相关历史。思考等级 HIGH，可能产生真实 API 费用。','commit-mode',state.mode==='llm'?'rules':'llm');break;
      case 'commit-mode':await command('mode','',id);await showSettings();break;
      case 'go-offline':await command('mode','','rules');break;
      case 'renew-confirm':confirm('授权下一批调用','明确允许重新开启一批模型请求额度。每次季度议事可能产生真实 API 费用。单批上限仍采用本地配置。','renew');break;
      case 'renew':model=await api('model/renew',{});toast('已授权下一批模型调用。');await showSettings();break;
      case 'title-confirm':confirm('返回标题','当前世界已经自动保存。返回后可读取继续，或选择新的宗门。','title');break;
      case 'title':stop();landingMode=true;closeModal();render();break;
      case 'help':showHelp();break;
      case 'ending-history':closeModal();page='history';render();break;
    }
  }catch(error){toast(error.message);}
});

async function startNew(input){
  endingShown=false;dismissedEvent=0;page='world';
  await transact(async()=>{const result=await api('new',input);landingMode=false;return result;});
}
async function loadSlot(slot){
  stop();dismissedEvent=0;endingShown=false;page='world';
  await transact(async()=>{const result=await api('load/'+slot,{});landingMode=false;return result;},{message:'宗门史已载入。'});
}
document.addEventListener('change',async event=>{
  const id=event.target.id;
  if(id==='person-filter'||id==='person-sort'){personFilter=document.querySelector('#person-filter').value;personSort=document.querySelector('#person-sort').value;document.querySelector('#world-content').innerHTML=peopleView(state,personFilter,personSearch,personSort);}
  if(id==='history-filter'){historyFilter=event.target.value;document.querySelector('#world-content').innerHTML=historyView(state,historyFilter);}
  if(id==='import-file'){
    const file=event.target.files[0];if(!file)return;
    if(file.size>8_000_000){toast('存档过大，最大支持 8 MB。');return;}
    const text=await file.text();
    try{const parsed=JSON.parse(text);if(parsed.schemaVersion!==1)throw new Error();confirm('导入宗门史','导入会替换当前世界。建议先保存当前分支；文件只在本机处理。','import-commit');modalRoot.querySelector('[data-action="import-commit"]').addEventListener('click',async()=>{if(busy)return;await transact(async()=>{const response=await fetch('/api/import',{method:'POST',headers:{'Content-Type':'application/json'},body:text});const result=await response.json();if(!response.ok)throw new Error(result.error);landingMode=false;dismissedEvent=0;endingShown=false;return result;},{message:'宗门史已导入。'});},{once:true});}catch{toast('不是有效的宗门纪存档，原有世界未改变。');}
  }
});
document.addEventListener('input',event=>{if(event.target.id==='person-search'){personSearch=event.target.value;const position=event.target.selectionStart;document.querySelector('#world-content').innerHTML=peopleView(state,personFilter,personSearch,personSort);const input=document.querySelector('#person-search');input.focus();try{input.setSelectionRange(position,position);}catch{}}});
document.addEventListener('keydown',event=>{
  const editing=['INPUT','SELECT','TEXTAREA'].includes(document.activeElement?.tagName);
  if(event.key==='Escape'){event.preventDefault();if(modalRoot.firstElementChild)closeModal();else stop();return;}
  if(event.key==='Tab'&&modalRoot.firstElementChild){const focusable=[...modalRoot.querySelectorAll('button:not(:disabled),a[href],input:not([hidden]),select')];if(!focusable.length)return;const first=focusable[0],last=focusable.at(-1);if(event.shiftKey&&(document.activeElement===first||document.activeElement.classList.contains('modal'))){last.focus();event.preventDefault();}else if(!event.shiftKey&&document.activeElement===last){first.focus();event.preventDefault();}}
  if(editing)return;
  if(event.code==='Space'&&!landingMode&&!modalRoot.firstElementChild){event.preventDefault();running?stop():play();}
  if(event.key.toLowerCase()==='s'&&!landingMode&&!busy&&!event.ctrlKey&&!event.metaKey){event.preventDefault();showSaves();}
  if(event.key==='Enter'&&document.activeElement?.matches('.map-canvas [data-action]'))document.activeElement.dispatchEvent(new MouseEvent('click',{bubbles:true}));
});
document.addEventListener('visibilitychange',()=>{if(document.hidden)stop();});
window.addEventListener('beforeunload',()=>{running=false;clearTimeout(timer);});

try{const [cat,current,saves]=await Promise.all([api('catalog'),api('state'),api('saves')]);catalog=cat;model=current.model;state=current.state;hasSave=saves.some(s=>s.slot==='auto'&&s.status!=='invalid');render();}catch{app.innerHTML=`<main class="boot"><div class="seal">道</div><h2>山河尚未展开</h2><p>未能连接本机游戏服务。请使用「启动游戏.cmd」，然后刷新页面。</p><button class="btn primary" id="retry-boot">重新连接</button></main>`;document.querySelector('#retry-boot').addEventListener('click',()=>location.reload());}
