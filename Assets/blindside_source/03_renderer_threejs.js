/* =========================================================
   3D TOP-VIEW RENDERER (three.js r128)
   ========================================================= */
const cv=document.getElementById('cv'), LAYER=document.getElementById('labels');
let VW=innerWidth, VH=innerHeight;
let renderer,scene,camera,raycaster,groundPl,mapRoot,dynRoot,floorCanvas,floorTex,fogData,fogTex,ovData,ovTex,sunLight;
const TILT=.32;
const R={cells:new Map(),units:new Map(),drones:new Map(),cams:new Map(),elec:new Map(),gas:new Map(),marks:new Map(),polys:new Map(),fx:[],labels:new Map(),pops:[],host:null};
const GEO={}, V3=new THREE.Vector3();
const mkStd=(c,o={})=>new THREE.MeshStandardMaterial(Object.assign({color:c,roughness:.82,metalness:.08},o));
const mkBasic=(c,o={})=>new THREE.MeshBasicMaterial(Object.assign({color:c},o));
const flat=m=>{ m.rotation.x=-Math.PI/2; return m; };


function makeWoodTex(){ const cv2=document.createElement('canvas'); cv2.width=cv2.height=128; const c=cv2.getContext('2d');
  const cols=['#8a5f37','#7d5431','#94683d','#80572f'];
  for(let i=0;i<4;i++){ c.fillStyle=cols[i]; c.fillRect(i*32,0,32,128);
    c.strokeStyle='rgba(50,28,12,.35)'; c.lineWidth=1; for(let g=0;g<6;g++){ c.beginPath(); const x0=i*32+4+g*5; c.moveTo(x0,0); for(let y=0;y<=128;y+=16) c.lineTo(x0+Math.sin(y/20+g+i)*2,y); c.stroke(); }
    c.fillStyle='rgba(30,16,6,.55)'; c.fillRect(i*32,0,2,128);
    c.fillStyle='rgba(40,22,8,.5)'; c.beginPath(); c.ellipse(i*32+16,40+i*19,3,5,0,0,7); c.fill();
    c.fillStyle='#3a3a3a'; c.fillRect(i*32+14,10,3,3); c.fillRect(i*32+14,115,3,3); }
  c.fillStyle='rgba(30,16,6,.35)'; c.fillRect(0,60,128,6);
  const t=new THREE.CanvasTexture(cv2); t.anisotropy=4; return t; }
function makeShutterTex(){ const cv2=document.createElement('canvas'); cv2.width=cv2.height=128; const c=cv2.getContext('2d');
  const g=c.createLinearGradient(0,0,128,0); g.addColorStop(0,'#5d6873'); g.addColorStop(.5,'#8e99a5'); g.addColorStop(1,'#5d6873'); c.fillStyle=g; c.fillRect(0,0,128,128);
  for(let y=0;y<128;y+=10){ c.fillStyle='rgba(255,255,255,.22)'; c.fillRect(0,y,128,2); c.fillStyle='rgba(20,26,34,.55)'; c.fillRect(0,y+7,128,3); }
  c.fillStyle='#4a525c'; c.fillRect(0,0,8,128); c.fillRect(120,0,8,128);
  c.fillStyle='rgba(0,0,0,.35)'; for(let y=5;y<128;y+=20){ c.fillRect(3,y,2,2); c.fillRect(123,y,2,2); }
  const t=new THREE.CanvasTexture(cv2); t.anisotropy=4; return t; }
function init3D(){
  renderer=new THREE.WebGLRenderer({canvas:cv,antialias:true});
  renderer.setPixelRatio(Math.min(2,window.devicePixelRatio||1));
  renderer.shadowMap.enabled=true; renderer.shadowMap.type=THREE.PCFSoftShadowMap;
  scene=new THREE.Scene(); scene.background=new THREE.Color(0x06080c);
  camera=new THREE.PerspectiveCamera(38,VW/VH,.1,300);
  scene.add(new THREE.HemisphereLight(0xd6e6ff,0x1b1e25,.55));
  sunLight=new THREE.DirectionalLight(0xfff2e0,.72); sunLight.position.set(5,28,-3); sunLight.target.position.set(12.5,0,9.5);
  sunLight.castShadow=true; sunLight.shadow.mapSize.set(2048,2048); const sc=sunLight.shadow.camera; sc.left=-22; sc.right=22; sc.top=22; sc.bottom=-22; sc.near=1; sc.far=70; sunLight.shadow.bias=-.0006;
  scene.add(sunLight); scene.add(sunLight.target);
  const fill=new THREE.DirectionalLight(0x7fb0ff,.28); fill.position.set(30,14,34); scene.add(fill);
  raycaster=new THREE.Raycaster(); groundPl=new THREE.Plane(new THREE.Vector3(0,1,0),0);
  mapRoot=new THREE.Group(); dynRoot=new THREE.Group(); scene.add(mapRoot); scene.add(dynRoot);
  const og=flat(new THREE.Mesh(new THREE.PlaneGeometry(160,160),mkStd(0x0b0f15,{roughness:1}))); og.position.set(12.5,-.03,9.5); og.receiveShadow=true; scene.add(og);
  const grid=new THREE.GridHelper(160,160,0x151b23,0x10151c); grid.position.set(12,-.02,10); scene.add(grid);
  GEO.box=new THREE.BoxGeometry(1,1,1); GEO.wood=makeWoodTex(); GEO.steel=makeShutterTex(); GEO.ring=new THREE.RingGeometry(.975,1,128); GEO.disc=new THREE.CircleGeometry(1,64); GEO.sph=new THREE.SphereGeometry(1,16,12);
  // dynamic helpers
  R.path=new THREE.Mesh(new THREE.BufferGeometry(),mkBasic(0xffd21f,{transparent:true,opacity:.95,depthWrite:false,side:THREE.DoubleSide})); R.path.renderOrder=6; dynRoot.add(R.path);
  R.pathEnd=flat(new THREE.Mesh(new THREE.RingGeometry(.3,.4,32),mkBasic(0xffd21f,{side:THREE.DoubleSide}))); R.pathEnd.position.y=.06; dynRoot.add(R.pathEnd);
  R.aim=new THREE.Mesh(new THREE.BufferGeometry(),mkBasic(0xff3b54,{transparent:true,opacity:.85,depthWrite:false,side:THREE.DoubleSide})); R.aim.renderOrder=6; dynRoot.add(R.aim);
  R.aimDot=flat(new THREE.Mesh(new THREE.RingGeometry(.22,.32,32),mkBasic(0xff3b54,{side:THREE.DoubleSide}))); R.aimDot.position.y=.07; dynRoot.add(R.aimDot);
  R.range=flat(new THREE.Mesh(GEO.ring,mkBasic(0xffd21f,{transparent:true,opacity:.7,side:THREE.DoubleSide}))); R.range.position.y=.05; dynRoot.add(R.range);
  R.eff=flat(new THREE.Mesh(GEO.disc,mkBasic(0xffd21f,{transparent:true,opacity:.18,side:THREE.DoubleSide,depthWrite:false}))); R.eff.position.y=.05; dynRoot.add(R.eff);
  R.effRing=flat(new THREE.Mesh(GEO.ring,mkBasic(0xffd21f,{side:THREE.DoubleSide}))); R.effRing.position.y=.055; dynRoot.add(R.effRing);
  R.hover=flat(new THREE.Mesh(new THREE.RingGeometry(.14,.2,24),mkBasic(0xffffff,{transparent:true,opacity:.5,side:THREE.DoubleSide}))); R.hover.position.y=.05; dynRoot.add(R.hover);
  resize3D();
}
function resize3D(){ VW=innerWidth; VH=innerHeight; if(!renderer) return; renderer.setSize(VW,VH); camera.aspect=VW/VH; camera.updateProjectionMatrix(); }
function updateCamera(){ if(G.cineCam){ const q=G.cineCam; camera.position.set(q.px,q.py,q.pz); camera.lookAt(q.lx,q.ly,q.lz); return; } const c=G.cam; camera.position.set(c.x,c.h,c.y+c.h*Math.tan(TILT)); camera.lookAt(c.x,0,c.y); }
function proj(x,h,y){ V3.set(x,h,y).project(camera); return {x:(V3.x+1)/2*VW, y:(1-V3.y)/2*VH, z:V3.z}; }
function groundAt(sx,sy){ raycaster.setFromCamera(new THREE.Vector2(sx/VW*2-1,-(sy/VH)*2+1),camera); const p=new THREE.Vector3(); if(!raycaster.ray.intersectPlane(groundPl,p)) return null; return {fx:p.x,fy:p.z,x:Math.round(p.x),y:Math.round(p.z)}; }

