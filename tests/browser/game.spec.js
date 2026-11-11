import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import fs from 'node:fs/promises';

async function newGame(page,sect='qingyun'){
  await page.goto('/');
  await page.locator(`[data-action="select-sect"][data-id="${sect}"]`).click();
  await page.locator('#new-mode').selectOption('rules');
  await page.locator('#new-seed').fill('20261111');
  await page.locator('[data-action="new-game"]').click();
  const confirm=page.locator('[data-action="start-confirm"]');
  if(await confirm.isVisible())await confirm.click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByRole('dialog').getByRole('heading',{name:'新掌门，第一道宗令',exact:true})).toBeVisible();
  await page.locator('[data-action="choose"][data-id="prosperity"]').click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
}
async function getState(page){return page.evaluate(async()=> (await(await fetch('/api/state')).json()).state);}
async function settleEvent(page){
  const state=await getState(page);
  if(!state.pendingEvent)return;
  if(!await page.getByRole('dialog').count())await page.locator('[data-action="event"]').click();
  const choices=page.locator('[data-action="choose"]:not(:disabled)');
  const free=page.locator('[data-action="choose"][data-id="ordinary"], [data-action="choose"][data-id="wait"], [data-action="choose"][data-id="leave"], [data-action="choose"][data-id="calm"], [data-action="choose"][data-id="prepare"], [data-action="choose"][data-id="endure"], [data-action="choose"][data-id="salvage"]');
  if(await free.count())await free.first().click();else await choices.last().click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
}

test('正式标题、六宗选择与入宗议事',async({page})=>{
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('/');await expect(page).toHaveTitle('无限修仙 · 宗门纪');await expect(page.locator('.sect-card')).toHaveCount(6);
  await fs.mkdir('artifacts/screenshots',{recursive:true});await page.screenshot({animations:'disabled',path:'artifacts/screenshots/title.png',fullPage:true});
  await newGame(page,'jing shui'.replace(' ',''));
  await expect(page.locator('.leader-summary').first()).toContainText('镜水宗');
  await expect(page.locator('.map-canvas .node')).toHaveCount(16);
  await page.screenshot({animations:'disabled',path:'artifacts/screenshots/world.png',fullPage:true});
  expect(errors).toEqual([]);
});

test('所有信息面板、人物详情、策略与宗令可操作',async({page})=>{
  await newGame(page);
  await page.locator('[data-action="page"][data-id="people"]').click();
  await expect(page.getByRole('heading',{name:'人物名录'})).toBeVisible();
  await page.locator('[data-action="person"]').first().click();
  await expect(page.getByRole('dialog',{name:'修士详情'})).toBeVisible();await page.keyboard.press('Escape');
  await page.locator('#person-filter').selectOption('all');await expect(page.locator('.data-table tbody tr')).toHaveCount(81);
  for(const id of ['diplomacy','relics','history','sect']){await page.locator(`[data-action="page"][data-id="${id}"]`).first().click();await expect(page.locator('#world-content .panel').first()).toBeVisible();}
  await page.locator('[data-action="set-policy"][data-id="prosperity"]').click();
  expect((await getState(page)).player.policy).toBe('prosperity');
  const before=await getState(page);await page.locator('[data-action="command-confirm"][data-id="recruit"]').click();await page.locator('[data-action="command"]').click();
  const after=await getState(page);expect(after.player.stones).toBe(before.player.stones-140);expect(after.people.length).toBe(before.people.length+4);expect(after.commandsLeft).toBe(1);
  await page.screenshot({animations:'disabled',path:'artifacts/screenshots/sect.png',fullPage:true});
});

test('地图地域、建设、月度演进和空格暂停',async({page})=>{
  await newGame(page);
  await page.locator('.map-canvas .node[data-id="r05"]').click();
  await expect(page.getByRole('dialog',{name:'地域详情'})).toBeVisible();
  await page.locator('[data-action="build-confirm"][data-id="r05:develop"]').click();await page.locator('[data-action="build"]').click();
  expect((await getState(page)).regions.find(r=>r.id==='r05').development).toBe(2);
  await page.locator('[data-action="advance"]').click();await expect.poll(async()=> (await getState(page)).month).toBe(1);
  await page.keyboard.press('Space');await expect.poll(async()=> (await getState(page)).month).toBeGreaterThan(1);
  await page.keyboard.press('Space');await settleEvent(page);const month=(await getState(page)).month;await page.waitForTimeout(3000);expect((await getState(page)).month).toBe(month);
  await page.locator('[data-action="region"][data-id="r10"]').last().click();await expect(page.getByRole('dialog')).toBeVisible();await page.keyboard.press('Escape');
});

