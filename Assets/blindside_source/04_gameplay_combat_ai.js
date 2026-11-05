/* =========================================================
   TWEENS / TIMING
   ========================================================= */
const TWS=[];
const dur=ms=>G.fast?ms*0.02:ms;
function sleep(ms){ return new Promise(r=>setTimeout(r,dur(ms))); }
const easeIO=p=>p<.5?2*p*p:1-Math.pow(-2*p+2,2)/2;
function tween(o,props,ms,lin){ ms=dur(ms); return new Promise(res=>{
  for(let i=TWS.length-1;i>=0;i--){ const w=TWS[i]; if(w.o===o&&Object.keys(props).some(k=>k in w.to)){ TWS.splice(i,1); w.res(); } }
  const from={}; for(const k in props) from[k]=o[k];
  if(ms<=1){ Object.assign(o,props); res(); return; }
  TWS.push({o,from,to:props,ms,t0:now(),res,e:lin?(p=>p):easeIO}); }); }
function updateTweens(t){ if(G.frozen) return; for(let i=TWS.length-1;i>=0;i--){ const w=TWS[i]; const p=Math.min(1,(t-w.t0)/w.ms), e=w.e(p); for(const k in w.to) w.o[k]=w.from[k]+(w.to[k]-w.from[k])*e; if(p>=1){ TWS.splice(i,1); w.res(); } } }
function panTo(x,y,ms=450){ return tween(G.cam,{x,y},ms); }

/* =========================================================
   MARKS / DISCOVERY
   ========================================================= */
function addMark(team,x,y,kind){ G.marks=G.marks.filter(m=>!(m.team===team&&Math.hypot(m.x-x,m.y-y)<.7)); G.marks.push({team,x,y,kind,until:G.phaseCount+2,t0:now()}); }
let discVer=-1;
function updateDiscovery(){
  if(discVer===G.ver||G.state!=='game') return; discVer=G.ver;
  const tv=teamVision('atk');
  for(const c of G.cams) if(c.alive&&tv.all.has(idx(Math.round(c.x),Math.round(c.y)))) c.discovered=true;
  for(const e of G.electrics) if(tv.all.has(idx(Math.round(e.x),Math.round(e.y)))) e.discovered=true;
  for(const p of panels){ if(!p.cells.some(c=>tv.all.has(idx(c.x,c.y)))) continue; let ch=false;
    if(p.reinforced&&!p.seenByAtk){ p.seenByAtk=true; ch=true; } if(p.electrified&&!p.elecSeen){ p.elecSeen=true; ch=true; }
    if(ch&&G.side==='atk') refreshPanel(p); }
}
function knownEnemies(team){
  const out=[]; for(const e of G.units) if(e.alive&&e.team!==team&&enemyVisible(team,e)) out.push({x:e.x,y:e.y,u:e});
  for(const m of G.marks) if(m.team===team&&m.until>G.phaseCount&&!out.some(o=>Math.hypot(o.x-m.x,o.y-m.y)<.7)) out.push({x:m.x,y:m.y,u:null});
  return out;
}

/* =========================================================
   COMBAT
   ========================================================= */
