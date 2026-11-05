# BLINDSIDE 전용 디스플레이 서체 "BS Tactical" 생성기
# 획 뼈대(격자 0..4 x 0..6) → 두께 입히기(mitre) → 9도 기울임 → TTF
import math
from shapely.geometry import LineString, Polygon, box, MultiPolygon
from shapely.ops import unary_union
from shapely.geometry.polygon import orient
from shapely import affinity
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

T=1.12           # 획 두께(격자 단위)
S=105            # 격자 1단위 = 105 font units  (cap height ≈ 6.0*105 = 630+)
GAP=0.85         # 글자 사이 간격(격자 단위)
SLANT=math.tan(math.radians(9))
OCT=[(1,0),(3,0),(4,1),(4,5),(3,6),(1,6),(0,5),(0,1),(1,0)]
def dot(x,y,s=T): return ('box',(x,y,x+s,y+s))
G={
 'A':[[(0,0),(0,4.4),(1.6,6),(2.4,6),(4,4.4),(4,0)],[(0,2.5),(4,2.5)]],
 'B':[[(0,0),(0,6),(2.9,6),(4,4.9),(4,4.1),(2.9,3.1),(0,3.1)],[(2.9,3.1),(4,2.0),(4,1.1),(2.9,0),(0,0)]],
 'C':[[(4,5),(3,6),(1,6),(0,5),(0,1),(1,0),(3,0),(4,1)]],
 'D':[[(0,0),(0,6),(2.8,6),(4,4.8),(4,1.2),(2.8,0),(0,0)]],
 'E':[[(4,6),(0,6),(0,0),(4,0)],[(0,3),(3,3)]],
 'F':[[(4,6),(0,6),(0,0)],[(0,3),(3,3)]],
 'G':[[(4,5),(3,6),(1,6),(0,5),(0,1),(1,0),(3,0),(4,1),(4,3),(2.2,3)]],
 'H':[[(0,0),(0,6)],[(4,0),(4,6)],[(0,3),(4,3)]],
 'I':[[(0,0),(0,6)]],
 'J':[[(4,6),(4,1),(3,0),(1,0),(0,1)]],
 'K':[[(0,0),(0,6)],[(4,6),(0.2,3.1)],[(1.3,3.9),(4,0)]],
 'L':[[(0,6),(0,0),(4,0)]],
 'M':[[(0,0),(0,6),(2,3.4),(4,6),(4,0)]],
 'N':[[(0,0),(0,6),(4,0),(4,6)]],
 'O':[OCT],
 'P':[[(0,0),(0,6),(2.9,6),(4,4.9),(4,4.1),(2.9,3),(0,3)]],
 'Q':[OCT,[(2.3,1.7),(4.1,-0.3)]],
 'R':[[(0,0),(0,6),(2.9,6),(4,4.9),(4,4.1),(2.9,3),(0,3)],[(2,3),(4,0)]],
 'S':[[(4,5),(3,6),(1,6),(0,5),(0,4),(1,3),(3,3),(4,2),(4,1),(3,0),(1,0),(0,1)]],
 'T':[[(0,6),(4,6)],[(2,6),(2,0)]],
 'U':[[(0,6),(0,1),(1,0),(3,0),(4,1),(4,6)]],
 'V':[[(0,6),(2,0),(4,6)]],
 'W':[[(0,6),(1,0),(2,3.2),(3,0),(4,6)]],
 'X':[[(0,6),(4,0)],[(0,0),(4,6)]],
 'Y':[[(0,6),(2,3)],[(4,6),(2,3)],[(2,3),(2,0)]],
 'Z':[[(0,6),(4,6),(0,0),(4,0)]],
 '0':[OCT,[(1.1,1.3),(2.9,4.7)]],
 '1':[[(0.3,4.6),(2,6),(2,0)],[(0.5,0),(3.5,0)]],
 '2':[[(0,5),(1,6),(3,6),(4,5),(4,4),(0,0),(4,0)]],
 '3':[[(0,5),(1,6),(3,6),(4,5),(4,4),(3,3),(1.4,3)],[(3,3),(4,2),(4,1),(3,0),(1,0),(0,1)]],
 '4':[[(3,0),(3,6)],[(3,6),(0,2.2),(4,2.2)]],
 '5':[[(4,6),(0,6),(0,3.3),(3,3.6),(4,2.6),(4,1),(3,0),(1,0),(0,1)]],
 '6':[[(4,5),(3,6),(1,6),(0,5),(0,1),(1,0),(3,0),(4,1),(4,2),(3,3),(0,3)]],
 '7':[[(0,6),(4,6),(1.4,0)]],
 '8':[[(1,3),(0,4),(0,5),(1,6),(3,6),(4,5),(4,4),(3,3),(1,3)],[(1,3),(0,2),(0,1),(1,0),(3,0),(4,1),(4,2),(3,3),(1,3)]],
 '9':[[(0,1),(1,0),(3,0),(4,1),(4,5),(3,6),(1,6),(0,5),(0,4),(1,3),(4,3)]],
 '.':[dot(0,0)], ',':[[(0.6,1),(0,-1.3)]], ':':[dot(0,0),dot(0,3.6)], ';':[dot(0,3.6),[(0.6,1),(0,-1.3)]],
 '-':[[(0.2,3),(3.6,3)]], '—':[[(0,3),(6.4,3)]], '_':[[(0,-1),(4,-1)]], '=':[[(0,2),(4,2)],[(0,4),(4,4)]],
 '/':[[(0,0),(4,6)]], '\\':[[(0,6),(4,0)]], '|':[[(0,-1),(0,7)]],
 '+':[[(2,1.2),(2,4.8)],[(0.2,3),(3.8,3)]], '×':[[(0.5,1),(3.5,5)],[(0.5,5),(3.5,1)]],
 '%':[[(0,0),(4,6)],dot(0,4.6,1.2),dot(2.8,0,1.2)],
 '(':[[(2,6.6),(0.5,5),(0.5,1),(2,-0.6)]], ')':[[(0,6.6),(1.5,5),(1.5,1),(0,-0.6)]],
 '[':[[(2,6),(0,6),(0,0),(2,0)]], ']':[[(0,6),(2,6),(2,0),(0,0)]],
 '<':[[(3.4,5),(0.4,3),(3.4,1)]], '>':[[(0.4,5),(3.4,3),(0.4,1)]],
 '!':[[(0,6),(0,2.2)],dot(-0.5,0)],
 '?':[[(0,5),(1,6),(3,6),(4,5),(4,4),(2,3),(2,2)],dot(1.5,0)],
 '·':[dot(0,2.5)], '▶':[[(0,0.5),(3.4,3),(0,5.5),(0,0.5)]], '■':[('box',(0,0,3.6,3.6))],
 "'":[[(0.3,6),(0,4.6)]], '#':[[(1,0),(1.6,6)],[(2.6,0),(3.2,6)],[(0,2),(4,2)],[(0,4),(4,4)]],
 '@':[OCT,[(1.2,1.2),(3,1.2),(3,4.6),(1.4,4.6),(1.4,2.6),(3,2.6)]],
}

