using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Snug.Windows.Services;

internal static class Probe
{
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern bool GetCursorPos(out P p);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(P p);
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h,uint flag);
 [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")] static extern IntPtr SetProcessDpiAwarenessContext(IntPtr c);
 [StructLayout(LayoutKind.Sequential)] struct P {public int X,Y;}
 static int clicks;
 // Runs a real ScrollViewer and timer with controlled release samples.
 // Does not synthesize input or touch the user's cursor.
 static int Programmatic()
 {
  var app=new Application();
  var scroll=new ScrollViewer{Content=new Border{Height=3000,Background=System.Windows.Media.Brushes.Transparent}};
  PhoneDragScroll.Attach(scroll);
  var win=new Window{Title="SNUG_INERTIA_TIMER_PROBE_ONLY",Width=320,Height=480,
      ShowActivated=false,ShowInTaskbar=false,Content=scroll,WindowStartupLocation=WindowStartupLocation.Manual,Left=0,Top=0};
  var result=1;
  win.Loaded+=async (_,_)=>{
   try{
    await Task.Delay(350);
    var adapterType=typeof(PhoneDragScroll);
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var attached=(DependencyProperty)adapterType.GetField("Attached",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null)!;
    var adapter=scroll.GetValue(attached);
    var momentum=(Snug.Windows.Core.MenuMomentum)adapterType.GetField("_momentum",flags)!.GetValue(adapter)!;
    void Start(double offset,double sign){
      scroll.ScrollToVerticalOffset(offset);
      adapterType.GetField("_coastOffset",flags)!.SetValue(adapter,offset);
      const double t=10;
      momentum.Begin(t,offset-sign*90);
      momentum.Sample(t+.05,offset-sign*40);
      momentum.Sample(t+.09,offset);
      adapterType.GetMethod("StartCoast",flags)!.Invoke(adapter,new object[]{t+.1});
    }
    Start(300,1);
    await Task.Delay(330);
    var moved=scroll.VerticalOffset;
    Console.WriteLine($"WPF_TIMER_OFFSET=300 -> {moved:F1}");
    if(moved<=360||!momentum.IsCoasting)throw new Exception("Live timer did not coast");
    PhoneDragScroll.StopScrolling(scroll);
    await Task.Delay(100);
    var stopped=scroll.VerticalOffset;
    await Task.Delay(400);
    Console.WriteLine($"WPF_TIMER_STOP={stopped:F1} -> {scroll.VerticalOffset:F1}");
    if(momentum.IsCoasting||Math.Abs(stopped-scroll.VerticalOffset)>1)throw new Exception("Timer stop failed");
    Start(stopped,-1);
    await Task.Delay(1500);
    var settled=scroll.VerticalOffset;
    await Task.Delay(150);
    if(settled>=stopped||momentum.IsCoasting||Math.Abs(settled-scroll.VerticalOffset)>1)throw new Exception("Reverse settle failed");
    Console.WriteLine($"WPF_TIMER_REVERSE={settled:F1}");
    Start(400,1);await Task.Delay(180);win.Hide();await Task.Delay(250);
    if(momentum.IsCoasting)throw new Exception("Hidden viewer continued coasting");
    Console.WriteLine("WPF_MENU_TIMER_AND_HIDE=PASS");
    result=0;
   }catch(Exception ex){Console.WriteLine(ex);}
   finally{win.Close();}
  };
  app.Run(win);return result;
 }
 [STAThread] static int Main(string[] args)
 {
  if(args.Contains("--programmatic"))return Programmatic();
  var app = new Application();
  var stack = new StackPanel();
  for (int i=0; i<30; i++)
  {
   var b=new Button {Content=$"Test item {i}",Height=55, Margin=new Thickness(5)};
   b.Click+=(s,e)=>clicks++;
   stack.Children.Add(b);
  }
  var scroll=new ScrollViewer {Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
  PhoneDragScroll.Attach(scroll);
  var win=new Window {Title="SNUG_MENU_SCROLL_FIXTURE_ONLY",Width=380,Height=520,Left=80,Top=100,WindowStartupLocation=WindowStartupLocation.Manual,Content=scroll,Topmost=true};
  int result=1;
  win.PreviewMouseWheel+=(_,e)=>Console.WriteLine($"EXTERNAL_WHEEL {e.Delta}");
  win.PreviewKeyDown+=(_,e)=>Console.WriteLine($"EXTERNAL_KEY {e.Key}");
  win.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,new System.Windows.Input.MouseButtonEventHandler((_,e)=>Console.WriteLine($"DOWN {e.GetPosition(scroll)} source={e.OriginalSource?.GetType().Name} handled={e.Handled}")),true);
  win.AddHandler(UIElement.MouseLeftButtonUpEvent,new System.Windows.Input.MouseButtonEventHandler((_,e)=>Console.WriteLine($"UP {e.GetPosition(scroll)} source={e.OriginalSource?.GetType().Name} handled={e.Handled}")),true);
  win.Loaded+=async (_,_)=>{
   GetCursorPos(out var old);
   try{
    await Task.Delay(700);
    var hwnd=new WindowInteropHelper(win).Handle;
    win.Activate(); await Task.Delay(250);
    void PointAt(double x,double y){
     var pt=scroll.PointToScreen(new Point(x,y));
     var p=new P{X=(int)pt.X,Y=(int)pt.Y};
     if(GetAncestor(WindowFromPoint(p),2)!=hwnd)throw new Exception("Fixture not under cursor; abort");
     SetCursorPos(p.X,p.Y);
    }
    void CheckAt(double x,double y){
     var pt=scroll.PointToScreen(new Point(x,y));GetCursorPos(out var actual);
     if(Math.Abs(actual.X-pt.X)>4||Math.Abs(actual.Y-pt.Y)>4)
       throw new Exception($"Concurrent pointer input: expected {pt}, actual {actual.X},{actual.Y}");
    }
    async Task Tap(double x,double y){
     PointAt(x,y); mouse_event(2,0,0,0,UIntPtr.Zero);
     try{await Task.Delay(70); CheckAt(x,y);} finally{mouse_event(4,0,0,0,UIntPtr.Zero);} await Task.Delay(150);
    }
    async Task Drag(double x,double y,double end, bool pause=false){
     PointAt(x,y);mouse_event(2,0,0,0,UIntPtr.Zero);
     await Task.Delay(60);
     try {for(int i=1;i<=12;i++){PointAt(x,y+(end-y)*i/12);await Task.Delay(18);}if(pause)await Task.Delay(250);CheckAt(x,end);}
     finally{mouse_event(4,0,0,0,UIntPtr.Zero);}
     await Task.Delay(160);
    }
    await Tap(130,31);
    Console.WriteLine($"TAP_CLICKS={clicks}");
    if(clicks!=1)throw new Exception("Short tap did not activate button");
    await Drag(130,410,110,true);
    Console.WriteLine($"UP_DRAG_OFFSET={scroll.VerticalOffset:F1} clicks={clicks}");
    if(scroll.VerticalOffset<270 || clicks!=1)throw new Exception("Up drag did not scroll without click");
    await Drag(130,110,410,true);
    Console.WriteLine($"DOWN_DRAG_OFFSET={scroll.VerticalOffset:F1} clicks={clicks}");
    if(scroll.VerticalOffset>10 || clicks!=1)throw new Exception("Reverse drag failed");
    await Tap(130,31);
    if(clicks!=2)throw new Exception("Tap after drag did not activate normally");
    // Fast drag must overshoot the finger travel; touching moving content
    // stops inertia rather than opening an application.
    await Drag(130,410,110);
    var coast=scroll.VerticalOffset;
    Console.WriteLine($"FLING_OFFSET={coast:F1}");
    if(coast<=340||clicks!=2)throw new Exception("Inertia missing");
    await Tap(130,220);
    var stopped=scroll.VerticalOffset;
    await Task.Delay(400);
    Console.WriteLine($"STOP_OFFSET={stopped:F1} -> {scroll.VerticalOffset:F1} clicks={clicks}");
    if(Math.Abs(stopped-scroll.VerticalOffset)>2||clicks!=2)throw new Exception("Tap did not stop inertia without opening item");
    await Tap(130,220);
    if(clicks!=3)throw new Exception("Tap after stopping failed");
    await Drag(130,110,410);
    await Task.Delay(1600);
    var settled=scroll.VerticalOffset;
    await Task.Delay(250);
    if(settled>coast||Math.Abs(settled-scroll.VerticalOffset)>2)throw new Exception("Reverse fling did not settle");
    Console.WriteLine($"REVERSE_SETTLED_OFFSET={settled:F1}");
    PhoneDragScroll.StopScrolling(scroll);
    scroll.ScrollToTop();
    await Task.Delay(240);
    if(scroll.VerticalOffset>2)throw new Exception("Tab/reset scroll failed");
    Console.WriteLine("WPF_MENU_INERTIA_STOP_AND_RESET=PASS");
    Console.WriteLine("WPF_MENU_REAL_MOUSE_DRAG_AND_TAP=PASS");
    result=0;
   }catch(Exception ex){Console.WriteLine(ex.ToString());}
   finally{SetCursorPos(old.X,old.Y);win.Close();}
  };
  app.Run(win);
  return result;
 }
}