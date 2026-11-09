/* =========================================================
   SOUND
   ========================================================= */
let AC=null, muted=false, noiseBuf=null;
function sfx(type){
  if(muted||G.fast) return;
  try{
    AC=AC||new (window.AudioContext||window.webkitAudioContext)(); const t=AC.currentTime;
    if(!noiseBuf){ noiseBuf=AC.createBuffer(1,AC.sampleRate*.6,AC.sampleRate); const d=noiseBuf.getChannelData(0); for(let i=0;i<d.length;i++) d[i]=Math.random()*2-1; }
    const g=AC.createGain(); g.connect(AC.destination);
    const noise=(len,freq,vol,type='bandpass')=>{ const s=AC.createBufferSource(); s.buffer=noiseBuf; const f=AC.createBiquadFilter(); f.type=type; f.frequency.value=freq; s.connect(f); f.connect(g); g.gain.setValueAtTime(vol,t); g.gain.exponentialRampToValueAtTime(.001,t+len); s.start(t); s.stop(t+len); };
    const tone=(f0,f1,len,vol,wave='square')=>{ const o=AC.createOscillator(); o.type=wave; o.frequency.setValueAtTime(f0,t); o.frequency.exponentialRampToValueAtTime(f1,t+len); o.connect(g); g.gain.setValueAtTime(vol,t); g.gain.exponentialRampToValueAtTime(.001,t+len); o.start(t); o.stop(t+len); };
    if(type==='shot') noise(.09,1800,.22); else if(type==='boom') noise(.55,300,.5,'lowpass'); else if(type==='hit') noise(.12,600,.3,'lowpass');
    else if(type==='ui') tone(1200,1800,.06,.05); else if(type==='click') tone(900,700,.04,.05); else if(type==='step') tone(180,120,.03,.02,'triangle');
    else if(type==='alert'){ tone(880,880,.12,.07); setTimeout(()=>sfx('alert2'),110); } else if(type==='alert2') tone(1320,1320,.14,.07);
    else if(type==='whoosh'){ const s=AC.createBufferSource(); s.buffer=noiseBuf; const f=AC.createBiquadFilter(); f.type='bandpass'; f.frequency.setValueAtTime(200,t); f.frequency.exponentialRampToValueAtTime(1800,t+.5); f.frequency.exponentialRampToValueAtTime(300,t+1.1); s.connect(f); f.connect(g); g.gain.setValueAtTime(.01,t); g.gain.linearRampToValueAtTime(.5,t+.5); g.gain.exponentialRampToValueAtTime(.001,t+1.2); s.loop=true; s.start(t); s.stop(t+1.2); }
    else if(type==='crit'){ tone(2400,1200,.18,.09,'sawtooth'); noise(.25,900,.5,'lowpass'); setTimeout(()=>sfx('crit2'),60); }
    else if(type==='crit2') tone(1800,2600,.2,.06,'square');
    else if(type==='zap') noise(.3,3500,.18,'highpass'); else if(type==='gas') noise(.5,900,.12,'lowpass');
  }catch(e){}
}

/* =========================================================
   HUD
   ========================================================= */
