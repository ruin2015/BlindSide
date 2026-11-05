/* =========================================================
   OPERATOR ILLUSTRATIONS — procedural angular ink-style SVG
   ========================================================= */
const ART_CACHE={}; let ART_N=0;
const INK='#121212', WHT='#f4f0e8', SKIN='#f2d3bd', SKIN2='#d8a98e';
function artRng(seed){ let s=seed%2147483647; if(s<=0) s+=2147483646; return ()=>{ s=s*16807%2147483647; return (s-1)/2147483646; }; }
const AP=a=>a.map(p=>p[0].toFixed(1)+','+p[1].toFixed(1)).join(' ');
const PG=(a,fill,sw=3.2,extra='')=>`<polygon points="${AP(a)}" fill="${fill}" stroke="${INK}" stroke-width="${sw}" stroke-linejoin="miter" ${extra}/>`;
const PN=(a,fill,extra='')=>`<polygon points="${AP(a)}" fill="${fill}" ${extra}/>`;
function abar(x1,y1,x2,y2,w1,w2=w1){ const a=Math.atan2(y2-y1,x2-x1), nx=-Math.sin(a), ny=Math.cos(a); return [[x1+nx*w1/2,y1+ny*w1/2],[x2+nx*w2/2,y2+ny*w2/2],[x2-nx*w2/2,y2-ny*w2/2],[x1-nx*w1/2,y1-ny*w1/2]]; }
function arect(cx,cy,w,h,ang=0){ const c=Math.cos(ang), s=Math.sin(ang); return [[-w/2,-h/2],[w/2,-h/2],[w/2,h/2],[-w/2,h/2]].map(([x,y])=>[cx+x*c-y*s,cy+x*s+y*c]); }
function astar(cx,cy,r1,r2,n,rot,R){ const a=[]; for(let i=0;i<n*2;i++){ const g=rot+i*Math.PI/n, r=(i%2?r2:r1)*(.65+R()*.7); a.push([cx+Math.cos(g)*r,cy+Math.sin(g)*r]); } return a; }
function afist(x,y,ang=0){ return PG(arect(x,y,34,30,ang),'#1a1a1a')+PN(arect(x+Math.cos(ang-1.57)*8,y+Math.sin(ang-1.57)*8,22,5,ang),'#4a4a4a'); }
function aarm(sx,sy,ex,ey,hx,hy,col=INK,cuff=WHT){
  const cx=ex+(hx-ex)*.72, cy=ey+(hy-ey)*.72;
  return PG(abar(sx,sy,ex,ey,42,34),col)+PG(abar(ex,ey,hx,hy,32,26),col)+PG(abar(cx,cy,hx,hy,30,27),cuff,2.4);
}
function ashards(R,n,cols,x0,y0,x1,y1,len=40){ let s=''; for(let i=0;i<n;i++){ const x=x0+R()*(x1-x0), y=y0+R()*(y1-y0), a=-.6+R()*.3, l=len*(.4+R()); s+=PN([[x,y],[x+Math.cos(a)*l,y+Math.sin(a)*l],[x+Math.cos(a)*l*.6+6*R(),y+Math.sin(a)*l*.6+8]],cols[i%cols.length]); } return s; }
function acasing(x,y,a,col){ return PG(arect(x,y,26,11,a),col,2)+PN(arect(x+Math.cos(a)*10,y+Math.sin(a)*10,5,11,a),'#6b4a14'); }
function bolt(pts,col,w){ return `<polyline points="${AP(pts)}" fill="none" stroke="${INK}" stroke-width="${w+5}" stroke-linejoin="miter"/><polyline points="${AP(pts)}" fill="none" stroke="${col}" stroke-width="${w}" stroke-linejoin="miter"/>`; }

