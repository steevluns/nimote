using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class Native {
 [DllImport("user32.dll", SetLastError=true)] public static extern bool RegisterHotKey(IntPtr hWnd,int id,uint modifiers,uint key);
 [DllImport("user32.dll", SetLastError=true)] public static extern bool UnregisterHotKey(IntPtr hWnd,int id);
 [DllImport("gdi32.dll", SetLastError=true)] public static extern bool SetDeviceGammaRamp(IntPtr hdc,ref GammaRamp ramp);
 [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd,IntPtr hdc);
 [StructLayout(LayoutKind.Sequential)] public struct GammaRamp {
  [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public ushort[] Red;
  [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public ushort[] Green;
  [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public ushort[] Blue;
 }
}
internal sealed class HotkeyWindow:Form {
 const int WM_HOTKEY=0x312; const uint MOD_ALT=1,MOD_CONTROL=2;
 NotifyIcon tray;
 public HotkeyWindow(){ShowInTaskbar=false;WindowState=FormWindowState.Minimized;FormBorderStyle=FormBorderStyle.FixedToolWindow;Opacity=0;Load+=Loaded;FormClosing+=Closing;}
 void Loaded(object s,EventArgs e){
  Reg(1,Keys.D1);Reg(2,Keys.D2);Reg(3,Keys.D3);Reg(4,Keys.D4);Reg(5,Keys.D5);Reg(6,Keys.D0);Reg(7,Keys.Q);
  tray=new NotifyIcon();tray.Icon=System.Drawing.SystemIcons.Application;tray.Text="Gamma Hotkeys";ContextMenu m=new ContextMenu();
  m.MenuItems.Add("Gamma low (Ctrl+Alt+1)",delegate{Apply(.80,1);});
  m.MenuItems.Add("Gamma normal (Ctrl+Alt+2)",delegate{Apply(1,1);});
  m.MenuItems.Add("Gamma high (Ctrl+Alt+3)",delegate{Apply(1.25,1);});
  m.MenuItems.Add("Brightness high (Ctrl+Alt+4)",delegate{Apply(1,1.25);});
  m.MenuItems.Add("Brightness low (Ctrl+Alt+5)",delegate{Apply(1,.75);});
  m.MenuItems.Add("Neutral (Ctrl+Alt+0)",delegate{Apply(1,1);});
  m.MenuItems.Add("Exit (Ctrl+Alt+Q)",delegate{Close();});tray.ContextMenu=m;tray.Visible=true;
  tray.ShowBalloonTip(2500,"Gamma Hotkeys","Hotkeys are ready.",ToolTipIcon.Info);
 }
 void Reg(int id,Keys key){if(!Native.RegisterHotKey(Handle,id,MOD_CONTROL|MOD_ALT,(uint)key)) MessageBox.Show("Hotkey registration failed: "+key);}
 protected override void WndProc(ref Message msg){if(msg.Msg==WM_HOTKEY){switch(msg.WParam.ToInt32()){
  case 1:Apply(.80,1);break;case 2:Apply(1,1);break;case 3:Apply(1.25,1);break;case 4:Apply(1,1.25);break;case 5:Apply(1,.75);break;case 6:Apply(1,1);break;case 7:Close();break;}}base.WndProc(ref msg);}
 Native.GammaRamp MakeRamp(double gamma,double brightness){Native.GammaRamp r=new Native.GammaRamp();r.Red=new ushort[256];r.Green=new ushort[256];r.Blue=new ushort[256];for(int i=0;i<256;i++){double x=i/255.0;double y=Math.Pow(x,1.0/gamma)*brightness;ushort v=(ushort)Math.Round(Math.Max(0,Math.Min(1,y))*65535);r.Red[i]=v;r.Green[i]=v;r.Blue[i]=v;}return r;}
 void Apply(double gamma,double brightness){Native.GammaRamp r=MakeRamp(gamma,brightness);IntPtr dc=Native.GetDC(IntPtr.Zero);bool ok=false;try{ok=Native.SetDeviceGammaRamp(dc,ref r);}finally{Native.ReleaseDC(IntPtr.Zero,dc);}if(tray!=null)tray.ShowBalloonTip(1000,"Gamma Hotkeys",ok?"Applied.":"Driver/game rejected gamma ramp.",ok?ToolTipIcon.Info:ToolTipIcon.Warning);}
 void Closing(object s,FormClosingEventArgs e){for(int i=1;i<=7;i++)Native.UnregisterHotKey(Handle,i);if(tray!=null){tray.Visible=false;tray.Dispose();}Apply(1,1);}
}
static class Program{[STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new HotkeyWindow());}}