const $=s=>document.querySelector(s);
const m1=v=>(Math.round(v*10)/10).toFixed(1);
function log(msg,cls){ const el=$('#log'); const d=document.createElement('div'); d.className=cls||''; d.textContent=msg; el.appendChild(d); while(el.children.length>7) el.firstChild.remove(); }
function buildRoster(){
  const el=$('#roster'); el.innerHTML='';
  G.units.filter(u=>u.team===G.side).forEach((u,i)=>{
    const d=document.createElement('div'); d.className='rcard'; d.dataset.id=u.id; d.style.setProperty('--c',u.c); d.style.animationDelay=(i*60)+'ms';
    d.innerHTML=`<div class="rc-por">${opArtSVG(u.key,true)}<span>${u.key.slice(0,2)}</span></div><div class="rc-body"><div class="rc-top"><b>${u.key}</b><small>${u.kr} · ${u.role}</small></div><div class="bar"><i></i></div><div class="rc-st"><span class="hpn"></span><span class="st"></span></div></div>`;
    d.onclick=()=>{ if(!u.alive) return; sfx('click'); if(playerCanAct()&&!u.ended) selectUnit(u); else { panTo(u.x,u.y,400); showUnitPanel(u); } };
    el.appendChild(d);
  });
}
function updateHUD(){
  if(G.state!=='game') return;
  for(const d of document.querySelectorAll('.rcard')){ const u=G.units.find(x=>x.id==d.dataset.id); if(!u) continue;
    d.querySelector('.bar i').style.width=(100*u.hp/u.maxHp)+'%'; d.querySelector('.hpn').textContent=`${u.hp}/${u.maxHp}`;
    const st=d.querySelector('.st'), my=G.phase===u.team||(G.phase==='prep'&&u.team==='def');
    if(G.phase==='deploy'){ st.textContent=u.placed===false?'헬기 탑승 중':'강하 완료'; st.className='st'; d.classList.toggle('sel',G.ui.unit===u); d.classList.remove('ended'); continue; }
    st.textContent=!u.alive?'':u.ended&&my?'행동 종료':my?`이동 ${m1(u.moveLeft)}m`:u.shield?'방패':u.ghost?'교란':''; st.className='st'+(u.ended?' end':'');
    d.classList.toggle('sel',G.ui.unit===u); d.classList.toggle('ended',u.alive&&u.ended&&my); d.classList.toggle('dead',!u.alive); }
  const pb=$('#phaseBadge'), mine=G.phase===G.side||G.phase==='prep'||G.phase==='deploy';
  pb.className=(G.phase==='prep'||G.phase==='deploy')?'prep':mine?'':'foe';
  pb.innerHTML=G.phase==='deploy'?'DEPLOYMENT<small>공격팀 배치 단계</small>':G.phase==='prep'?'PREPARATION<small>방어 준비 단계</small>':G.phase==='atk'?`ATTACKER PHASE<small>공격팀 차례${mine?' · 아군':' · 적'}</small>`:`DEFENDER PHASE<small>방어팀 차례${mine?' · 아군':' · 적'}</small>`;
  $('#roundNum').textContent=String(Math.max(0,G.round)).padStart(2,'0'); $('#roundMax').textContent='/'+G.maxRound;
  const obj=G.side==='atk'?'① 인질을 구출 지점(EXFIL)까지 호송<br>② 또는 방어팀 전원 제압':`① ${G.maxRound}라운드 종료까지 인질 사수<br>② 또는 공격팀 전원 제압`;
  $('#objective').innerHTML=`<b>OBJECTIVE // 둘 중 하나 달성 시 승리</b>${obj}${G.phase==='prep'?`<br><span style="color:var(--acc)">남은 강화 ${G.reinforceLeft}/10</span>`:''}${G.phase==='deploy'?'<br><span style="color:var(--acc)">건물 외부 원하는 위치에 대원 배치</span>':''}`;
  const pip=team=>G.units.filter(u=>u.team===team).map(u=>`<i class="${u.alive?'':'off'}"></i>`).join('');
  const ft=other(G.side);
  $('#teamCount').innerHTML=`<span style="color:var(--ally)">ALLY<span class="pips">${pip(G.side)}</span></span><span style="color:var(--foe)">ENEMY<span class="pips">${G.units.some(u=>u.team===ft)?pip(ft):'<i class="off"></i>'.repeat(5)}</span></span>`;
  $('#modeTag').textContent=G.side==='atk'?'공격팀 · 인질 구출 작전':'방어팀 · 인질 사수 작전';
  const eb=$('#endBtn'); eb.disabled=!playerCanAct(); const pre=G.phase==='prep'||G.phase==='deploy'; eb.querySelector('b').textContent=pre?'START OP':'END TURN'; eb.querySelector('small').textContent=pre?'작전 개시':'턴 종료';
  const my=G.units.filter(u=>u.alive&&u.team===G.side); eb.classList.toggle('pulse',playerCanAct()&&my.length>0&&my.every(u=>u.ended));
  $('#busyTag').classList.toggle('hidden',!(G.busy&&!G.over&&G.phase!==G.side&&G.phase!=='prep'&&G.phase!=='deploy'));
  if(G.ui.panelUnit&&!$('#unitPanel').classList.contains('hidden')) showUnitPanel(G.ui.panelUnit,true);
  if(G.ui.mode==='menu'&&G.ui.unit) renderMenu(G.ui.unit);
}
function showUnitPanel(u,noAnim){
  const el=$('#unitPanel'), w=WEAP[u.weapon], foe=u.team!==G.side; G.ui.panelUnit=u;
  el.classList.remove('hidden'); el.classList.toggle('foe',foe);
  const max=OPS[u.team].find(o=>o.key===u.key).charges, pipsN=u.charges>=99?'∞':'●'.repeat(u.charges)+'○'.repeat(Math.max(0,max-u.charges));
  const tags=[]; if(u.shield) tags.push('<span>방패 전개</span>'); if(u.ghost) tags.push('<span>전파 교란</span>'); if(u.stim) tags.push('<span class="w">자극제</span>'); if(u.lockFacing) tags.push('<span class="w">시야 고정</span>'); if(G.hostage&&G.hostage.escort===u) tags.push('<span class="w">인질 호송</span>'); if(u.ended) tags.push('<span class="r">행동 종료</span>');
  el.style.setProperty('--c',u.c);
  el.innerHTML=`<div class="up-head"><div class="up-por">${opArtSVG(u.key,true)}</div><div class="up-id"><div class="up-code">OP-${String(u.id).padStart(3,'0')} // ${u.team==='atk'?'ASSAULT':'DEFENSE'} // ${WEAP[u.weapon].name.split(' ')[0]}</div><div class="up-name">${u.key}</div><div class="up-kr">${u.kr}<em>${u.role} · ${u.team==='atk'?'공격팀':'방어팀'}</em></div></div><div class="up-lv"><b>${u.hp}</b><small>HP<br>/${u.maxHp}</small></div></div>
  <div class="up-bd"><div class="up-hp"><div class="bar" style="--c:${foe?'var(--foe)':'var(--ally)'}"><i style="width:${100*u.hp/u.maxHp}%"></i></div><span>${u.hp} / ${u.maxHp}</span></div>
  <div class="up-grid"><div><label>MOVE</label><b>${foe?moveRange(u):m1(u.moveLeft)}</b><small>/${moveRange(u)}m</small></div><div><label>SPD</label><b>${speedOf(u)}</b></div><div><label>DMG</label><b>${w.dmg}×${w.shots}</b></div><div><label>ACC</label><b>${w.acc}</b></div><div><label>RNG</label><b>${w.range}</b></div></div>
  <div class="up-wpn">${w.name}${w.breach?' · 근접 사격으로 일반 벽 파괴':''}</div>
  <div class="up-ab"><div class="ab-name">✦ ${u.abName}<span class="ab-ch">${foe?'':pipsN}</span></div><div class="ab-desc">${u.desc}</div></div>
  ${tags.length?`<div class="up-tags">${tags.join('')}</div>`:''}</div>`;
  if(!noAnim){ el.classList.remove('swap'); void el.offsetWidth; el.classList.add('swap'); }
}
function commands(u){
  const prep=G.phase==='prep', L=[], h=G.hostage;
  L.push({id:'move',kr:'이동',en:'MOVE',ic:'➜',ok:u.moveLeft>.3,sub:m1(u.moveLeft)+'m'});
  L.push({id:'face',kr:'시야 변경',en:'VIEW',ic:'◐',ok:true,sub:u.lockFacing?'고정':''});
  if(!prep) L.push({id:'fire',kr:'공격',en:'FIRE',ic:'✛',ok:!u.acted});
  L.push({id:'skill',kr:u.abName,en:'ABILITY',ic:'✦',ok:u.charges>0&&(prep||!u.abilityUsed)&&!(u.ab==='shield'&&u.shield)&&!(u.ab==='ghost'&&u.ghost),sub:u.charges>=99?'∞':u.charges+'회'});
  if(u.team==='atk') L.push({id:'gadget',kr:'드론',en:'DRONE',ic:'⌖',ok:!u.gadgetUsed&&(!u.droneSpent||(u.drone&&u.drone.alive&&u.drone.moveLeft>.3)),sub:u.drone&&u.drone.alive?'조종':u.droneSpent?'소실':'전개'});
  else L.push({id:'gadget',kr:'감시카메라',en:'CAMERA',ic:'◉',ok:G.cams.some(c=>c.alive),sub:G.cams.filter(c=>c.alive).length+'대'});
  if(u.team==='def'&&prep) L.push({id:'reinforce',kr:'벽 강화',en:'REINFORCE',ic:'▦',ok:G.reinforceLeft>0&&panels.some(p=>canReinforce(p)&&panelNear(p,u)),sub:G.reinforceLeft+'/10'});
  if(u.team==='atk'&&h&&h.alive&&!h.escort&&dist(u,h)<=1.6) L.push({id:'escort',kr:'인질 확보',en:'SECURE',ic:'♥',ok:segFree(u,h,false)});
  L.push({id:'wait',kr:'대기',en:'END',ic:'■',ok:true});
  return L;
}
function renderMenu(u){
  const el=$('#cmdMenu'), L=commands(u), fresh=el.dataset.uid!=String(u.id)||el.classList.contains('hidden');
  el.dataset.uid=u.id; el.classList.remove('hidden');
  el.innerHTML=`<div class="ch">${u.key} // COMMAND</div>`+L.map((c,i)=>`<button class="cmd" data-c="${c.id}" ${c.ok?'':'disabled'} style="animation-delay:${fresh?i*35:0}ms;${fresh?'':'animation:none'}"><span class="k">${i+1}</span><span class="ic">${c.ic}</span><span class="t">${c.kr}<span class="en">${c.en}</span></span><span class="sub">${c.sub||''}</span></button>`).join('');
  el.querySelectorAll('.cmd').forEach(b=>b.onclick=e=>{ e.stopPropagation(); doCommand(u,b.dataset.c); });
  positionMenu();
}
function positionMenu(){ const el=$('#cmdMenu'), u=G.ui.unit; if(el.classList.contains('hidden')||!u) return; const s=proj(u.rx,.5,u.ry), w=el.offsetWidth, h=el.offsetHeight; el.style.left=clamp(s.x+60,230,VW-w-12)+'px'; el.style.top=clamp(s.y-h/2,90,VH-h-90)+'px'; }
function hideMenu(){ $('#cmdMenu').classList.add('hidden'); }
function setHint(main,sub,btns){ const el=$('#hint'); el.classList.remove('hidden'); el.innerHTML=`<div style="flex:1">${main}${sub?`<small>${sub}</small>`:''}</div>`+(btns||[]).map(b=>`<button class="hb ${b.on?'on':''}" data-h="${b.id}">${b.label}</button>`).join(''); el.querySelectorAll('.hb').forEach(b=>b.onclick=e=>{ e.stopPropagation(); hintAction(b.dataset.h); }); el.style.animation='none'; void el.offsetWidth; el.style.animation=''; }
function hideHint(){ $('#hint').classList.add('hidden'); }
function playerCanAct(){ return G.state==='game'&&!G.busy&&!G.over&&!G.modal&&(G.phase===G.side||(G.phase==='prep'&&G.side==='def')||(G.phase==='deploy'&&G.side==='atk')); }

