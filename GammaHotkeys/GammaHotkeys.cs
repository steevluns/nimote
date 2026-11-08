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

// AMD ADL saturation control. Uses the AMD driver so the adjustment is applied
// at the display-driver level rather than through the Magnification API.
internal static class AmdAdl {
 const int ADL_OK=0, ADL_DISPLAY_COLOR_SATURATION=1<<2;
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr AllocDelegate(int size);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int MainCreateDelegate(AllocDelegate callback,int enumConnectedAdapters);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int MainDestroyDelegate();
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int AdapterCountDelegate(ref int count);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ColorGetDelegate(int adapter,int display,int type,ref int current,ref int def,ref int min,ref int max,ref int step);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ColorSetDelegate(int adapter,int display,int type,int current);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi,SetLastError=true)] static extern IntPtr LoadLibrary(string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi,SetLastError=true)] static extern IntPtr GetProcAddress(IntPtr module,string name);
 [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
 static IntPtr module=IntPtr.Zero;
 static MainDestroyDelegate destroy;
 static ColorSetDelegate set;
 static int adapter=-1,display=-1,original=100,defaultValue=100,minValue=0,maxValue=200;
 static bool initialized=false;
 static IntPtr Alloc(int size){return Marshal.AllocHGlobal(size);}
 static T Proc<T>(string name) where T:class {
  IntPtr p=GetProcAddress(module,name);if(p==IntPtr.Zero)return null;
  return Marshal.GetDelegateForFunctionPointer(p,typeof(T)) as T;
 }
 public static bool Initialize(){
  if(initialized)return true;
  module=LoadLibrary("atiadlxx.dll");if(module==IntPtr.Zero)module=LoadLibrary("atiadlxy.dll");
  if(module==IntPtr.Zero)return false;
  MainCreateDelegate create=Proc<MainCreateDelegate>("ADL_Main_Control_Create");
  destroy=Proc<MainDestroyDelegate>("ADL_Main_Control_Destroy");
  AdapterCountDelegate countFn=Proc<AdapterCountDelegate>("ADL_Adapter_NumberOfAdapters_Get");
  ColorGetDelegate get=Proc<ColorGetDelegate>("ADL_Display_Color_Get");
  set=Proc<ColorSetDelegate>("ADL_Display_Color_Set");
  if(create==null||destroy==null||countFn==null||get==null||set==null){Shutdown();return false;}
  AllocDelegate callback=new AllocDelegate(Alloc);
  if(create(callback,1)!=ADL_OK){Shutdown();return false;}
  int count=0;if(countFn(ref count)!=ADL_OK||count<=0){Shutdown();return false;}
  for(int a=0;a<count;a++)for(int d=0;d<16;d++){
   int cur=0,def=0,mn=0,mx=0,step=0;
   if(get(a,d,ADL_DISPLAY_COLOR_SATURATION,ref cur,ref def,ref mn,ref mx,ref step)==ADL_OK&&mx>mn){
    adapter=a;display=d;original=cur;defaultValue=def;minValue=mn;maxValue=mx;initialized=true;return true;
   }
  }
  Shutdown();return false;
 }
 public static bool SetPercent(int percent){
  if(!initialized||set==null)return false;
  percent=Math.Max(0,Math.Min(200,percent));
  int value;
  if(percent==100)value=defaultValue;
  else if(percent<100)value=minValue+(int)Math.Round((defaultValue-minValue)*(percent/100.0));
  else value=defaultValue+(int)Math.Round((maxValue-defaultValue)*((percent-100)/100.0));
  value=Math.Max(minValue,Math.Min(maxValue,value));
  return set(adapter,display,ADL_DISPLAY_COLOR_SATURATION,value)==ADL_OK;
 }
 public static void Restore(){if(initialized&&set!=null)set(adapter,display,ADL_DISPLAY_COLOR_SATURATION,original);}
 public static void Shutdown(){
  try{if(destroy!=null)destroy();}catch{}
  destroy=null;set=null;initialized=false;adapter=-1;display=-1;
  if(module!=IntPtr.Zero){FreeLibrary(module);module=IntPtr.Zero;}
 }
}