function hitChance(a,t,o={}){ const w=WEAP[a.weapon], d=dist(a,t); let c=w.acc; if(w.breach&&d<=2.5) c+=15; if(d>w.range) c-=(d-w.range)*12; c-=(o.walls||0)*22; if(o.preempt) c+=15; return Math.round(clamp(c,5,95)); }
function shieldBlocks(t,a){ if(!t.shield) return false; return Math.abs(angNorm(Math.atan2(a.y-t.y,a.x-t.x)-t.facing))<=1.2; }
function canShoot(a,t){ return !traceLine(a.x,a.y,t.x,t.y).blocked&&dist(a,t)<=WEAP[a.weapon].range+4; }
const HS_BASE={AR:10,SMG:6,SG:4,PST:14};
function headshotChance(a,t,o={}){ if(!t||t.team==='host') return 0; const w=WEAP[a.weapon], d=dist(a,t); let c=HS_BASE[a.weapon]||8; if(o.preempt) c+=18; if(d>w.range) c-=5; if(d<=2.5) c+=4; c-=(o.walls||0)*4; return Math.round(clamp(c,2,60)); }
async function hitStop(ms){ const d=dur(ms); if(d<2) return; G.frozen=true; const s=now(); await new Promise(r=>setTimeout(r,d)); const el=now()-s; for(const w of TWS) w.t0+=el; for(const f of R.fx) f.t0+=el; for(const p of R.pops) p.t0+=el; G.frozen=false; }
async function headshotFx(a,t,dmg){
  const mine=a.team===G.side;
  boom(t.x,t.y,0xff2a44,18); fxRing(t.x,t.y,0xffffff,420,1.1); fxRing(t.x,t.y,mine?0xffc81e:0xff2a44,650,1.8);
  const s=proj(t.rx,1.05,t.ry), el=document.createElement('div');
  el.className='hs '+(mine?'':'foe');
  el.innerHTML=`<div class="hs-ic"><svg viewBox="0 0 64 64"><circle cx="32" cy="32" r="22" fill="none" stroke="currentColor" stroke-width="5"/><path d="M32 2v18M32 44v18M2 32h18M44 32h18" stroke="currentColor" stroke-width="5"/><circle cx="32" cy="32" r="6" fill="currentColor"/></svg></div><div class="hs-t"><b data-t="HEADSHOT!">HEADSHOT!</b><span>헤드샷 · 치명타 ×2 · -${dmg}</span></div>`;
  el.style.left=clamp(s.x,160,VW-160)+'px'; el.style.top=clamp(s.y,120,VH-160)+'px';
  document.body.appendChild(el); setTimeout(()=>el.classList.add('out'),dur(1150)); setTimeout(()=>el.remove(),dur(1550));
  const fl=document.getElementById('hsFlash'); fl.style.setProperty('--x',s.x+'px'); fl.style.setProperty('--y',s.y+'px'); fl.classList.remove('on'); void fl.offsetWidth; fl.classList.add('on');
  sfx('crit');
  await hitStop(150);
  G.shake=Math.max(G.shake,.32);
  const h0=G.cam.h; G.cam.h=h0*.93; tween(G.cam,{h:h0},320);
}
async function shoot(a,t,o={}){
  if(!a.alive||!t.alive||G.over) return;
  const w=WEAP[a.weapon], tr=traceLine(a.x,a.y,t.x,t.y);
  if(tr.blocked||dist(a,t)>w.range+4){ log(`${a.kr}: 사선이 막혀 사격할 수 없습니다`,''); return; }
  a.facing=Math.atan2(t.y-a.y,t.x-a.x); G.ver++;
  a.revealT=now()+3500; t.revealT=Math.max(t.revealT||0,now()+2500);
  const chance=hitChance(a,t,{preempt:o.preempt,walls:tr.walls}), hsC=headshotChance(a,t,{preempt:o.preempt,walls:tr.walls}), dmg=Math.round(w.dmg*(o.preempt?1.4:1)), blocked=shieldBlocks(t,a);
  combatShow(a,t,chance,dmg,w.shots,o.preempt,tr.walls,blocked,blocked?0:hsC);
  await sleep(700);
  let hits=0,total=0,hs=0;
  for(let i=0;i<w.shots;i++){ if(!t.alive) break;
    const hit=Math.random()*100<chance; fxTracer(a,t,hit); sfx('shot');
    if(hit){ if(blocked) popup(t,'BLOCK','#9fd8ff');
      else { const crit=Math.random()*100<hsC, d=crit?dmg*2:dmg; hits++; total+=d; t.hp=Math.max(0,t.hp-d);
        if(crit){ hs++; popup(t,'-'+d+' CRIT','#ff3b54',true); combatUpdate(t); combatCrit(hs); await headshotFx(a,t,d); }
        else { popup(t,'-'+d,'#ffd21f'); G.shake=Math.max(G.shake,.12); } } }
    else popup(t,'MISS','#9aa4ae');
    combatUpdate(t); await sleep(w.shots>4?90:150);
  }
  addMark(other(a.team),a.x,a.y,'shot');
  log(`${a.kr} → ${t.kr}: ${blocked?'방패에 막힘':`${hits}/${w.shots} 명중, ${total} 피해`}${hs?` (헤드샷 ×${hs})`:''}${o.preempt?' (선제)':''}`,a.team===G.side?'a':'f');
  combatResult(hits,w.shots,total,blocked,hs);
  if(t.hp<=0) kill(t,a);
  updateHUD(); await sleep(750); combatHide(); await sleep(150);
}
async function exchange(f,s,preempt){ await shoot(f,s,{preempt}); if(G.over) return; if(f.alive&&s.alive&&s.team!=='host'&&canShoot(s,f)) await shoot(s,f,{}); }
function kill(t,by){
  t.hp=0; t.alive=false; G.ver++; popup(t,t.team==='host'?'인질 사망':'K.I.A','#ff3b54'); boom(t.x,t.y,0xff3b54,12);
  if(t.team==='host'){ t.killedBy=by.team; log(`인질이 사망했습니다! (${by.kr})`,'w'); }
  else { const h=G.hostage; if(h&&h.escort===t){ h.escort=null; log('호송 중이던 인질이 그 자리에서 대기합니다','w'); } G.stats[by.team]++; log(`${t.kr} 사살됨`,t.team===G.side?'f':'a'); }
  checkWin();
}
function checkWin(){
  if(G.over) return true; const h=G.hostage;
  if(h&&!h.alive){ const l=h.killedBy; finish(l==='atk'?'def':'atk',l==='atk'?'공격팀의 사격으로 인질이 사망했습니다.':'방어팀의 사격으로 인질이 사망했습니다.'); return true; }
  const aa=G.units.some(u=>u.alive&&u.team==='atk'), da=G.units.some(u=>u.alive&&u.team==='def');
  if(G.phase!=='prep'&&!aa){ finish('def','공격팀 전원이 제압되었습니다.'); return true; }
  if(!da){ finish('atk','방어팀 전원이 제압되었습니다.'); return true; }
  if(h&&h.escort&&h.escort.alive&&TLp(h.escort.x,h.escort.y)==='extract'){ finish('atk','인질을 구출 지점까지 무사히 호송했습니다.'); return true; }
  return false;
}

/* =========================================================
   DIRECTIONAL FIRE
   ========================================================= */