function showBanner(team){
  return new Promise(res=>{ const el=$('#banner'); let bg, main, kr, sub=`ROUND ${String(G.round).padStart(2,'0')} / ${G.maxRound}`;
    if(team==='prep'){ bg='#ffc81e'; main='PREPARATION'; kr='방어 준비 단계 — 벽 강화·장치 설치·배치'; sub='BEFORE BREACH'; }
    else if(team==='deploy'){ bg='#ffc81e'; main='DEPLOYMENT'; kr='공격팀 배치 — 건물 외부 원하는 위치에 대원을 배치'; sub='BEFORE BREACH'; }
    else { const mine=team===G.side; bg=mine?'#1f96e8':'#e5364d'; main=team==='atk'?'ATTACKER PHASE':'DEFENDER PHASE'; kr=mine?'아군 행동 개시':'적 행동 개시'; }
    el.innerHTML=`<div class="bn-strip" style="--bgc:${bg}"><div class="bn-in"><div class="bn-blk"></div><div><div class="bn-sub">${sub} // TAC-OS PHASE SYNC</div><div class="bn-main">${main}</div><div class="bn-kr">${kr}</div></div><div class="bn-blk"></div></div></div>`;
    el.classList.add('show'); sfx('ui'); setTimeout(()=>{ el.classList.remove('show'); res(); },dur(1700)); });
}
function showContact(u,e,kind){
  return new Promise(res=>{ const el=$('#contact'), pm=u.team===G.side; let cls='', sub;
    if(kind===1){ sub=pm?`${u.kr}이(가) 먼저 발견 — 기습 공격 가능`:`${u.kr}에게 발견됨 — 적의 기습!`; cls=pm?'m1':''; }
    else if(kind===2){ sub=`상호 조우 — 속도 판정 (${u.kr} ${speedOf(u)} : ${e.kr} ${speedOf(e)})`; cls='m2'; }
    else { sub=pm?`${e.kr}에게 발각됨 — 적의 선제 공격!`:`${e.kr}이(가) 적을 포착 — 선제 공격!`; cls=pm?'':'m1'; }
    el.innerHTML=`<div class="ct-in ${cls}"><div class="ct-mark">!</div><div><div class="ct-en">CONTACT</div><div class="ct-kr">적과 조우했습니다!</div><div class="ct-sub">${sub}</div></div></div>`;
    el.classList.add('show'); setTimeout(()=>{ el.classList.remove('show'); res(); },dur(1100)); });
}
function modal(title,en,body,buttons){
  return new Promise(res=>{ const el=$('#dialog'); G.modal=true;
    el.innerHTML=`<div class="mbox brk"><div class="mh">${title}<small>${en}</small></div><div class="mb">${body}</div><div class="mbtns">${buttons.map((b,i)=>`<button data-i="${i}" class="${b.cls||''}">${b.label}</button>`).join('')}</div></div>`;
    el.classList.add('show');
    const done=v=>{ el.classList.remove('show'); G.modal=false; document.removeEventListener('keydown',kd,true); sfx('click'); res(v); };
    const kd=e=>{ if(e.key==='Enter'||e.key==='y'){ e.stopPropagation(); done(buttons[0].v); } else if(e.key==='Escape'||e.key==='n'){ e.stopPropagation(); done(buttons[buttons.length-1].v); } };
    document.addEventListener('keydown',kd,true);
    el.querySelectorAll('button').forEach(b=>b.onclick=()=>done(buttons[+b.dataset.i].v)); });
}
function askEngage(u,e){
  if(G.autoplay) return Promise.resolve(true);
  const tr=traceLine(u.x,u.y,e.x,e.y), c=hitChance(u,e,{preempt:true,walls:tr.walls}), w=WEAP[u.weapon];
  return modal('적 발견 — 공격하시겠습니까?','AMBUSH',`${u.kr} → <span style="color:var(--foe)">${e.kr}</span> (${e.hp}/${e.maxHp})<br>선제 공격: 명중 <b>${c}%</b> · 피해 <b>${Math.round(w.dmg*1.4)}×${w.shots}</b><br>헤드샷 확률 <b style="color:var(--foe)">${headshotChance(u,e,{preempt:true,walls:tr.walls})}%</b> <small style="color:var(--dimp)">(선제 공격 보너스 적용 · 피해 ×2)</small>${tr.walls?` · 벽 관통 ${tr.walls}`:''}<br><small style="color:var(--dim)">공격하면 이 자리에서 이동을 종료합니다. 무시하면 그린 경로대로 계속 이동합니다.</small>`,[{label:'공격',v:true,cls:'red'},{label:'무시하고 이동',v:false}]);
}
function combatShow(a,t,ch,dmg,shots,pre,walls,blocked,hsC){
  const el=$('#combat'), ca=a.team===G.side?'var(--ally)':'var(--foe)', ct=t.team==='host'?'#ffb13b':t.team===G.side?'var(--ally)':'var(--foe)';
  el.innerHTML=`<div class="cb-side l" style="--c:${ca}"><div class="cb-name">${a.key}</div><div class="cb-kr">${a.kr} · ${WEAP[a.weapon].name}</div><div class="bar" style="--c:${ca}"><i style="width:${100*a.hp/a.maxHp}%"></i></div><div class="cb-hpn">${a.hp}/${a.maxHp}</div></div>
  <div class="cb-mid"><div class="cb-tag ${pre?'p':''}">${pre?'PREEMPTIVE · 선제 공격 ×1.4':'ENGAGE · 교전'}</div><div class="cb-stat">명중 <b>${ch}%</b> · <b>${dmg}</b>×${shots}</div>${hsC?`<div class="cb-hs">HEADSHOT <b>${hsC}%</b> · 피해 ×2</div>`:''}<div class="cb-res" id="cbRes">${walls?`벽 관통 ${walls} (-${walls*22}%)`:''}${blocked?' · 정면 방패':''}</div></div>
  <div class="cb-side r" style="--c:${ct}"><div class="cb-name">${t.key}</div><div class="cb-kr">${t.kr}</div><div class="bar" style="--c:${ct}"><i id="cbHp" style="width:${100*t.hp/t.maxHp}%"></i></div><div class="cb-hpn" id="cbHpn">${t.hp}/${t.maxHp}</div></div>`;
  el.classList.remove('show'); void el.offsetWidth; el.classList.add('show');
}
function combatUpdate(t){ const i=$('#cbHp'); if(i){ i.style.width=(100*t.hp/t.maxHp)+'%'; $('#cbHpn').textContent=`${t.hp}/${t.maxHp}`; } }
function combatResult(h,n,tot,bl,hs){ const r=$('#cbRes'); if(r) r.innerHTML=bl?'BLOCKED — 방패가 막아냄':`${h}/${n} HIT · -${tot}${hs?` <span class="cb-crit">HEADSHOT ×${hs}</span>`:''}`; }
function combatCrit(n){ const c=$('#combat'); if(!c) return; c.classList.remove('crit'); void c.offsetWidth; c.classList.add('crit'); }
function combatHide(){ $('#combat').classList.remove('show'); }

/* =========================================================
   SELECTION / COMMANDS
   ========================================================= */