test('存档读写、导出、同源保护与过期命令拒绝',async({page,request})=>{
  await newGame(page);
  await page.locator('[data-action="saves"]').first().click();await page.locator('[data-action="save-slot"][data-id="one"]').click();await expect(page.getByRole('dialog')).toContainText('手动存档 · 壹');await page.keyboard.press('Escape');
  await page.locator('[data-action="advance"]').click();await expect.poll(async()=> (await getState(page)).month).toBe(1);
  await page.locator('[data-action="saves"]').first().click();await page.locator('[data-action="load-confirm"][data-id="one"]').click();await page.locator('[data-action="load-slot"]').click();expect((await getState(page)).month).toBe(0);
  const revision=(await getState(page)).revision;
  const denied=await request.post('/api/command',{headers:{Origin:'https://untrusted.example'},data:{revision,type:'policy',option:'balanced'}});expect(denied.status()).toBe(403);
  const stale=await request.post('/api/command',{data:{revision:revision-1,type:'policy',option:'balanced'}});expect(stale.status()).toBe(409);
  const exported=await request.get('/api/export');expect(exported.status()).toBe(200);const json=await exported.json();expect(json.schemaVersion).toBe(1);expect(JSON.stringify(json)).not.toContain('apiKey');
  const status=await request.get('/api/catalog');expect(JSON.stringify(await status.json())).not.toContain('endpoint');
});

test('窄屏与键盘窗口无横向溢出，琴音设置可保存',async({page})=>{
  await page.setViewportSize({width:390,height:844});await newGame(page,'baicao');
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2)).toBeTruthy();
  await page.getByRole('button',{name:'游戏设置',exact:true}).click();await expect(page.getByRole('dialog',{name:'游戏设置'})).toBeVisible();
  const sound=page.getByRole('switch',{name:'界面琴音'});const before=await sound.getAttribute('aria-checked');await sound.click();await expect(page.getByRole('switch',{name:'界面琴音'})).toHaveAttribute('aria-checked',before==='true'?'false':'true');
  await page.keyboard.press('Escape');await page.screenshot({animations:'disabled',path:'artifacts/screenshots/mobile.png',fullPage:true});
});

test('末座山门撤离、选择薪火与无空地重建的完整界面',async({page})=>{
  await newGame(page);
  const fixture=await getState(page);
  for(const r of fixture.regions)if(r.owner===''||r.owner===fixture.playerId)r.owner='chixia';
  const last=fixture.regions.find(r=>r.id==='r07');last.owner=fixture.playerId;
  const p=fixture.sects.find(s=>s.id===fixture.playerId);p.capital=last.id;p.stones=0;p.herbs=0;
  const originalPeople=fixture.people.filter(p=>p.sectId===fixture.playerId&&p.alive).map(p=>p.id);
  fixture.pendingEvent={id:fixture.nextId++,type:'defense',title:'末座山门告急',eyebrow:'边境急报',text:'此测试场景验证末座山门撤离后仍可继续游玩。',actor:'chixia',target:last.id,choices:[{id:'retreat',label:'护送门徒撤离',description:'保存薪火，不与敌军硬拼。',stones:0,herbs:0}]};
  const imported=await page.request.post('/api/import',{data:fixture});expect(imported.ok()).toBeTruthy();
  await page.reload();await page.locator('[data-action="continue"]').click();
  await page.locator('[data-action="choose"][data-id="retreat"]').click();
  await expect(page.locator('[data-action="choose"][data-id="hidden"]')).toBeVisible();
  await page.screenshot({animations:'disabled',path:'artifacts/screenshots/ember.png',fullPage:true});
  await page.locator('[data-action="choose"][data-id="hidden"]').click();
  const exiled=await getState(page);expect(exiled.sects.find(s=>s.id===fixture.playerId).exiled).toBeTruthy();
  await page.locator('.map-canvas .node[data-id="r05"]').click();
  await page.locator('[data-action="settle-confirm"]').click();await page.locator('[data-action="settle"]').click();
  const rebuilt=await getState(page);expect(rebuilt.sects.find(s=>s.id===fixture.playerId).exiled).toBeFalsy();expect(rebuilt.rebirths).toBe(1);
  expect(rebuilt.regions.find(r=>r.id==='r05').owner).toBe(fixture.playerId);
  expect(rebuilt.people.filter(p=>p.alive&&p.sectId===fixture.playerId).every(p=>originalPeople.includes(p.id))).toBeTruthy();
});

test('真实通关战役可读取终局并回看历史',async({page})=>{
  const result=await promisify(execFile)(process.execPath,['tools/verify-campaign.mjs']);
  expect(JSON.parse(result.stdout).passed).toBeTruthy();
  const ending=JSON.parse(await readFile('artifacts/verification/campaign-ending.json','utf8'));
  expect(ending.status).toBe('victory');expect(ending.sects.filter(s=>s.eliminated)).toHaveLength(5);
  const imported=await page.request.post('/api/import',{data:ending});expect(imported.ok()).toBeTruthy();
  await page.goto('/');await page.locator('[data-action="continue"]').click();
  await expect(page.getByRole('heading',{name:'万宗归一',exact:true})).toBeVisible();
  await page.screenshot({animations:'disabled',path:'artifacts/screenshots/victory.png',fullPage:true});
  await page.locator('[data-action="ending-history"]').click();
  await expect(page.locator('#world-content')).toContainText('万宗归一');
});
