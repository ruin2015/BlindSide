'use strict';
/* =========================================================
   BLINDSIDE v2 — core (continuous positions, grid map)
   ========================================================= */
const MAP=[
'EEE.......................',
'EEE.....o.........o.......',
'..........................',
'......o..........o........',
'..........................',
'...HWWWWWHWW,,WWHWWWWWH...',
'...W,,,,,W,,,,,,W,,,,,W...',
'...W,c,,,W,,,,,,,,,,,,W...',
'...W,,,,,,,,o,,,W,,,,,,...',
'...W,,,,,W,,,o,,W,,,,,W...',
'...W,,,,,W,,,,,,W,,,c,W...',
'...HWW,WWH,,,,,,HWW,WWH...',
'...,,,,,,,,,,,,,,,o,,,W...',
'...HWW,WWWHWW,WWHWW,WWH...',
'...W,,,,,,W,,,,,W,,,,,W...',
'...W,,,,,,W,,h,,W,,,,,W...',
'...W,c,,,,W,,,,,W,,,,,W...',
'...HWWWWWWHWWWWWHWW,WWH...',
'..........................',
'.....o...............o....'];

const WEAP={
  AR :{name:'K-417 돌격소총', dmg:18, shots:3, acc:76, range:9},
  SMG:{name:'V-9 기관단총',   dmg:12, shots:5, acc:70, range:6},
  SG :{name:'M-870 산탄총',   dmg:10, shots:6, acc:62, range:4, breach:true},
  PST:{name:'P-12 권총',      dmg:22, shots:2, acc:74, range:6},
};
const OPS={
  atk:[
    {key:'CRUSHER', kr:'크러셔', role:'브리처', hp:120, mv:2, weapon:'AR', ab:'hammer', abName:'브리칭 해머', charges:5, c:'#2fe4ff',
     desc:'인접한 일반 벽을 해머로 부숴 지나갈 수 있는 새 통로를 만든다.'},
    {key:'FLARE', kr:'플레어', role:'원거리 브리처', hp:100, mv:3, weapon:'AR', ab:'launcher', abName:'브리칭 런처', charges:4, c:'#7cf0c8',
     desc:'원거리에서 폭발물을 발사해 벽·강화벽에 구멍을 낸다. 시야만 열리고 통과는 불가.'},
    {key:'IGNIS', kr:'이그니스', role:'하드 브리처', hp:120, mv:2, weapon:'SMG', ab:'thermite', abName:'발열 폭약', charges:2, c:'#ff9d1c',
     desc:'인접한 벽·강화벽에 폭약을 설치해 통과 가능한 통로를 연다. 전류가 흐르는 벽에는 설치 불가.'},
    {key:'SERUM', kr:'세럼', role:'메딕', hp:100, mv:3, weapon:'SMG', ab:'stim', abName:'전투 자극제', charges:3, c:'#b98cff',
     desc:'가까운 아군에게 약물을 투여해 체력 50 회복, 이번 턴 이동력 1.5배.'},
    {key:'BASTION', kr:'바스티온', role:'실드', hp:140, mv:1, weapon:'PST', ab:'shield', abName:'전개형 방패', charges:99, c:'#ffd21f',
     desc:'방패를 전개해 다음 내 턴까지 정면에서 오는 공격을 무효화한다. 전개 중 이동 가능.'},
  ],
  def:[
    {key:'VOLT', kr:'볼트', role:'전자전', hp:120, mv:2, weapon:'SG', ab:'shock', abName:'쇼크 와이어', charges:5, c:'#4fb3ff',
     desc:'원형 전기장을 설치한다. 범위 안의 적 가젯을 파괴하고 강화벽에 전류를 흘려 폭약 설치를 막는다.'},
    {key:'MIASMA', kr:'미아즈마', role:'독가스', hp:120, mv:2, weapon:'SMG', ab:'gas', abName:'독성 가스탄', charges:3, c:'#8cff5a',
     desc:'가스탄을 던져 시야를 가리고 범위 안의 적에게 피해를 준다. 2턴 지속.'},
    {key:'WRAITH', kr:'레이스', role:'로머', hp:100, mv:3, weapon:'SMG', ab:'ghost', abName:'전파 교란', charges:3, c:'#c9a0ff',
     desc:'다음 내 턴까지 적의 드론·감시 장비에 포착되지 않는다. 사용 후 이동 가능.'},
    {key:'OBSIDIAN', kr:'옵시디언', role:'정보', hp:140, mv:1, weapon:'AR', ab:'mirror', abName:'블랙 미러', charges:2, c:'#9fb6c8',
     desc:'인접한 벽에 편면 방탄 거울을 설치한다. 방어팀만 건너편을 볼 수 있다.'},
    {key:'ARGUS', kr:'아르고스', role:'감시', hp:120, mv:2, weapon:'AR', ab:'cam', abName:'블랙 아이', charges:3, c:'#ff6b8a',
     desc:'주변에 추가 감시카메라를 설치한다. 가젯에서 카메라 화면을 볼 수 있다.'},
  ]
};
const ATK_SPAWN=[[4,2],[7,2],[10,3],[13,2],[16,2]];
const DEF_POST={VOLT:[12,14,-Math.PI/2], MIASMA:[14,14,-Math.PI/2], WRAITH:[11,15,Math.PI], OBSIDIAN:[15,15,0], ARGUS:[13,16,Math.PI/2]};
const BLD={x0:3,x1:22,y0:5,y1:17};
const insideBld=(x,y)=>{ const cx=Math.round(x), cy=Math.round(y); return cx>=BLD.x0&&cx<=BLD.x1&&cy>=BLD.y0&&cy<=BLD.y1; };
const VIS={circle:2.3, range:10, half:0.68};
const UR=0.28;
const DIR4=[[1,0],[-1,0],[0,1],[0,-1]];
const DIR8=[[1,0],[-1,0],[0,1],[0,-1],[1,1],[1,-1],[-1,1],[-1,-1]];
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
const angNorm=a=>{while(a>Math.PI)a-=2*Math.PI;while(a<-Math.PI)a+=2*Math.PI;return a;};
const other=t=>t==='atk'?'def':'atk';
const now=()=>performance.now();
const dist=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y);

