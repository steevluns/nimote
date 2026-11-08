using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class Native {
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
internal sealed class HotkeyWindow:Form {
 NotifyIcon tray;
 int brightness=100,contrast=100,gamma=100,saturation=100,red=100,green=100,blue=100;
 bool magnificationReady=false;
 string ConfigFile { get { return Path.Combine(Application.UserAppDataPath,"settings.ini"); } }
 public HotkeyWindow(){
  LoadConfig();
  ShowInTaskbar=false;WindowState=FormWindowState.Minimized;FormBorderStyle=FormBorderStyle.FixedToolWindow;Opacity=0;
  Load+=Loaded;FormClosing+=Closing;
 }
 void Loaded(object s,EventArgs e){
  try{magnificationReady=Native.MagInitialize();if(magnificationReady)Native.MagSetFullscreenTransform(1.0f,0,0);}catch{magnificationReady=false;}
  tray=new NotifyIcon();tray.Icon=SystemIcons.Application;tray.Text="Gamma Hotkeys";ContextMenu m=new ContextMenu();
  m.MenuItems.Add("الإعدادات: الألوان والسطوع",delegate{ShowSettings();});
  m.MenuItems.Add("تطبيق الإعدادات الحالية",delegate{ApplyCurrent();});
  m.MenuItems.Add("إعادة محايد",delegate{SetNeutral();});
  m.MenuItems.Add("خروج",delegate{Close();});tray.ContextMenu=m;tray.Visible=true;
  tray.ShowBalloonTip(2200,"Gamma Hotkeys","اضغط بزر الفأرة الأيمن على الأيقونة لفتح الإعدادات.",ToolTipIcon.Info);
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
  }}catch(Exception ex){MessageBox.Show("تعذر حفظ الإعدادات: "+ex.Message);}
 }
 void LoadConfig(){
  try{if(!File.Exists(ConfigFile))return;string[] a=File.ReadAllLines(ConfigFile);if(a.Length<7)return;
   brightness=Clamp(int.Parse(a[0]),20,200);contrast=Clamp(int.Parse(a[1]),20,200);gamma=Clamp(int.Parse(a[2]),20,200);
   saturation=Clamp(int.Parse(a[3]),0,200);red=Clamp(int.Parse(a[4]),20,200);green=Clamp(int.Parse(a[5]),20,200);blue=Clamp(int.Parse(a[6]),20,200);
  }catch{}
 }
 static int Clamp(int v,int min,int max){return Math.Max(min,Math.Min(max,v));}
 void Closing(object s,FormClosingEventArgs e){
  if(tray!=null){tray.Visible=false;tray.Dispose();}SetNeutral();if(magnificationReady){try{Native.MagSetFullscreenColorEffect(new float[]{1,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,1});Native.MagUninitialize();}catch{}}
 }
 sealed class SettingsForm:Form {
  HotkeyWindow owner;TrackBar[] bars=new TrackBar[7];Label[] values=new Label[7];
  string[] labels={"Brightness / السطوع","Contrast / التباين","Gamma","Color Saturation / تشبع الألوان","Red / الأحمر","Green / الأخضر","Blue / الأزرق"};
  public SettingsForm(HotkeyWindow o){
   owner=o;
   Text="إعدادات Gamma Hotkeys";StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.Sizable;MaximizeBox=false;MinimizeBox=false;ClientSize=new Size(650,650);MinimumSize=new Size(666,689);
   CheckBox fullScreen=new CheckBox();fullScreen.Text="عرض شاشة كاملة";fullScreen.AutoSize=true;fullScreen.SetBounds(15,12,180,24);Controls.Add(fullScreen);
   fullScreen.CheckedChanged+=delegate{
    if(fullScreen.Checked){WindowState=FormWindowState.Maximized;}
    else{WindowState=FormWindowState.Normal;ClientSize=new Size(650,650);StartPosition=FormStartPosition.CenterScreen;}
   };
   int y=46;
   for(int i=0;i<7;i++){
    Label l=new Label();l.Text=labels[i];l.SetBounds(15,y,125,24);Controls.Add(l);
    bars[i]=new TrackBar();bars[i].Minimum=20;bars[i].Maximum=200;bars[i].TickFrequency=20;bars[i].SetBounds(140,y-9,390,40);Controls.Add(bars[i]);
    values[i]=new Label();values[i].SetBounds(545,y,65,24);Controls.Add(values[i]);int ix=i;bars[i].ValueChanged+=delegate{values[ix].Text=bars[ix].Value+"%";Preview();};y+=38;
   }
   Button close=new Button();close.Text="إغلاق";close.SetBounds(170,600,95,30);close.Click+=delegate{Close();};Controls.Add(close);
   Button save=new Button();save.Text="حفظ وتطبيق";save.SetBounds(275,600,115,30);save.Click+=delegate{Save();};Controls.Add(save);
   Button reset=new Button();reset.Text="افتراضي";reset.SetBounds(400,600,95,30);reset.Click+=delegate{ResetAll();};Controls.Add(reset);
   int[] v={o.brightness,o.contrast,o.gamma,o.saturation,o.red,o.green,o.blue};for(int i=0;i<7;i++){bars[i].Value=v[i];values[i].Text=v[i]+"%";}
  }
  void Preview(){if(bars[0]!=null){owner.brightness=bars[0].Value;owner.contrast=bars[1].Value;owner.gamma=bars[2].Value;owner.saturation=bars[3].Value;owner.red=bars[4].Value;owner.green=bars[5].Value;owner.blue=bars[6].Value;owner.ApplyCurrent();}}
  void Save(){
   Preview();owner.SaveConfig();MessageBox.Show("تم حفظ الإعدادات.");Close();
  }
  void ResetAll(){
   int[] v={100,100,100,100,100,100,100};for(int i=0;i<7;i++)bars[i].Value=v[i];
   Preview();
  }
 }
}
static class Program{[STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new HotkeyWindow());}}