/* ---------- static map ---------- */
function drawFloor(){
  const c=floorCanvas.getContext('2d'), S=48;
  for(let y=0;y<H;y++) for(let x=0;x<W;x++){
    const t=tile[idx(x,y)], inside=x>=3&&x<=22&&y>=5&&y<=17;
    c.fillStyle=t==='extract'?'#3a3211':inside?(((x+y)&1)?'#3a434f':'#3e4854'):(((x+y)&1)?'#1e252d':'#222a33');
    c.fillRect(x*S,y*S,S,S);
    if(inside){ c.strokeStyle='rgba(255,255,255,.05)'; c.lineWidth=1; c.strokeRect(x*S+.5,y*S+.5,S/2,S/2); c.strokeRect(x*S+S/2+.5,y*S+S/2+.5,S/2-1,S/2-1); }
    else { c.fillStyle='rgba(255,255,255,.03)'; for(let i=0;i<5;i++) c.fillRect(x*S+((x*7+y*13+i*17)%S),y*S+((x*11+y*5+i*23)%S),2,2); }
    if(t==='extract'){ c.save(); c.beginPath(); c.rect(x*S,y*S,S,S); c.clip(); c.strokeStyle='rgba(255,210,31,.6)'; c.lineWidth=7; for(let i=-2;i<4;i++){ c.beginPath(); c.moveTo(x*S+i*18-10,y*S+S+10); c.lineTo(x*S+i*18+S,y*S-10); c.stroke(); } c.restore(); }
    if(t==='rubble'){ c.fillStyle='#6b4d31'; for(let i=0;i<9;i++){ c.fillRect(x*S+((x*9+i*19)%40)+2,y*S+((y*7+i*13)%40)+2,5,4); } }
    c.strokeStyle='rgba(0,0,0,.28)'; c.lineWidth=1; c.strokeRect(x*S+.5,y*S+.5,S-1,S-1);
  }
  c.fillStyle='#ffd21f'; c.font='700 26px "Chakra Petch",sans-serif'; c.textAlign='center'; c.fillText('EXFIL',1.5*S,1.1*S);
  floorTex.needsUpdate=true;
}
function build3DMap(){
  while(mapRoot.children.length) mapRoot.remove(mapRoot.children[0]); R.cells.clear();
  floorCanvas=document.createElement('canvas'); floorCanvas.width=W*48; floorCanvas.height=H*48;
  floorTex=new THREE.CanvasTexture(floorCanvas); floorTex.anisotropy=4; drawFloor();
  const fl=flat(new THREE.Mesh(new THREE.PlaneGeometry(W,H),mkStd(0xffffff,{map:floorTex,roughness:.95}))); fl.position.set((W-1)/2,0,(H-1)/2); fl.receiveShadow=true; mapRoot.add(fl);
  fogData=new Uint8Array(W*H*4); fogTex=new THREE.DataTexture(fogData,W,H,THREE.RGBAFormat); fogTex.magFilter=THREE.LinearFilter; fogTex.minFilter=THREE.LinearFilter; fogTex.needsUpdate=true;
  const fm=flat(new THREE.Mesh(new THREE.PlaneGeometry(W,H),mkBasic(0xffffff,{map:fogTex,transparent:true,depthWrite:false}))); fm.position.set((W-1)/2,.02,(H-1)/2); fm.renderOrder=1; mapRoot.add(fm);
  ovData=new Uint8Array(W*H*4); ovTex=new THREE.DataTexture(ovData,W,H,THREE.RGBAFormat); ovTex.magFilter=THREE.NearestFilter; ovTex.minFilter=THREE.NearestFilter; ovTex.needsUpdate=true;
  const om=flat(new THREE.Mesh(new THREE.PlaneGeometry(W,H),mkBasic(0xffffff,{map:ovTex,transparent:true,depthWrite:false}))); om.position.set((W-1)/2,.03,(H-1)/2); om.renderOrder=2; mapRoot.add(om);
  for(let y=0;y<H;y++) for(let x=0;x<W;x++) cellMesh(x,y);
  G._visSync=-1;
}
function cellMesh(x,y){
  const k=idx(x,y), old=R.cells.get(k); if(old){ mapRoot.remove(old); R.cells.delete(k); }
  const t=tile[k]; if(!['wall','hard','hole','crate','rubble'].includes(t)) return;
  const g=new THREE.Group(); g.position.set(x,0,y);
  if(t==='rubble'){ for(let i=0;i<6;i++){ const d=new THREE.Mesh(GEO.box,mkStd(i%2?0x7a5231:0x5a3b22)); const s=.08+((x*7+y*3+i*5)%7)*.025; d.scale.set(s,s*.8,s*1.2); d.position.set(((x*13+i*29)%9)/9-.45,s*.4,((y*17+i*23)%9)/9-.45); d.rotation.y=i; d.castShadow=true; g.add(d);} mapRoot.add(g); R.cells.set(k,g); g.userData={main:null}; return; }
  const p=t==='wall'?panels[panelOf[k]]:null; let h,col,opts={};
  if(t==='hard'){ h=1.5; col=0x4a525e; }
  else if(t==='crate'){ h=.62; col=0x6f6848; }
  else if(t==='hole'){ h=.34; col=0x2f353d; }
  else { h=1.2; col=0xffffff; opts={map:GEO.wood,roughness:.92};
    if(p.mirror){ col=0x0f3a4a; opts={transparent:true,opacity:G.side==='def'?.42:.88,emissive:0x04303c,metalness:.6,roughness:.12}; }
    else if(p.reinforced&&(G.side==='def'||p.seenByAtk)){ col=0xffffff; opts={map:GEO.steel,metalness:.35,roughness:.55}; } }
  if(t==='hole'){ col=0x4a3320; }
  const m=new THREE.Mesh(GEO.box,mkStd(col,opts)); const w=t==='crate'?.82:1; m.scale.set(w,h,w); m.position.y=h/2; m.castShadow=t!=='hole'; m.receiveShadow=true; g.add(m);
  g.userData={main:m,base:new THREE.Color(col),panel:p?p.id:-1,h};
  if(t==='hole'){ const inn=new THREE.Mesh(GEO.box,mkBasic(0x020304)); inn.scale.set(.46,.02,.46); inn.position.y=h+.011; g.add(inn); }
  if(t==='crate'){ const b=new THREE.Mesh(GEO.box,mkBasic(0xb89a2a)); b.scale.set(.84,.05,.84); b.position.y=h*.5; g.add(b); }
  if(p&&p.reinforced&&!p.mirror&&(G.side==='def'||p.seenByAtk)){ const fr=new THREE.Mesh(GEO.box,mkStd(0x3a414a,{metalness:.3,roughness:.6})); fr.scale.set(1.04,.06,1.04); fr.position.y=h*.5; g.add(fr); const rail=new THREE.Mesh(GEO.box,mkBasic(0xffd21f)); rail.scale.set(1.03,.05,1.03); rail.position.y=.05; g.add(rail); }
  if(p&&p.electrified&&(G.side==='def'||p.elecSeen)){ const e=new THREE.Mesh(GEO.box,mkBasic(0x4fb3ff,{transparent:true,opacity:.85})); e.scale.set(1.06,.07,1.06); e.position.y=h*.55; g.add(e); g.userData.elec=e; }
  if(p&&p.mirror){ const e=new THREE.Mesh(GEO.box,mkBasic(0x2fe4ff)); e.scale.set(1.03,.04,1.03); e.position.y=h; g.add(e); }
  mapRoot.add(g); R.cells.set(k,g);
}
function refreshPanel(p){ for(const c of p.cells) cellMesh(c.x,c.y); G._visSync=-1; }