function enemyGadgets(team){ return team==='atk'?G.cams.filter(c=>c.alive):G.drones.filter(d=>d.alive); }
function fireCast(u,ang){
  const w=WEAP[u.weapon], maxR=w.range+4, cs=Math.cos(ang), sn=Math.sin(ang), s=idx(Math.round(u.x),Math.round(u.y)); let walls=0,last=-1;
  const gs=enemyGadgets(u.team), h=G.hostage;
  for(let d=.3;d<=maxR;d+=.08){ const px=u.x+cs*d, py=u.y+sn*d, end={x:px,y:py};
    const e=G.units.find(e=>e.alive&&e.team!==u.team&&Math.hypot(e.x-px,e.y-py)<.32); if(e) return {end:{x:e.x,y:e.y},walls,unit:e};
    if(h&&h.alive&&Math.hypot(h.x-px,h.y-py)<.32) return {end:{x:h.x,y:h.y},walls,unit:h};
    const g=gs.find(g=>Math.hypot(g.x-px,g.y-py)<.3); if(g) return {end:{x:g.x,y:g.y},walls,gadget:g};
    const cx=Math.round(px), cy=Math.round(py), k=idx(cx,cy); if(k===last||k===s) continue; last=k;
    const t=TL(cx,cy); if(t==='void'||t==='hard') return {end,walls,stop:true};
    if(t==='wall'){ const p=panels[panelOf[k]]; if(p.reinforced||p.mirror) return {end,walls,stop:true,reinforced:true};
      if(w.breach&&walls===0&&d<=2.4) return {end:{x:cx,y:cy},walls,breach:p}; walls++; }
  }
  return {end:{x:u.x+cs*maxR,y:u.y+sn*maxR},walls};
}
async function fireDirection(u,ang){
  const c=fireCast(u,ang); u.facing=ang; G.ver++; u.acted=true;
  if(c.unit) await exchange(u,c.unit,true);
  else if(c.gadget) await destroyGadget(u,c.gadget);
  else { for(let i=0;i<WEAP[u.weapon].shots;i++){ fxTracer(u,{rx:c.end.x+(Math.random()-.5)*.3,ry:c.end.y+(Math.random()-.5)*.3},true); sfx('shot'); await sleep(110); }
    addMark(other(u.team),u.x,u.y,'shot');
    if(c.breach){ breakPanel(c.breach,'rubble'); log(`${u.kr}: 산탄총으로 벽을 파괴했습니다`,'a'); addMark(other(u.team),c.breach.cx,c.breach.cy,'breach'); }
    else log(`${u.kr}: 사격 — 명중한 목표 없음`,''); }
}
async function destroyGadget(u,g){
  u.facing=Math.atan2(g.y-u.y,g.x-u.x); G.ver++; fxTracer(u,{rx:g.x,ry:g.y},true); sfx('shot'); await sleep(160);
  g.alive=false; G.ver++; boom(g.x,g.y,0x7cf0c8,10); log(`${u.kr}: ${g.owner?'드론':'감시카메라'} 파괴`,u.team===G.side?'a':'f'); await sleep(300);
}

/* =========================================================
   MOVEMENT (free path) + ENCOUNTER
   ========================================================= */