function showOpArt(u){
  const el=$('#opArt'); if(!u||u.team!==G.side){ hideOpArt(); return; }
  if(el.dataset.k===u.key&&!el.classList.contains('hidden')) return;
  el.dataset.k=u.key; el.style.setProperty('--c',u.c);
  el.innerHTML=`<div class="oa-tag"><i></i>OPERATOR FILE // OP-${String(u.id).padStart(3,'0')} // ${u.team==='atk'?'ASSAULT':'DEFENSE'}</div><div class="oa-img">${opArtSVG(u.key)}</div><div class="oa-name"><b>${u.key}</b><span>${u.kr} · ${u.role}</span><em>✦ ${u.abName}</em></div>`;
  el.classList.remove('hidden','show'); void el.offsetWidth; el.classList.add('show');
}
function hideOpArt(){ const el=$('#opArt'); if(!el) return; el.classList.add('hidden'); el.dataset.k=''; }
function clearUI(){ G.ui={mode:'idle'}; hideMenu(); hideOpArt(); hideHint(); $('#camPanel').classList.add('hidden'); $('#tip').classList.add('hidden'); }
function deselect(){ clearUI(); $('#unitPanel').classList.add('hidden'); updateHUD(); }
function selectUnit(u){
  if(!u.alive) return; if(G.phase==='deploy'){ enterDeploy(u); return; } const changed=G.ui.unit!==u;
  G.ui={mode:'menu',unit:u,panelUnit:u}; hideHint(); $('#camPanel').classList.add('hidden');
  showUnitPanel(u,!changed); showOpArt(u); renderMenu(u); if(changed) panTo(u.x,u.y,350); updateHUD(); sfx('click');
}
function startMoveMode(u,mover,isDrone){
  const {D}=dijkstra(Math.round(mover.x),Math.round(mover.y),isDrone,isDrone?null:blockedSet(u.team));
  G.ui={mode:'move',unit:u,panelUnit:u,mover,isDrone,budget:mover.moveLeft,reach:D,reachKey:'r'+now(),path:[]};
}
function doCommand(u,c){
  if(!playerCanAct()) return; sfx('click');
  const btn=commands(u).find(x=>x.id===c); if(!btn||!btn.ok) return;
  hideMenu();
  if(c==='move'){ startMoveMode(u,u,false); setHint('이동 경로를 자유롭게 그리십시오',`좌클릭한 채 드래그하여 경로 그리기 · 한 번 클릭하면 자동 경로 · 이동력 ${m1(u.moveLeft)}m`,[{id:'cancel',label:'ESC 취소'}]); }
  else if(c==='face') enterFacing(u,false);
  else if(c==='fire'){ G.ui={mode:'fire',unit:u,panelUnit:u,aim:u.facing,backup:u.facing,cast:fireCast(u,u.facing)}; setHint('공격 방향을 선택하십시오','마우스로 조준 후 좌클릭 사격 · 강화벽을 제외한 벽은 관통 (명중률 감소)',[{id:'cancel',label:'ESC 취소'}]); }
  else if(c==='skill'){ const sp=abilitySpec(u);
    if(sp.kind==='instant'){ G.busy=true; useAbility(u).then(()=>{ G.busy=false; afterAction(u); }); return; }
    G.ui={mode:'ability',unit:u,panelUnit:u,spec:sp};
    if(sp.kind==='panel'){ G.ui.validPanels=new Set(panels.filter(sp.valid).map(p=>p.id)); if(!G.ui.validPanels.size){ log('선택 가능한 벽이 없습니다 — 벽에 더 가까이 이동하십시오','w'); selectUnit(u); return; } }
    if(sp.kind==='ally'&&!G.units.some(sp.valid)){ log('대상 아군이 없습니다','w'); selectUnit(u); return; }
    setHint(sp.hint,sp.sub,[{id:'cancel',label:'ESC 취소'}]); }
  else if(c==='gadget'){
    if(u.team==='atk'){ let d=u.drone&&u.drone.alive?u.drone:null; if(!d){ d=deployDrone(u); log(`${u.kr}: 드론 전개`,'a'); }
      startMoveMode(u,d,true); setHint('드론 이동 경로를 그리십시오',`드론 주변 원형 시야 · 발견한 적은 이번 턴 동안 맵에 표시 · 이동력 ${m1(d.moveLeft)}m`,[{id:'cancel',label:'ESC 취소'}]); }
    else openCamPanel(u); }
  else if(c==='reinforce'){ G.ui={mode:'reinforce',unit:u,panelUnit:u,validPanels:new Set(panels.filter(p=>canReinforce(p)&&panelNear(p,u)).map(p=>p.id))}; setHint('강화할 벽을 선택해주십시오',`남은 강화 ${G.reinforceLeft}/10 · 벽마다 필요한 강화 수가 다릅니다`,[{id:'cancel',label:'ESC 취소'}]); }
  else if(c==='escort'){ setEscort(u); log(`${u.kr}: 인질 확보 — 구출 지점(EXFIL)으로 호송하십시오`,'a'); sfx('ui'); popup(G.hostage,'SECURED','#ffb13b'); selectUnit(u); }
  else if(c==='wait') enterFacing(u,true);
}
function enterFacing(u,endAfter,keepBackup){
  const bk=keepBackup!=null?keepBackup:u.facing;
  G.ui={mode:'facing',unit:u,panelUnit:u,endAfter,backup:bk}; hideMenu();
  setHint(endAfter?'행동 종료 전 시야각을 설정하십시오':'시야각을 조정하십시오','마우스로 방향 지정 · 좌클릭 확정 · ESC '+(endAfter?'현재 방향으로 종료':'취소'),[{id:'lock',label:`시야 고정 ${u.lockFacing?'ON':'OFF'} [F]`,on:u.lockFacing},{id:'cancel',label:'ESC'}]);
}
function endUnit(u){ u.ended=true; u.moveLeft=0; G.ver++; deselect(); const my=G.units.filter(x=>x.alive&&x.team===G.side); if(my.every(x=>x.ended)) log('모든 대원이 행동을 마쳤습니다. 턴을 종료하십시오.','w'); updateHUD(); }
function afterAction(u){
  if(G.over) return; updateHUD();
  if(!u.alive){ deselect(); return; }
  if(G.phase!=='prep'&&u.moveLeft<=0&&!u.ended){ enterFacing(u,true); return; }
  if(u.ended){ deselect(); return; }
  selectUnit(u);
}
function hintAction(id){
  if(id==='cancel') cancelMode();
  else if(id==='lock'){ const u=G.ui.unit; u.lockFacing=!u.lockFacing; sfx('click'); enterFacing(u,G.ui.endAfter,G.ui.backup); }
}
function cancelMode(){
  const ui=G.ui; if(!playerCanAct()) return;
  if(ui.mode==='facing'){ ui.unit.facing=ui.backup; G.ver++; if(ui.endAfter){ endUnit(ui.unit); return; } selectUnit(ui.unit); return; }
  if(ui.mode==='menu'||ui.mode==='deploy'){ deselect(); return; }
  if(ui.mode==='fire'){ ui.unit.facing=ui.backup; G.ver++; $('#tip').classList.add('hidden'); }
  if(ui.unit&&ui.unit.alive&&!ui.unit.ended){ $('#camPanel').classList.add('hidden'); selectUnit(ui.unit); } else deselect();
}
function openCamPanel(u){
  G.ui={mode:'cams',unit:u,panelUnit:u}; const el=$('#camPanel'); el.classList.remove('hidden');
  const cams=G.cams.filter(c=>c.alive);
  el.innerHTML=`<div class="h">CAMERA FEED<span>감시카메라</span></div>`+cams.map((c,i)=>`<button data-i="${i}"><span>CAM-${String(i+1).padStart(2,'0')} ${c.placed?'(설치)':'(고정)'}</span><span class="rec">● REC</span></button>`).join('');
  el.querySelectorAll('button').forEach(b=>b.onclick=()=>{ const c=cams[+b.dataset.i]; panTo(c.x,c.y,400); sfx('ui'); fxRing(c.x,c.y,0xff6b8a,700,3.2);
    let n=0; for(const e of G.units) if(e.alive&&e.team==='atk'&&!e.ghost&&Math.hypot(e.x-c.x,e.y-c.y)<=3.2&&losF(c.x,c.y,e.x,e.y,'def')){ addMark('def',e.x,e.y,'drone'); e.revealT=now()+2000; n++; }
    log(`CAM-${String(+b.dataset.i+1).padStart(2,'0')}: ${n?`적 ${n}명 포착`:'이상 없음'}`,n?'w':''); });
  setHint('확인할 감시카메라를 선택하십시오','카메라 주변 원형 시야 · 포착된 적은 맵에 표시',[{id:'cancel',label:'ESC 닫기'}]);
}


