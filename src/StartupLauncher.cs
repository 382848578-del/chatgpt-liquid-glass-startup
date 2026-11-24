using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// Native GUI entry point: the first painted frame precedes application activation.
public sealed class StartupAppConfig { public string[] ProcessNames {get;set;} }
public sealed class StartupLauncher {
    readonly string root=AppDomain.CurrentDomain.BaseDirectory;
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly JavaScriptSerializer json=new JavaScriptSerializer();
    Window window,stub,covering;
    StartupVector vector;
    IntPtr overlay,target;
    DispatcherTimer scan,animation;
    StartupWindows.WinEventProc windowEvents;
    IntPtr eventHook;
    bool started,closed,test,replaced,resized,captureStarted,blackFirstFrame,testedCover;
    double firstFrame=-1,launchTime=-1,targetSince,lastFrameTime=-1,revealStart=-1;
    int frames,rebinds,boundsMismatches,layeringFailures,coveringSamples;
    string reason="unknown";
    StartupWindows.RECT lastBounds;
    static StartupWindows.RECT Rect(int x,int y,int w,int h) {return new StartupWindows.RECT{Left=x,Top=y,Right=x+w,Bottom=y+h};}

    [STAThread] public static int Main(string[] args) {
        bool own;using(var mutex=new Mutex(true,"Local\\ChatGPTStartupAnimation_x20",out own)) {
            if(!own)return 0;
            try {return new StartupLauncher().Run(args);}
            catch(Exception ex) {
                string root=AppDomain.CurrentDomain.BaseDirectory;
                try{File.AppendAllText(Path.Combine(root,"launcher-error.log"),DateTime.Now.ToString("o")+" "+ex+Environment.NewLine);}catch{}
                if(Array.IndexOf(args,"--self-test")<0)try{Process.Start(new ProcessStartInfo(Path.Combine(root,"ChatGPT-original.lnk")){UseShellExecute=true});}catch{}
                return 1;
            } finally {mutex.ReleaseMutex();}
        }
    }
    int Run(string[] args) {
        test=Array.IndexOf(args,"--self-test")>=0;
        StartupWindows.SetThreadDpiAwarenessContext(new IntPtr(-4));
        StartupVector.Paths=json.Deserialize<string[]>(File.ReadAllText(Path.Combine(root,"startup-paths.json")));
        string config=Path.Combine(root,"app-config.json");
        if(File.Exists(config))try{var c=json.Deserialize<StartupAppConfig>(File.ReadAllText(config));if(c.ProcessNames!=null&&c.ProcessNames.Length>0)StartupWindows.ProcessNames=c.ProcessNames;}catch{}
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        vector=new StartupVector{Time=0};
        // The provisional position uses the existing/last window, never the entire screen.
        target=test?IntPtr.Zero:StartupWindows.FindApp();
        if(target!=IntPtr.Zero)lastBounds=StartupWindows.ContentBounds(target);
        else {
            try{lastBounds=json.Deserialize<StartupWindows.RECT>(File.ReadAllText(Path.Combine(root,"window-placement.json")));}catch{}
            Rect work=SystemParameters.WorkArea;
            if(lastBounds.Right-lastBounds.Left<200||lastBounds.Bottom-lastBounds.Top<150)lastBounds=Rect((int)(work.Left+work.Width*.12),(int)(work.Top+work.Height*.1),(int)(work.Width*.76),(int)(work.Height*.8));
        }
        if(test)lastBounds=Rect(80,80,900,620);
        window=new Window{Title="ChatGPT Startup Animation",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,WindowState=WindowState.Normal,ShowInTaskbar=false,ShowActivated=true,Topmost=false,Background=new SolidColorBrush(Color.FromRgb(8,7,14)),Content=vector};
        window.Left=lastBounds.Left;window.Top=lastBounds.Top;window.Width=lastBounds.Right-lastBounds.Left;window.Height=lastBounds.Bottom-lastBounds.Top;
        window.SourceInitialized+=delegate{overlay=new WindowInteropHelper(window).Handle;Position(lastBounds);};
        window.ContentRendered+=FirstPaint;
        window.Activated+=delegate{if(started&&target!=IntPtr.Zero&&StartupWindows.IsWindow(target))StartupWindows.SetForegroundWindow(target);};
        window.MouseLeftButtonDown+=delegate(object s,System.Windows.Input.MouseButtonEventArgs e){if(target!=IntPtr.Zero&&e.GetPosition(window).Y<40){StartupWindows.DragApp(target);e.Handled=true;}};
        window.KeyDown+=delegate(object s,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Escape)Finish("skipped");};
        window.Closed+=delegate{closed=true;};
        scan=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};scan.Tick+=Scan;
        animation=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(8.33)};animation.Tick+=Render;
        CompositionTarget.Rendering+=Presented;
        window.Show();
        app.Run();
        CompositionTarget.Rendering-=Presented;scan.Stop();animation.Stop();
        if(eventHook!=IntPtr.Zero)StartupWindows.UnhookWinEvent(eventHook);
        try {
            File.WriteAllText(Path.Combine(root,test?"launcher-self-test.json":"last-run.json"),json.Serialize(new {Elapsed=clock.Elapsed.TotalSeconds,FirstFrameMs=firstFrame*1000,ApplicationLaunchMs=launchTime*1000,RenderCallbacks=frames,WindowRebinds=rebinds,BoundsMismatchFrames=boundsMismatches,Reason=reason,BlackFirstFrame=blackFirstFrame,LayeringFailures=layeringFailures,CoveringSamples=coveringSamples,NativeExecutable=true,Test=test,RendererTier=RenderCapability.Tier>>16}));
            if(!test)File.WriteAllText(Path.Combine(root,"window-placement.json"),json.Serialize(lastBounds));
        }catch{}
        if(test)return blackFirstFrame&&reason=="complete"&&frames>20&&rebinds>=2&&boundsMismatches==0&&layeringFailures==0&&coveringSamples>3&&firstFrame>=0&&launchTime>=firstFrame?0:2;
        return 0;
    }
    void FirstPaint(object s,EventArgs e) {
        if(started)return;started=true;firstFrame=clock.Elapsed.TotalSeconds;
        if(test) {
            int w=(int)vector.ActualWidth,h=(int)vector.ActualHeight;
            var image=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);image.Render(vector);
            var pixels=new byte[w*h*4];image.CopyPixels(pixels,w*4,0);blackFirstFrame=true;
            for(int i=0;i<pixels.Length;i+=4)if(pixels[i]!=14||pixels[i+1]!=7||pixels[i+2]!=8||pixels[i+3]!=255){blackFirstFrame=false;break;}
        }else blackFirstFrame=vector.Time==0;
        // Native render flush, then dispatch activation after the visible animation frame.
        StartupWindows.DwmFlush();
        windowEvents=delegate(IntPtr hook,uint evt,IntPtr h,int obj,int child,uint thread,uint time) {
            if(!closed&&obj==0&&child==0&&h!=overlay&&(h==target||StartupWindows.IsAppWindow(h)))try{Scan(null,EventArgs.Empty);}catch{}
        };
        eventHook=StartupWindows.SetWinEventHook(0x8001,0x800B,IntPtr.Zero,windowEvents,0,0,2);
        window.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate {
            if(closed)return;
            launchTime=clock.Elapsed.TotalSeconds;
            if(test)CreateStub(false);
            else {Process.Start(new ProcessStartInfo(Path.Combine(root,"ChatGPT-original.lnk")){UseShellExecute=true});}
            scan.Start();
        }));
        animation.Start();
    }
    void CreateStub(bool main) {
        var old=stub;
        stub=new Window{Title=main?"Startup regression: replacement":"Startup regression: transient",Width=900,Height=620,Left=80,Top=80,ShowActivated=false,ShowInTaskbar=false,Background=main?new SolidColorBrush(Color.FromRgb(28,30,36)):Brushes.White};
        stub.Show();if(old!=null)old.Close();
    }
    void Scan(object s,EventArgs e) {
        double elapsed=clock.Elapsed.TotalSeconds-firstFrame;
        if(test&&elapsed>1.15&&!replaced){replaced=true;CreateStub(true);}
        if(test&&elapsed>2.5&&!resized){resized=true;stub.Left=120;stub.Top=100;stub.Width=1000;stub.Height=650;}
        if(test&&elapsed>3.05&&!testedCover) {
            testedCover=true;covering=new Window{Title="Other application layering test",Left=180,Top=150,Width=560,Height=360,ShowInTaskbar=false,ShowActivated=true,Background=Brushes.White};covering.Show();covering.Activate();
        }
        if(test&&covering!=null&&elapsed>3.2&&elapsed<3.9){coveringSamples++;if(StartupWindows.IsAbove(overlay,new WindowInteropHelper(covering).Handle))layeringFailures++;}
        if(test&&covering!=null&&elapsed>=4){covering.Close();covering=null;}
        IntPtr found=test?(stub==null?IntPtr.Zero:new WindowInteropHelper(stub).Handle):StartupWindows.FindApp();
        if(found!=IntPtr.Zero&&StartupWindows.IsWindow(found)) {
            if(found!=target){target=found;targetSince=clock.Elapsed.TotalSeconds;rebinds++;captureStarted=false;}
            if(!StartupWindows.IsIconic(target)) {
                lastBounds=StartupWindows.ContentBounds(target);Position(lastBounds);
                if(!window.IsVisible)window.Show();
            }else window.Hide();
            if(!test&&!captureStarted&&clock.Elapsed.TotalSeconds-targetSince>.8)CaptureAsync(target);
        }
        // Do not attach GWLP_HWNDPARENT: destroying an app's temporary window must not destroy the splash.
        // A destroyed/replaced HWND is re-discovered above while the animation stays on screen.
        if(elapsed>25)Finish("window-timeout");
    }
    void CaptureAsync(IntPtr h) {
        captureStarted=true;
        var thread=new Thread(delegate() {
            try {ImageSource image=StartupWindows.CaptureClient(h);if(image!=null)window.Dispatcher.BeginInvoke(new Action(delegate{if(!closed&&target==h)vector.Backdrop=image;}));}catch{}
        });thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();
    }
    void Position(StartupWindows.RECT b) {
        if(overlay==IntPtr.Zero||b.Right<=b.Left||b.Bottom<=b.Top)return;
        uint flags=0x0010|0x0040;IntPtr after=IntPtr.Zero;
        if(target!=IntPtr.Zero&&StartupWindows.IsWindow(target)) {
            after=StartupWindows.GetWindow(target,3);
            if(after==overlay){after=IntPtr.Zero;flags|=0x0004;}
            else if(after!=IntPtr.Zero&&(StartupWindows.GetLong(after,-20).ToInt64()&8)!=0)after=IntPtr.Zero;
        } else flags|=0x0004;
        StartupWindows.SetWindowPos(overlay,after,b.Left,b.Top,b.Right-b.Left,b.Bottom-b.Top,flags);
    }
    void Presented(object sender,EventArgs args) {
        var rendering=args as RenderingEventArgs;
        double stamp=rendering==null?clock.Elapsed.TotalSeconds:rendering.RenderingTime.TotalSeconds;
        if(stamp!=lastFrameTime){lastFrameTime=stamp;frames++;}
    }
    void Render(object sender,EventArgs args) {
        if(closed||!started)return;
        double elapsed=clock.Elapsed.TotalSeconds-firstFrame;
        if(target!=IntPtr.Zero&&StartupWindows.IsWindow(target)&&!StartupWindows.IsIconic(target)) {
            lastBounds=StartupWindows.ContentBounds(target);Position(lastBounds);
            if(test&&(StartupWindows.GetLong(overlay,-20).ToInt64()&8)!=0)layeringFailures++;
            if(test){StartupWindows.RECT actual;StartupWindows.GetWindowRect(overlay,out actual);if(actual.Left!=lastBounds.Left||actual.Top!=lastBounds.Top||actual.Right!=lastBounds.Right||actual.Bottom!=lastBounds.Bottom)boundsMismatches++;}
        }
        IntPtr fg=StartupWindows.GetForegroundWindow();
        if((fg==target||fg==overlay)&&StartupWindows.GetAsyncKeyState(27)<0){Finish("skipped");return;}
        double timeline=StartupVector.AnimationTime(elapsed);
        bool ready=target!=IntPtr.Zero&&StartupWindows.IsWindow(target)&&clock.Elapsed.TotalSeconds-targetSince>.8;
        if(timeline>=7.25) {
            if(revealStart<0&&ready)revealStart=elapsed;
            timeline=revealStart<0?7.249:7.25+(elapsed-revealStart);
        }
        vector.Time=Math.Max(0,timeline);vector.InvalidateVisual();
        StartupWindows.Reveal(overlay,timeline);
        if(timeline>=8.4)Finish("complete");
    }
    void Finish(string why) {
        if(closed)return;reason=why;scan.Stop();animation.Stop();closed=true;
        if(covering!=null)covering.Close();if(stub!=null)stub.Close();window.Close();Application.Current.Shutdown();
    }
}
