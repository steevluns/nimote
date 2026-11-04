using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class Native {
 [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr hWnd,int id,uint modifiers,uint key);
 [DllImport("user32.dll",SetLastError=true)] public static extern bool UnregisterHotKey(IntPtr hWnd,int id);
 [DllImport("gdi32.dll",SetLastError=true)] public static extern bool SetDeviceGammaRamp(IntPtr hdc,ref GammaRamp ramp);
 [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd,IntPtr hdc);
 [DllImport("Magnification.dll")] public static extern bool MagInitialize();
 [DllImport("Magnification.dll")] public static extern bool MagUninitialize();
 [DllImport("Magnification.dll")] public static extern bool MagSetFullscreenTransform(float magnification,int x,int y);
 [DllImport("Magnification.dll")] public static extern bool MagSetFullscreenColorEffect([In] float[] effect);
 [StructLayout(LayoutKind.Sequential)] public struct GammaRamp {
  [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public ushort[] Red;
  [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public ushort[] Green;
  [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public ushort[] Blue;
 }
}
internal sealed class HotkeySetting {
 public string Name; public Keys Key; public uint Modifiers;
 public HotkeySetting(string name,Keys key,uint mods){Name=name;Key=key;Modifiers=mods;}
 public string Display { get { string s="";if((Modifiers&2)!=0)s+="Ctrl + ";if((Modifiers&1)!=0)s+="Alt + ";if((Modifiers&4)!=0)s+="Shift + ";return s+Key.ToString(); } }
}
internal sealed class HotkeyWindow:Form {
 const int WM_HOTKEY=0x312;
 NotifyIcon tray;
 HotkeySetting[] hotkeys;
 int brightness=100,contrast=100,gamma=100,saturation=100,red=100,green=100,blue=100;
 bool magnificationReady=false;
 string ConfigFile { get { return Path.Combine(Application.UserAppDataPath,"settings.ini"); } }
 public HotkeyWindow(){
  hotkeys=new HotkeySetting[]{new HotkeySetting("Gamma منخفض",Keys.D1,3),new HotkeySetting("Gamma طبيعي",Keys.D2,3),new HotkeySetting("Gamma مرتفع",Keys.D3,3),new HotkeySetting("سطوع مرتفع",Keys.D4,3),new HotkeySetting("سطوع منخفض",Keys.D5,3),new HotkeySetting("إعادة محايد",Keys.D0,3),new HotkeySetting("خروج",Keys.Q,3)};
  LoadConfig();
  ShowInTaskbar=false;WindowState=FormWindowState.Minimized;FormBorderStyle=FormBorderStyle.FixedToolWindow;Opacity=0;
  Load+=Loaded;FormClosing+=Closing;
 }
 void Loaded(object s,EventArgs e){
  RegisterAll();
  try{magnificationReady=Native.MagInitialize();if(magnificationReady)Native.MagSetFullscreenTransform(1.0f,0,0);}catch{magnificationReady=false;}
  tray=new NotifyIcon();tray.Icon=SystemIcons.Application;tray.Text="Gamma Hotkeys";ContextMenu m=new ContextMenu();
  m.MenuItems.Add("الإعدادات: الألوان والسطوع والاختصارات",delegate{ShowSettings();});
  m.MenuItems.Add("تطبيق الإعدادات الحالية",delegate{ApplyCurrent();});
  m.MenuItems.Add("إعادة محايد",delegate{SetNeutral();});
  m.MenuItems.Add("خروج",delegate{Close();});tray.ContextMenu=m;tray.Visible=true;
  tray.ShowBalloonTip(2200,"Gamma Hotkeys","اضغط بزر الفأرة الأيمن على الأيقونة لفتح الإعدادات.",ToolTipIcon.Info);
 }
 void RegisterAll(){
  for(int i=0;i<7;i++)Native.UnregisterHotKey(Handle,i+1);
  for(int i=0;i<7;i++)if(!Native.RegisterHotKey(Handle,i+1,hotkeys[i].Modifiers,(uint)hotkeys[i].Key))
   if(tray!=null)tray.ShowBalloonTip(1500,"اختصار غير متاح",hotkeys[i].Display,ToolTipIcon.Warning);
 }
 protected override void WndProc(ref Message msg){
  if(msg.Msg==WM_HOTKEY){int id=msg.WParam.ToInt32();if(id>=1&&id<=7)RunAction(id-1);}
  base.WndProc(ref msg);
 }
 void RunAction(int n){
  switch(n){
   case 0:gamma=80;ApplyCurrent();break;
   case 1:SetNeutral();break;
   case 2:gamma=125;ApplyCurrent();break;
   case 3:brightness=125;ApplyCurrent();break;
   case 4:brightness=75;ApplyCurrent();break;
   case 5:SetNeutral();break;
   case 6:Close();break;
  }
 }
 void SetNeutral(){brightness=100;contrast=100;gamma=100;saturation=100;red=100;green=100;blue=100;ApplyCurrent();}
 Native.GammaRamp MakeRamp(){
  Native.GammaRamp r=new Native.GammaRamp();r.Red=new ushort[256];r.Green=new ushort[256];r.Blue=new ushort[256];
  for(int i=0;i<256;i++){
   double x=i/255.0;
   r.Red[i]=Value(x,red);r.Green[i]=Value(x,green);r.Blue[i]=Value(x,blue);
  }return r;
 }
 ushort Value(double x,int channel){
  double y=Math.Pow(x,100.0/Math.Max(20,gamma));
  y=((y-.5)*(contrast/100.0)+.5)*(brightness/100.0)*(channel/100.0);
  return (ushort)Math.Round(Math.Max(0,Math.Min(1,y))*65535.0);
 }
 void ApplyCurrent(){
  Native.GammaRamp r=MakeRamp();IntPtr dc=Native.GetDC(IntPtr.Zero);bool ok=false;
  bool saturationOk=false;
  if(magnificationReady){float sat=saturation/100.0f,lr=0.2126f,lg=0.7152f,lb=0.0722f,iv=1.0f-sat;float[] m=new float[25];m[0]=lr*iv+sat;m[1]=lg*iv;m[2]=lb*iv;m[5]=lr*iv;m[6]=lg*iv+sat;m[7]=lb*iv;m[10]=lr*iv;m[11]=lg*iv;m[12]=lb*iv+sat;m[18]=1.0f;m[24]=1.0f;try{saturationOk=Native.MagSetFullscreenColorEffect(m);}catch{}}
  try{ok=Native.SetDeviceGammaRamp(dc,ref r);}finally{Native.ReleaseDC(IntPtr.Zero,dc);}
  if(tray!=null)tray.ShowBalloonTip(1200,"Gamma Hotkeys",(ok?"تم تطبيق السطوع والتباين وGamma":"تعذر تطبيق Gamma")+(magnificationReady?(saturationOk?"، وتشبع الألوان":"، تعذر تطبيق تشبع الألوان"):"، تشبع الألوان غير مدعوم"),ok&&(!magnificationReady||saturationOk)?ToolTipIcon.Info:ToolTipIcon.Warning);
 }
 void ShowSettings(){using(SettingsForm f=new SettingsForm(this))f.ShowDialog();}
 void SaveConfig(){
  try{Directory.CreateDirectory(Application.UserAppDataPath);using(StreamWriter w=new StreamWriter(ConfigFile,false)){
   w.WriteLine(brightness);w.WriteLine(contrast);w.WriteLine(gamma);w.WriteLine(saturation);w.WriteLine(red);w.WriteLine(green);w.WriteLine(blue);
   foreach(HotkeySetting h in hotkeys)w.WriteLine(((int)h.Key)+","+h.Modifiers);
  }}catch(Exception ex){MessageBox.Show("تعذر حفظ الإعدادات: "+ex.Message);}
 }
 void LoadConfig(){
  try{if(!File.Exists(ConfigFile))return;string[] a=File.ReadAllLines(ConfigFile);if(a.Length<11)return;
   brightness=Clamp(int.Parse(a[0]),20,200);contrast=Clamp(int.Parse(a[1]),20,200);gamma=Clamp(int.Parse(a[2]),20,200);
   if(a.Length>=14){saturation=Clamp(int.Parse(a[3]),0,200);red=Clamp(int.Parse(a[4]),20,200);green=Clamp(int.Parse(a[5]),20,200);blue=Clamp(int.Parse(a[6]),20,200);}else{saturation=Clamp(int.Parse(a[3]),0,200);red=green=blue=100;}
   int offset=a.Length>=14?7:4;
   for(int i=0;i<7;i++){string[] p=a[i+offset].Split(',');hotkeys[i].Key=(Keys)int.Parse(p[0]);hotkeys[i].Modifiers=uint.Parse(p[1]);}
  }catch{}
 }
 static int Clamp(int v,int min,int max){return Math.Max(min,Math.Min(max,v));}
 void Closing(object s,FormClosingEventArgs e){
  for(int i=1;i<=7;i++)Native.UnregisterHotKey(Handle,i);
  if(tray!=null){tray.Visible=false;tray.Dispose();}SetNeutral();if(magnificationReady){try{Native.MagSetFullscreenColorEffect(new float[]{1,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,1});Native.MagUninitialize();}catch{}}
 }
 sealed class SettingsForm:Form {
  HotkeyWindow owner;TrackBar[] bars=new TrackBar[7];Label[] values=new Label[7];TextBox[] keys=new TextBox[7];HotkeySetting[] edited;
  string[] labels={"Brightness / السطوع","Contrast / التباين","Gamma","Color Saturation / تشبع الألوان","Red / الأحمر","Green / الأخضر","Blue / الأزرق"};
  public SettingsForm(HotkeyWindow o){
   owner=o;edited=new HotkeySetting[7];for(int i=0;i<7;i++)edited[i]=new HotkeySetting(o.hotkeys[i].Name,o.hotkeys[i].Key,o.hotkeys[i].Modifiers);
   Text="إعدادات Gamma Hotkeys";StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ClientSize=new Size(510,740);
   int y=12;
   for(int i=0;i<7;i++){
    Label l=new Label();l.Text=labels[i];l.SetBounds(15,y,90,24);Controls.Add(l);
    bars[i]=new TrackBar();bars[i].Minimum=20;bars[i].Maximum=200;bars[i].TickFrequency=20;bars[i].SetBounds(105,y-8,310,42);Controls.Add(bars[i]);
    values[i]=new Label();values[i].SetBounds(420,y,65,24);Controls.Add(values[i]);int ix=i;bars[i].ValueChanged+=delegate{values[ix].Text=bars[ix].Value+"%";Preview();};y+=50;
   }
   Label title=new Label();title.Text="تخصيص اختصارات لوحة المفاتيح (اضغط داخل المربع ثم المفاتيح)";title.SetBounds(15,y,480,25);Controls.Add(title);y+=30;
   string[] names={"Gamma منخفض","Gamma طبيعي","Gamma مرتفع","سطوع مرتفع","سطوع منخفض","إعادة محايد","خروج"};
   for(int i=0;i<7;i++){
    Label l=new Label();l.Text=names[i];l.SetBounds(18,y,145,25);Controls.Add(l);
    keys[i]=new TextBox();keys[i].ReadOnly=true;keys[i].SetBounds(170,y-2,300,25);keys[i].Text=edited[i].Display;keys[i].Tag=i;keys[i].KeyDown+=CaptureKey;Controls.Add(keys[i]);y+=32;
   }
   Button save=new Button();save.Text="حفظ وتطبيق";save.SetBounds(275,690,105,30);save.Click+=delegate{Save();};Controls.Add(save);
   Button reset=new Button();reset.Text="افتراضي";reset.SetBounds(390,690,90,30);reset.Click+=delegate{ResetAll();};Controls.Add(reset);
   Button close=new Button();close.Text="إغلاق";close.SetBounds(175,690,90,30);close.Click+=delegate{Close();};Controls.Add(close);
   int[] v={o.brightness,o.contrast,o.gamma,o.saturation,o.red,o.green,o.blue};for(int i=0;i<7;i++){bars[i].Value=v[i];values[i].Text=v[i]+"%";}
  }
  void CaptureKey(object sender,KeyEventArgs e){
   int i=(int)((TextBox)sender).Tag;Keys k=e.KeyCode;if(k==Keys.ControlKey||k==Keys.Menu||k==Keys.ShiftKey)return;
   uint mods=0;if(e.Control)mods|=2;if(e.Alt)mods|=1;if(e.Shift)mods|=4;
   if(mods==0){MessageBox.Show("اختر الاختصار مع Ctrl أو Alt أو Shift.");return;}
   edited[i].Key=k;edited[i].Modifiers=mods;keys[i].Text=edited[i].Display;e.SuppressKeyPress=true;e.Handled=true;
  }
  void Preview(){if(bars[0]!=null){owner.brightness=bars[0].Value;owner.contrast=bars[1].Value;owner.gamma=bars[2].Value;owner.saturation=bars[3].Value;owner.red=bars[4].Value;owner.green=bars[5].Value;owner.blue=bars[6].Value;owner.ApplyCurrent();}}
  void Save(){
   for(int i=0;i<7;i++)for(int j=i+1;j<7;j++)if(edited[i].Key==edited[j].Key&&edited[i].Modifiers==edited[j].Modifiers){MessageBox.Show("يوجد اختصار مكرر: "+edited[i].Display);return;}
   for(int i=0;i<7;i++){owner.hotkeys[i].Key=edited[i].Key;owner.hotkeys[i].Modifiers=edited[i].Modifiers;}
   Preview();owner.SaveConfig();owner.RegisterAll();MessageBox.Show("تم حفظ الإعدادات والاختصارات.");Close();
  }
  void ResetAll(){
   int[] v={100,100,100,100,100,100,100};for(int i=0;i<7;i++)bars[i].Value=v[i];
   Keys[] k={Keys.D1,Keys.D2,Keys.D3,Keys.D4,Keys.D5,Keys.D0,Keys.Q};
   for(int i=0;i<7;i++){edited[i].Key=k[i];edited[i].Modifiers=3;keys[i].Text=edited[i].Display;}
   Preview();
  }
 }
}
static class Program{[STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new HotkeyWindow());}}