/* ---------- base figure ---------- */
function afigure(o){
  const A=o.accent; let s='';
  // cape / scarf flowing right
  s+=PG([[258,226],[330,236],[398,300],[372,312],[392,352],[330,318],[300,300],[268,262]],o.cape||A);
  s+=PN([[330,236],[398,300],[372,312],[340,282]],INK);
  // torso
  s+=PG([[116,238],[178,206],[238,206],[300,238],[322,340],[314,522],[96,522],[90,340]],o.jacket||INK);
  s+=PG([[128,252],[184,216],[198,300],[152,366],[110,340]],WHT,2.6);
  s+=PN([[120,300],[160,286],[150,330],[112,346]],A);
  s+=PG([[232,214],[292,244],[306,300],[250,318]],'#262626',2.4);
  s+=PG([[182,216],[202,212],[274,424],[252,432]],A,2.4);
  s+=PG([[98,432],[318,418],[320,446],[98,460]],'#2b2b2b',2.4);
  s+=PG(arect(214,436,34,26,-.06),A,2.4)+PN(arect(214,436,14,10,-.06),INK);
  s+=PN([[150,470],[196,466],[190,522],[146,522]],A,'opacity=".85"');
  s+=PN([[260,380],[300,370],[308,420],[266,426]],'#303030');
  // collar
  s+=PG([[164,198],[200,232],[242,200],[256,222],[204,256],[152,224]],o.collar||WHT);
  // neck
  s+=PG([[188,184],[224,188],[222,216],[192,214]],SKIN2,2.4);
  // head
  s+=PG([[166,116],[238,104],[252,150],[234,196],[198,206],[172,178],[158,146]],SKIN);
  s+=PN([[232,118],[252,150],[234,196],[216,196],[230,152]],SKIN2);
  s+=PG([[238,138],[254,134],[252,166],[240,166]],SKIN,2.4);
  if(o.mask!=='hood'&&o.mask!=='helmet'&&o.mask!=='visor'){
    s+=PN([[176,148],[204,140],[207,153],[182,158]],INK)+PN([[189,145],[200,143],[202,153],[191,155]],o.eye||A)+PN([[192,146],[196,145],[196,149],[192,150]],WHT);
    s+=PN([[222,142],[236,139],[237,149],[223,152]],INK)+PN([[226,142],[233,141],[233,149],[227,150]],o.eye||A);
    s+=PN([[172,136],[206,126],[208,132],[176,142]],INK)+PN([[220,128],[238,124],[238,129],[222,133]],INK);
  }
  if(o.mask!=='gasmask'&&o.mask!=='hood') s+=PN([[196,184],[212,181],[212,184],[197,187]],'#7a3b2c');
  // hair
  if(o.mask!=='helmet'&&o.mask!=='hood'){
    const hc=o.hair||WHT;
    s+=PG([[150,162],[138,118],[158,126],[146,82],[178,104],[180,54],[204,94],[228,48],[234,92],[266,66],[252,108],[284,108],[256,132],[262,166],[246,150],[240,124],[212,114],[200,136],[188,114],[176,146],[168,130],[160,168]],hc);
    s+=PN([[178,104],[180,54],[196,88]],o.streak||A)+PN([[252,108],[284,108],[258,120]],o.streak||A)+PN([[146,82],[168,110],[158,126]],o.streak2||INK);
    s+=PG([[196,114],[214,112],[200,160],[190,150]],hc,2.4);
  }
  // masks
  if(o.mask==='helmet'){
    s+=PG([[148,132],[160,90],[206,70],[254,84],[270,126],[260,146],[244,124],[172,126],[160,150]],o.helm||INK);
    s+=PN([[176,86],[230,78],[236,90],[180,98]],'#333');
    s+=PG([[168,128],[250,120],[254,148],[170,154]],A,2.6)+PN([[176,132],[240,126],[242,131],[178,137]],WHT,'opacity=".8"');
    s+=PG([[172,158],[180,156],[196,204],[188,206]],INK,2);
  }
  if(o.mask==='gasmask'){
    s+=PG([[174,158],[242,148],[252,178],[228,208],[196,210],[174,188]],'#262626');
    s+=PG(arect(206,196,34,26,-.1),A,2.4)+PN(arect(206,196,20,4,-.1),INK)+PN(arect(206,188,20,3,-.1),INK);
    s+=PG(arect(246,186,20,30,.3),'#3a3a3a',2.2);
  }
  if(o.mask==='visor'){
    s+=PG([[160,136],[254,124],[258,154],[164,162]],INK);
    s+=PN([[170,146],[248,136],[248,142],[170,152]],A)+PN([[180,146],[200,143],[200,147],[180,150]],WHT);
    s+=PG(arect(252,150,16,28,.1),'#333',2);
  }
  if(o.mask==='hood'){
    s+=PG([[136,200],[146,108],[190,64],[252,74],[280,128],[272,210],[250,166],[238,130],[180,136],[164,196]],o.hoodc||INK);
    s+=PN([[168,134],[242,126],[246,160],[172,168]],'#050505');
    s+=PN([[184,148],[204,144],[205,150],[186,153]],A)+PN([[222,142],[234,140],[234,146],[223,148]],A);
    s+=PG([[176,166],[244,158],[240,196],[204,208],[180,192]],'#1d1d1d',2.4);
  }
  return s;
}