const isHuman=team=>team===G.side&&!G.autoplay;
function setEscort(u){ const h=G.hostage; h.escort=u; h.trail=[{x:h.x,y:h.y},{x:u.x,y:u.y}]; }
function followHostage(u){
  const h=G.hostage; if(!h||!h.alive||h.escort!==u) return;
  h.trail.push({x:u.x,y:u.y}); if(h.trail.length>80) h.trail.shift();
  let acc=0; for(let i=h.trail.length-1;i>0;i--){ const a=h.trail[i], b=h.trail[i-1], s=dist(a,b);
    if(acc+s>=.85){ const t=(.85-acc)/s; h.x=a.x+(b.x-a.x)*t; h.y=a.y+(b.y-a.y)*t; tween(h,{rx:h.x,ry:h.y},90,true); return; } acc+=s; }
}
async function moveAlong(u,pts,human){
  const enemies=()=>G.units.filter(e=>e.alive&&e.team!==u.team);
  const trigM=new Set(), trigE=new Set();
  for(const e of enemies()){ if(sees(u,e)) trigM.add(e.id); if(sees(e,u)) trigE.add(e.id); }
  let stopped=false, cur={x:u.x,y:u.y}; const STEP=.2;
  outer: for(const q of pts){
    let seg=dist(cur,q); const a=Math.atan2(q.y-cur.y,q.x-cur.x), cs=Math.cos(a), sn=Math.sin(a);
    while(seg>1e-3){
      if(!u.alive||G.over||u.moveLeft<=1e-3) break outer;
      const st=Math.min(STEP,seg,u.moveLeft), nx=cur.x+cs*st, ny=cur.y+sn*st;
      const blk=G.units.find(e=>e.alive&&e.team!==u.team&&Math.hypot(e.x-nx,e.y-ny)<.56);
      const h=G.hostage, hb=h&&h.alive&&h.escort!==u&&Math.hypot(h.x-nx,h.y-ny)<.5;
      if(blk||hb){ if(blk){ blk.revealT=now()+3000; addMark(u.team,blk.x,blk.y,'drone'); } log(`${u.kr}: 경로가 막혀 이동을 멈췄습니다`,''); break outer; }
      if(!u.lockFacing) u.facing=a;
      u.x=nx; u.y=ny; cur={x:nx,y:ny}; u.moveLeft-=st; seg-=st; G.ver++;
      followHostage(u);
      const show=u.team===G.side||enemyVisible(G.side,u);
      if(show) panTo(nx,ny,200);
      if(Math.random()<.35) sfx('step');
      await tween(u,{rx:nx,ry:ny},show?st*230:6,true);
      if(checkWin()) return true;
      let enc=null;
      for(const e of enemies()){
        const us=sees(u,e), es=sees(e,u);
        if((us||es)&&!canShoot(u,e)&&!canShoot(e,u)){ if(us&&!trigM.has(e.id)){trigM.add(e.id); addMark(u.team,e.x,e.y,'drone');} if(es&&!trigE.has(e.id)){trigE.add(e.id); addMark(e.team,u.x,u.y,'drone');} continue; }
        if(es&&!trigE.has(e.id)){ trigE.add(e.id); if(us) trigM.add(e.id); enc={e,kind:us?2:3}; break; }
        if(us&&!trigM.has(e.id)){ trigM.add(e.id); enc={e,kind:1}; break; }
      }
      if(enc){ const r=await resolveEncounter(u,enc.e,enc.kind,human); if(r==='stop'){ stopped=true; break outer; } }
    }
    cur={x:u.x,y:u.y};
  }
  if(stopped||u.moveLeft<.35) u.moveLeft=0;
  updateHUD(); return stopped;
}
async function resolveEncounter(u,e,kind,human){
  const T=now();
  if(kind===1) u.alertT=T; else if(kind===2){u.alertT=T;e.alertT=T;} else e.alertT=T;
  e.revealT=T+3000; u.revealT=T+3000;
  await panTo((u.x+e.x)/2,(u.y+e.y)/2,500); sfx('alert'); await showContact(u,e,kind);
  if(kind===1){
    const attack=human?await askEngage(u,e):hitChance(u,e,{preempt:true,walls:traceLine(u.x,u.y,e.x,e.y).walls})>=30;
    if(!attack){ log(`${u.kr}: 적을 무시하고 이동을 계속합니다`,''); return 'go'; }
    await exchange(u,e,true); u.acted=true; return 'stop';
  }
  if(kind===2){ const first=speedOf(u)>=speedOf(e)?u:e, second=first===u?e:u; log(`상호 조우 — 속도 판정: ${first.kr} 선공 (SPD ${speedOf(first)} vs ${speedOf(second)})`,'w'); await exchange(first,second,false); u.acted=true; return 'stop'; }
  await exchange(e,u,true); u.acted=true; return 'stop';
}
async function moveDrone(d,pts){
  let cur={x:d.x,y:d.y};
  outer: for(const q of pts){
    let seg=dist(cur,q); const a=Math.atan2(q.y-cur.y,q.x-cur.x);
    while(seg>1e-3){ if(!d.alive||d.moveLeft<=1e-3) break outer;
      const st=Math.min(.25,seg,d.moveLeft), nx=cur.x+Math.cos(a)*st, ny=cur.y+Math.sin(a)*st;
      d.x=nx; d.y=ny; cur={x:nx,y:ny}; d.moveLeft-=st; seg-=st; G.ver++;
      const show=d.team===G.side||cellVisible(G.side,nx,ny); if(show) panTo(nx,ny,160);
      await tween(d,{rx:nx,ry:ny},show?st*200:5,true);
      const el=G.electrics.find(e=>Math.hypot(e.x-nx,e.y-ny)<=e.r);
      if(el){ d.alive=false; G.ver++; boom(nx,ny,0x4fb3ff,14); el.discovered=true; log('드론이 전기장에 의해 파괴되었습니다','w'); break outer; }
      for(const e of G.units) if(e.alive&&e.team==='def'&&!e.ghost&&Math.hypot(e.x-nx,e.y-ny)<=2.5&&losF(nx,ny,e.x,e.y,'atk')){
        if(!G.marks.some(m=>m.team==='atk'&&Math.hypot(m.x-e.x,m.y-e.y)<.7&&m.until>G.phaseCount)){ addMark('atk',e.x,e.y,'drone'); if(d.team===G.side){ log(`드론: ${e.kr} 위치 표시`,'a'); sfx('alert'); } } }
    }
    cur={x:d.x,y:d.y};
  }
  if(d.moveLeft<.3) d.moveLeft=0; G.ver++;
}

/* =========================================================
   ABILITIES
   ========================================================= */