internal sealed class HotkeyWindow:Form {
 const int WM_HOTKEY=0x312;
 NotifyIcon tray;
 Keys[] saveKeys=new Keys[]{Keys.D1,Keys.D2,Keys.D3,Keys.D4,Keys.D5,Keys.D6,Keys.D7,Keys.D8,Keys.D9};
 uint[] saveMods=new uint[]{3,3,3,3,3,3,3,3,3};
 int[][] profiles=new int[9][];
 int brightness=100,contrast=100,gamma=100,saturation=100,red=100,green=100,blue=100;
 bool magnificationReady=false; bool amdSaturationReady=false;
 string ConfigFile { get { return Path.Combine(Application.UserAppDataPath,"settings.ini"); } }
 public HotkeyWindow(){
  LoadConfig();LoadProfiles();
  ShowInTaskbar=false;WindowState=FormWindowState.Minimized;FormBorderStyle=FormBorderStyle.FixedToolWindow;Opacity=0;
  Load+=Loaded;FormClosing+=Closing;
 }
 void Loaded(object s,EventArgs e){
  RegisterSaveHotkeys();
  try{magnificationReady=Native.MagInitialize();if(magnificationReady)Native.MagSetFullscreenTransform(1.0f,0,0);}catch{magnificationReady=false;}
  amdSaturationReady=AmdAdl.Initialize();
  tray=new NotifyIcon();tray.Icon=SystemIcons.Application;tray.Text="Gamma Hotkeys";ContextMenu m=new ContextMenu();
  m.MenuItems.Add("الإعدادات: الألوان والسطوع",delegate{ShowSettings();});
  m.MenuItems.Add("تطبيق الإعدادات الحالية",delegate{ApplyCurrent();});
  for(int p=0;p<9;p++){int slot=p+1;m.MenuItems.Add("استعادة الوضع "+slot,delegate{LoadProfile(slot-1);});}
  m.MenuItems.Add("إعادة محايد",delegate{SetNeutral();});
  m.MenuItems.Add("خروج",delegate{Close();});tray.ContextMenu=m;tray.Visible=true;
  tray.ShowBalloonTip(2200,"Gamma Hotkeys","اضغط بزر الفأرة الأيمن على الأيقونة لفتح الإعدادات.",ToolTipIcon.Info);
 }
 void RegisterSaveHotkeys(){for(int i=0;i<9;i++){Native.UnregisterHotKey(Handle,i+1);Native.RegisterHotKey(Handle,i+1,saveMods[i],(uint)saveKeys[i]);}}
 protected override void WndProc(ref Message msg){if(msg.Msg==WM_HOTKEY){int id=msg.WParam.ToInt32();if(id>=1&&id<=9)SaveProfile(id-1);}base.WndProc(ref msg);}
 int[] CurrentValues(){return new int[]{brightness,contrast,gamma,saturation,red,green,blue};}
 void SaveProfile(int slot){profiles[slot]=CurrentValues();try{string path=Path.Combine(Application.UserAppDataPath,"profiles.ini");string[] lines=new string[9];for(int i=0;i<9;i++)lines[i]=profiles[i]==null?"":String.Join(",",Array.ConvertAll(profiles[i],delegate(int x){return x.ToString();}));File.WriteAllLines(path,lines);if(tray!=null)tray.ShowBalloonTip(1300,"تم حفظ الوضع "+(slot+1),"تم حفظ إعدادات الشاشة الحالية.",ToolTipIcon.Info);}catch(Exception ex){MessageBox.Show("تعذر حفظ الوضع: "+ex.Message);}}
 void LoadProfile(int slot){if(profiles[slot]==null){MessageBox.Show("لا يوجد وضع محفوظ في الخانة "+(slot+1));return;}int[] v=profiles[slot];brightness=v[0];contrast=v[1];gamma=v[2];saturation=v[3];red=v[4];green=v[5];blue=v[6];ApplyCurrent();}
 void LoadProfiles(){try{string p=Path.Combine(Application.UserAppDataPath,"profiles.ini");if(!File.Exists(p))return;string[] lines=File.ReadAllLines(p);for(int i=0;i<Math.Min(9,lines.Length);i++){if(String.IsNullOrWhiteSpace(lines[i]))continue;string[] x=lines[i].Split(',');if(x.Length!=7)continue;profiles[i]=Array.ConvertAll(x,delegate(string t){return int.Parse(t);});}}catch{}}
 void SetNeutral(){brightness=100;contrast=100;gamma=100;saturation=100;red=100;green=100;blue=100;ApplyCurrent();}
 Native.GammaRamp MakeRamp(){
  Native.GammaRamp r=new Native.GammaRamp();r.Red=new ushort[256];r.Green=new ushort[256];r.Blue=new ushort[256];
  // Use per-channel gamma ramps as a fallback when the Magnification color matrix is unavailable.
  int rampRed=magnificationReady?100:red;
  int rampGreen=magnificationReady?100:green;
  int rampBlue=magnificationReady?100:blue;
  for(int i=0;i<256;i++){
   double x=i/255.0;
   r.Red[i]=Value(x,rampRed);r.Green[i]=Value(x,rampGreen);r.Blue[i]=Value(x,rampBlue);
  }return r;
 }
 ushort Value(double x,int channel){
  double y=Math.Pow(x,100.0/Math.Max(20,gamma));
  y=((y-.5)*(contrast/100.0)+.5)*(brightness/100.0)*(channel/100.0);
  return (ushort)Math.Round(Math.Max(0,Math.Min(1,y))*65535.0);
 }
 void ApplyCurrent(){
  Native.GammaRamp r=MakeRamp();IntPtr dc=Native.GetDC(IntPtr.Zero);bool ok=false;
  bool saturationOk=amdSaturationReady&&AmdAdl.SetPercent(saturation);
  try{ok=Native.SetDeviceGammaRamp(dc,ref r);}finally{Native.ReleaseDC(IntPtr.Zero,dc);}
  if(tray!=null)tray.ShowBalloonTip(1200,"Gamma Hotkeys",
   (ok?"تم تطبيق السطوع والتباين وGamma":"تعذر تطبيق Gamma")+
   (saturationOk?"، وتم تطبيق تشبع الألوان عبر AMD ADL":(amdSaturationReady?"، فشل تعريف AMD في تطبيق التشبع":"، لم يتم العثور على دعم AMD ADL للتشبع")),
   ok&&saturationOk?ToolTipIcon.Info:ToolTipIcon.Warning);
 }
 void ShowSettings(){using(SettingsForm f=new SettingsForm(this))f.ShowDialog();}
 void SaveConfig(){
  try{Directory.CreateDirectory(Application.UserAppDataPath);using(StreamWriter w=new StreamWriter(ConfigFile,false)){
   w.WriteLine(brightness);w.WriteLine(contrast);w.WriteLine(gamma);w.WriteLine(saturation);w.WriteLine(red);w.WriteLine(green);w.WriteLine(blue);
   for(int i=0;i<9;i++)w.WriteLine(((int)saveKeys[i])+","+saveMods[i]);
  }}catch(Exception ex){MessageBox.Show("تعذر حفظ الإعدادات: "+ex.Message);}
 }
 void LoadConfig(){
  try{if(!File.Exists(ConfigFile))return;string[] a=File.ReadAllLines(ConfigFile);if(a.Length<7)return;
   brightness=Clamp(int.Parse(a[0]),20,200);contrast=Clamp(int.Parse(a[1]),20,200);gamma=Clamp(int.Parse(a[2]),20,200);
   saturation=Clamp(int.Parse(a[3]),0,200);red=Clamp(int.Parse(a[4]),20,200);green=Clamp(int.Parse(a[5]),20,200);blue=Clamp(int.Parse(a[6]),20,200);
   for(int i=0;i<9&&a.Length>=16;i++){string[] q=a[7+i].Split(',');saveKeys[i]=(Keys)int.Parse(q[0]);saveMods[i]=uint.Parse(q[1]);}
  }catch{}
 }
 static int Clamp(int v,int min,int max){return Math.Max(min,Math.Min(max,v));}
 void Closing(object s,FormClosingEventArgs e){
  for(int i=1;i<=9;i++)Native.UnregisterHotKey(Handle,i);
  if(tray!=null){tray.Visible=false;tray.Dispose();}
  AmdAdl.Restore();AmdAdl.Shutdown();SetNeutral();
  if(magnificationReady){try{Native.MagUninitialize();}catch{}}
 }
 sealed class SettingsForm:Form {
  HotkeyWindow owner;TrackBar[] bars=new TrackBar[7];Label[] values=new Label[7];TextBox[] hotkeyBoxes=new TextBox[9];Keys[] editedKeys=new Keys[9];uint[] editedMods=new uint[9];
  string[] labels={"Brightness / السطوع","Contrast / التباين","Gamma","Color Saturation / تشبع الألوان","Red / الأحمر","Green / الأخضر","Blue / الأزرق"};
  public SettingsForm(HotkeyWindow o){
   owner=o;for(int i=0;i<9;i++){editedKeys[i]=o.saveKeys[i];editedMods[i]=o.saveMods[i];}
   Text="إعدادات Gamma Hotkeys";StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.Sizable;MaximizeBox=false;MinimizeBox=false;ClientSize=new Size(650,720);MinimumSize=new Size(666,759);
   CheckBox fullScreen=new CheckBox();fullScreen.Text="عرض شاشة كاملة";fullScreen.AutoSize=true;fullScreen.SetBounds(15,12,180,24);Controls.Add(fullScreen);
   fullScreen.CheckedChanged+=delegate{
    if(fullScreen.Checked){WindowState=FormWindowState.Maximized;}
    else{WindowState=FormWindowState.Normal;ClientSize=new Size(650,720);StartPosition=FormStartPosition.CenterScreen;}
   };
   int y=46;
   for(int i=0;i<7;i++){
    Label l=new Label();l.Text=labels[i];l.SetBounds(15,y,125,24);Controls.Add(l);
    bars[i]=new TrackBar();bars[i].Minimum=(i==3?0:20);bars[i].Maximum=200;bars[i].TickFrequency=20;bars[i].SetBounds(140,y-9,390,40);Controls.Add(bars[i]);
    values[i]=new Label();values[i].SetBounds(545,y,65,24);Controls.Add(values[i]);int ix=i;bars[i].ValueChanged+=delegate{values[ix].Text=bars[ix].Value+"%";Preview();};y+=38;
   }
   Label hotkeyTitle=new Label();hotkeyTitle.Text="اختصارات حفظ الوضع الحالي";hotkeyTitle.SetBounds(15,330,610,24);Controls.Add(hotkeyTitle);
   for(int i=0;i<9;i++){Label hl=new Label();hl.Text="حفظ الوضع "+(i+1);hl.SetBounds(18,356+i*32,140,25);Controls.Add(hl);hotkeyBoxes[i]=new TextBox();hotkeyBoxes[i].ReadOnly=true;hotkeyBoxes[i].Tag=i;hotkeyBoxes[i].SetBounds(170,354+i*32,300,25);hotkeyBoxes[i].Text=Format(editedKeys[i],editedMods[i]);hotkeyBoxes[i].KeyDown+=CaptureSaveKey;Controls.Add(hotkeyBoxes[i]);}
   Button close=new Button();close.Text="إغلاق";close.SetBounds(170,660,95,30);close.Click+=delegate{Close();};Controls.Add(close);
   Button save=new Button();save.Text="حفظ وتطبيق";save.SetBounds(275,660,115,30);save.Click+=delegate{Save();};Controls.Add(save);
   Button reset=new Button();reset.Text="افتراضي";reset.SetBounds(400,660,95,30);reset.Click+=delegate{ResetAll();};Controls.Add(reset);
   int[] v={o.brightness,o.contrast,o.gamma,o.saturation,o.red,o.green,o.blue};for(int i=0;i<7;i++){bars[i].Value=v[i];values[i].Text=v[i]+"%";}
  }
  string Format(Keys k,uint m){string x="";if((m&2)!=0)x+="Ctrl + ";if((m&1)!=0)x+="Alt + ";if((m&4)!=0)x+="Shift + ";return x+k.ToString();}
  void CaptureSaveKey(object sender,KeyEventArgs e){int i=(int)((TextBox)sender).Tag;Keys k=e.KeyCode;if(k==Keys.ControlKey||k==Keys.Menu||k==Keys.ShiftKey)return;uint m=0;if(e.Control)m|=2;if(e.Alt)m|=1;if(e.Shift)m|=4;if(m==0){MessageBox.Show("اختر الاختصار مع Ctrl أو Alt أو Shift.");return;}editedKeys[i]=k;editedMods[i]=m;hotkeyBoxes[i].Text=Format(k,m);e.SuppressKeyPress=true;e.Handled=true;}
  void Preview(){if(bars[0]!=null){owner.brightness=bars[0].Value;owner.contrast=bars[1].Value;owner.gamma=bars[2].Value;owner.saturation=bars[3].Value;owner.red=bars[4].Value;owner.green=bars[5].Value;owner.blue=bars[6].Value;owner.ApplyCurrent();}}
  void Save(){
   for(int i=0;i<9;i++)for(int j=i+1;j<9;j++)if(editedKeys[i]==editedKeys[j]&&editedMods[i]==editedMods[j]){MessageBox.Show("يوجد اختصار مكرر.");return;}
   for(int i=0;i<9;i++){owner.saveKeys[i]=editedKeys[i];owner.saveMods[i]=editedMods[i];}
   Preview();owner.SaveConfig();owner.RegisterSaveHotkeys();MessageBox.Show("تم حفظ الإعدادات والاختصارات.");Close();
  }
  void ResetAll(){
   int[] v={100,100,100,100,100,100,100};for(int i=0;i<7;i++)bars[i].Value=v[i];
   Preview();
  }
 }
}
static class Program{[STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new HotkeyWindow());}}