/* =========================================================
   CINEMATIC: helicopter insertion + rappel
   ========================================================= */
let skipRes=null;
function waitS(ms){ return new Promise(r=>{ if(G.cineSkip) return r(); const t=setTimeout(()=>{ skipRes=null; r(); },dur(ms)); skipRes=()=>{ clearTimeout(t); skipRes=null; r(); }; }); }
function skipCine(){ if(!G.cineOn||G.cineSkip) return; G.cineSkip=true; sfx('click'); if(skipRes) skipRes(); }
function caption(tag,main,sub){ const el=$('#cineCap'); el.innerHTML=`<div class="cc-tag">${tag}</div><div class="cc-main">${main}</div><div class="cc-sub">${sub}</div>`; el.classList.remove('show'); void el.offsetWidth; el.classList.add('show'); }
function fade(on,ms=600){ const f=$('#fade'); f.style.transitionDuration=dur(ms)+'ms'; f.classList.toggle('on',on); return new Promise(r=>setTimeout(r,dur(ms)+30)); }
const lerp=(a,b,p)=>a+(b-a)*p;
let HELI_P=null;
function heliPath(p){ p=clamp(p,0,1);
  const P=HELI_P||[{x:-2,y:44,z:9},{x:4,y:27,z:7},{x:9.5,y:13,z:7},{x:10.5,y:1.5,z:6}];
  const s=p*(P.length-1), i=Math.min(P.length-2,Math.floor(s)), q=s-i, a=P[i], b=P[i+1];
  return {x:lerp(a.x,b.x,q),y:lerp(a.y,b.y,q),z:lerp(a.z,b.z,q),hd:Math.atan2(b.y-a.y,b.x-a.x)};
}
async function playIntro(){
  G.cineOn=true; G.cineSkip=false; G.ver++; $('#cine').classList.add('show'); $('#hud').classList.add('hidden');
  const h=G.heli; h.vis=false; const HX=12.5, HZ=11;
  // one continuous shot: far orbit around the house; the helicopter enters from behind the camera mid-shot
  const TOTAL=dur(11500), HELI_AT=dur(4200), HELI_DUR=dur(6200);
  const camAt=p=>{ const e=p*p*(3-2*p), a=1.0+e*.7, R=44-e*6; return {a,px:HX+Math.cos(a)*R,py:20-e*5,pz:HZ+Math.sin(a)*R}; };
  const ca=camAt(HELI_AT/TOTAL), dx=Math.cos(ca.a), dz=Math.sin(ca.a), side=-Math.sin(ca.a), sidez=Math.cos(ca.a);
  HELI_P=[{x:HX+dx*54+side*3,y:HZ+dz*54+sidez*3,z:19},{x:HX+dx*30+side*2,y:HZ+dz*30+sidez*2,z:13},{x:HX+dx*6,y:HZ+dz*6,z:8.5},{x:10.5,y:1.5,z:6}];
  let t0=now(), started=false, whoosh=false;
  G.cineFn=t=>{ const el=t-t0, p=clamp(el/TOTAL,0,1), c=camAt(p);
    let lx=HX, ly=1.2, lz=HZ;
    if(el>=HELI_AT){ h.vis=true; const q0=clamp((el-HELI_AT)/HELI_DUR,0,1), q=1-Math.pow(1-q0,2.2), hp=heliPath(q); Object.assign(h,hp); h.tilt=-.32*Math.pow(1-q,1.2);
      if(!whoosh){ whoosh=true; sfx('whoosh'); } const k=.25*q; lx=lerp(HX,hp.x,k); ly=lerp(1.2,hp.z*.4,k); lz=lerp(HZ,hp.y,k); }
    G.cineCam={px:c.px,py:c.py,pz:c.pz,lx,ly,lz}; };
  const D=new Date(), pad=n=>String(n).padStart(2,'0'), days=['SUN','MON','TUE','WED','THU','FRI','SAT'];
  const ds=`${D.getFullYear()}.${pad(D.getMonth()+1)}.${pad(D.getDate())} ${days[D.getDay()]}`; const dEl=$('#chDate'); dEl.textContent=ds; dEl.dataset.t=ds;
  const tStart=now(); const tick=()=>{ const n=new Date(); $('#chTime').textContent=`LOCAL ${pad(n.getHours())}:${pad(n.getMinutes())}:${pad(n.getSeconds())} KST // T-00:0${Math.max(0,9-Math.floor((now()-tStart)/1000))}`; const el=now()-tStart, f=Math.floor(el/1000*30)%30; $('#chTC').textContent=`00:00:${pad(Math.floor(el/1000))}:${pad(f)}`; };
  tick(); const iv=setInterval(tick,50);
  const ttl=$('#chTitle'); ttl.classList.remove('off','on'); void ttl.offsetWidth; ttl.classList.add('on'); sfx('alert');
  await fade(false,1000);
  await waitS(1700);
  ttl.classList.remove('on'); ttl.classList.add('off');
  caption('TARGET // 목표 건물','교외 단독주택 — 인질 1명 억류','무장 인원 5명 추정 · 내부 벽 일부 강화 흔적');
  await waitS(1600);
  if(!G.cineSkip) caption('INSERTION // 강하','알파 팀, 목표 상공 진입','건물 외곽 원하는 지점에 로프로 강하합니다');
  await waitS(TOTAL/ (G.fast?0.02:1) - 4300);
  await fade(true,600); clearInterval(iv); ttl.classList.remove('on','off');
  G.cineFn=null; G.cineCam=null; Object.assign(h,heliPath(1)); h.vis=true; h.tilt=0; h.rope=0;
  $('#cine').classList.remove('show'); $('#cineCap').classList.remove('show');
  G.cineOn=false; G.ver++; G.cam.x=11; G.cam.y=6; G.cam.h=27;
  $('#hud').classList.remove('hidden'); updateHUD();
  await fade(false,600);
}
async function rappel(u,x,y,fast){
  const h=G.heli, d=Math.hypot(x-h.x,y-h.y), T=Math.min(1300,350+d*110)*(fast?.5:1);
  if(d>.05){ h.hd=Math.atan2(y-h.y,x-h.x); panTo(x,y,T); h.tilt=-.1; await tween(h,{x,y,z:6},T); h.tilt=0; }
  sfx('ui'); await tween(h,{rope:4.75},fast?160:320,true);
  u.x=x; u.y=y; u.rx=x; u.ry=y; u.rz=4.4; u.placed=true; u.facing=Math.atan2(11-y,13-x); G.ver++;
  await tween(u,{rz:0},fast?420:950,true);
  fxRing(x,y,0xd6c9a8,500,.9); sfx('step'); G.shake=.08; updateHUD();
  await tween(h,{rope:0},fast?140:280,true);
}
async function heliLeave(){ const h=G.heli; if(!h.vis) return; h.hd=Math.atan2(-12-h.y,h.x<13?-30:60-h.x); h.tilt=-.15; await tween(h,{x:h.x<13?-18:42,y:-12,z:9},1400); h.vis=false; h.tilt=0; }