function breakPanel(p,st){ p.state=st; p.reinforced=false; p.electrified=false; p.mirror=false; for(const c of p.cells){ tile[idx(c.x,c.y)]=st; boom(c.x,c.y,0xffb13b,16); } sfx('boom'); bumpMap(); drawFloor(); refreshPanel(p); }
function abilitySpec(u){
  const inR=(x,y,r)=>Math.hypot(x-u.x,y-u.y)<=r;
  switch(u.ab){
    case 'hammer': return {kind:'panel',hint:'파괴할 벽을 선택해주십시오',sub:'인접한 일반 벽만 가능',valid:p=>p.state==='intact'&&!p.reinforced&&!p.mirror&&panelNear(p,u)};
    case 'launcher': return {kind:'panel',range:6,hint:'제거할 벽을 선택해주십시오',sub:'사거리 6m · 투척 경로의 장애물에 막힐 수 있음',valid:p=>p.state==='intact'&&throwClear(u,p)};
    case 'thermite': return {kind:'panel',hint:'폭파시킬 벽을 선택해주십시오',sub:'인접한 벽·강화벽 · 전류가 흐르는 벽 불가',valid:p=>p.state==='intact'&&!p.mirror&&!p.electrified&&panelNear(p,u)};
    case 'stim': return {kind:'ally',range:2.5,hint:'약물을 투여할 아군을 선택해주십시오',sub:'2.5m 이내 아군 (자신 포함)',valid:a=>a.alive&&a.team===u.team&&dist(a,u)<=2.5};
    case 'shield': case 'ghost': return {kind:'instant'};
    case 'shock': return {kind:'point',range:3,radius:1.6,hint:'전기를 설치할 구역을 선택해주십시오',sub:'설치 범위 3m',valid:(x,y)=>freeAt(x,y,false,.15)&&inR(x,y,3)&&losF(u.x,u.y,x,y,u.team)};
    case 'gas': return {kind:'point',range:6,radius:1.6,hint:'가스탄을 던질 위치를 선택해주십시오',sub:'투척 거리 6m',valid:(x,y)=>freeAt(x,y,true,.05)&&inR(x,y,6)&&losF(u.x,u.y,x,y,u.team)};
    case 'mirror': return {kind:'panel',hint:'설치할 벽을 선택해주십시오',sub:'인접한 벽 · 방어팀만 건너편을 볼 수 있음',valid:p=>p.state==='intact'&&!p.mirror&&panelNear(p,u)};
    case 'cam': return {kind:'point',range:4,radius:3.2,hint:'카메라를 설치할 구역을 선택해주십시오',sub:'설치 범위 4m',valid:(x,y)=>freeAt(x,y,false,.15)&&inR(x,y,4)&&losF(u.x,u.y,x,y,u.team)};
  }
}
function spendAbility(u){ if(u.charges<99) u.charges--; if(G.phase!=='prep') u.abilityUsed=true; }
async function useAbility(u,target){
  const ot=other(u.team), mine=u.team===G.side?'a':'f';
  switch(u.ab){
    case 'hammer': { const p=target; u.facing=Math.atan2(p.cy-u.y,p.cx-u.x); G.ver++; for(let i=0;i<3;i++){ G.shake=.15; sfx('hit'); fxRing(p.cx,p.cy,0xffd21f,250,.6); await sleep(190);} breakPanel(p,'rubble'); addMark(ot,p.cx,p.cy,'breach'); log(`${u.kr}: 해머로 벽을 부쉈습니다`,mine); break; }
    case 'launcher': { const p=target; u.facing=Math.atan2(p.cy-u.y,p.cx-u.x); G.ver++; fxArc(u,p.cx,p.cy,0xff9d1c,dur(600)); sfx('shot'); await sleep(620); const r=p.reinforced; breakPanel(p,'hole'); addMark(ot,p.cx,p.cy,'breach'); log(`${u.kr}: ${r?'강화벽':'벽'}에 구멍을 냈습니다 (통과 불가)`,mine); break; }
    case 'thermite': { const p=target; u.facing=Math.atan2(p.cy-u.y,p.cx-u.x); G.ver++; for(let i=0;i<5;i++){ fxRing(p.cx,p.cy,0xff9d1c,300,.8); sfx('hit'); await sleep(160);} const r=p.reinforced; breakPanel(p,'rubble'); addMark(ot,p.cx,p.cy,'breach'); log(`${u.kr}: 발열 폭약으로 ${r?'강화벽':'벽'}을 개방했습니다`,mine); break; }
    case 'stim': { const a=target; a.hp=Math.min(a.maxHp,a.hp+50); const add=moveRange(a)*.5; a.moveLeft+=add; a.stim=true; a.ended=false; popup(a,'+50 HP','#5dffa2'); fxRing(a.x,a.y,0xb98cff,600,1); sfx('ui'); log(`${u.kr} → ${a.kr}: 전투 자극제 (체력 +50, 이동력 +${add.toFixed(1)})`,mine); await sleep(400); break; }
    case 'shield': u.shield=true; popup(u,'SHIELD','#9fd8ff'); sfx('ui'); log(`${u.kr}: 방패 전개`,mine); await sleep(300); break;
    case 'ghost': u.ghost=true; popup(u,'JAMMING','#c9a0ff'); sfx('ui'); if(u.team===G.side) log(`${u.kr}: 전파 교란 — 드론·카메라에 포착되지 않음`,'a'); await sleep(300); break;
    case 'shock': { const [x,y]=target, e={x,y,r:1.6,discovered:false}; G.electrics.push(e); let n=0;
      for(const p of panels) if(p.reinforced&&p.cells.some(c=>Math.hypot(c.x-x,c.y-y)<=e.r+.6)){ if(!p.electrified){ p.electrified=true; refreshPanel(p); } n++; }
      for(const d of G.drones) if(d.alive&&Math.hypot(d.x-x,d.y-y)<=e.r){ d.alive=false; boom(d.x,d.y,0x4fb3ff,10); }
      fxRing(x,y,0x4fb3ff,700,1.6); sfx('zap'); G.ver++; if(u.team===G.side) log(`${u.kr}: 쇼크 와이어 설치${n?` — 강화벽 ${n}개에 전류`:''}`,'a'); await sleep(350); break; }
    case 'gas': { const [x,y]=target; fxArc(u,x,y,0x8cff5a,dur(550)); await sleep(560);
      G.gases.push({x,y,r:1.6,ttl:2}); refreshGas(); sfx('gas'); log(`${u.kr}: 독성 가스탄 투척`,mine);
      for(const e of G.units) if(e.alive&&e.team==='atk'&&Math.hypot(e.x-x,e.y-y)<=1.6){ e.hp=Math.max(0,e.hp-20); popup(e,'-20 GAS','#8cff5a'); if(e.hp<=0) kill(e,u); }
      await sleep(300); break; }
    case 'mirror': { const p=target; p.mirror=true; p.reinforced=false; bumpMap(); refreshPanel(p); sfx('ui'); for(const c of p.cells) fxRing(c.x,c.y,0x2fe4ff,600,.8); if(u.team===G.side) log(`${u.kr}: 블랙 미러 설치`,'a'); await sleep(300); break; }
    case 'cam': { const [x,y]=target; G.cams.push({x,y,alive:true,discovered:false,placed:true}); G.ver++; sfx('ui'); if(u.team===G.side) log(`${u.kr}: 감시카메라 설치`,'a'); await sleep(250); break; }
  }
  spendAbility(u); updateHUD(); checkWin();
}
function refreshGas(){ gasSet=new Set(); for(const g of G.gases) for(let y=Math.floor(g.y-g.r);y<=Math.ceil(g.y+g.r);y++) for(let x=Math.floor(g.x-g.r);x<=Math.ceil(g.x+g.r);x++){ if(Math.hypot(x-g.x,y-g.y)<=g.r&&TL(x,y)!=='void') gasSet.add(idx(x,y)); } bumpMap(); }