/* ---------- per-operator composition ---------- */
function artParts(key,A,R){
  let back='', front='', fx='', fig={accent:A};
  switch(key){
    case 'CRUSHER': fig.mask='helmet';
      front+=PG(abar(92,506,330,104,17),'#2a2a2a')+PN(abar(92,506,120,458,17),A);
      front+=PG(arect(334,98,128,58,Math.atan2(104-506,330-92)+Math.PI/2),INK)+PG(arect(334,98,96,22,Math.atan2(104-506,330-92)+Math.PI/2),A,2.4);
      front+=aarm(128,252,112,350,152,404)+afist(152,404,.4)+aarm(294,252,300,320,214,300)+afist(214,300,-.6);
      fx+=PG(astar(362,62,54,18,8,.3,R),WHT)+ashards(R,9,[WHT,INK,A],330,20,395,150,34);
      for(let i=0;i<5;i++) fx+=PG(arect(300+R()*90,20+R()*120,30,9,R()*3),i%2?'#8a6a45':WHT,2);
      break;
    case 'FLARE': fig.hair=WHT; fig.streak=A;
      fx+=`<path d="M40 276 Q 70 90 188 44" fill="none" stroke="${INK}" stroke-width="9" stroke-dasharray="16 10"/><path d="M40 276 Q 70 90 188 44" fill="none" stroke="${A}" stroke-width="4" stroke-dasharray="16 10"/>`;
      fx+=PG(arect(188,44,30,20,-.5),INK)+PN(arect(188,44,14,20,-.5),A)+PG(astar(214,36,40,14,7,.2,R),A)+PG(astar(214,36,20,8,6,.5,R),WHT,2);
      front+=aarm(294,252,256,330,210,346)+aarm(128,252,92,300,112,318);
      front+=PG(abar(26,296,196,320,28),INK)+PN(abar(40,298,190,318,7),A)+PG(arect(164,334,68,60,.12),'#262626')+PN(arect(164,334,52,8,.12),A)+PN(arect(164,352,52,6,.12),A);
      front+=PG(abar(196,320,290,352,32,26),'#2b2b2b')+PG(arect(110,286,40,16,.1),INK,2.4)+afist(210,352,.2)+afist(112,322,.1);
      fx+=PG(astar(22,296,70,20,9,0,R),A)+PG(astar(22,296,36,12,8,.3,R),WHT,2.4);
      break;
    case 'IGNIS': fig.mask='gasmask'; fig.hair=INK; fig.streak=A;
      front+=aarm(128,252,100,330,138,382)+aarm(294,252,314,322,268,372);
      front+=PG(arect(204,384,150,82,-.06),'#1b1b1b')+PN(arect(204,380,130,14,-.06),A);
      front+=PN([[150,412],[166,396],[182,412]],A)+PN([[230,408],[246,392],[262,408]],A)+PG(arect(204,360,26,26,-.06),A,2.4)+PN(arect(204,360,10,10,-.06),WHT);
      front+=afist(140,386,.2)+afist(268,376,-.2);
      front+=`<polyline points="170,344 160,300 186,286" fill="none" stroke="${A}" stroke-width="4"/><polyline points="240,340 256,300 236,282" fill="none" stroke="${WHT}" stroke-width="4"/>`;
      for(let i=0;i<26;i++){ const a=Math.PI*(.15+R()*.7), l=30+R()*120, x=204+(R()-.5)*120, y=426; fx+=`<line x1="${x}" y1="${y}" x2="${(x+Math.cos(a)*l*(R()<.5?-1:1)*.6).toFixed(1)}" y2="${(y+Math.sin(a)*l).toFixed(1)}" stroke="${i%3?A:WHT}" stroke-width="${2+R()*3}"/>`; }
      fx+=PG(astar(204,430,60,18,9,0,R),A)+PG(astar(204,430,30,10,8,.4,R),WHT,2);
      break;
    case 'SERUM': fig.hair=WHT; fig.streak=A; fig.streak2=A;
      front+=aarm(128,252,106,350,132,418)+PG(arect(140,456,100,62,-.1),WHT)+PG(arect(140,456,32,10,-.1),A,2)+PG(arect(140,456,10,32,-.1),A,2)+afist(132,420,.2);
      front+=aarm(294,252,338,196,320,128)+PG(arect(318,210,40,18,-1.1),WHT,2)+PN(arect(318,210,16,6,-1.1),A)+PN(arect(318,210,6,16,-1.1),A);
      front+=PG(abar(290,158,364,70,24),WHT)+PN(abar(298,148,350,86,14),A)+PG(abar(364,70,392,36,4),'#bbb',2)+PG(abar(290,158,268,184,7),'#444',2)+PG(arect(266,186,24,8,-.87),INK,2);
      front+=afist(320,128,-.9);
      for(let i=0;i<7;i++){ const x=40+R()*80, y=80+R()*200; fx+=PG(arect(x,y,30,13,R()*3),i%2?A:WHT,2)+PN(arect(x,y,2,13,0),INK); }
      fx+=PG(astar(388,34,30,10,7,0,R),A)+PG(astar(388,34,14,6,6,0,R),WHT,2);
      break;
    case 'BASTION': fig.mask='helmet'; fig.helm='#2c2c2c';
      back+=aarm(294,252,346,272,386,250)+PG(arect(398,240,56,24,-.12),INK)+afist(384,252,-.1)+PG(astar(422,236,26,9,7,0,R),A);
      front+=PG([[56,226],[256,200],[276,522],[66,522]],'#161616',4)+PN([[70,240],[244,216],[262,510],[80,510]],'none','stroke="'+A+'" stroke-width="5"');
      front+=PG(arect(160,262,104,28,-.13),'#1e2f3a')+PN(arect(160,258,90,6,-.13),A)+PN(arect(150,264,40,4,-.13),WHT,'opacity=".7"');
      front+=`<text x="112" y="380" font-family="Chakra Petch,Arial Black,sans-serif" font-weight="700" font-size="54" fill="${A}" transform="rotate(-6 160 380)" letter-spacing="4">R·07</text>`;
      for(let i=0;i<4;i++) front+=PN([[84+i*44,500],[112+i*44,470],[130+i*44,470],[102+i*44,500]],A);
      front+=afist(256,330,.1);
      fx+=PG(astar(118,318,26,8,8,0,R),WHT,2)+PG(astar(208,430,22,7,7,.4,R),WHT,2)+PG(astar(90,440,16,6,6,.2,R),A,2);
      fx+=acasing(330,120,.6,'#e0a030')+acasing(360,170,-.3,'#e0a030')+acasing(20,150,1.2,'#e0a030');
      break;
    case 'VOLT': fig.mask='visor'; fig.hair=INK; fig.streak=A;
      back+=PG(abar(250,452,372,112,20),INK)+PN(abar(260,420,300,310,8),A)+PG(arect(372,108,26,40,Math.atan2(112-452,372-250)+1.57),'#333',2);
      front+=aarm(294,252,310,350,290,424)+afist(290,424,.2);
      front+=aarm(128,252,90,326,128,378)+PG(arect(128,408,64,92,.2),INK)+[-26,0,26].map(d=>PN(arect(128-d*.2,408+d,70,7,.2),A)).join('')+PG(abar(118,358,112,332,6),'#666',2)+PG(abar(140,362,146,336,6),'#666',2)+afist(130,378,.2);
      fx+=bolt([[112,330],[80,290],[100,280],[60,230],[84,222],[40,170]],A,6)+bolt([[146,334],[190,290],[172,280],[222,250]],WHT,4)+bolt([[100,452],[56,480],[76,492],[30,520]],A,5)+bolt([[160,440],[200,480],[184,488],[226,516]],WHT,3);
      fx+=PG(astar(128,408,70,20,10,0,R),A,2,'opacity=".35"');
      break;
    case 'MIASMA': fig.mask='gasmask'; fig.hair=INK; fig.streak=A; fig.cape=A;
      back+=PN(astar(300,440,150,90,9,.2,R),A,'opacity=".45"')+PN(astar(90,300,120,70,8,0,R),A,'opacity=".35"');
      front+=aarm(128,252,100,340,120,420)+afist(120,420,.1);
      front+=aarm(294,252,334,200,306,150)+PG(arect(304,120,42,74,.3),INK)+PN(arect(304,122,44,12,.3),A)+`<circle cx="${320}" cy="${80}" r="11" fill="none" stroke="${INK}" stroke-width="5"/><circle cx="320" cy="80" r="11" fill="none" stroke="${A}" stroke-width="2"/>`+afist(306,152,-.4);
      fx+=PG([[20,520],[40,430],[90,440],[110,380],[170,420],[220,380],[250,440],[300,420],[330,470],[400,450],[400,522],[20,522]],A,3,'opacity=".78"')+PN([[60,520],[90,470],[140,480],[170,450],[210,480],[260,470],[300,500],[340,522]],WHT,'opacity=".35"');
      fx+=PG(astar(340,70,40,14,8,0,R),A,2,'opacity=".7"');
      break;
    case 'WRAITH': fig.mask='hood'; fig.cape=INK;
      back+=PG(abar(282,232,336,112,7),'#333',2)+PG(arect(338,108,18,24,.4),INK,2);
      for(let i=1;i<4;i++) back+=`<path d="M${338+i*16} ${86-i*6} a ${i*18} ${i*18} 0 0 1 ${-i*10} ${i*34}" fill="none" stroke="${A}" stroke-width="4"/>`;
      front+=aarm(128,252,114,350,126,432)+afist(126,432,.1);
      front+=aarm(294,252,300,330,218,212)+PG(arect(218,214,30,28,-.4),'#1a1a1a')+PG(abar(212,202,204,166,10),'#1a1a1a',2.4);
      for(let i=0;i<22;i++){ const y=230+R()*290, x=222+R()*150, w=20+R()*90; fx+=PN([[x,y],[x+w,y],[x+w,y+4+R()*14],[x,y+4+R()*14]],[A,INK,WHT,'transparent'][i%4],`opacity="${(.5+R()*.5).toFixed(2)}"`); }
      fx+=PN([[0,150],[70,150],[70,156],[0,156]],A,'opacity=".6"')+PN([[20,330],[110,330],[110,336],[20,336]],WHT,'opacity=".5"');
      break;
    case 'OBSIDIAN': fig.hair='#cfd6dc'; fig.streak=INK; fig.streak2=A; fig.eye='#6fd8ff';
      back+=PG(abar(236,480,372,262,15),INK)+PN(abar(300,380,340,316,6),A);
      front+=aarm(294,252,312,340,300,372)+afist(300,372,-.4);
      front+=PG(arect(112,360,136,236,-.12),'#0b1216',4)+PN(arect(112,360,118,218,-.12),'#13222a')+PN(abar(60,300,150,250,10),WHT,'opacity=".55"')+PN(abar(70,340,160,290,4),WHT,'opacity=".4"')+PN(abar(80,470,170,420,6),WHT,'opacity=".3"');
      front+=PN([[84,372],[112,356],[142,368],[112,384]],A,'opacity=".9"')+`<circle cx="112" cy="370" r="7" fill="${INK}"/>`;
      front+=PN(arect(112,360,128,228,-.12),'none',`stroke="${A}" stroke-width="3"`);
      front+=aarm(128,252,96,300,170,310)+afist(172,312,-.2);
      fx+=`<text x="40" y="520" font-family="JetBrains Mono,monospace" font-size="16" font-weight="700" fill="${INK}">ONE-WAY //</text>`;
      break;
    case 'ARGUS': fig.mask='visor'; fig.hair=WHT; fig.streak=A;
      front+=aarm(128,252,106,330,150,370)+aarm(294,252,304,330,258,366);
      front+=PG(arect(204,360,132,86,0),INK)+PG(arect(170,316,40,16,0),'#2c2c2c',2)+`<circle cx="204" cy="364" r="34" fill="#1d1d1d" stroke="${INK}" stroke-width="4"/><circle cx="204" cy="364" r="22" fill="${A}"/><circle cx="204" cy="364" r="10" fill="${INK}"/><circle cx="198" cy="358" r="4" fill="${WHT}"/>`;
      front+=afist(150,372,.1)+afist(258,368,-.1);
      for(const [x,y] of [[60,110],[352,92],[360,430],[46,400]]){ fx+=PG(arect(x,y,40,26,.1),INK,2.4)+`<circle cx="${x+4}" cy="${y}" r="8" fill="${A}" stroke="${INK}" stroke-width="2"/>`; fx+=`<path d="M${x-30} ${y-24} h12 M${x-30} ${y-24} v12 M${x+30} ${y+24} h-12 M${x+30} ${y+24} v-12" stroke="${A}" stroke-width="3"/>`; }
      fx+=`<path d="M170 300 h-24 v24 M238 300 h24 v24 M170 428 h-24 v-24 M238 428 h24 v-24" fill="none" stroke="${WHT}" stroke-width="4"/>`;
      break;
  }
  return {back,front,fx,fig};
}
function opArtSVG(key,crop){
  const ck=key+(crop?':c':''); if(ART_CACHE[ck]) return ART_CACHE[ck];
  const op=[...OPS.atk,...OPS.def].find(o=>o.key===key); const A=op.c; const n=++ART_N;
  const seed=[...key].reduce((s,c)=>s*31+c.charCodeAt(0),7)%100000+11, R=artRng(seed);
  const parts=artParts(key,A,artRng(seed+5));
  let bg=PN(astar(212,262,250,120,9,R()*.6,R),A)+PN(astar(212,262,150,70,8,.3,R),WHT,'opacity=".55"');
  for(let i=0;i<6;i++){ const x=R()*400, y=R()*520, a=-.7+R()*.4, l=80+R()*140; bg+=PN([[x,y],[x+Math.cos(a)*l,y+Math.sin(a)*l],[x+Math.cos(a)*l*.7+14,y+Math.sin(a)*l*.7+18]],i%2?INK:A,'opacity=".85"'); }
  bg+=ashards(artRng(seed+9),14,[A,INK],0,0,400,520,28);
  const vb=crop?'130 48 156 156':'0 0 420 522';
  const svg=`<svg viewBox="${vb}" xmlns="http://www.w3.org/2000/svg" preserveAspectRatio="xMidYMid ${crop?'slice':'meet'}"><defs><filter id="rough${n}" x="-5%" y="-5%" width="110%" height="110%"><feTurbulence type="fractalNoise" baseFrequency="0.045" numOctaves="2" seed="${seed%97}"/><feDisplacementMap in="SourceGraphic" scale="${crop?2.5:4}"/></filter></defs>
    <g filter="url(#rough${n})">${crop?'':bg}${parts.back}${afigure(parts.fig)}${parts.front}${crop?'':parts.fx}</g></svg>`;
  ART_CACHE[ck]=svg; return svg;
}