let W=26,H=20,tile=[],panelOf=null,panels=[],walls=[],gasSet=new Set();
const idx=(x,y)=>y*W+x;
function TL(x,y){ return (x<0||y<0||x>=W||y>=H)?'void':tile[y*W+x]; }
const TLp=(px,py)=>TL(Math.round(px),Math.round(py));

const G={state:'title', side:'atk', maxRound:15, cam:{x:12.5,y:9.5,h:24}, ver:1, cineOn:false, cineCam:null, cineFn:null, cineSkip:false, heli:{x:0,y:0,z:6,hd:0,rope:0,vis:false}, mapVer:1,
  units:[], drones:[], cams:[], electrics:[], gases:[], marks:[], hostage:null, phase:'none', phaseCount:0, round:0,
  busy:true, over:false, modal:false, ui:{mode:'idle'}, reinforceLeft:10, autoplay:false, fast:false, stats:{atk:0,def:0}};
function bumpMap(){ G.mapVer++; G.ver++; }

function buildMap(){
  H=MAP.length; W=26; tile=new Array(W*H); panelOf=new Int16Array(W*H).fill(-1); panels=[]; walls=[];
  const fixedCams=[]; let hostage=null;
  for(let y=0;y<H;y++){ const row=MAP[y].padEnd(W,'.'); for(let x=0;x<W;x++){ const ch=row[x]; let t='out';
    if(ch===',') t='floor'; else if(ch==='W') t='wall'; else if(ch==='H') t='hard'; else if(ch==='o') t='crate';
    else if(ch==='E') t='extract'; else if(ch==='c'){t='floor';fixedCams.push({x,y});} else if(ch==='h'){t='floor';hostage={x,y};}
    tile[y*W+x]=t; } }
  const wallish=(x,y)=>{const t=TL(x,y);return t==='wall'||t==='hard';};
  for(let y=0;y<H;y++) for(let x=0;x<W;x++){
    if(tile[idx(x,y)]!=='wall'||panelOf[idx(x,y)]>=0) continue;
    const hz=wallish(x-1,y)||wallish(x+1,y), vt=wallish(x,y-1)||wallish(x,y+1);
    const horiz=hz||!vt; const dx=horiz?1:0, dy=horiz?0:1; const run=[]; let cx=x,cy=y;
    while(TL(cx,cy)==='wall'&&panelOf[idx(cx,cy)]<0){ run.push({x:cx,y:cy}); cx+=dx; cy+=dy; }
    const wall={id:walls.length,panels:[]}; walls.push(wall);
    for(let i=0;i<run.length;i+=2){ const cells=run.slice(i,i+2);
      const p={id:panels.length,wall:wall.id,cells,state:'intact',reinforced:false,electrified:false,mirror:false,
        cx:cells.reduce((s,c)=>s+c.x,0)/cells.length, cy:cells.reduce((s,c)=>s+c.y,0)/cells.length};
      for(const c of cells) panelOf[idx(c.x,c.y)]=p.id; panels.push(p); wall.panels.push(p.id); }
  }
  gasSet=new Set(); bumpMap();
  return {fixedCams,hostage};
}
const panelAt=(x,y)=>{ if(TL(x,y)!=='wall') return null; const k=panelOf[idx(x,y)]; return k>=0?panels[k]:null; };