/* =========================================================
   AI
   ========================================================= */
function bestTarget(u){
  let best=null,bs=-1e9;
  for(const e of G.units){ if(!e.alive||e.team===u.team||!sees(u,e)||!canShoot(u,e)) continue; const c=hitChance(u,e,{walls:traceLine(u.x,u.y,e.x,e.y).walls}); const s=c+(100-e.hp)*.3-(shieldBlocks(e,u)?90:0); if(s>bs){bs=s;best=e;} }
  return best;
}
async function aiFire(u,t){ await panTo((u.x+t.x)/2,(u.y+t.y)/2,400); await exchange(u,t,!sees(t,u)); u.acted=true; }
async function aiShootGadgets(team){
  for(const g of enemyGadgets(team)){ const sh=G.units.find(u=>u.alive&&u.team===team&&seesPt(u,g.x,g.y)&&!traceLine(u.x,u.y,g.x,g.y).blocked); if(sh){ if(team===G.side||enemyVisible(G.side,sh)) await panTo(g.x,g.y,300); await destroyGadget(sh,g); } }
}
function occupiedCell(x,y,self){ return G.units.some(o=>o.alive&&o!==self&&Math.hypot(o.x-x,o.y-y)<.7)||(G.hostage&&G.hostage.alive&&Math.hypot(G.hostage.x-x,G.hostage.y-y)<.7); }
async function aiMoveTo(u,cx,cy){
  const path=planPath(u,cx,cy,false,u.team,u.moveLeft); if(!path||path.length<2) return;
  const end=path[path.length-1]; if(!spotClear(end.x,end.y,u)) return;
  if(enemyVisible(G.side,u)) await panTo(u.x,u.y,300);
  await moveAlong(u,path.slice(1),false);
}
async function aiDef(u){
  let t=bestTarget(u); if(t){ await aiFire(u,t); return; }
  const known=knownEnemies('def');
  if(u.ab==='ghost'&&u.charges>0&&!u.ghost&&G.round%3===1) await useAbility(u);
  if(u.ab==='gas'&&u.charges>0&&!u.abilityUsed){ const k=known.find(k=>Math.hypot(k.x-u.x,k.y-u.y)<=6&&losF(u.x,u.y,k.x,k.y,'def')); if(k){ if(enemyVisible(G.side,u)) await panTo(u.x,u.y,300); await useAbility(u,[k.x,k.y]); if(G.over) return; } }
  if(known.length){
    known.sort((a,b)=>Math.hypot(a.x-u.x,a.y-u.y)-Math.hypot(b.x-u.x,b.y-u.y)); const k=known[0], w=WEAP[u.weapon];
    const leash=u.ab!=='ghost'&&G.hostage&&G.hostage.alive&&!G.hostage.escort;
    if(Math.hypot(k.x-u.x,k.y-u.y)<=moveRange(u)+w.range+2){
      const {D}=dijkstra(Math.round(u.x),Math.round(u.y),false,blockedSet('def')); let best=null,bs=-1e9;
      for(let i=0;i<W*H;i++){ if(D[i]>u.moveLeft-.3) continue; const x=i%W, y=(i/W)|0; if(occupiedCell(x,y,u)) continue; const d=Math.hypot(x-k.x,y-k.y); if(d>w.range+1||!losF(x,y,k.x,k.y,'def')) continue; if(leash&&Math.hypot(x-G.hostage.x,y-G.hostage.y)>9) continue; const s=-D[i]*1.5-Math.abs(d-w.range*.6); if(s>bs){bs=s;best=i;} }
      if(best!==null&&D[best]>.5){ await aiMoveTo(u,best%W,(best/W)|0); if(!u.alive||G.over) return; }
    }
    if(!u.lockFacing||true){ u.facing=Math.atan2(k.y-u.y,k.x-u.x); G.ver++; }
    if(!u.acted){ t=bestTarget(u); if(t) await aiFire(u,t); }
  } else { u.facing=u.homeFacing+Math.sin(G.round*1.3+u.id)*.75; G.ver++; }
}
async function aiAtk(u){
  if(u.ab==='shield'&&!u.shield) await useAbility(u);
  let t=bestTarget(u); if(t){ await aiFire(u,t); return; }
  if(u.ab==='stim'&&u.charges>0&&!u.abilityUsed){ const a=G.units.find(a=>a.alive&&a.team==='atk'&&dist(a,u)<=2.5&&a.hp<a.maxHp-40); if(a) await useAbility(u,a); }
  const h=G.hostage;
  if(h&&h.alive&&!h.escort&&dist(u,h)<=1.6&&segFree(u,h,false)){ setEscort(u); log(`${u.kr}: 인질 확보`,u.team===G.side?'a':'f'); }
  let goals;
  if(h&&h.alive&&h.escort===u){ goals=[]; for(let i=0;i<W*H;i++) if(tile[i]==='extract') goals.push(i); }
  else if(h&&h.alive&&!h.escort) goals=[idx(Math.round(h.x),Math.round(h.y))];
  else { const ds=G.units.filter(e=>e.alive&&e.team==='def').sort((a,b)=>dist(a,u)-dist(b,u)); goals=ds.length?[idx(Math.round(ds[0].x),Math.round(ds[0].y))]:[idx(13,12)]; }
  const passC=(x,y)=>!blockCell(x,y,false);
  let Df=distField(goals,passC);
  const ux=Math.round(u.x), uy=Math.round(u.y);
  if((u.ab==='hammer'||u.ab==='thermite')&&u.charges>0&&!u.abilityUsed){
    const cur=Math.min(Df[idx(ux,uy)],999); let best=null,gain=0;
    for(const p of panels){ if(p.state!=='intact'||p.mirror||!panelNear(p,u)) continue; if(u.ab==='hammer'&&p.reinforced) continue; if(u.ab==='thermite'&&p.electrified) continue;
      let m=1e9; for(const c of p.cells) for(const [dx,dy] of DIR4){ const nx=c.x+dx, ny=c.y+dy; if(passC(nx,ny)&&Math.hypot(nx-u.x,ny-u.y)>1.2) m=Math.min(m,Df[idx(nx,ny)]); }
      const g=cur-(m+2); if(g>gain){gain=g;best=p;} }
    if(best&&gain>=4){ if(enemyVisible(G.side,u)) await panTo(u.x,u.y,300); await useAbility(u,best); if(G.over) return; Df=distField(goals,passC); }
  }
  if(u.ab==='launcher'&&u.charges>0&&!u.abilityUsed&&Math.random()<.5){
    const cur=Df[idx(ux,uy)]; const cand=panels.filter(p=>p.state==='intact'&&!p.mirror&&throwClear(u,p)&&p.cells.some(c=>DIR4.some(([dx,dy])=>passC(c.x+dx,c.y+dy)&&Df[idx(c.x+dx,c.y+dy)]<cur-5)));
    if(cand.length){ if(enemyVisible(G.side,u)) await panTo(u.x,u.y,300); await useAbility(u,cand[(Math.random()*cand.length)|0]); if(G.over) return; }
  }
  const {D}=dijkstra(ux,uy,false,blockedSet('atk')); let best=null,bv=1e9;
  for(let i=0;i<W*H;i++){ if(D[i]>u.moveLeft-.3||Df[i]>=1e9) continue; const x=i%W, y=(i/W)|0; if(occupiedCell(x,y,u)) continue; const v=Df[i]*10+D[i]; if(v<bv){bv=v;best=i;} }
  if(best!==null&&best!==idx(ux,uy)){ await aiMoveTo(u,best%W,(best/W)|0); if(!u.alive||G.over) return; }
  if(h&&h.alive&&!h.escort&&dist(u,h)<=1.6&&segFree(u,h,false)){ setEscort(u); log(`${u.kr}: 인질 확보`,u.team===G.side?'a':'f'); }
  if(!u.acted){ t=bestTarget(u); if(t) await aiFire(u,t); }
}
function distField(goals,passFn){ const D=new Int32Array(W*H).fill(1e9), q=[]; for(const g of goals){ D[g]=0; q.push(g); }
  while(q.length){ const c=q.shift(), cx=c%W, cy=(c/W)|0; for(const [dx,dy] of DIR4){ const nx=cx+dx, ny=cy+dy; if(nx<0||ny<0||nx>=W||ny>=H) continue; const n=idx(nx,ny); if(D[n]<=D[c]+1||!passFn(nx,ny)) continue; D[n]=D[c]+1; q.push(n); } }
  return D; }