/* ---------- units ---------- */
function makeFigure(team,opColor,isHost){
  const ally=team===G.side, tc=isHost?0xffb13b:ally?0x2fe4ff:0xff3b54;
  const g=new THREE.Group(), rot=new THREE.Group(); g.add(rot); const mats=[];
  const M=(c,o)=>{ const m=mkStd(c,Object.assign({transparent:true},o||{})); mats.push(m); return m; };
  const ring=flat(new THREE.Mesh(new THREE.RingGeometry(.34,.43,40),mkBasic(tc,{side:THREE.DoubleSide,transparent:true}))); ring.position.y=.045; g.add(ring); mats.push(ring.material);
  const sel=flat(new THREE.Mesh(new THREE.RingGeometry(.52,.6,40),mkBasic(0xffd21f,{side:THREE.DoubleSide}))); sel.position.y=.05; sel.visible=false; g.add(sel);
  const body=new THREE.Mesh(new THREE.CylinderGeometry(.19,.24,.62,16),M(isHost?0xd9cbb3:ally?0x1f6275:0x7c2234)); body.position.y=.34; body.castShadow=true; rot.add(body);
  const band=new THREE.Mesh(new THREE.CylinderGeometry(.205,.21,.09,16),M(isHost?0xffb13b:new THREE.Color(opColor))); band.position.y=.54; rot.add(band);
  const head=new THREE.Mesh(new THREE.SphereGeometry(.15,16,12),M(isHost?0xffe0b8:ally?0xd4f8ff:0xffd0d6)); head.position.y=.8; head.castShadow=true; rot.add(head);
  let shield=null;
  if(!isHost){
    const visor=new THREE.Mesh(GEO.box,M(0x0d1117)); visor.scale.set(.07,.05,.2); visor.position.set(.12,.82,0); rot.add(visor);
    const gun=new THREE.Mesh(GEO.box,M(0xdfe6ee,{metalness:.5,roughness:.4})); gun.scale.set(.52,.07,.07); gun.position.set(.3,.55,.13); gun.castShadow=true; rot.add(gun);
    const arrowG=new THREE.BufferGeometry(); arrowG.setAttribute('position',new THREE.Float32BufferAttribute([.5,.046,-.13, .5,.046,.13, .74,.046,0],3));
    const arrow=new THREE.Mesh(arrowG,mkBasic(tc,{side:THREE.DoubleSide,transparent:true})); mats.push(arrow.material); rot.add(arrow);
    shield=new THREE.Mesh(GEO.box,M(0xa8d4ff,{opacity:.7,metalness:.4,roughness:.3,emissive:0x0b2233})); shield.scale.set(.07,.78,.62); shield.position.set(.38,.42,0); shield.castShadow=true; rot.add(shield); shield.visible=false;
  }
  return {g,rot,sel,shield,mats,op:1,dispA:0};
}
function syncUnits(t){
  for(const u of G.units){ let m=R.units.get(u); if(!m){ m=makeFigure(u.team,u.c,false); m.dispA=u.facing; R.units.set(u,m); dynRoot.add(m.g); }
    const show=u.alive&&u.placed!==false&&shownToPlayer(u); m.g.visible=show; if(!show) continue;
    m.g.position.set(u.rx,u.rz||0,u.ry); if(u.rz>0){ m.rot.rotation.z=Math.sin(t/180)*.06; } else m.rot.rotation.z=0; m.dispA+=angNorm(u.facing-m.dispA)*.3; m.rot.rotation.y=-m.dispA;
    m.sel.visible=G.ui.unit===u; if(m.sel.visible){ const s=1+.07*Math.sin(t/160); m.sel.scale.set(s,s,s); }
    m.shield.visible=!!u.shield;
    const op=(u.ended&&G.phase===u.team)?.42:u.ghost?.5:1; if(m.op!==op){ m.op=op; for(const mt of m.mats) mt.opacity=op; }
  }
  const h=G.hostage;
  if(h){ if(!R.host){ R.host=makeFigure('host',0xffb13b,true); dynRoot.add(R.host.g); } R.host.g.visible=h.alive; R.host.g.position.set(h.rx,0,h.ry); const f=h.escort?Math.atan2(h.escort.ry-h.ry,h.escort.rx-h.rx):Math.PI/2; R.host.dispA+=angNorm(f-R.host.dispA)*.2; R.host.rot.rotation.y=-R.host.dispA; }
}
function clearDynamic(){
  for(const m of R.units.values()) dynRoot.remove(m.g); R.units.clear();
  if(R.host){ dynRoot.remove(R.host.g); R.host=null; }
  for(const k of ['drones','cams','elec','gas','marks','polys']){ for(const o of R[k].values()) dynRoot.remove(o.g||o); R[k].clear(); }
  for(const f of R.fx) if(f.m) dynRoot.remove(f.m); R.fx=[];
  for(const el of R.labels.values()) el.remove(); R.labels.clear();
  for(const p of R.pops) p.el.remove(); R.pops=[];
}