/* ---------- opacity / collision ---------- */
function opaqueRaw(x,y,team){
  const t=TL(x,y);
  if(t==='void'||t==='hard') return true;
  if(t==='wall'){ const p=panels[panelOf[idx(x,y)]]; if(p&&p.mirror&&team==='def') return false; return true; }
  return gasSet.has(idx(x,y));
}
const OPQ={ver:-1,atk:null,def:null};
function opqGrid(team){ if(OPQ.ver!==G.mapVer){OPQ.ver=G.mapVer;OPQ.atk=null;OPQ.def=null;} if(OPQ[team]) return OPQ[team];
  const a=new Uint8Array(W*H); for(let y=0;y<H;y++) for(let x=0;x<W;x++) a[idx(x,y)]=opaqueRaw(x,y,team)?1:0; return OPQ[team]=a; }
function blockCell(x,y,isDrone){ const t=TL(x,y); if(t==='floor'||t==='out'||t==='rubble'||t==='extract') return false; if(isDrone&&t==='hole') return false; return true; }
function freeAt(px,py,isDrone,r=UR){
  for(let cy=Math.round(py-r);cy<=Math.round(py+r);cy++) for(let cx=Math.round(px-r);cx<=Math.round(px+r);cx++){
    if(!blockCell(cx,cy,isDrone)) continue;
    const qx=clamp(px,cx-.5,cx+.5), qy=clamp(py,cy-.5,cy+.5);
    if((px-qx)*(px-qx)+(py-qy)*(py-qy)<r*r) return false; }
  return true;
}
function segFree(a,b,isDrone){ const d=Math.hypot(b.x-a.x,b.y-a.y), n=Math.max(1,Math.ceil(d/.08)); for(let i=1;i<=n;i++){ const t=i/n; if(!freeAt(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,isDrone)) return false; } return true; }
function distToCell(px,py,cx,cy){ const qx=clamp(px,cx-.5,cx+.5), qy=clamp(py,cy-.5,cy+.5); return Math.hypot(px-qx,py-qy); }