import math as _m
def _star(n,R,r,cx=2,cy=3,rot=-90):
    pts=[]
    for i in range(n*2):
        rad=R if i%2==0 else r; a=_m.radians(rot+i*180/n); pts.append((round(cx+rad*_m.cos(a),3),round(cy+rad*_m.sin(a),3)))
    return pts
def _hex(R,cx=2,cy=3):
    return [(round(cx+R*_m.cos(_m.radians(60*i)),3),round(cy+R*_m.sin(_m.radians(60*i)),3)) for i in range(6)]
OCT2=[(1,1),(3,1),(4,2),(4,4),(3,5),(1,5),(0,4),(0,2),(1,1)]
G.update({
 '\u2726':[('poly',[(2,0.4),(2.65,2.35),(4.2,3),(2.65,3.65),(2,5.6),(1.35,3.65),(-0.2,3),(1.35,2.35)])],          # ✦ 네 꼭짓점 별
 '\u271A':[('box',(0,2.2,4,3.8)),('box',(1.2,0.4,2.8,5.6))],                                                      # ✚ 굵은 십자
 '\u271B':[[(2,0.5),(2,2.1)],[(2,3.9),(2,5.5)],[(0,3),(1.1,3)],[(2.9,3),(4,3)]],                                  # ✛ 가운데가 빈 십자
 '\u2739':[('poly',_star(12,2.3,1.35))],                                                                          # ✹ 열두 꼭짓점 별
 '\u2B22':[('poly',_hex(2.15))],                                                                                  # ⬢ 육각형
 '\u25A3':[[(0,1),(4,1),(4,5),(0,5),(0,1)],('box',(1.2,2.2,2.8,3.8))],                                            # ▣ 상자 안의 상자
 '\u25CE':[OCT2,('poly',[(1.65,2.1),(2.35,2.1),(2.9,2.65),(2.9,3.35),(2.35,3.9),(1.65,3.9),(1.1,3.35),(1.1,2.65)])], # ◎ 이중 팔각
 '\u2316':[[(1,1.4),(3,1.4),(3.8,2.2),(3.8,3.8),(3,4.6),(1,4.6),(0.2,3.8),(0.2,2.2),(1,1.4)],[(2,0.2),(2,1.6)],[(2,4.4),(2,5.8)],[(0,3),(1.4,3)],[(2.6,3),(4,3)]], # ⌖ 조준선
 '\u279C':[[(0,3),(2.6,3)],('poly',[(2,1.3),(4.1,3),(2,4.7)])],                                                    # ➜ 화살표
 '\u2190':[[(0.6,3),(4,3)],[(2,1.4),(0.4,3),(2,4.6)]],                                                            # ←
 '\u2192':[[(0,3),(3.4,3)],[(2,1.4),(3.6,3),(2,4.6)]],                                                            # →
 '\u2191':[[(2,0.5),(2,5.4)],[(0.4,3.6),(2,5.4),(3.6,3.6)]],                                                      # ↑
 '\u2193':[[(2,0.6),(2,5.5)],[(0.4,2.4),(2,0.6),(3.6,2.4)]],                                                      # ↓
 '\u2013':[[(0.2,3),(3.6,3)]], '\u2026':[('box',(0,0,1,1)),('box',(1.7,0,2.7,1)),('box',(3.4,0,4.4,1))],          # – …
})

