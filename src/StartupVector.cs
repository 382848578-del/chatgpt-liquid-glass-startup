using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

public sealed class StartupVector : FrameworkElement {
    public double Time { get; set; }
    public const double Duration = 7.8;
    public static double AnimationTime(double elapsed) { return elapsed<.35?elapsed:(elapsed<4.35?.35+(elapsed-.35)*1.15:elapsed+.6); }
    public static string[] Paths;
    readonly List<Point[][]> words = new List<Point[][]>();
    Point[][] extension, loop;
    double word0Length,word1Length,extensionLength,loopLength;
    readonly Color[] colors = { C("#90bdff"),C("#d2eaff"),C("#f2f7ff"),C("#a8d5f5"),C("#b9bcf9"),C("#ebd6ef"),C("#f6d7ae"),C("#d6ebff"),C("#96c4ff") };
    public ImageSource Backdrop {get;set;}
    readonly Brush background = new SolidColorBrush(C("#08070e"));
    FormattedText skipHint;
    FormattedText escapeKey;
    double skipHintDpi;
    void DrawSkipHint(DrawingContext dc) {
        double opacity=Ease((Time-.35)/.18)*(1-Ease((Time-7.25)/.22));
        if(opacity<=0)return;
        double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if(skipHint==null||skipHintDpi!=dpi) {
            skipHintDpi=dpi;
            skipHint=new FormattedText("\u8df3\u8fc7",System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),12,new SolidColorBrush(C("#aeb8c8")),dpi);
            escapeKey=new FormattedText("esc",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI Variable Text"),11.5,new SolidColorBrush(C("#c9d1de")),dpi);
        }
        dc.PushOpacity(opacity);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(14,225,236,255)),new Pen(new SolidColorBrush(Color.FromArgb(36,214,230,255)),.75),new Rect(22,20,33,23),6,6);
        dc.DrawText(escapeKey,new Point(38.5-escapeKey.Width/2,31.5-escapeKey.Height/2));
        dc.DrawText(skipHint,new Point(64,31.5-skipHint.Height/2));dc.Pop();
    }
    static Color C(string s) { return (Color)ColorConverter.ConvertFromString(s); }
    public StartupVector() {
        foreach(string data in Paths) {
            PathGeometry flat = Geometry.Parse(data).GetFlattenedPathGeometry(.012, ToleranceType.Absolute);
            var figures = new List<Point[]>();
            foreach(PathFigure f in flat.Figures) {
                var pts=new List<Point>();pts.Add(f.StartPoint);
                foreach(PathSegment seg in f.Segments) {
                    PolyLineSegment poly=seg as PolyLineSegment;
                    if(poly!=null) foreach(Point p in poly.Points) pts.Add(p);
                    LineSegment line=seg as LineSegment;if(line!=null)pts.Add(line.Point);
                }
                figures.Add(pts.ToArray());
            }
            words.Add(figures.ToArray());
        }
        word0Length=Length(words[0]);word1Length=Length(words[1]);
        Point first=LoopPoint(0);
        extension=Flatten(String.Format(System.Globalization.CultureInfo.InvariantCulture,"M 458 98 C 480 91 505 114 510 94 C 514 79 {0} 83 {0} {1}",first.X,first.Y));
        var lp=new List<Point>();for(int i=0;i<=960;i++)lp.Add(LoopPoint((double)i/960));loop=new Point[][]{lp.ToArray()};
        extensionLength=Length(extension);loopLength=Length(loop);
    }
    static double Length(Point[][] a) { double n=0;foreach(var f in a)for(int i=1;i<f.Length;i++)n+=(f[i]-f[i-1]).Length;return n; }
    static Point[][] Flatten(string data) { var g=Geometry.Parse(data).GetFlattenedPathGeometry(.012,ToleranceType.Absolute);var fs=new List<Point[]>();foreach(var f in g.Figures){var ps=new List<Point>();ps.Add(f.StartPoint);foreach(var s in f.Segments){var p=s as PolyLineSegment;if(p!=null)foreach(var q in p.Points)ps.Add(q);var l=s as LineSegment;if(l!=null)ps.Add(l.Point);}fs.Add(ps.ToArray());}return fs.ToArray(); }
    Point LoopPoint(double u) { double a=Math.PI+u*2*Math.PI,r=43*(1+.065*Math.Cos(a*6+.5));return new Point(548+r*Math.Cos(a),65+r*Math.Sin(a)); }
    static double Clamp(double v) { return Math.Max(0,Math.Min(1,v)); }
    static double Ease(double v) { return 1-Math.Pow(1-Clamp(v),3); }
    static double Smooth(double v) { v=Clamp(v);return v*v*(3-2*v); }
    public static double RevealProgress(double t) { return Ease((t-7.25)/1.15); }
    public static double GlassWidth(double t) { return (3.3+7.2*Ease((t-6.3)/.65))*(1+RevealProgress(t)*2); }
    public static double RevealRadius(double t,double w,double h) { double s=Math.Min(w/720,h/400)*1.16;double max=Math.Sqrt(w*w+h*h)/2/s/.92+30;return 43+(max-43)*RevealProgress(t); }
    Brush Rainbow(double x1,double x2) {
        var brush=new LinearGradientBrush();brush.MappingMode=BrushMappingMode.Absolute;
        brush.StartPoint=new Point(x1,0);brush.EndPoint=new Point(x2,0);
        for(int i=0;i<colors.Length;i++)brush.GradientStops.Add(new GradientStop(colors[i],(double)i/(colors.Length-1)));
        brush.Freeze();return brush;
    }
    Pen MakePen(Brush b,double w) { var p=new Pen(b,w);p.StartLineCap=PenLineCap.Round;p.EndLineCap=PenLineCap.Round;p.LineJoin=PenLineJoin.Round;p.Freeze();return p; }
    void Partial(DrawingContext dc,Point[][] paths,double begin,double end,Brush brush,double width,double opacity) {
        if(end<=begin||opacity<=0)return;
        double total=0;foreach(Point[] a in paths)for(int i=1;i<a.Length;i++)total+=(a[i]-a[i-1]).Length;
        double at=0,lo=total*Clamp(begin),hi=total*Clamp(end);
        var geom=new StreamGeometry();
        using(var g=geom.Open())foreach(Point[] a in paths) {
            bool started=false;
            for(int i=1;i<a.Length;i++) {
                double len=(a[i]-a[i-1]).Length;if(len<.000001)continue;
                double next=at+len;
                if(next>lo&&at<hi) {
                    Point p=a[i-1]+(a[i]-a[i-1])*Clamp((lo-at)/len);
                    Point q=a[i-1]+(a[i]-a[i-1])*Clamp((hi-at)/len);
                    if(!started){g.BeginFigure(p,false,false);started=true;}
                    g.LineTo(q,true,false);
                }
                at=next;
            }
        }
        geom.Freeze();dc.PushOpacity(opacity);
        dc.PushTransform(new TranslateTransform(.4,.9));dc.DrawGeometry(null,MakePen(new SolidColorBrush(Color.FromArgb(90,0,0,0)),width+1),geom);dc.Pop();
        dc.PushOpacity(.88);dc.DrawGeometry(null,MakePen(brush,width),geom);dc.Pop();
        if(Backdrop!=null&&width>=5) {
            double sc=Math.Min(ActualWidth/720,ActualHeight/400)*1.16;
            var viewport=new Rect(229-ActualWidth/sc/2-3,77-ActualHeight/sc/2-2,ActualWidth/sc*1.018,ActualHeight/sc*1.018);
            var image=new ImageBrush(Backdrop);image.Stretch=Stretch.Fill;image.ViewportUnits=BrushMappingMode.Absolute;image.Viewport=viewport;
            dc.PushClip(geom.GetWidenedPathGeometry(MakePen(Brushes.White,width*.76)));
            dc.PushOpacity(.38);dc.DrawRectangle(image,null,viewport);dc.Pop();dc.Pop();
        }
        dc.PushTransform(new TranslateTransform(-width*.17,-width*.20));
        dc.DrawGeometry(null,MakePen(new SolidColorBrush(Color.FromArgb(83,244,250,255)),Math.Max(.35,width*.16)),geom);
        var shine=new LinearGradientBrush();shine.MappingMode=BrushMappingMode.Absolute;
        double sx=229+170*Math.Sin(Time*1.7);shine.StartPoint=new Point(sx-85,0);shine.EndPoint=new Point(sx+85,40);
        shine.GradientStops.Add(new GradientStop(Color.FromArgb(0,255,255,255),0));shine.GradientStops.Add(new GradientStop(Color.FromArgb(0,255,255,255),.33));shine.GradientStops.Add(new GradientStop(Color.FromArgb(235,255,255,255),.51));shine.GradientStops.Add(new GradientStop(Color.FromArgb(0,255,255,255),.72));shine.GradientStops.Add(new GradientStop(Color.FromArgb(0,255,255,255),1));
        dc.DrawGeometry(null,MakePen(shine,Math.Max(.5,width*.23)),geom);dc.Pop();
        dc.Pop();
    }
    Point Flower(double u,double r) {
        double a=u*2*Math.PI-Math.PI/2,rad=r*(1+.065*Math.Cos(a*6+.5));
        return new Point(229+Math.Cos(a)*rad,77+Math.Sin(a)*rad);
    }
    StreamGeometry FlowerGeometry(double radius) {
        var g=new StreamGeometry();using(var c=g.Open()) {
            c.BeginFigure(Flower(0,radius),true,true);
            for(int i=1;i<=480;i++)c.LineTo(Flower((double)i/480,radius),true,false);
        }g.Freeze();return g;
    }
    LinearGradientBrush GlassGradient(Point from,Point to,params GradientStop[] stops) {
        var b=new LinearGradientBrush();b.MappingMode=BrushMappingMode.Absolute;b.StartPoint=from;b.EndPoint=to;
        foreach(var s in stops)b.GradientStops.Add(s);b.Freeze();return b;
    }
    GradientStop Stop(string hex,byte alpha,double at) {var c=C(hex);c.A=alpha;return new GradientStop(c,at);}
    void GlassContour(DrawingContext dc,double radius,double t) {
        double width=GlassWidth(t),amount=Ease((t-6.3)/.65);
        var outer=FlowerGeometry(radius+width/2);
        var inner=FlowerGeometry(Math.Max(1,radius-width/2));
        var body=new CombinedGeometry(GeometryCombineMode.Exclude,outer,inner);body.Freeze();
        // A transmissive interior and a raised, curved rim give the material physical thickness.
        var lens=GlassGradient(new Point(229-radius,77-radius),new Point(229+radius,77+radius),
            Stop("#ecf7ff",(byte)(18*amount),0),Stop("#9dc8f4",(byte)(3*amount),.46),Stop("#c2b6ef",(byte)(9*amount),1));
        dc.DrawGeometry(lens,null,inner);
        dc.PushTransform(new TranslateTransform(width*.12,width*.25));
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(130,0,0,0)),null,body);dc.Pop();
        var material=GlassGradient(new Point(229-radius,77-radius),new Point(229+radius,77+radius),
            Stop("#a6d2ff",205,0),Stop("#f4faff",240,.20),Stop("#537aa4",130,.39),Stop("#1c304a",100,.51),Stop("#d6c5ee",180,.68),Stop("#f5d1ab",225,.83),Stop("#b4dbff",220,1));
        dc.DrawGeometry(material,null,body);
        if(Backdrop!=null) {
            double sc=Math.Min(ActualWidth/720,ActualHeight/400)*1.16;
            var area=new Rect(229-ActualWidth/sc/2-width*.55,77-ActualHeight/sc/2-width*.3,ActualWidth/sc*1.09,ActualHeight/sc*1.09);
            var b=new ImageBrush(Backdrop);b.Stretch=Stretch.Fill;b.ViewportUnits=BrushMappingMode.Absolute;b.Viewport=area;
            // Two nested magnifications bend the content more strongly at the edge than at the core.
            dc.PushClip(body);dc.PushOpacity(.64);dc.DrawRectangle(b,null,area);dc.Pop();
            dc.PushOpacity(.16);dc.PushTransform(new TranslateTransform(width*.28,-width*.18));dc.DrawRectangle(b,null,area);dc.Pop();dc.Pop();dc.Pop();
        }
        var rim=GlassGradient(new Point(229-radius,77-radius),new Point(229+radius,77+radius),
            Stop("#ffffff",255,0),Stop("#f1faff",240,.23),Stop("#8ebce9",30,.45),Stop("#f3ddff",175,.70),Stop("#ffffff",248,1));
        dc.DrawGeometry(null,MakePen(rim,Math.Max(.6,width*.105)),outer);
        // The dark inner meniscus separates the transparent body from the mirror highlight.
        dc.DrawGeometry(null,MakePen(new SolidColorBrush(Color.FromArgb(145,7,18,35)),Math.Max(.5,width*.12)),inner);
        dc.PushTransform(new TranslateTransform(-width*.08,-width*.10));
        dc.DrawGeometry(null,MakePen(new SolidColorBrush(Color.FromArgb(146,224,244,255)),Math.Max(.5,width*.065)),inner);dc.Pop();
        // Moving, tapered reflection arcs, rather than a uniform glowing outline.
        for(int band=0;band<2;band++) {
            double start=(.12+t*.055+band*.47)%1;
            for(int j=0;j<18;j++) {
                double u=start+j*.006,shine=Math.Sin((j+.5)/18*Math.PI);
                var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(Flower(u,radius+width*.29),false,false);for(int k=1;k<=4;k++)c.LineTo(Flower(u+k*.0015,radius+width*.29),true,false);}g.Freeze();
                dc.DrawGeometry(null,MakePen(new SolidColorBrush(Color.FromArgb((byte)(235*shine),250,254,255)),Math.Max(.65,width*.16*shine)),g);
            }
        }
    }
    void Span(DrawingContext dc,Point[][] section,double offset,double lo,double hi,Brush brush) { double length=Length(section);if(hi<=offset||lo>=offset+length)return;Partial(dc,section,(lo-offset)/length,(hi-offset)/length,brush,3.3,1); }
    protected override void OnRender(DrawingContext dc) {
        base.OnRender(dc);double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;
        dc.DrawRectangle(background,null,new Rect(0,0,w,h));
        DrawSkipHint(dc);
        double scale=Math.Min(w/720,h/400)*1.16;
        dc.PushTransform(new TranslateTransform(w/2-229*scale,h/2-77*scale));dc.PushTransform(new ScaleTransform(scale,scale));
        double t=Time;
        if(t<4.95) {
            Partial(dc,words[0],0,Clamp((t-.35)/3.25),Rainbow(0,458),3.3,1);
            Partial(dc,words[1],0,Clamp((t-3.65)/1.30),Rainbow(0,458),3.3,1);
        } else if(t<6.3) {
            double p=Smooth((t-4.95)/1.35),wl=word0Length+word1Length;
            double head=wl+(extensionLength+loopLength)*p,tail=(wl+extensionLength)*p;
            dc.PushTransform(new TranslateTransform(-319*p,12*p));
            Span(dc,words[0],0,tail,head,Rainbow(0,458));
            Span(dc,words[1],word0Length,tail,head,Rainbow(0,458));
            Span(dc,extension,wl,tail,head,Rainbow(458,594));
            Span(dc,loop,wl+extensionLength,tail,head,Rainbow(503,593));
            dc.Pop();
        }
        if(t>=6.3) {
            double reveal=RevealProgress(t),r=RevealRadius(t,w,h);
            GlassContour(dc,r,t);
            if(t>7.25)for(int ring=1;ring<=3;ring++) {
                var pp=new List<Point>();for(int i=0;i<=480;i++)pp.Add(Flower((double)i/480,r+ring*(9+reveal*32)));
                Partial(dc,new Point[][]{pp.ToArray()},0,1,new SolidColorBrush(C("#c5abe5")),.7,.16/ring*(1-reveal));
            }
        }
        if(t>=6.3 && t<7.6) {
            double a1=Ease((t-6.3)/.4)*(1-Ease((t-7.25)/.25));dc.PushOpacity(a1);
            var pen=MakePen(new SolidColorBrush(C("#f4effa")),3.6);
            dc.DrawLine(pen,new Point(214,65),new Point(222,77));dc.DrawLine(pen,new Point(222,77),new Point(214,89));
            dc.DrawLine(pen,new Point(232,89),new Point(247,89));dc.DrawEllipse(pen.Brush,null,new Point(234,73),1.7,1.7);dc.Pop();
        }
        dc.Pop();dc.Pop();
    }
}