/* ---------- line of sight ---------- */
function losF(x0,y0,x1,y1,team,ex){
  const dx=x1-x0, dy=y1-y0, d=Math.hypot(dx,dy); if(d<1e-6) return true;
  const n=Math.ceil(d/.1), sx=Math.round(x0), sy=Math.round(y0), g=opqGrid(team);
  for(let i=1;i<n;i++){ const cx=Math.round(x0+dx*i/n), cy=Math.round(y0+dy*i/n);
    if(cx===sx&&cy===sy) continue; if(cx<0||cy<0||cx>=W||cy>=H) return false;
    const k=cy*W+cx; if(ex!=null&&(ex===k||(ex instanceof Set&&ex.has(k)))) continue;
    if(g[k]) return false; }
  return true;
}
function inView(u,px,py){ const dx=px-u.x, dy=py-u.y, d=Math.hypot(dx,dy); if(d<=VIS.circle) return true; if(d>VIS.range) return false; return Math.abs(angNorm(Math.atan2(dy,dx)-u.facing))<=VIS.half; }
function seesPt(u,px,py,ex){ return u.alive&&u.placed!==false&&inView(u,px,py)&&losF(u.x,u.y,px,py,u.team,ex); }
const sees=(a,b)=>a.alive&&b.alive&&b.placed!==false&&seesPt(a,b.x,b.y);

function fovCells(u){
  if(u._fv===G.ver) return u._fov; const s=new Set(), R=VIS.range;
  if(u.alive&&u.placed!==false){ const ux=Math.round(u.x), uy=Math.round(u.y);
    for(let y=Math.max(0,uy-R);y<=Math.min(H-1,uy+R);y++) for(let x=Math.max(0,ux-R);x<=Math.min(W-1,ux+R);x++){
      const k=idx(x,y); if(inView(u,x,y)&&losF(u.x,u.y,x,y,u.team,k)) s.add(k); }
    s.add(idx(ux,uy)); }
  u._fv=G.ver; u._fov=s; return s;
}
function rayLen(x,y,a,maxR,team){ const cs=Math.cos(a), sn=Math.sin(a), sx=Math.round(x), sy=Math.round(y), g=opqGrid(team);
  for(let d=.05;d<=maxR;d+=.05){ const cx=Math.round(x+cs*d), cy=Math.round(y+sn*d); if(cx===sx&&cy===sy) continue; if(cx<0||cy<0||cx>=W||cy>=H) return d; if(g[cy*W+cx]) return d; } return maxR; }
function visionPoly(u){
  if(u._vpv===G.ver) return u._vp;
  const pts=[], H2=VIS.half, D=Math.PI/180;
  const push=(off,r)=>{ const a=u.facing+off, L=rayLen(u.x,u.y,a,r,u.team); pts.push([u.x+Math.cos(a)*L,u.y+Math.sin(a)*L]); };
  for(let o=-Math.PI;o<-H2;o+=5*D) push(o,VIS.circle);
  push(-H2-1e-4,VIS.circle); for(let o=-H2;o<=H2+1e-6;o+=.9*D) push(o,VIS.range); push(H2+1e-4,VIS.circle);
  for(let o=H2+5*D;o<Math.PI;o+=5*D) push(o,VIS.circle);
  u._vpv=G.ver; u._vp=pts; return pts;
}
function circleCells(cx,cy,r,team,out){ for(let y=Math.floor(cy-r);y<=Math.ceil(cy+r);y++) for(let x=Math.floor(cx-r);x<=Math.ceil(cx+r);x++){ if(TL(x,y)==='void'||Math.hypot(x-cx,y-cy)>r) continue; if(losF(cx,cy,x,y,team,idx(x,y))) out.add(idx(x,y)); } return out; }
const TVC={ver:-1,atk:null,def:null};
function teamVision(team){
  if(TVC.ver!==G.ver){TVC.ver=G.ver;TVC.atk=null;TVC.def=null;}
  if(TVC[team]) return TVC[team];
  const units=new Set(), gadget=new Set();
  for(const u of G.units) if(u.alive&&u.team===team) for(const k of fovCells(u)) units.add(k);
  if(team==='atk'){ for(const d of G.drones) if(d.alive) circleCells(d.x,d.y,2.5,'atk',gadget); }
  else for(const c of G.cams) if(c.alive) circleCells(c.x,c.y,3.2,'def',gadget);
  const all=new Set(units); for(const k of gadget) all.add(k);
  return TVC[team]={units,gadget,all};
}
function gadgetSees(team,px,py){
  if(team==='atk'){ for(const d of G.drones) if(d.alive&&Math.hypot(d.x-px,d.y-py)<=2.5&&losF(d.x,d.y,px,py,'atk')) return true; }
  else for(const c of G.cams) if(c.alive&&Math.hypot(c.x-px,c.y-py)<=3.2&&losF(c.x,c.y,px,py,'def')) return true;
  return false;
}
function enemyVisible(team,e){
  if(!e.alive) return false;
  const key=G.ver+team; if(e._evk===key) return e._ev;
  let v=G.units.some(u=>u.alive&&u.team===team&&seesPt(u,e.x,e.y));
  if(!v&&!e.ghost) v=gadgetSees(team,e.x,e.y);
  e._evk=key; e._ev=v; return v;
}
const cellVisible=(team,px,py)=>teamVision(team).all.has(idx(Math.round(px),Math.round(py)));
function shownToPlayer(e){ if(e.team===G.side||e.team==='host') return true; return enemyVisible(G.side,e)||e.revealT>now(); }