/* =========================================================
   INPUT — free-form path drawing
   ========================================================= */
G.hover=null; G.mouse={x:0,y:0}; let down=null; const keys={};
function pickUnit(sx,sy){ let best=null,bd=1e9; for(const u of G.units){ if(!u.alive||!shownToPlayer(u)) continue; const s=proj(u.rx,.5,u.ry), d=Math.hypot(s.x-sx,s.y-sy); if(d<30&&d<bd){bd=d;best=u;} } return best; }
function extendPath(g){
  const ui=G.ui, P=ui.path; if(!P.length||!g) return;
  const q={x:g.fx,y:g.fy}, last=P[P.length-1];
  if(dist(last,q)<.12) return;
  for(let i=0;i<P.length-3;i++) if(dist(P[i],q)<.22){ P.length=i+1; return; }   // backtrack
  const rem=ui.budget-pathLen(P); if(rem<=.02) return;
  const bodyOk=(a,b)=>ui.isDrone||bodyClear(a,b,bodyObstacles(G.side,ui.mover));
  if(segFree(last,q,ui.isDrone)&&bodyOk(last,q)){ const d=dist(last,q); if(d<=rem) P.push(q); else P.push({x:last.x+(q.x-last.x)*rem/d,y:last.y+(q.y-last.y)*rem/d}); return; }
  const pl=planPath(last,q.x,q.y,ui.isDrone,G.side,rem); if(pl&&pl.length>1) P.push(...pl.slice(1));
}
async function onMoveRelease(){
  const ui=G.ui, P=ui.path; ui.dragging=false;
  if(P.length<=1&&G.hover){ const pl=planPath(ui.mover,G.hover.fx,G.hover.fy,ui.isDrone,G.side,ui.budget); if(pl) ui.path=pl; }
  const Q=ui.path;
  if(!ui.isDrone){ while(Q.length>1&&!spotClear(Q[Q.length-1].x,Q[Q.length-1].y,ui.mover)){ const a=Q[Q.length-2], b=Q[Q.length-1], d=dist(a,b); if(d<=.15) Q.pop(); else { b.x-= (b.x-a.x)/d*.15; b.y-=(b.y-a.y)/d*.15; } } }
  if(Q.length<=1||pathLen(Q)<.15){ ui.path=[]; return; }
  const L=pathLen(Q);
  const ok=await modal('이동하겠습니까?','CONFIRM MOVE',`${ui.isDrone?'드론':ui.unit.kr} · 경로 <b>${m1(L)}</b>m · 이동 후 잔여 이동력 ${m1(ui.budget-L)}m${ui.unit.lockFacing&&!ui.isDrone?'<br><small style="color:var(--acc)">시야 고정 중 — 현재 시야각을 유지하며 이동합니다</small>':''}`,[{label:'예',v:true},{label:'아니요',v:false}]);
  if(!ok){ deselect(); return; }
  const u=ui.unit, pts=Q.slice(1); G.busy=true; hideHint(); ui.reach=null; ui.reachKey=''; ui.path=[];
  if(ui.isDrone){ await moveDrone(ui.mover,pts); u.gadgetUsed=true; }
  else await moveAlong(u,pts,true);
  G.busy=false; if(G.over) return; afterAction(u);
}
function onClick(g,sx,sy){
  if(!playerCanAct()||!g) return; const ui=G.ui;
  if(G.phase==='deploy'){ const pu=pickUnit(sx,sy);
    if(pu&&pu.team===G.side&&pu!==ui.unit){ enterDeploy(pu); return; }
    if(ui.mode==='deploy'&&ui.unit){ const u=ui.unit; if(!deployValid(g.fx,g.fy,u)){ log('건물 외부의 빈 공간에만 배치할 수 있습니다','w'); return; }
      const gx=g.fx, gy=g.fy; G.busy=true; hideHint(); G.ui={mode:'busy',unit:u,panelUnit:u}; updateHUD();
      rappel(u,gx,gy).then(()=>{ G.busy=false; const next=G.units.find(o=>o.team==='atk'&&o.alive&&o.placed===false);
        if(next) enterDeploy(next); else { deselect(); setHint('배치를 확인한 뒤 [작전 개시]를 누르십시오','배치한 대원을 클릭하면 다시 강하시킬 수 있습니다',[]); } }); return; }
    if(pu&&pu.team===G.side){ enterDeploy(pu); return; }
    return; }
  if(ui.mode==='facing'){ const u=ui.unit; sfx('click'); if(ui.endAfter) endUnit(u); else selectUnit(u); return; }
  if(ui.mode==='fire'){ const u=ui.unit, ang=ui.aim; G.busy=true; hideHint(); $('#tip').classList.add('hidden'); G.ui={mode:'busy',unit:u};
    fireDirection(u,ang).then(()=>{ G.busy=false; if(G.over) return; endUnit(u); }); return; }
  if(ui.mode==='ability'){ const u=ui.unit, sp=ui.spec; let target=null;
    if(sp.kind==='panel'){ const p=panelAt(g.x,g.y); if(p&&ui.validPanels.has(p.id)) target=p; }
    else if(sp.kind==='point'){ if(sp.valid(g.fx,g.fy)) target=[g.fx,g.fy]; }
    else if(sp.kind==='ally'){ const a=pickUnit(sx,sy); if(a&&sp.valid(a)) target=a; }
    if(!target){ sfx('click'); return; }
    G.busy=true; hideHint(); G.ui={mode:'busy',unit:u}; useAbility(u,target).then(()=>{ G.busy=false; afterAction(u); }); return; }
  if(ui.mode==='reinforce'){ const u=ui.unit, p=panelAt(g.x,g.y); if(!p||!ui.validPanels.has(p.id)) return;
    p.reinforced=true; G.reinforceLeft--; G.ver++; refreshPanel(p); sfx('hit'); for(const c of p.cells) fxRing(c.x,c.y,0xffd21f,500,.8);
    const pr=wallProgress(p); log(`벽 강화 — 이 벽 ${pr.r}/${pr.n} 강화 완료 (남은 강화 ${G.reinforceLeft})`,'a'); selectUnit(u); return; }
  if(ui.mode==='move'||ui.mode==='cams') return;
  const u=pickUnit(sx,sy);
  if(u&&u.team===G.side){ if(!u.ended) selectUnit(u); else { showUnitPanel(u); G.ui={mode:'idle',panelUnit:u}; hideMenu(); } return; }
  const gd=enemyGadgets(G.side).find(x=>Math.hypot(x.x-g.fx,x.y-g.fy)<.5);
  if(gd&&cellVisible(G.side,gd.x,gd.y)){ const cand=[ui.unit,...G.units].filter(a=>a&&a.alive&&a.team===G.side&&seesPt(a,gd.x,gd.y)&&canShoot(a,gd)); const sh=cand[0];
    if(sh){ G.busy=true; destroyGadget(sh,gd).then(()=>{ G.busy=false; updateHUD(); }); } else log('사격 가능한 아군이 없습니다 (행동력 소모 없음 · 시야 내 대원 필요)','w'); return; }
  if(u&&u.team!==G.side){ showUnitPanel(u); hideMenu(); G.ui={mode:'idle',panelUnit:u}; return; }
  deselect();
}
function updateAim(){
  const ui=G.ui, h=G.hover; if(!h) return;
  if(ui.mode==='facing'){ const u=ui.unit, a=Math.atan2(h.fy-u.y,h.fx-u.x); if(Math.abs(angNorm(a-u.facing))>.01){ u.facing=a; G.ver++; } }
  if(ui.mode==='fire'){ const u=ui.unit, e=pickUnit(G.mouse.x,G.mouse.y); const a=e&&e.team!==G.side?Math.atan2(e.y-u.y,e.x-u.x):Math.atan2(h.fy-u.y,h.fx-u.x); ui.aim=a; ui.cast=fireCast(u,a); if(Math.abs(angNorm(a-u.facing))>.01){ u.facing=a; G.ver++; } updateTip(); }
  if(ui.mode==='ability'&&ui.spec&&ui.spec.kind==='panel'){ const p=panelAt(h.x,h.y); ui.hoverPanel=p?p.id:null; }
  if(ui.mode==='reinforce'){ const p=panelAt(h.x,h.y); ui.hoverPanel=p?p.id:null; }
}
function updateTip(){
  const tip=$('#tip'), ui=G.ui; if(ui.mode!=='fire'||!ui.cast){ tip.classList.add('hidden'); return; }
  const c=ui.cast, u=ui.unit; let html;
  if(c.unit&&(c.unit.team==='host'||shownToPlayer(c.unit))){ const ch=hitChance(u,c.unit,{preempt:true,walls:c.walls}); html=c.unit.team==='host'?`<span class="r">⚠ 인질이 사선에 있습니다!</span><br>명중 <b>${ch}%</b>`:`<span class="r">${c.unit.kr}</span> ${c.unit.hp}/${c.unit.maxHp}<br>선제 명중 <b>${ch}%</b> · ${Math.round(WEAP[u.weapon].dmg*1.4)}×${WEAP[u.weapon].shots}<br>헤드샷 <b>${headshotChance(u,c.unit,{preempt:true,walls:c.walls})}%</b>`; }
  else if(c.gadget) html='적 장비 — 파괴'; else if(c.breach) html='산탄 사격 — 벽 파괴'; else html=`목표 미확인${c.reinforced?' · 강화벽에서 정지':''}`;
  if(c.walls) html+=`<br>벽 관통 ×${c.walls} (명중 -${c.walls*22}%)`;
  tip.innerHTML=html; tip.classList.remove('hidden'); tip.style.left=(G.mouse.x+18)+'px'; tip.style.top=(G.mouse.y+14)+'px';
}
function bindInput(){
  cv.addEventListener('contextmenu',e=>e.preventDefault());
  cv.addEventListener('wheel',e=>{ e.preventDefault(); G.cam.h=clamp(G.cam.h*(e.deltaY>0?1.1:.9),9,42); },{passive:false});
  cv.addEventListener('pointerdown',e=>{
    if(G.state!=='game'||G.cineOn) return;
    G.mouse={x:e.clientX,y:e.clientY}; G.hover=groundAt(e.clientX,e.clientY);
    down={x:e.clientX,y:e.clientY,b:e.button,moved:false,g0:G.hover,path:false};
    if(e.button===0&&G.ui.mode==='move'&&playerCanAct()){ const m=G.ui.mover; G.ui.path=[{x:m.x,y:m.y}]; G.ui.dragging=true; down.path=true; }
  });
  window.addEventListener('pointermove',e=>{
    G.mouse={x:e.clientX,y:e.clientY}; if(G.state!=='game') return;
    G.hover=groundAt(e.clientX,e.clientY);
    if(down){ if(Math.hypot(e.clientX-down.x,e.clientY-down.y)>6) down.moved=true;
      if(down.path){ if(down.moved) extendPath(G.hover); }
      else if(down.moved&&(down.b!==0||['idle','menu','cams'].includes(G.ui.mode))&&down.g0&&G.hover){ G.cam.x+=down.g0.fx-G.hover.fx; G.cam.y+=down.g0.fy-G.hover.fy; G.hover=groundAt(e.clientX,e.clientY); } }
    updateAim();
  });
  window.addEventListener('pointerup',e=>{
    if(!down||G.state!=='game'){ down=null; return; } const d=down; down=null;
    if(d.b===2&&!d.moved){ cancelMode(); return; }
    if(d.b!==0) return;
    if(d.path){ onMoveRelease(); return; }
    if(!d.moved&&e.target===cv) onClick(groundAt(e.clientX,e.clientY),e.clientX,e.clientY);
  });
  window.addEventListener('keydown',e=>{
    keys[e.key.toLowerCase()]=true; if(G.cineOn){ if(['Escape',' ','Enter'].includes(e.key)) skipCine(); return; } if(G.state!=='game'||G.modal) return;
    if(e.key==='m'){ muted=!muted; log(muted?'사운드 끔 (M)':'사운드 켬 (M)',''); }
    else if(e.key==='Escape') cancelMode();
    else if(e.key==='Tab'){ e.preventDefault(); if(!playerCanAct()) return; const my=G.units.filter(u=>u.alive&&u.team===G.side&&!u.ended); if(!my.length) return; const i=my.indexOf(G.ui.unit); selectUnit(my[(i+1)%my.length]); }
    else if(e.key==='f'&&G.ui.mode==='facing') hintAction('lock');
    else if(/^[1-9]$/.test(e.key)&&G.ui.mode==='menu'&&G.ui.unit){ const c=commands(G.ui.unit)[+e.key-1]; if(c) doCommand(G.ui.unit,c.id); }
  });
  window.addEventListener('keyup',e=>{ keys[e.key.toLowerCase()]=false; });
  $('#endBtn').onclick=()=>{ if(!playerCanAct()) return; sfx('ui'); for(const u of G.units) if(u.alive&&u.team===G.side) u.ended=true; endPhase(); };
  window.addEventListener('resize',resize3D);
  $('#skipBtn').onclick=skipCine;
}