public static class StartupWindows {
    public static string[] ProcessNames = new string[]{"ChatGPT"};
    public delegate bool EnumProc(IntPtr h,IntPtr p);
    public delegate void WinEventProc(IntPtr hook,uint evt,IntPtr h,int obj,int child,uint thread,uint time);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEventProc callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
    public static bool IsAppWindow(IntPtr h) {uint id;GetWindowThreadProcessId(h,out id);if(id==0)return false;try{using(var p=Process.GetProcessById((int)id)){foreach(string name in ProcessNames)if(String.Equals(p.ProcessName,name,StringComparison.OrdinalIgnoreCase))return true;return false;}}catch{return false;}}
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p,IntPtr arg);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out RECT r);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,int attr,out RECT r,int size);
    [DllImport("dwmapi.dll")] public static extern int DwmFlush();
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h,ref POINT p);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h,uint command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    public static bool IsAbove(IntPtr a,IntPtr b) {IntPtr h=GetWindow(b,3);for(int i=0;i<1000&&h!=IntPtr.Zero;i++,h=GetWindow(h,3))if(h==a)return true;return false;}
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int cmd);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] public static extern IntPtr SetLong(IntPtr h,int n,IntPtr value);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetLong(IntPtr h,int n);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
    [DllImport("gdi32.dll")] public static extern IntPtr CreatePolygonRgn(POINT[] p,int n,int mode);
    [DllImport("gdi32.dll")] public static extern int CombineRgn(IntPtr dest,IntPtr a,IntPtr b,int mode);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);
    [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr h,IntPtr region,bool redraw);
    [DllImport("user32.dll")] public static extern int GetWindowRgn(IntPtr h,IntPtr region);
    [DllImport("gdi32.dll")] public static extern bool PtInRegion(IntPtr region,int x,int y);
    public static void Reveal(IntPtr window,double time) {
        if(time<7.25)return;
        RECT bounds;GetClientRect(window,out bounds);double w=bounds.Right,h=bounds.Bottom;
        double s=Math.Min(w/720,h/400)*1.16;
        double radius=Math.Max(0,StartupVector.RevealRadius(time,w,h)-StartupVector.GlassWidth(time)/2-2)*Math.Max(0,Math.Min(1,(time-7.25)/.13));
        var ps=new POINT[960];for(int i=0;i<ps.Length;i++){double a=(double)i/ps.Length*2*Math.PI-Math.PI/2;double r=radius*(1+.065*Math.Cos(a*6+.5))*s;ps[i]=new POINT{X=(int)Math.Round(w/2+Math.Cos(a)*r),Y=(int)Math.Round(h/2+Math.Sin(a)*r)};}
        IntPtr outer=CreateRectRgn(0,0,(int)w,(int)h),hole=CreatePolygonRgn(ps,ps.Length,1);
        CombineRgn(outer,outer,hole,4);DeleteObject(hole);
        if(SetWindowRgn(window,outer,true)==0)DeleteObject(outer);
    }
    public static IntPtr FindApp() {
        var ids=new HashSet<uint>();foreach(string name in ProcessNames)foreach(var p in Process.GetProcessesByName(name))using(p)ids.Add((uint)p.Id);
        IntPtr best=IntPtr.Zero,front=GetForegroundWindow();long area=0;
        EnumWindows(delegate(IntPtr h,IntPtr unused) {
            uint id;GetWindowThreadProcessId(h,out id);if(!ids.Contains(id)||!IsWindowVisible(h))return true;
            RECT r;GetClientRect(h,out r);long a=(long)(r.Right-r.Left)*(r.Bottom-r.Top);
            if(a>20000&&(h==front||a>area)){best=h;area=h==front?long.MaxValue:a;}return true;
        },IntPtr.Zero);return best;
    }
    public static RECT ContentBounds(IntPtr h) {
        // DWM bounds include the title/menu bar, excluding invisible resize borders.
        RECT r;
        if(DwmGetWindowAttribute(h,9,out r,Marshal.SizeOf(typeof(RECT)))==0&&r.Right>r.Left&&r.Bottom>r.Top)return r;
        GetWindowRect(h,out r);return r;
    }
    public static void DragApp(IntPtr h) { ReleaseCapture();SendMessage(h,0x00A1,new IntPtr(2),IntPtr.Zero); }
    public static ImageSource CaptureClient(IntPtr h) {
        RECT r;GetWindowRect(h,out r);int w=r.Right-r.Left,ht=r.Bottom-r.Top;
        if(w<1||ht<1||w>8192||ht>8192)return null;
        using(var bitmap=new System.Drawing.Bitmap(w,ht)) {
            using(var g=System.Drawing.Graphics.FromImage(bitmap)){IntPtr dc=g.GetHdc();try{if(!PrintWindow(h,dc,2))return null;}finally{g.ReleaseHdc(dc);}}
            IntPtr native=bitmap.GetHbitmap();try {
                var src=System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(native,IntPtr.Zero,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                RECT c=ContentBounds(h);int x=Math.Max(0,c.Left-r.Left),y=Math.Max(0,c.Top-r.Top);
                var crop=new System.Windows.Media.Imaging.CroppedBitmap(src,new Int32Rect(x,y,Math.Min(w-x,c.Right-c.Left),Math.Min(ht-y,c.Bottom-c.Top)));crop.Freeze();return crop;
            }finally{DeleteObject(native);}
        }
    }
    public static bool Follow(IntPtr overlay,IntPtr target) {
        if(!IsWindow(target))return false;
        if(IsIconic(target)||!IsWindowVisible(target)){ShowWindow(overlay,0);return true;}
        RECT r=ContentBounds(target);if(r.Right<=r.Left||r.Bottom<=r.Top)return true;
        SetWindowPos(overlay,IntPtr.Zero,r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top,0x0010|0x0004|0x0040);
        return true;
    }
}