/* ---------- vision polygons & fog ---------- */
function mkPoly(){ const fill=new THREE.Mesh(new THREE.BufferGeometry(),mkBasic(0xffffff,{transparent:true,depthWrite:false,side:THREE.DoubleSide})); const line=new THREE.LineLoop(new THREE.BufferGeometry(),new THREE.LineBasicMaterial({transparent:true})); fill.renderOrder=3; line.renderOrder=4; const g=new THREE.Group(); g.add(fill); g.add(line); return {g,fill,line}; }
function setPoly(o,u,pts){
  const n=pts.length, pos=new Float32Array(n*9), lp=new Float32Array(n*3);
  for(let i=0;i<n;i++){ const a=pts[i], b=pts[(i+1)%n]; pos.set([u.x,.04,u.y,a[0],.04,a[1],b[0],.04,b[1]],i*9); lp.set([a[0],.045,a[1]],i*3); }
  o.fill.geometry.dispose(); o.fill.geometry=new THREE.BufferGeometry(); o.fill.geometry.setAttribute('position',new THREE.BufferAttribute(pos,3));
  o.line.geometry.dispose(); o.line.geometry=new THREE.BufferGeometry(); o.line.geometry.setAttribute('position',new THREE.BufferAttribute(lp,3));
}
function syncVision(){
  if(G._visSync===G.ver) return; G._visSync=G.ver;
  const title=G.state!=='game'||G.cineOn, tv=title?null:teamVision(G.side);
  for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const k=idx(x,y), v=title||tv.all.has(k), i=((H-1-y)*W+x)*4; fogData[i]=3; fogData[i+1]=5; fogData[i+2]=9; fogData[i+3]=v?0:175; }
  fogTex.needsUpdate=true;
  for(const [k,g] of R.cells){ const m=g.userData.main; if(!m) continue; const v=title||tv.all.has(k); m.material.color.copy(g.userData.base).multiplyScalar(v?1:.36); }
  const want=new Set();
  if(!title) for(const u of G.units){ if(!u.alive||u.placed===false) continue; const ally=u.team===G.side; if(!ally&&!enemyVisible(G.side,u)) continue; want.add(u);
    let o=R.polys.get(u); if(!o){ o=mkPoly(); R.polys.set(u,o); dynRoot.add(o.g); } setPoly(o,u,visionPoly(u)); o.ally=ally; }
  for(const [u,o] of R.polys) if(!want.has(u)){ dynRoot.remove(o.g); R.polys.delete(u); }
}
function stylePolys(){ for(const [u,o] of R.polys){ const sel=G.ui.unit===u; o.fill.material.color.setHex(o.ally?0x2fe4ff:0xff3b54); o.fill.material.opacity=o.ally?(sel?.2:.07):.14; o.line.material.color.setHex(o.ally?0x2fe4ff:0xff3b54); o.line.material.opacity=o.ally?(sel?.85:.3):.6; } }

/* ---------- overlay: reach cells ---------- */
let ovKey='';
function syncOverlay(){
  const ui=G.ui, key=ui.reachKey||''; if(key===ovKey) return; ovKey=key; ovData.fill(0);
  if(ui.reach){ const col=ui.isDrone?[124,240,200]:[47,228,255]; for(let k=0;k<W*H;k++){ if(ui.reach[k]<=ui.budget+.5&&ui.reach[k]>0){ const x=k%W, y=(k/W)|0, i=((H-1-y)*W+x)*4; ovData[i]=col[0]; ovData[i+1]=col[1]; ovData[i+2]=col[2]; ovData[i+3]=40; } } }
  ovTex.needsUpdate=true;
}

/* ---------- gadgets ---------- */
function syncGadgets(t){
  const vis=(x,y)=>cellVisible(G.side,x,y);
  // drones
  for(const d of G.drones){ let o=R.drones.get(d); if(!o){ o=new THREE.Group(); const b=new THREE.Mesh(GEO.box,mkStd(0x1b2129)); b.scale.set(.26,.08,.26); o.add(b); const col=d.team===G.side?0x7cf0c8:0xff3b54; for(const [a,c] of [[1,1],[1,-1],[-1,1],[-1,-1]]){ const r=flat(new THREE.Mesh(new THREE.RingGeometry(.06,.1,16),mkBasic(col,{side:THREE.DoubleSide}))); r.position.set(a*.17,.05,c*.17); o.add(r);} const eye=new THREE.Mesh(GEO.sph,mkBasic(col)); eye.scale.set(.04,.04,.04); eye.position.set(.14,0,0); o.add(eye); o.traverse(m=>m.castShadow=true); R.drones.set(d,o); dynRoot.add(o); if(d.team===G.side){ const rr=flat(new THREE.Mesh(GEO.ring,mkBasic(0x7cf0c8,{transparent:true,opacity:.4,side:THREE.DoubleSide}))); rr.scale.set(2.5,2.5,2.5); rr.position.y=-.3; o.userData.rr=rr; o.add(rr);} }
    o.visible=d.alive&&(d.team===G.side||vis(d.x,d.y)); o.position.set(d.rx,.34+Math.sin(t/200)*.04,d.ry); if(o.userData.rr) o.userData.rr.position.y=-o.position.y+.05; }
  // cams
  for(const c of G.cams){ let o=R.cams.get(c); if(!o){ o=new THREE.Group(); const pole=new THREE.Mesh(new THREE.CylinderGeometry(.035,.035,.9,8),mkStd(0x2a3038)); pole.position.y=.45; o.add(pole); const hd=new THREE.Mesh(GEO.box,mkStd(0x3d4652)); hd.scale.set(.24,.15,.18); hd.position.y=.95; o.add(hd); const led=new THREE.Mesh(GEO.sph,mkBasic(0xff3b54)); led.scale.set(.05,.05,.05); led.position.set(.13,.97,0); o.add(led); o.userData.led=led; o.traverse(m=>m.castShadow=true);
      if(G.side==='def'){ const rr=flat(new THREE.Mesh(GEO.ring,mkBasic(0xff6b8a,{transparent:true,opacity:.35,side:THREE.DoubleSide}))); rr.scale.set(3.2,3.2,3.2); rr.position.y=.05; o.add(rr); }
      R.cams.set(c,o); dynRoot.add(o); }
    o.visible=c.alive&&(G.side==='def'||c.discovered||vis(c.x,c.y)); o.position.set(c.x,0,c.y); o.rotation.y=Math.sin(t/1500+c.x)*.8; o.userData.led.visible=Math.sin(t/200)>0; }
  // electric fields
  for(const e of G.electrics){ let o=R.elec.get(e); if(!o){ o=new THREE.Group(); const disc=flat(new THREE.Mesh(GEO.disc,mkBasic(0x4fb3ff,{transparent:true,opacity:.07,depthWrite:false,side:THREE.DoubleSide}))); disc.scale.set(e.r,e.r,e.r); disc.position.y=.05; o.add(disc); const ring=flat(new THREE.Mesh(GEO.ring,mkBasic(0x4fb3ff,{transparent:true,opacity:.8,side:THREE.DoubleSide}))); ring.scale.set(e.r,e.r,e.r); ring.position.y=.055; o.add(ring); const nd=new THREE.Mesh(GEO.box,mkStd(0x1b3550,{emissive:0x0a3a6a})); nd.scale.set(.26,.2,.26); nd.position.y=.1; o.add(nd);
      const sp=new THREE.Line(new THREE.BufferGeometry(),new THREE.LineBasicMaterial({color:0x9fd8ff})); o.add(sp); o.userData.sp=sp; R.elec.set(e,o); dynRoot.add(o); }
    o.visible=G.side==='def'||e.discovered; o.position.set(e.x,0,e.y);
    if(o.visible&&Math.random()<.35){ const pts=[]; let a=Math.random()*6.28; for(let i=0;i<6;i++){ const r=i/5*e.r; pts.push(Math.cos(a)*r,.12+Math.random()*.25,Math.sin(a)*r); a+=(Math.random()-.5)*.8; } o.userData.sp.geometry.dispose(); o.userData.sp.geometry=new THREE.BufferGeometry(); o.userData.sp.geometry.setAttribute('position',new THREE.Float32BufferAttribute(pts,3)); } }
  // gas
  for(const g of G.gases){ let o=R.gas.get(g); if(!o){ o=new THREE.Group(); for(let i=0;i<9;i++){ const s=new THREE.Mesh(GEO.sph,mkBasic(0x8cff5a,{transparent:true,opacity:.16,depthWrite:false})); const a=i/9*6.28, r=i?g.r*.55:0; s.position.set(Math.cos(a)*r,.55+Math.random()*.3,Math.sin(a)*r); const sc=.75+Math.random()*.35; s.scale.set(sc,sc*.6,sc); o.add(s);} R.gas.set(g,o); dynRoot.add(o); }
    o.position.set(g.x,0,g.y); o.rotation.y=t/4000; }
  for(const [g,o] of R.gas) if(!G.gases.includes(g)){ dynRoot.remove(o); R.gas.delete(g); }
  // marks
  const live=new Set();
  for(const m of G.marks){ if(m.team!==G.side||m.until<=G.phaseCount) continue; live.add(m); let o=R.marks.get(m);
    if(!o){ o=new THREE.Group(); const r1=flat(new THREE.Mesh(new THREE.RingGeometry(.3,.38,32),mkBasic(0xff3b54,{transparent:true,side:THREE.DoubleSide}))); r1.position.y=.07; o.add(r1); const pin=new THREE.Mesh(new THREE.ConeGeometry(.12,.3,4),mkBasic(0xff3b54)); pin.rotation.x=Math.PI; pin.position.y=1.1; o.add(pin); o.userData.r1=r1; o.userData.pin=pin;
      const el=document.createElement('div'); el.className='mk'; el.textContent=m.kind==='shot'?'총성':m.kind==='breach'?'파괴음':'적 포착'; LAYER.appendChild(el); o.userData.el=el; R.marks.set(m,o); dynRoot.add(o); }
    const hidden=G.units.some(u=>u.alive&&u.team!==G.side&&Math.hypot(u.x-m.x,u.y-m.y)<.5&&enemyVisible(G.side,u)); o.visible=!hidden; o.userData.el.style.display=hidden?'none':'';
    o.position.set(m.x,0,m.y); const ph=((t-m.t0)/900)%1; o.userData.r1.scale.set(1+ph*1.5,1+ph*1.5,1); o.userData.r1.material.opacity=1-ph; o.userData.pin.position.y=1.1+Math.sin(t/200)*.08;
    const s=proj(m.x,1.35,m.y); o.userData.el.style.left=s.x+'px'; o.userData.el.style.top=s.y+'px'; }
  for(const [m,o] of R.marks) if(!live.has(m)){ dynRoot.remove(o); o.userData.el.remove(); R.marks.delete(m); }
  // electrified wall flicker
  for(const g of R.cells.values()) if(g.userData.elec) g.userData.elec.material.opacity=.35+.6*Math.abs(Math.sin(t/70+g.position.x));
}