/* =========================================================
   TITLE / START / RESULT
   ========================================================= */
function renderSquad(){
  $('#squadList').innerHTML=OPS[G.side].map((o,i)=>{ const w=WEAP[o.weapon]; return `<div class="sq" style="--c:${o.c};animation-delay:${i*60}ms"><div class="p">${opArtSVG(o.key,true)}<span>${o.key.slice(0,2)}</span></div><div class="m"><b>${o.key}<small>${o.kr} · ${o.role}</small></b><p>${o.desc}</p></div><div class="s"><div><span>HP</span><i style="width:${o.hp*.6}px"></i>${o.hp}</div><div><span>MOV</span><i style="width:${o.mv*26}px"></i>${moveRange(o)}m</div><div><span>WPN</span>${w.name.split(' ')[1]}</div></div></div>`; }).join('');
  $('#winBox').innerHTML=G.side==='atk'?'<b>VICTORY // 둘 중 하나 달성</b><span>① 인질을 살려서 구출 지점(EXFIL)까지 호송</span><span>② 방어팀 전원 제압</span><em>시작 전 건물 외부 원하는 위치에 배치 · 패배: 15라운드 초과 · 전원 사망 · 아군 사격으로 인질 사망</em>':'<b>VICTORY // 둘 중 하나 달성</b><span>① 15라운드 종료까지 인질을 빼앗기지 않음</span><span>② 공격팀 전원 제압</span><em>전원 인질 방에서 시작 · 패배: 인질 구출당함 · 전원 사망 · 아군 사격으로 인질 사망</em>';
}
function initTitle(){
  $('#logo').innerHTML=[...'BLINDSIDE'].map((c,i)=>`<span style="animation-delay:${i*45}ms">${c}</span>`).join('');
  document.querySelectorAll('.side-card').forEach(b=>b.onclick=()=>{ document.querySelectorAll('.side-card').forEach(x=>x.classList.remove('sel')); b.classList.add('sel'); G.side=b.dataset.side; renderSquad(); sfx('click'); });
  $('#startBtn').onclick=()=>{ sfx('ui'); startGame(); };
  renderSquad(); buildMap(); build3DMap(); G.state='title';
}
let UID=0;
function mkUnit(team,op,x,y,f){ return Object.assign({},op,{id:++UID,team,x,y,rx:x,ry:y,facing:f,homeFacing:f,hp:op.hp,maxHp:op.hp,alive:true,moveLeft:0,acted:false,abilityUsed:false,gadgetUsed:false,ended:false,shield:false,ghost:false,stim:false,lockFacing:false,alertT:-1e9,revealT:0,drone:null,droneSpent:false}); }
function randomAtkSpots(){
  const zones={n:[],s:[],w:[],e:[]};
  for(let y=0;y<H;y++) for(let x=0;x<W;x++){ if(TL(x,y)!=='out'||!freeAt(x,y,false)) continue;
    if(y<=3&&x>=3) zones.n.push({x,y}); else if(y>=19) zones.s.push({x,y}); else if(x<=1&&y>=4) zones.w.push({x,y}); else if(x>=24) zones.e.push({x,y}); }
  const keys=Object.keys(zones).sort(()=>Math.random()-.5).slice(0,2), out=[];
  for(let i=0;i<5;i++){ const z=zones[keys[i%2]]; for(let tries=0;tries<60;tries++){ const c=z[(Math.random()*z.length)|0]; if(out.every(o=>Math.hypot(o.x-c.x,o.y-c.y)>1.4)){ out.push(c); break; } } }
  while(out.length<5) out.push({x:ATK_SPAWN[out.length][0],y:ATK_SPAWN[out.length][1]});
  return out;
}
function spawnAttackers(random){ const spots=random?randomAtkSpots():ATK_SPAWN.map(([x,y])=>({x,y}));
  OPS.atk.forEach((o,i)=>{ const s=spots[i]; G.units.push(mkUnit('atk',o,s.x,s.y,Math.atan2(11-s.y,13-s.x))); }); G.ver++; }