function deployDrone(u){ const d={id:'d'+u.id,team:'atk',owner:u,x:u.x,y:u.y,rx:u.x,ry:u.y,alive:true,moveLeft:8}; G.drones.push(d); u.drone=d; u.droneSpent=true; G.ver++; return d; }
async function aiDrone(){
  if(G.round>2) return; const u=G.units.find(u=>u.alive&&u.team==='atk'&&!u.droneSpent); if(!u) return;
  const d=deployDrone(u), h=G.hostage; const tgt=h&&h.alive?h:G.units.find(e=>e.alive&&e.team==='def'); if(!tgt) return;
  const p=planPath(d,Math.round(tgt.x),Math.round(tgt.y)-1,true,'atk',8); if(p) await moveDrone(d,p.slice(1));
}
async function aiPhase(team){
  await sleep(300); await aiShootGadgets(team); if(G.over) return;
  if(team==='atk') await aiDrone();
  const us=G.units.filter(u=>u.alive&&u.team===team).map(u=>({u,b:bestTarget(u)?1:0})).sort((a,b)=>b.b-a.b).map(o=>o.u);
  for(const u of us){ if(G.over) return; if(!u.alive) continue; if(team==='def') await aiDef(u); else await aiAtk(u); u.ended=true; G.ver++; updateHUD(); await sleep(120); }
}
function aiDefPrep(){
  const h=G.hostage;
  const cand=panels.filter(canReinforce).sort((a,b)=>Math.hypot(a.cx-h.x,a.cy-h.y)-Math.hypot(b.cx-h.x,b.cy-h.y));
  for(const p of cand.slice(0,10)){ p.reinforced=true; G.reinforceLeft--; }
  const volt=G.units.find(u=>u.key==='VOLT'), argus=G.units.find(u=>u.key==='ARGUS'), obs=G.units.find(u=>u.key==='OBSIDIAN');
  for(const [x,y] of [[11,14],[15,16]]){ G.electrics.push({x,y,r:1.6,discovered:false}); volt.charges--; for(const p of panels) if(p.reinforced&&p.cells.some(c=>Math.hypot(c.x-x,c.y-y)<=2.2)) p.electrified=true; }
  for(const [x,y] of [[8,12],[19,15]]){ G.cams.push({x,y,alive:true,discovered:false,placed:true}); argus.charges--; }
  const mp=panelAt(14,13); if(mp){ mp.mirror=true; mp.reinforced=false; obs.charges--; }
  bumpMap();
}