function traceLine(x0,y0,x1,y1){
  const dx=x1-x0, dy=y1-y0, n=Math.max(1,Math.ceil(Math.hypot(dx,dy)/.1)); let walls=0,last=-1; const s=idx(Math.round(x0),Math.round(y0)), e=idx(Math.round(x1),Math.round(y1));
  for(let i=1;i<n;i++){ const cx=Math.round(x0+dx*i/n), cy=Math.round(y0+dy*i/n), k=idx(cx,cy); if(k===s||k===e||k===last) continue; last=k;
    const t=TL(cx,cy); if(t==='hard'||t==='void') return {blocked:true,walls};
    if(t==='wall'){ const p=panels[panelOf[k]]; if(p.reinforced||p.mirror) return {blocked:true,walls}; walls++; } }
  return {blocked:false,walls};
}

/* ---------- path planning ---------- */
function blockedSet(team){ const s=new Set(); const h=G.hostage; if(h&&h.alive&&!(h.escort&&h.escort.team===team)) s.add(idx(Math.round(h.x),Math.round(h.y)));
  for(const e of G.units) if(e.alive&&e.team!==team&&(team!==G.side||G.autoplay||enemyVisible(team,e))) s.add(idx(Math.round(e.x),Math.round(e.y)));
  return s; }
function dijkstra(sx,sy,isDrone,blocked){
  const N=W*H, D=new Float32Array(N).fill(1e9), P=new Int32Array(N).fill(-1), done=new Uint8Array(N), s=idx(sx,sy); D[s]=0; const open=[s];
  const pass=(x,y)=>x>=0&&y>=0&&x<W&&y<H&&!blockCell(x,y,isDrone);
  while(open.length){ let bi=0; for(let i=1;i<open.length;i++) if(D[open[i]]<D[open[bi]]) bi=i; const c=open[bi]; open[bi]=open[open.length-1]; open.pop(); if(done[c]) continue; done[c]=1;
    const cx=c%W, cy=(c/W)|0;
    for(const [dx,dy] of DIR8){ const nx=cx+dx, ny=cy+dy; if(!pass(nx,ny)) continue; const n=idx(nx,ny); if(done[n]||(blocked&&blocked.has(n))) continue;
      if(dx&&dy&&(!pass(cx+dx,cy)||!pass(cx,cy+dy))) continue; const nd=D[c]+(dx&&dy?1.4142:1); if(nd<D[n]){ D[n]=nd; P[n]=c; open.push(n); } } }
  return {D,P};
}
function pathLen(pts){ let l=0; for(let i=1;i<pts.length;i++) l+=dist(pts[i],pts[i-1]); return l; }
function truncate(pts,b){ const out=[pts[0]]; let rem=b; for(let i=1;i<pts.length;i++){ const s=dist(pts[i],pts[i-1]); if(s<=rem){ out.push(pts[i]); rem-=s; } else { const t=rem/s; if(rem>.02) out.push({x:pts[i-1].x+(pts[i].x-pts[i-1].x)*t,y:pts[i-1].y+(pts[i].y-pts[i-1].y)*t}); break; } } return out; }
function bodyObstacles(team,from){
  const obs=[], h=G.hostage;
  if(h&&h.alive&&!(h.escort&&h.escort.team===team)) obs.push(h);
  for(const e of G.units) if(e.alive&&e.team!==team&&(team!==G.side||G.autoplay||enemyVisible(team,e))) obs.push(e);
  return from?obs.filter(o=>Math.hypot(o.x-from.x,o.y-from.y)>.3):obs;
}
function bodyClear(a,b,obs){ if(!obs||!obs.length) return true; const d=Math.hypot(b.x-a.x,b.y-a.y), n=Math.max(1,Math.ceil(d/.1));
  for(let i=1;i<=n;i++){ const px=a.x+(b.x-a.x)*i/n, py=a.y+(b.y-a.y)*i/n; for(const o of obs) if(Math.hypot(o.x-px,o.y-py)<.58) return false; } return true; }