def geom(strokes):
    parts=[]
    for s in strokes:
        if isinstance(s,tuple) and s[0]=='box': parts.append(box(*s[1]))
        elif isinstance(s,tuple) and s[0]=='poly': parts.append(Polygon(s[1]).buffer(0))
        elif len(s)>=2: parts.append(LineString(s).buffer(T/2,cap_style='flat',join_style='mitre',mitre_limit=2.1))
    return unary_union(parts)
CLIP=False
def to_glyph(g):
    """shapely → TTGlyph, 기울임 적용, 폰트 단위로 변환. (glyph, xmin, xmax) 반환"""
    if g.is_empty: return TTGlyphPen(None).glyph(),0,0
    g=affinity.translate(g,0,T/2)                       # 획 아랫면이 y=0
    if CLIP: g=g.intersection(box(-9,0,20,6+T))       # 영문·숫자는 기준선/윗선으로 잘라 삐침 제거
    g=affinity.skew(g,xs=0,ys=0)                        # noop(안전)
    g=affinity.affine_transform(g,[1,SLANT,0,1,0,0])    # x += y*tan(9°)
    g=affinity.scale(g,S,S,origin=(0,0))
    polys=list(g.geoms) if isinstance(g,MultiPolygon) else [g]
    pen=TTGlyphPen(None)
    for p in polys:
        p=orient(p,sign=-1.0)                            # 바깥=시계, 안쪽=반시계
        for ring in [p.exterior]+list(p.interiors):
            pts=[(round(x),round(y)) for x,y in ring.coords[:-1]]
            pen.moveTo(pts[0])
            for q in pts[1:]: pen.lineTo(q)
            pen.closePath()
    minx,_,maxx,_=g.bounds
    return pen.glyph(),round(minx),round(maxx)
order=['.notdef','space']; glyphs={}; metrics={}
# .notdef: 네모 테두리
nd=geom([[(0,0),(0,6),(4,6),(4,0),(0,0)]]); gl,x0,x1=to_glyph(nd); glyphs['.notdef']=gl; metrics['.notdef']=(x1+round(GAP*S),x0)
glyphs['space']=TTGlyphPen(None).glyph(); metrics['space']=(int(3.0*S),0)
cmap={32:'space'}
def name(ch):
    if ord(ch)>127 and ch not in '×—·▶■': return 'uni%04X'%ord(ch)
    return name0(ch)
def name0(ch): return {'.':'period',',':'comma',':':'colon',';':'semicolon','-':'hyphen','—':'emdash','_':'underscore','=':'equal','/':'slash','\\':'backslash','|':'bar','+':'plus','×':'multiply','%':'percent','(':'parenleft',')':'parenright','[':'bracketleft',']':'bracketright','<':'less','>':'greater','!':'exclam','?':'question','·':'periodcentered','▶':'uni25B6','■':'uni25A0',"'":'quotesingle','#':'numbersign','@':'at'}.get(ch,ch)
for ch,st in G.items():
    n=name(ch); CLIP=ch.isalnum(); gl,x0,x1=to_glyph(geom(st)); glyphs[n]=gl; order.append(n)
    metrics[n]=(x1+round(GAP*S)+(x0 if x0<0 else 0),min(x0,0)) if False else (max(x1,0)+round(GAP*S),x0)
    cmap[ord(ch)]=n
    if ch.isalpha(): cmap[ord(ch.lower())]=n          # 소문자도 같은 대문자 글리프
fb=FontBuilder(1000,isTTF=True)
fb.setupGlyphOrder(['.notdef','space']+[n for n in order if n not in('.notdef','space')])
fb.setupCharacterMap(cmap)
fb.setupGlyf(glyphs)
fb.setupHorizontalMetrics({k:metrics[k] for k in fb.font.getGlyphOrder()})
fb.setupHorizontalHeader(ascent=820,descent=-220)
fb.setupNameTable({'familyName':'BS Tactical','styleName':'Bold'})
fb.setupOS2(sTypoAscender=820,sTypoDescender=-220,usWinAscent=900,usWinDescent=300,sCapHeight=700,sxHeight=700,fsType=0,achVendID='BSLD')
fb.setupPost(); fb.save('BSTactical-Bold.ttf'); print('saved',len(cmap),'chars')