/* ---------- UI helpers in 3D (path, aim, ranges, highlight) ---------- */
function ribbonGeo(pts,w,y){ const pos=[]; for(let i=0;i<pts.length-1;i++){ const a=pts[i], b=pts[i+1], dx=b.x-a.x, dy=b.y-a.y, l=Math.hypot(dx,dy)||1, nx=-dy/l*w/2, ny=dx/l*w/2;
  pos.push(a.x+nx,y,a.y+ny, a.x-nx,y,a.y-ny, b.x+nx,y,b.y+ny, a.x-nx,y,a.y-ny, b.x-nx,y,b.y-ny, b.x+nx,y,b.y+ny); }
  const g=new THREE.BufferGeometry(); g.setAttribute('position',new THREE.Float32BufferAttribute(pos,3)); return g; }
let pathKey='';
function syncUIHelpers(t){
  const ui=G.ui;
  const P=ui.path&&ui.path.length>1?ui.path:null, k=P?P.length+':'+P[P.length-1].x.toFixed(2)+P[P.length-1].y.toFixed(2):'';
  if(k!==pathKey){ pathKey=k; R.path.geometry.dispose(); R.path.geometry=P?ribbonGeo(P,.1,.07):new THREE.BufferGeometry(); }
  R.path.visible=!!P; R.pathEnd.visible=!!P; if(P){ const l=P[P.length-1]; R.pathEnd.position.set(l.x,.07,l.y); }
  const pl=$('#pathLbl'); if(P){ const l=P[P.length-1], s=proj(l.x,0,l.y); pl.classList.remove('hidden'); pl.style.left=s.x+'px'; pl.style.top=s.y+'px'; pl.innerHTML=`${pathLen(P).toFixed(1)}<small> / ${ui.budget.toFixed(1)}m</small>`; } else pl.classList.add('hidden');
  const A=ui.mode==='fire'&&ui.cast; R.aim.visible=!!A; R.aimDot.visible=!!A;
  if(A){ const u=ui.unit; R.aim.geometry.dispose(); R.aim.geometry=ribbonGeo([{x:u.x,y:u.y},ui.cast.end],.05,.08); R.aimDot.position.set(ui.cast.end.x,.08,ui.cast.end.y); const s=1+.15*Math.sin(t/90); R.aimDot.scale.set(s,s,s); }
  const sp=ui.mode==='ability'&&ui.spec;
  R.range.visible=!!(sp&&sp.range); if(R.range.visible){ const u=ui.unit, r=sp.range; R.range.position.set(u.x,.05,u.y); R.range.scale.set(r,r,r); }
  const showEff=sp&&sp.kind==='point'&&G.hover; R.eff.visible=R.effRing.visible=!!showEff;
  if(showEff){ const h=G.hover, ok=sp.valid(h.fx,h.fy), r=sp.radius; for(const m of [R.eff,R.effRing]){ m.position.set(h.fx,m===R.eff?.05:.055,h.fy); m.scale.set(r,r,r); m.material.color.setHex(ok?0xffd21f:0xff3b54); } }
  R.hover.visible=G.state==='game'&&!G.cineOn&&!!G.hover; if(G.hover) R.hover.position.set(G.hover.fx,.05,G.hover.fy);
  // panel highlight
  for(const g of R.cells.values()){ const m=g.userData.main; if(!m) continue; const pid=g.userData.panel; const v=ui.validPanels&&pid>=0&&ui.validPanels.has(pid);
    const e=v?(ui.hoverPanel===pid?0x9a7d00:(Math.sin(t/200)>0?0x4a3c00:0x2a2200)):(g.userData.base&&m.material.emissive&&g.userData.panel>=0&&panels[pid].mirror?0x04303c:0);
    if(m.material.emissive&&m.material.emissive.getHex()!==e) m.material.emissive.setHex(e); }
  // ally highlight for stim
  for(const [u,m] of R.units){ const on=sp&&sp.kind==='ally'&&sp.valid(u); m.sel.visible=m.sel.visible||on; if(on) m.sel.material.color.setHex(0xb98cff); else m.sel.material.color.setHex(0xffd21f); }
}

