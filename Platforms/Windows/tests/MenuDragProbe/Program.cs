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
 [STAThread] static int Main()
 {
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
  win.Loaded+=async (_,_)=>{
   GetCursorPos(out var old);
   try{
    await Task.Delay(700);
    var hwnd=new WindowInteropHelper(win).Handle;
    void PointAt(double x,double y){
     var pt=scroll.PointToScreen(new Point(x,y));
     var p=new P{X=(int)pt.X,Y=(int)pt.Y};
     if(GetAncestor(WindowFromPoint(p),2)!=hwnd)throw new Exception("Fixture not under cursor; abort");
     SetCursorPos(p.X,p.Y);
    }
    async Task Tap(double x,double y){
     PointAt(x,y); mouse_event(2,0,0,0,UIntPtr.Zero);
     await Task.Delay(70); mouse_event(4,0,0,0,UIntPtr.Zero); await Task.Delay(150);
    }
    async Task Drag(double x,double y,double end){
     PointAt(x,y);mouse_event(2,0,0,0,UIntPtr.Zero);
     await Task.Delay(60);
     try {for(int i=1;i<=12;i++){PointAt(x,y+(end-y)*i/12);await Task.Delay(18);}}
     finally{mouse_event(4,0,0,0,UIntPtr.Zero);}
     await Task.Delay(160);
    }
    await Tap(130,31);
    Console.WriteLine($"TAP_CLICKS={clicks}");
    if(clicks!=1)throw new Exception("Short tap did not activate button");
    await Drag(130,410,110);
    Console.WriteLine($"UP_DRAG_OFFSET={scroll.VerticalOffset:F1} clicks={clicks}");
    if(scroll.VerticalOffset<270 || clicks!=1)throw new Exception("Up drag did not scroll without click");
    await Drag(130,110,410);
    Console.WriteLine($"DOWN_DRAG_OFFSET={scroll.VerticalOffset:F1} clicks={clicks}");
    if(scroll.VerticalOffset>10 || clicks!=1)throw new Exception("Reverse drag failed");
    await Tap(130,31);
    if(clicks!=2)throw new Exception("Tap after drag did not activate normally");
    Console.WriteLine("WPF_MENU_REAL_MOUSE_DRAG_AND_TAP=PASS");
    result=0;
   }catch(Exception ex){Console.WriteLine(ex.ToString());}
   finally{SetCursorPos(old.X,old.Y);win.Close();}
  };
  app.Run(win);
  return result;
 }
}