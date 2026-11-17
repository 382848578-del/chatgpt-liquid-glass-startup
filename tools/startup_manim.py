"""Manim CE project: continuous pen trail, refractive rim, six-lobe aperture.
Render: python -m manim -r 3840,2160 --fps 60 startup_manim.py Startup
The neutral preview backdrop represents the live app revealed by the Windows launcher.
"""
from pathlib import Path
import json
import numpy as np
from manim import *
from manim.utils.bezier import partial_bezier_points
from svgelements import Path as SVGPath, CubicBezier, Line as SVGLine, Move

PALETTE=['#90bdff','#d2eaff','#f2f7ff','#a8d5f5','#b9bcf9','#ebd6ef','#f6d7ae','#d6ebff','#96c4ff']
SCALE=0.0229167
START_TIME=0
END_TIME=7.8
def clamp(v):return np.clip(v,0,1)
def ease(v):return 1-(1-clamp(v))**3
def smooth(v):v=clamp(v);return v*v*(3-2*v)
def xy(p):return np.array([(float(p.x)-229)*SCALE,(77-float(p.y))*SCALE,0])
def rainbow(u):
    p=clamp(u)*(len(PALETTE)-1);i=min(int(p),len(PALETTE)-2)
    return interpolate_color(ManimColor(PALETTE[i]),ManimColor(PALETTE[i+1]),p-i)
def loop(u,r=43,center=(548,65),start=PI):
    a=start+u*TAU;rad=r*(1+.065*np.cos(a*6+.5))
    return np.array([center[0]+np.cos(a)*rad,center[1]+np.sin(a)*rad])
def screenpoint(p):return np.array([(p[0]-229)*SCALE,(77-p[1])*SCALE,0])

class Startup(Scene):
    def construct(self):
        self.camera.background_color='#17181c'
        # Only a neutral preview behind the aperture. The installed launcher reveals the real window.
        backdrop=VGroup(RoundedRectangle(width=8,height=1.05,corner_radius=.22,fill_color='#25272e',fill_opacity=1,stroke_width=0).shift(DOWN*1.8))
        self.add(backdrop)
        mask=Rectangle(width=config.frame_width+.1,height=config.frame_height+.1,stroke_width=0,fill_color='#08070e',fill_opacity=1)
        self.add(mask)
        mask.add_updater(lambda m: None)
        paths=json.loads((Path(__file__).resolve().parents[1]/'startup-paths.json').read_text(encoding='utf-8'))
        first=loop(0)
        ext=f'M 458 98 C 480 91 505 114 510 94 C 514 79 {first[0]} 83 {first[0]} {first[1]}'
        specs=[]
        sections=[]
        at=0
        def addpath(data,xlo,xhi):
            nonlocal at
            start=at
            for seg in SVGPath(data):
                if isinstance(seg,Move):continue
                if isinstance(seg,CubicBezier):cs=np.array([xy(p) for p in [seg.start,seg.control1,seg.control2,seg.end]])
                elif isinstance(seg,SVGLine):
                    a,b=xy(seg.start),xy(seg.end);cs=np.array([a,a+(b-a)/3,a+2*(b-a)/3,b])
                else:continue
                for j in range(5):
                    c=partial_bezier_points(cs,j/5,(j+1)/5)
                    length=sum(np.linalg.norm(bezier(c)(k/12)-bezier(c)((k-1)/12)) for k in range(1,13))/SCALE
                    m=VMobject(stroke_width=7.2,cap_style=CapStyleType.ROUND).set_points(c)
                    m.set_stroke(rainbow((c[0][0]/SCALE+229-xlo)/(xhi-xlo)))
                    specs.append((m,m.copy(),at,length,len(sections)))
                    at+=length
            sections.append((start,at-start))
        addpath(paths[0],0,458);addpath(paths[1],0,458);addpath(ext,458,594)
        lp=[loop(i/160) for i in range(161)]
        start=at
        for i in range(160):
            pts=np.array([screenpoint(loop((i+j/8)/160)) for j in range(9)])
            m=VMobject(stroke_width=7.2,cap_style=CapStyleType.ROUND).set_points_smoothly(pts)
            length=np.linalg.norm(lp[i+1]-lp[i]);m.set_stroke(rainbow((lp[i][0]-503)/90))
            specs.append((m,m.copy(),at,length,3));at+=length
        sections.append((start,at-start))
        master=VGroup(*[s[0] for s in specs]);self.add(master)
        terminal=VGroup(VMobject(cap_style=CapStyleType.ROUND).set_points_as_corners([np.array([-15,12,0])*SCALE,np.array([-7,0,0])*SCALE,np.array([-15,-12,0])*SCALE]),Line(np.array([3,-12,0])*SCALE,np.array([18,-12,0])*SCALE,cap_style=CapStyleType.ROUND),Dot(np.array([5,4,0])*SCALE,radius=1.7*SCALE)).set_color('#f4effa')
        terminal[0].set_stroke(width=7.2);terminal[1].set_stroke(width=7.2);self.add(terminal)
        highlights=VGroup();self.add(highlights)
        base_rect=mask.copy()
        def update(_m,alpha):
            elapsed=START_TIME+alpha*(END_TIME-START_TIME)
            t=elapsed if elapsed<.35 else (.35+(elapsed-.35)*1.15 if elapsed<4.35 else elapsed+.6)
            w0=sections[0][1];wl=w0+sections[1][1];el=sections[2][1];ll=sections[3][1]
            p=smooth((t-4.95)/1.35)
            lo=(wl+el)*p if t>=4.95 else 0
            if t<3.65:hi=w0*clamp((t-.35)/3.25)
            elif t<4.95:hi=w0+sections[1][1]*clamp((t-3.65)/1.3)
            else:hi=wl+(el+ll)*p
            rev=ease((t-7.25)/1.15)
            radius=43+(415-43)*rev
            offset=np.array([-319,-12,0])*SCALE*p
            for m,full,start,length,section in specs:
                a=clamp((lo-start)/length);b=clamp((hi-start)/length)
                if b<=a or (t>=6.3 and section!=3):m.set_stroke(opacity=0);continue
                m.pointwise_become_partial(full,a,b);m.shift(offset);m.set_stroke(opacity=.92,width=7.2)
                if t>=6.3:
                    m.scale(radius/43,about_point=ORIGIN)
                    m.set_stroke(width=(7.2+5*ease((t-6.3)/.6))*(1+rev*3))
            terminal.set_opacity(ease((t-6.3)/.4)*(1-ease((t-7.25)/.25)))
            # A moving, cool-white specular rim catches the surface of the glass.
            highlights.submobjects=[]
            if t>=6.3:
                for k in range(10):
                    u=(.12+t*.09+k*.002)%1
                    ps=[screenpoint(loop(u+j*.001,radius,center=(229,77),start=-PI/2)) for j in range(5)]
                    shine=VMobject(stroke_color='#f5fbff',stroke_width=2.5*(1+rev*3),stroke_opacity=.45,cap_style=CapStyleType.ROUND).set_points_smoothly(ps)
                    highlights.add(shine)
            if t<7.25:mask.become(base_rect)
            else:
                hr=max(0,radius-10)*clamp((t-7.25)/.13)
                hole=VMobject(fill_opacity=1,stroke_width=0).set_points_smoothly([screenpoint(loop(i/160,hr,(229,77),-PI/2)) for i in range(161)])
                mask.become(Difference(base_rect,hole,fill_color='#08070e',fill_opacity=1,stroke_width=0))
        self.play(UpdateFromAlphaFunc(master,update,rate_func=linear),run_time=END_TIME-START_TIME)