/* ---------- FX ---------- */
function fxTracer(a,b,hit){
  const ox=hit?0:(Math.random()-.5)*.9, oy=hit?0:(Math.random()-.5)*.9; const x1=b.rx+ox, y1=b.ry+oy, dx=x1-a.rx, dy=y1-a.ry, l=Math.hypot(dx,dy);
  const m=new THREE.Mesh(GEO.box,mkBasic(0xfff0b4,{transparent:true})); m.scale.set(l,.035,.035); m.position.set((a.rx+x1)/2,.55,(a.ry+y1)/2); m.rotation.y=-Math.atan2(dy,dx); dynRoot.add(m); R.fx.push({m,t0:now(),dur:200,k:'fade'});
  const f=new THREE.Mesh(GEO.sph,mkBasic(0xffd27a,{transparent:true})); f.position.set(a.rx+Math.cos(a.facing)*.55,.55,a.ry+Math.sin(a.facing)*.55); f.scale.set(.14,.14,.14); dynRoot.add(f); R.fx.push({m:f,t0:now(),dur:90,k:'fade'});
}
function fxRing(x,y,col,d=600,r=1){ const m=flat(new THREE.Mesh(GEO.ring,mkBasic(col,{transparent:true,side:THREE.DoubleSide}))); m.position.set(x,.08,y); dynRoot.add(m); R.fx.push({m,t0:now(),dur:d,k:'ring',r}); }
function boom(x,y,col=0xffb13b,n=22){
  for(let i=0;i<n;i++){ const m=new THREE.Mesh(GEO.box,mkBasic(Math.random()<.5?col:0x8a939e,{transparent:true})); const s=.05+Math.random()*.07; m.scale.set(s,s,s); m.position.set(x,.5,y); dynRoot.add(m); const a=Math.random()*6.28, v=.03+Math.random()*.07; R.fx.push({m,t0:now(),dur:800,k:'part',vx:Math.cos(a)*v,vy:.06+Math.random()*.08,vz:Math.sin(a)*v}); }
  const f=new THREE.Mesh(GEO.sph,mkBasic(0xffc060,{transparent:true})); f.position.set(x,.5,y); dynRoot.add(f); R.fx.push({m:f,t0:now(),dur:350,k:'flash'});
  fxRing(x,y,col,500,1.4); G.shake=Math.max(G.shake,.25);
}
function fxArc(a,bx,by,col,d){ const m=new THREE.Mesh(GEO.sph,mkBasic(col)); m.scale.set(.1,.1,.1); dynRoot.add(m); R.fx.push({m,t0:now(),dur:d,k:'arc',x0:a.x,y0:a.y,x1:bx,y1:by}); }
function popup(o,text,col,big){ const el=document.createElement('div'); el.className='pop'+(big?' big':''); el.textContent=text; el.style.color=col; LAYER.appendChild(el); R.pops.push({el,x:o.rx!=null?o.rx:o.x,y:o.ry!=null?o.ry:o.y,t0:now(),off:(Math.random()-.5)*30}); }
function syncFx(t){
  if(G.frozen) return;
  R.fx=R.fx.filter(f=>{ const p=(t-f.t0)/f.dur; if(p>=1){ dynRoot.remove(f.m); return false; }
    if(f.k==='fade') f.m.material.opacity=1-p;
    else if(f.k==='ring'){ const s=(.3+p)*f.r; f.m.scale.set(s,s,s); f.m.material.opacity=1-p; }
    else if(f.k==='part'){ f.m.position.x+=f.vx; f.m.position.z+=f.vz; f.m.position.y=Math.max(.03,f.m.position.y+f.vy); f.vy-=.008; f.m.material.opacity=1-p; }
    else if(f.k==='flash'){ const s=.2+p*.8; f.m.scale.set(s,s,s); f.m.material.opacity=1-p; }
    else if(f.k==='arc'){ f.m.position.set(f.x0+(f.x1-f.x0)*p,.6+Math.sin(p*Math.PI)*2.2,f.y0+(f.y1-f.y0)*p); }
    return true; });
  R.pops=R.pops.filter(p=>{ const q=(t-p.t0)/1100; if(q>=1){ p.el.remove(); return false; } const s=proj(p.x,1.3,p.y); p.el.style.left=(s.x+p.off)+'px'; p.el.style.top=(s.y-q*40)+'px'; p.el.style.opacity=1-q*q; return true; });
}

/* ---------- DOM labels ---------- */
function syncLabels(t){
  const list=[...G.units]; if(G.hostage) list.push(G.hostage);
  for(const u of list){ let el=R.labels.get(u);
    if(!el){ el=document.createElement('div'); el.className='ul '+(u.team==='host'?'host':u.team===G.side?'ally':'foe'); LAYER.appendChild(el); R.labels.set(u,el); el._k=''; }
    const show=G.state==='game'&&!G.cineOn&&u.alive&&u.placed!==false&&(u.team==='host'||shownToPlayer(u)); if(!show){ el.style.display='none'; continue; } el.style.display='';
    const s=proj(u.rx,1.2+(u.rz||0),u.ry); el.style.left=s.x+'px'; el.style.top=s.y+'px';
    const alert=t-(u.alertT||-1e9)<1800, ended=u.ended&&G.phase===u.team;
    const k=[u.hp,ended,alert,u.lockFacing,u.escort,u.team==='host'&&!!u.escort].join('|');
    if(k!==el._k){ el._k=k; el.innerHTML=`${alert?'<div class="ul-a">!</div>':''}${ended?'<div class="ul-t">행동 종료</div>':''}<div class="ul-n">${u.team==='host'?(u.escort?'인질 · 호송 중':'인질'):u.key}${u.lockFacing&&u.team===G.side?' <em>LOCK</em>':''}</div><div class="ul-hp"><i style="width:${100*u.hp/u.maxHp}%"></i></div>`; }
  }
}


/* ---------- house exterior (cinematic only) ---------- */
function makeShingleTex(){ const c2=document.createElement('canvas'); c2.width=c2.height=128; const c=c2.getContext('2d');
  c.fillStyle='#5a2a22'; c.fillRect(0,0,128,128);
  for(let r=0;r<8;r++){ const off=(r%2)*8; for(let i=-1;i<9;i++){ const x=i*16+off, y=r*16; c.fillStyle=['#6e3328','#7a3a2d','#63302a','#733629'][(i*3+r)&3]; c.fillRect(x+1,y+1,14,14); c.fillStyle='rgba(0,0,0,.35)'; c.fillRect(x,y+13,16,3); } }
  const t=new THREE.CanvasTexture(c2); t.wrapS=t.wrapT=THREE.RepeatWrapping; t.repeat.set(10,4); t.anisotropy=4; return t; }