function deployValid(x,y,u){ return !insideBld(x,y)&&['out','extract'].includes(TLp(x,y))&&freeAt(x,y,false)&&!G.units.some(o=>o!==u&&o.alive&&o.team==='atk'&&o.placed!==false&&Math.hypot(o.x-x,o.y-y)<.7); }
function deployMask(){ const m=new Float32Array(W*H).fill(1e9); for(let y=0;y<H;y++) for(let x=0;x<W;x++) if(!insideBld(x,y)&&['out','extract'].includes(TL(x,y))) m[idx(x,y)]=1; return m; }
function enterDeploy(u){
  const changed=G.ui.unit!==u;
  G.ui={mode:'deploy',unit:u,panelUnit:u,reach:deployMask(),budget:2,reachKey:'dep',isDrone:false}; hideMenu(); showUnitPanel(u,!changed); showOpArt(u); if(changed&&u.placed!==false) panTo(u.x,u.y,350); updateHUD(); sfx('click');
  setHint(`${u.kr} — 강하할 위치를 클릭하십시오`,'건물 외부의 밝게 표시된 구역 어디든 가능 · 다른 대원은 클릭 또는 Tab으로 선택',[{id:'cancel',label:'ESC 선택 해제'}]);
}
function startGame(){
  clearDynamic();
  const info=buildMap();
  Object.assign(G,{state:'game',units:[],drones:[],cams:[],electrics:[],gases:[],marks:[],phaseCount:0,round:0,busy:true,over:false,modal:false,ui:{mode:'idle'},reinforceLeft:10,stats:{atk:0,def:0}});
  G.hostage={x:info.hostage.x,y:info.hostage.y,rx:info.hostage.x,ry:info.hostage.y,hp:100,maxHp:100,alive:true,team:'host',kr:'인질',key:'HOSTAGE',escort:null,trail:[],shield:false,facing:0};
  for(const c of info.fixedCams) G.cams.push({x:c.x,y:c.y,alive:true,discovered:false,placed:false});
  OPS.def.forEach(o=>{ const [x,y,f]=DEF_POST[o.key]; G.units.push(mkUnit('def',o,x,y,f)); });
  $('#title').classList.add('hidden'); $('#result').classList.remove('show'); if(!(G.side==='atk'&&!G.autoplay)) $('#hud').classList.remove('hidden'); else { $('#fade').style.transitionDuration='0ms'; $('#fade').classList.add('on'); } $('#log').innerHTML='';
  G.cam.h=27; Object.assign(G.heli,{vis:false,rope:0,tilt:0});
  if(G.side==='atk'){ aiDefPrep(); spawnAttackers(false); build3DMap(); buildRoster(); G.cam.x=11; G.cam.y=6; G.round=0;
    if(G.autoplay){ G.round=1; phase('atk'); }
    else { G.phase='deploy'; for(const a of G.units) if(a.team==='atk') a.placed=false; G.ver++; updateHUD();
      playIntro().then(()=>showBanner('deploy')).then(()=>{ G.busy=false; updateHUD(); enterDeploy(G.units.find(u=>u.team==='atk')); log('방어팀은 인질이 있는 방에 모여 있습니다. 강하 지점을 정하십시오.','w'); }); } }
  else { build3DMap(); buildRoster(); G.phase='prep'; G.round=0; for(const u of G.units) u.moveLeft=moveRange(u); G.ver++; updateHUD(); G.cam.x=13; G.cam.y=12;
    showBanner('prep').then(()=>{ G.busy=false; updateHUD(); log('준비 단계: 벽 강화(최대 10)와 장치 설치 후 [작전 개시]를 누르십시오.','w'); }); }
}
function finish(winner,reason){
  if(G.over) return; G.over=true; G.busy=true; const win=winner===G.side;
  setTimeout(()=>{ clearUI(); const el=$('#result');
    el.innerHTML=`<div class="rs"><div class="rs-h ${win?'':'lose'}"><small>${win?'OPERATION SUCCESS':'OPERATION FAILED'}</small><h2>${win?'MISSION COMPLETE':'MISSION FAILED'}</h2><p>${win?'작전 성공':'작전 실패'}</p></div>
    <div class="rs-b">${reason}<div class="rs-st"><div><label>ROUNDS</label><b>${Math.max(1,G.round)}</b></div><div><label>적 사살</label><b>${G.stats[G.side]}</b></div><div><label>아군 손실</label><b>${G.units.filter(u=>u.team===G.side&&!u.alive).length}</b></div></div></div>
    <div class="mbtns"><button id="rsRetry">재도전</button><button id="rsTitle">타이틀로</button></div></div>`;
    el.classList.add('show'); sfx(win?'ui':'boom');
    $('#rsRetry').onclick=()=>{ sfx('click'); startGame(); };
    $('#rsTitle').onclick=()=>{ sfx('click'); el.classList.remove('show'); $('#hud').classList.add('hidden'); $('#title').classList.remove('hidden'); clearDynamic(); G.state='title'; G.units=[]; G.hostage=null; buildMap(); build3DMap(); };
  },dur(1200));
}

/* =========================================================
   LOOP
   ========================================================= */
let lastT=now();
function loop(t){
  const dt=Math.min(50,t-lastT); lastT=t; updateTweens(t); G.shake*=.86;
  if(G.state==='title'){ G.cam.x=12.5+Math.sin(t/5200)*5; G.cam.y=9.5+Math.cos(t/6100)*2; G.cam.h=21; }
  else { const sp=.012*dt*G.cam.h/20; if(keys.w||keys.arrowup) G.cam.y-=sp; if(keys.s||keys.arrowdown) G.cam.y+=sp; if(keys.a||keys.arrowleft) G.cam.x-=sp; if(keys.d||keys.arrowright) G.cam.x+=sp; updateDiscovery(); positionMenu(); }
  render3D(t); requestAnimationFrame(loop);
}
G.shake=0;
init3D(); bindInput(); initTitle(); requestAnimationFrame(loop);