/* =========================================================
   PHASES
   ========================================================= */
function beginPhase(team){
  G.phase=team; G.phaseCount++;
  for(const u of G.units) if(u.alive&&u.team===team){ u.moveLeft=moveRange(u); u.acted=false; u.abilityUsed=false; u.gadgetUsed=false; u.ended=false; u.shield=false; u.ghost=false; u.stim=false; }
  for(const d of G.drones) if(d.team===team) d.moveLeft=8;
  G.marks=G.marks.filter(m=>m.until>G.phaseCount);
  if(team==='def'){ for(const g of G.gases) g.ttl--; G.gases=G.gases.filter(g=>g.ttl>0); refreshGas(); }
  if(team==='atk') for(const u of G.units) if(u.alive&&u.team==='atk'&&gasSet.has(idx(Math.round(u.x),Math.round(u.y)))){ u.hp=Math.max(0,u.hp-15); popup(u,'-15 GAS','#8cff5a'); log(`${u.kr}: 독가스 피해 15`,'w'); if(u.hp<=0) kill(u,{team:'def',kr:'독가스'}); }
  G.ver++;
}
async function phase(team){
  if(G.over) return;
  clearUI(); beginPhase(team); if(checkWin()) return; updateHUD();
  await showBanner(team);
  if(isHuman(team)){ G.busy=false; updateHUD(); const f=G.units.find(u=>u.alive&&u.team===team); if(f) panTo(f.x,f.y,500); }
  else { G.busy=true; updateHUD(); await aiPhase(team); if(G.over) return; await sleep(300); await endPhase(); }
}
async function endPhase(){
  if(G.over) return; G.busy=true; clearUI(); updateHUD();
  if(G.phase==='prep'){ spawnAttackers(true); G.round=1; await phase('atk'); return; }
  if(G.phase==='deploy'){ const rest=G.units.filter(a=>a.team==='atk'&&a.placed===false);
    for(const a of rest){ let s=ATK_SPAWN.map(([x,y])=>({x,y})).find(s=>deployValid(s.x,s.y,a)); if(!s) s={x:ATK_SPAWN[0][0]+Math.random(),y:ATK_SPAWN[0][1]}; await rappel(a,s.x,s.y,true); }
    heliLeave(); await sleep(500); G.round=1; await phase('atk'); return; }
  if(G.phase==='atk') await phase('def');
  else { G.round++; if(G.round>G.maxRound){ finish('def','제한 시간이 종료되었습니다. 공격팀이 인질을 구출하지 못했습니다.'); return; } await phase('atk'); }
}