function gable(g,x0,x1,z0,z1,by,rise,ax,mat,endMat){
  const ov=.35; x0-=ov; x1+=ov; z0-=ov; z1+=ov; const cx=(x0+x1)/2, cz=(z0+z1)/2;
  if(ax==='x'){ const L=x1-x0, hw=(z1-z0)/2, a=Math.atan(rise/hw), sl=Math.hypot(hw,rise);
    for(const s of [-1,1]){ const m=new THREE.Mesh(GEO.box,mat); m.scale.set(L,.13,sl); m.position.set(cx,by+rise/2,cz+s*hw/2); m.rotation.x=s<0?-a:a; m.castShadow=m.receiveShadow=true; g.add(m); }
    for(const x of [x0+.35,x1-.35]){ const geo=new THREE.BufferGeometry(); geo.setAttribute('position',new THREE.Float32BufferAttribute([x,by,z0+.35, x,by,z1-.35, x,by+rise-.08,cz],3)); geo.setAttribute('uv',new THREE.Float32BufferAttribute([0,0,2,0,1,1],2)); geo.computeVertexNormals(); g.add(new THREE.Mesh(geo,endMat)); }
    const rd=new THREE.Mesh(GEO.box,mkStd(0x2e1814)); rd.scale.set(L+.05,.14,.24); rd.position.set(cx,by+rise+.02,cz); g.add(rd);
  } else { const L=z1-z0, hw=(x1-x0)/2, a=Math.atan(rise/hw), sl=Math.hypot(hw,rise);
    for(const s of [-1,1]){ const m=new THREE.Mesh(GEO.box,mat); m.scale.set(sl,.13,L); m.position.set(cx+s*hw/2,by+rise/2,cz); m.rotation.z=s<0?a:-a; m.castShadow=m.receiveShadow=true; g.add(m); }
    for(const z of [z0+.35,z1-.35]){ const geo=new THREE.BufferGeometry(); geo.setAttribute('position',new THREE.Float32BufferAttribute([x0+.35,by,z, x1-.35,by,z, cx,by+rise-.08,z],3)); geo.setAttribute('uv',new THREE.Float32BufferAttribute([0,0,2,0,1,1],2)); geo.computeVertexNormals(); g.add(new THREE.Mesh(geo,endMat)); }
    const rd=new THREE.Mesh(GEO.box,mkStd(0x2e1814)); rd.scale.set(.24,.14,L+.05); rd.position.set(cx,by+rise+.02,cz); g.add(rd);
  }
}
function simpleHouse(g,cx,cz,w,d,roofM,wallM,rot){
  const h=new THREE.Group(); h.position.set(cx,0,cz); h.rotation.y=rot||0;
  const b=new THREE.Mesh(GEO.box,wallM); b.scale.set(w,2.2,d); b.position.y=1.1; b.castShadow=b.receiveShadow=true; h.add(b);
  gable(h,-w/2,w/2,-d/2,d/2,2.2,1.9,'x',roofM,wallM);
  const wm=mkBasic(0xffc46e); for(const x of [-w/4,w/4]){ const wn=new THREE.Mesh(GEO.box,wm); wn.scale.set(.7,.55,.05); wn.position.set(x,1.3,d/2+.03); h.add(wn); }
  g.add(h);
}
function makeHouse(){
  const g=new THREE.Group(), roofM=mkStd(0xffffff,{map:makeShingleTex(),roughness:.85}), roofM2=mkStd(0x55606b,{roughness:.7});
  const woodM=mkStd(0xffffff,{map:GEO.wood,roughness:.9,side:THREE.DoubleSide}), sidingM=mkStd(0xd9d3c4,{roughness:.9,side:THREE.DoubleSide}), trimM=mkStd(0xf1ede4);
  // hide interior: flat ceiling slab
  const eave=new THREE.Mesh(GEO.box,mkStd(0x2a1f18)); eave.scale.set(20.2,.1,13.2); eave.position.set(12.5,1.28,11); g.add(eave);
  // left wing (single story, ridge along z)
  gable(g,2.5,8.5,4.5,17.5,1.3,1.7,'z',roofM,woodM);
  // main two-storey block: upper walls + cross gable (ridge along x)
  const up=new THREE.Mesh(GEO.box,sidingM); up.scale.set(8,1.5,9); up.position.set(12.5,2.05,10.5); up.castShadow=up.receiveShadow=true; g.add(up);
  for(const y of [1.32,2.78]){ const tr=new THREE.Mesh(GEO.box,trimM); tr.scale.set(8.1,.08,9.1); tr.position.set(12.5,y,10.5); g.add(tr); }
  gable(g,8.5,16.5,6,15,2.8,2.1,'x',roofM,sidingM);
  // right wing: garage (flatter roof, ridge along z)
  gable(g,16.5,22.5,4.5,17.5,1.3,1.2,'z',roofM2,woodM);
  // lower roof over rest of main footprint (front & back strips)
  for(const [z0,z1] of [[4.5,6],[15,17.5]]){ const f=new THREE.Mesh(GEO.box,roofM2); f.scale.set(8.4,.12,z1-z0+.35); f.position.set(12.5,1.42,(z0+z1)/2); f.castShadow=true; g.add(f); }
  // windows
  const wm=mkBasic(0xffc46e), fm=mkStd(0xf1ede4);
  const win=(x,y,z,ry,w=.6,h=.5)=>{ const f=new THREE.Mesh(GEO.box,fm); f.scale.set(w+.12,h+.12,.06); f.position.set(x,y,z); f.rotation.y=ry; g.add(f); const p=new THREE.Mesh(GEO.box,wm); p.scale.set(w,h,.07); p.position.set(x,y,z); p.rotation.y=ry; g.add(p); };
  for(const x of [5,7.5]) win(x,.72,4.48,0); for(const x of [5,8,15]) win(x,.72,17.52,0);
  for(const z of [7,15.5]) win(2.48,.72,z,Math.PI/2);
  for(const x of [10.2,14.8]) { win(x,2.05,5.97,0,.8,.6); win(x,2.05,15.03,0,.8,.6); }
  win(8.47,2.05,10.5,Math.PI/2,.8,.6); win(16.53,2.05,10.5,Math.PI/2,.8,.6);
  // garage door
  const gd=new THREE.Mesh(GEO.box,mkStd(0xffffff,{map:GEO.steel,roughness:.6})); gd.scale.set(3.2,1.05,.08); gd.position.set(19.5,.55,4.46); g.add(gd);
  const gdf=new THREE.Mesh(GEO.box,trimM); gdf.scale.set(3.45,.1,.1); gdf.position.set(19.5,1.12,4.45); g.add(gdf);
  // front porch + door
  const pr=new THREE.Mesh(GEO.box,roofM2); pr.scale.set(3.2,.1,1.3); pr.position.set(12.5,1.45,3.95); pr.rotation.x=-.12; pr.castShadow=true; g.add(pr);
  for(const x of [11.2,13.8]){ const po=new THREE.Mesh(GEO.box,trimM); po.scale.set(.12,1.45,.12); po.position.set(x,.72,3.4); po.castShadow=true; g.add(po); }
  const step=new THREE.Mesh(GEO.box,mkStd(0x8d8a84)); step.scale.set(2.6,.12,1); step.position.set(12.5,.06,3.9); g.add(step);
  const lamp=new THREE.PointLight(0xffb45a,1.3,7); lamp.position.set(12.5,1.2,3.6); g.add(lamp);
  // chimney
  const ch=new THREE.Mesh(GEO.box,mkStd(0x7b4a3a)); ch.scale.set(.7,1.6,.7); ch.position.set(15,4.1,9); ch.castShadow=true; g.add(ch);
  // driveway, car, mailbox
  const drv=flat(new THREE.Mesh(new THREE.PlaneGeometry(3.6,7),mkStd(0x55585c,{roughness:1}))); drv.position.set(19.5,.012,.9); drv.receiveShadow=true; g.add(drv);
  const car=new THREE.Group(); const cb=new THREE.Mesh(GEO.box,mkStd(0x1d4f7a,{metalness:.5,roughness:.35})); cb.scale.set(1.25,.45,2.5); cb.position.y=.38; car.add(cb);
  const cc=new THREE.Mesh(GEO.box,mkStd(0x0e1a24,{metalness:.6,roughness:.2})); cc.scale.set(1.1,.38,1.3); cc.position.set(0,.78,-.1); car.add(cc);
  for(const [x,z] of [[-.6,-.8],[.6,-.8],[-.6,.8],[.6,.8]]){ const w=new THREE.Mesh(new THREE.CylinderGeometry(.2,.2,.14,12),mkStd(0x111111)); w.rotation.z=Math.PI/2; w.position.set(x,.2,z); car.add(w); }
  car.traverse(m=>m.castShadow=true); car.position.set(19.5,0,1.6); g.add(car);
  const mb=new THREE.Mesh(GEO.box,mkStd(0x2b2b2b)); mb.scale.set(.25,.2,.4); mb.position.set(15.5,.8,-.9); g.add(mb); const mp=new THREE.Mesh(GEO.box,mkStd(0x6a5a44)); mp.scale.set(.06,.7,.06); mp.position.set(15.5,.35,-.9); g.add(mp);
  // road + neighbours
  const road=flat(new THREE.Mesh(new THREE.PlaneGeometry(90,4.5),mkStd(0x1a1d21,{roughness:1}))); road.position.set(12.5,.011,-4.2); road.receiveShadow=true; g.add(road);
  for(let x=-30;x<56;x+=3){ const d=flat(new THREE.Mesh(new THREE.PlaneGeometry(1.4,.12),mkBasic(0xc9a227))); d.position.set(x,.015,-4.2); g.add(d); }
  const nWall=mkStd(0xcfc6b4,{roughness:.9}), nWall2=mkStd(0xa9b4bc,{roughness:.9}), nRoof=mkStd(0x3c4650,{roughness:.8}), nRoof2=mkStd(0x6b3a2c,{roughness:.8});
  simpleHouse(g,0,-12,9,7,nRoof,nWall,Math.PI); simpleHouse(g,14,-12.5,10,7.5,nRoof2,nWall2,Math.PI); simpleHouse(g,28,-12,9,7,nRoof,nWall,Math.PI);
  simpleHouse(g,-13,9,8,10,nRoof2,nWall2,Math.PI/2); simpleHouse(g,38,11,8,10,nRoof,nWall,-Math.PI/2);
  // trees & fence
  const trunkM=mkStd(0x4a3423), leafM=mkStd(0x2c4a2e,{roughness:.9}), leafM2=mkStd(0x355a33,{roughness:.9});
  const tree=(x,z,s)=>{ const t=new THREE.Mesh(new THREE.CylinderGeometry(.12*s,.16*s,1.2*s,8),trunkM); t.position.set(x,.6*s,z); t.castShadow=true; g.add(t);
    for(let i=0;i<3;i++){ const c=new THREE.Mesh(new THREE.ConeGeometry((1.2-i*.28)*s,1.3*s,10),i%2?leafM2:leafM); c.position.set(x,(1.3+i*.65)*s,z); c.castShadow=true; g.add(c); } };
  [[-3,6,1.9],[-2.5,14,1.7],[-4,20,2],[28,6,1.8],[27.5,14,2],[29,20,1.7],[-1,24,1.8],[21,24,1.9],[6,23.5,1.6]].forEach(a=>tree(...a));
  const postM=mkStd(0xe8e2d6);
  for(let x=-1;x<=26;x+=1.5){ for(const z of [-1.4,22]){ if(z<0&&x>17.4&&x<21.6) continue; const p=new THREE.Mesh(GEO.box,postM); p.scale.set(.1,.6,.1); p.position.set(x,.3,z); g.add(p);} }
  for(const [x0,x1,z] of [[-1,17.4,-1.4],[21.6,26,-1.4],[-1,26,22]]){ const rl=new THREE.Mesh(GEO.box,postM); rl.scale.set(x1-x0,.06,.06); rl.position.set((x0+x1)/2,.45,z); g.add(rl); }
  g.visible=false; return g;
}
/* ---------- helicopter ---------- */
function makeHeli(){
  const g=new THREE.Group(), body=new THREE.Group(); g.add(body);
  const hull=mkStd(0x2e3a33,{roughness:.6,metalness:.3}), dark=mkStd(0x151a1f,{roughness:.5,metalness:.4});
  const add=(geo,mat,sx,sy,sz,x,y,z)=>{ const m=new THREE.Mesh(geo,mat); m.scale.set(sx,sy,sz); m.position.set(x,y,z); m.castShadow=true; body.add(m); return m; };
  add(GEO.box,hull,1.9,.8,.95,0,0,0);
  add(GEO.sph,mkStd(0x2b8fa8,{roughness:.1,metalness:.6,transparent:true,opacity:.85}),.62,.42,.46,.95,.05,0);
  add(GEO.box,hull,2.1,.2,.2,-1.95,.15,0);
  add(GEO.box,hull,.35,.7,.08,-2.9,.45,0);
  add(GEO.box,hull,.3,.08,.7,-2.85,.2,0);
  add(GEO.box,dark,.6,.25,.6,0,.52,0);
  add(GEO.box,mkBasic(0xffd21f),1.92,.05,.97,0,-.15,0);
  for(const z of [-.45,.45]){ add(GEO.box,dark,1.8,.05,.06,0,-.72,z); add(GEO.box,dark,.05,.35,.05,.5,-.55,z); add(GEO.box,dark,.05,.35,.05,-.5,-.55,z); }
  const rotor=new THREE.Group(); rotor.position.set(0,.7,0); body.add(rotor);
  for(let i=0;i<4;i++){ const b=new THREE.Mesh(GEO.box,dark); b.scale.set(2.3,.025,.14); b.position.x=0; const p=new THREE.Group(); p.rotation.y=i*Math.PI/2; b.position.x=1.15; p.add(b); rotor.add(p); b.castShadow=true; }
  const disc=flat(new THREE.Mesh(GEO.disc,mkBasic(0x9aa4ae,{transparent:true,opacity:.08,side:THREE.DoubleSide,depthWrite:false}))); disc.scale.set(2.3,2.3,2.3); rotor.add(disc);
  const trot=new THREE.Group(); trot.position.set(-2.9,.5,.08); body.add(trot);
  for(let i=0;i<2;i++){ const b=new THREE.Mesh(GEO.box,dark); b.scale.set(.55,.06,.02); b.rotation.z=i*Math.PI/2; trot.add(b); }
  const beam=new THREE.Mesh(new THREE.ConeGeometry(1.4,5,24,1,true),mkBasic(0xfff4c2,{transparent:true,opacity:.035,depthWrite:false,side:THREE.DoubleSide})); beam.position.set(.6,-2.8,0); beam.visible=false;
  const rope=new THREE.Mesh(new THREE.CylinderGeometry(.03,.03,1,6),mkStd(0xd8c9a0)); rope.position.set(0,-.8,0); g.add(rope);
  g.scale.set(1.5,1.5,1.5);
  return {g,body,rotor,trot,rope};
}
function syncHeli(t){
  const h=G.heli; if(!R.heli){ R.heli=makeHeli(); scene.add(R.heli.g); }
  const o=R.heli; o.g.visible=h.vis; if(!h.vis) return;
  o.g.position.set(h.x,h.z+Math.sin(t/400)*.08,h.y); o.g.rotation.y=-h.hd; o.body.rotation.z=(h.tilt||0);
  o.rotor.rotation.y=t/28; o.trot.rotation.z=t/20;
  const L=Math.max(.001,h.rope)/1.5; o.rope.visible=h.rope>.02; o.rope.scale.set(1,L,1); o.rope.position.y=-.8-L/2;
}
function render3D(t){
  if(!renderer) return;
  const sh=G.shake>.02?(Math.random()-.5)*G.shake:0;

  if(G.cineFn) G.cineFn(t);
  if(G.state==='game') syncHeli(t);
  if(!R.house){ R.house=makeHouse(); scene.add(R.house); } R.house.visible=!!G.cineOn;
  G.cam.x+=sh; updateCamera(); G.cam.x-=sh;
  if(tile.length&&fogTex){ syncVision(); if(G.state==='game'){ stylePolys(); syncUnits(t); syncGadgets(t); syncOverlay(); syncUIHelpers(t); syncFx(t); } syncLabels(t); }
  renderer.render(scene,camera);
}