function planPath(from,tx,ty,isDrone,team,budget){
  const sx=Math.round(from.x), sy=Math.round(from.y), gx=Math.round(tx), gy=Math.round(ty);
  if(gx<0||gy<0||gx>=W||gy>=H) return null;
  const bl=isDrone?null:blockedSet(team); bl&&bl.delete(idx(sx,sy));
  const obs=isDrone?null:bodyObstacles(team,from);
  const ok=(a,b)=>segFree(a,b,isDrone)&&bodyClear(a,b,obs);
  const {D,P}=dijkstra(sx,sy,isDrone,bl); const gk=idx(gx,gy); if(D[gk]>=1e9) return null;
  const cells=[]; let c=gk; while(c!==idx(sx,sy)&&c>=0){ cells.push({x:c%W,y:(c/W)|0}); c=P[c]; } cells.reverse();
  const pts=[{x:from.x,y:from.y},...cells];
  const end={x:tx,y:ty};
  if(freeAt(tx,ty,isDrone)&&ok(pts[pts.length-1],end)&&dist(pts[pts.length-1],end)>.05){ if(cells.length&&ok(pts[pts.length-2],end)) pts[pts.length-1]=end; else pts.push(end); }
  const out=[pts[0]]; let i=0;
  while(i<pts.length-1){ let j=pts.length-1; while(j>i+1&&!ok(pts[i],pts[j])) j--; out.push(pts[j]); i=j; }
  return budget!=null?truncate(out,budget):out;
}
function spotClear(px,py,self){ const h=G.hostage;
  if(h&&h.alive&&h!==self&&!(h.escort===self)&&Math.hypot(h.x-px,h.y-py)<.6) return false;
  return !G.units.some(u=>u.alive&&u!==self&&Math.hypot(u.x-px,u.y-py)<.6&&(u.team===self.team||self.team!==G.side||G.autoplay||enemyVisible(self.team,u))); }
const moveRange=u=>u.mv*3+1;
const speedOf=u=>u.mv+(u.stim?1:0);
const panelNear=(p,u,r=.85)=>p.cells.some(c=>distToCell(u.x,u.y,c.x,c.y)<=r);
function throwClear(u,p){ const ex=new Set(p.cells.map(c=>idx(c.x,c.y))); return p.cells.some(c=>Math.hypot(c.x-u.x,c.y-u.y)<=6.5&&losF(u.x,u.y,c.x,c.y,u.team,ex)); }
function wallProgress(p){ const w=walls[p.wall]; return {r:w.panels.filter(i=>panels[i].reinforced).length,n:w.panels.length}; }
const canReinforce=p=>p.state==='intact'&&!p.reinforced&&!p.mirror;
