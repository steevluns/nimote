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
 [DllImport("user32.dll", EntryPoint="SetMagnificationDesktopColorEffect", SetLastError=true)] public static extern bool SetMagnificationDesktopColorEffect([In] float[] effect);
 [StructLayout(LayoutKind.Sequential)] public struct GammaRamp {
  [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public ushort[] Red;
  [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public ushort[] Green;
  [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public ushort[] Blue;
 }
}

internal sealed class HotkeyWindow:Form {
 int[][] profiles=new int[9][];
 Keys[] profileKeys=new Keys[9]; uint[] profileMods=new uint[9]; string[] profileNames=new string[9];
 const int WM_HOTKEY=0x312;
 int brightness=100,contrast=100,gamma=100,saturation=100,red=100,green=100,blue=100;
 bool magnificationReady=false;
 string ConfigFile { get { return Path.Combine(Application.UserAppDataPath,"settings.ini"); } }
 public HotkeyWindow(){
  LoadConfig();LoadProfiles();
  ShowInTaskbar=false;WindowState=FormWindowState.Minimized;FormBorderStyle=FormBorderStyle.FixedToolWindow;Opacity=0;
  Load+=Loaded;FormClosing+=Closing;
 }
 void Loaded(object s,EventArgs e){
  try{magnificationReady=Native.MagInitialize();if(magnificationReady)Native.MagSetFullscreenTransform(1.0f,0,0);}catch{magnificationReady=false;}
  tray=new NotifyIcon();tray.Icon=SystemIcons.Application;tray.Text="Gamma Hotkeys";ContextMenu m=new ContextMenu();
  m.MenuItems.Add("الإعدادات: الألوان والسطوع",delegate{ShowSettings();});
  m.MenuItems.Add("تطبيق الإعدادات الحالية",delegate{ApplyCurrent();});
  for(int p=0;p<9;p++){int slot=p;m.MenuItems.Add("استعادة: "+(String.IsNullOrEmpty(profileNames[slot])?"الوضع "+(slot+1):profileNames[slot]),delegate{LoadProfile(slot);});}
  RegisterProfileHotkeys();
  m.MenuItems.Add("إعادة محايد",delegate{SetNeutral();});
  m.MenuItems.Add("خروج",delegate{Close();});tray.ContextMenu=m;tray.Visible=true;
 }
 NotifyIcon tray;
 protected override void WndProc(ref Message msg){if(msg.Msg==WM_HOTKEY){int id=msg.WParam.ToInt32()-1;if(id>=0&&id<9)LoadProfile(id);}base.WndProc(ref msg);}
 void RegisterProfileHotkeys(){for(int i=0;i<9;i++){Native.UnregisterHotKey(Handle,i+1);if(profiles[i]!=null&&profileKeys[i]!=Keys.None)Native.RegisterHotKey(Handle,i+1,profileMods[i],(uint)profileKeys[i]);}}
 int[] CurrentValues(){return new int[]{brightness,contrast,gamma,saturation,red,green,blue};}
 void PersistProfiles(){try{Directory.CreateDirectory(Application.UserAppDataPath);string path=Path.Combine(Application.UserAppDataPath,"profiles.ini");string[] lines=new string[9];for(int i=0;i<9;i++)lines[i]=profiles[i]==null?"":((int)profileKeys[i])+","+profileMods[i]+","+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(profileNames[i]??""))+","+String.Join(",",Array.ConvertAll(profiles[i],delegate(int x){return x.ToString();}));File.WriteAllLines(path,lines);}catch(Exception ex){MessageBox.Show("تعذر حفظ الأوضاع: "+ex.Message);}}
 void LoadProfile(int slot){if(profiles[slot]==null)return;int[] v=profiles[slot];brightness=v[0];contrast=v[1];gamma=v[2];saturation=v[3];red=v[4];green=v[5];blue=v[6];ApplyCurrent();}
 void LoadProfiles(){try{string p=Path.Combine(Application.UserAppDataPath,"profiles.ini");if(!File.Exists(p))return;string[] lines=File.ReadAllLines(p);for(int i=0;i<Math.Min(9,lines.Length);i++){if(String.IsNullOrWhiteSpace(lines[i]))continue;string[] x=lines[i].Split(',');if(x.Length==7){profiles[i]=Array.ConvertAll(x,delegate(string t){return int.Parse(t);});continue;}if(x.Length==10){profileKeys[i]=(Keys)int.Parse(x[0]);profileMods[i]=uint.Parse(x[1]);profileNames[i]=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(x[2]));profiles[i]=new int[7];for(int j=0;j<7;j++)profiles[i][j]=int.Parse(x[j+3]);}}}catch{}}
 void SetNeutral(){brightness=100;contrast=100;gamma=100;saturation=100;red=100;green=100;blue=100;ApplyCurrent();}
 Native.GammaRamp MakeRamp(){
  Native.GammaRamp r=new Native.GammaRamp();r.Red=new ushort[256];r.Green=new ushort[256];r.Blue=new ushort[256];
  for(int i=0;i<256;i++){double x=i/255.0;r.Red[i]=Value(x,red);r.Green[i]=Value(x,green);r.Blue[i]=Value(x,blue);}
  return r;
 }
 ushort Value(double x,int channel){
  double y=Math.Pow(x,100.0/Math.Max(20,gamma));
  y=((y-.5)*(contrast/100.0)+.5)*(brightness/100.0)*(channel/100.0);
  return (ushort)Math.Round(Math.Max(0,Math.Min(1,y))*65535.0);
 }
 void ApplyCurrent(){
  Native.GammaRamp r=MakeRamp();IntPtr dc=Native.GetDC(IntPtr.Zero);
  try{Native.SetDeviceGammaRamp(dc,ref r);}finally{Native.ReleaseDC(IntPtr.Zero,dc);}
  ApplyColorMatrix();
 }
 float ClampF(float v,float lo,float hi){return Math.Max(lo,Math.Min(hi,v));}
 bool ApplyColorMatrix(){
  float sat=ClampF(saturation/100.0f,0.0f,2.0f);
  float c=1.0f,b=0.0f,lr=0.213f,lg=0.715f,lb=0.072f;float[] m=new float[25];
  m[0]=(lr+sat*(1-lr))*c;m[1]=(lg-sat*lr)*c;m[2]=(lb-sat*lr)*c;
  m[5]=(lr-sat*lg)*c;m[6]=(lg+sat*(1-lg))*c;m[7]=(lb-sat*lg)*c;
  m[10]=(lr-sat*lb)*c;m[11]=(lg-sat*lb)*c;m[12]=(lb+sat*(1-lb))*c;
  m[18]=1.0f;m[4]=0.5f*(1-c)+b;m[9]=0.5f*(1-c)+b;m[14]=0.5f*(1-c)+b;m[24]=1.0f;
  try{if(Environment.OSVersion.Version.Major==6&&Environment.OSVersion.Version.Minor==1)return Native.SetMagnificationDesktopColorEffect(m);if(magnificationReady)return Native.MagSetFullscreenColorEffect(m);}catch{}
  return false;
 }
 void ResetColorMatrix(){
  float[] identity=new float[]{1,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,1,0,0,0,0,0,1};
  try{if(Environment.OSVersion.Version.Major==6&&Environment.OSVersion.Version.Minor==1)Native.SetMagnificationDesktopColorEffect(identity);else if(magnificationReady)Native.MagSetFullscreenColorEffect(identity);}catch{}
 }
 void ShowSettings(){using(SettingsForm f=new SettingsForm(this))f.ShowDialog();}
 void SaveConfig(){try{Directory.CreateDirectory(Application.UserAppDataPath);using(StreamWriter w=new StreamWriter(ConfigFile,false)){w.WriteLine(brightness);w.WriteLine(contrast);w.WriteLine(gamma);w.WriteLine(saturation);w.WriteLine(red);w.WriteLine(green);w.WriteLine(blue);}}catch(Exception ex){MessageBox.Show("تعذر حفظ الإعدادات: "+ex.Message);}}
 void LoadConfig(){try{if(!File.Exists(ConfigFile))return;string[] a=File.ReadAllLines(ConfigFile);if(a.Length<7)return;brightness=Clamp(int.Parse(a[0]),20,200);contrast=Clamp(int.Parse(a[1]),20,200);gamma=Clamp(int.Parse(a[2]),20,200);saturation=Clamp(int.Parse(a[3]),0,200);red=Clamp(int.Parse(a[4]),20,200);green=Clamp(int.Parse(a[5]),20,200);blue=Clamp(int.Parse(a[6]),20,200);}catch{}}
 static int Clamp(int v,int min,int max){return Math.Max(min,Math.Min(max,v));}
 void Closing(object s,FormClosingEventArgs e){for(int i=1;i<=9;i++)Native.UnregisterHotKey(Handle,i);if(tray!=null){tray.Visible=false;tray.Dispose();}ResetColorMatrix();SetNeutral();if(magnificationReady){try{Native.MagUninitialize();}catch{}}}
 sealed class SettingsForm:Form {
  HotkeyWindow owner;TrackBar[] bars=new TrackBar[7];Label[] values=new Label[7]; TextBox hotkeyBox; TextBox profileNameBox; Keys capturedKey=Keys.None; uint capturedMods=0;
  string[] labels={"Brightness / السطوع","Contrast / التباين","Gamma","Color Saturation / تشبع الألوان","Red / الأحمر","Green / الأخضر","Blue / الأزرق"};
  public SettingsForm(HotkeyWindow o){
   owner=o;Text="إعدادات Gamma Hotkeys";StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.Sizable;MaximizeBox=false;MinimizeBox=false;ClientSize=new Size(650,430);MinimumSize=new Size(666,469);
   CheckBox fullScreen=new CheckBox();fullScreen.Text="عرض شاشة كاملة";fullScreen.AutoSize=true;fullScreen.SetBounds(15,12,180,24);Controls.Add(fullScreen);
   fullScreen.CheckedChanged+=delegate{if(fullScreen.Checked)WindowState=FormWindowState.Maximized;else{WindowState=FormWindowState.Normal;ClientSize=new Size(650,430);StartPosition=FormStartPosition.CenterScreen;}};
   int y=46;
   for(int i=0;i<7;i++){Label l=new Label();l.Text=labels[i];l.SetBounds(15,y,125,24);Controls.Add(l);bars[i]=new TrackBar();bars[i].Minimum=(i==3?0:20);bars[i].Maximum=200;bars[i].TickFrequency=20;bars[i].SetBounds(140,y-9,390,40);Controls.Add(bars[i]);values[i]=new Label();values[i].SetBounds(545,y,65,24);Controls.Add(values[i]);int ix=i;bars[i].ValueChanged+=delegate{values[ix].Text=bars[ix].Value+"%";Preview();};y+=42;}
   Label hkLabel=new Label();hkLabel.Text="Hotkeys";hkLabel.SetBounds(15,345,80,25);Controls.Add(hkLabel);hotkeyBox=new TextBox();hotkeyBox.ReadOnly=true;hotkeyBox.Text="اضغط هنا ثم أدخل الاختصار";hotkeyBox.SetBounds(100,342,220,28);hotkeyBox.Click+=delegate{hotkeyBox.Focus();hotkeyBox.Text="اضغط الاختصار الآن";};hotkeyBox.KeyDown+=CaptureHotkey;Controls.Add(hotkeyBox);
   Label nameLabel=new Label();nameLabel.Text="حفظ باسم";nameLabel.SetBounds(15,380,80,25);Controls.Add(nameLabel);profileNameBox=new TextBox();profileNameBox.SetBounds(100,377,220,28);Controls.Add(profileNameBox);
   Button saveProfile=new Button();saveProfile.Text="حفظ الوضع والاختصار";saveProfile.SetBounds(335,342,170,30);saveProfile.Click+=delegate{SaveNamedProfile();};Controls.Add(saveProfile);
   Button close=new Button();close.Text="إغلاق";close.SetBounds(170,410,95,30);close.Click+=delegate{Close();};Controls.Add(close);
   Button save=new Button();save.Text="حفظ الإعدادات";save.SetBounds(275,410,115,30);save.Click+=delegate{Save();};Controls.Add(save);
   Button reset=new Button();reset.Text="افتراضي";reset.SetBounds(400,410,95,30);reset.Click+=delegate{ResetAll();};Controls.Add(reset);
   int[] v={o.brightness,o.contrast,o.gamma,o.saturation,o.red,o.green,o.blue};for(int i=0;i<7;i++){bars[i].Value=v[i];values[i].Text=v[i]+"%";}
  }
  void CaptureHotkey(object sender,KeyEventArgs e){if(e.KeyCode==Keys.ControlKey||e.KeyCode==Keys.Menu||e.KeyCode==Keys.ShiftKey)return;uint m=0;if(e.Control)m|=2;if(e.Alt)m|=1;if(e.Shift)m|=4;if(m==0){MessageBox.Show("اختر الاختصار مع Ctrl أو Alt أو Shift.");return;}capturedKey=e.KeyCode;capturedMods=m;hotkeyBox.Text=((m&2)!=0?"Ctrl + ":"")+((m&1)!=0?"Alt + ":"")+((m&4)!=0?"Shift + ":"")+capturedKey.ToString();e.SuppressKeyPress=true;e.Handled=true;}
  void SaveNamedProfile(){if(capturedKey==Keys.None||String.IsNullOrWhiteSpace(profileNameBox.Text)){MessageBox.Show("حدد الاختصار واكتب اسم الوضع.");return;}int slot=-1;for(int i=0;i<9;i++)if(owner.profiles[i]!=null&&owner.profileKeys[i]==capturedKey&&owner.profileMods[i]==capturedMods){slot=i;break;}if(slot<0)for(int i=0;i<9;i++)if(owner.profiles[i]==null){slot=i;break;}if(slot<0){MessageBox.Show("اكتملت الأوضاع التسعة. استخدم اختصار وضع موجود لتحديثه.");return;}Preview();owner.profiles[slot]=owner.CurrentValues();owner.profileKeys[slot]=capturedKey;owner.profileMods[slot]=capturedMods;owner.profileNames[slot]=profileNameBox.Text.Trim();owner.PersistProfiles();owner.RegisterProfileHotkeys();MessageBox.Show("تم حفظ الوضع: "+owner.profileNames[slot]);}
  void Preview(){if(bars[0]!=null){owner.brightness=bars[0].Value;owner.contrast=bars[1].Value;owner.gamma=bars[2].Value;owner.saturation=bars[3].Value;owner.red=bars[4].Value;owner.green=bars[5].Value;owner.blue=bars[6].Value;owner.ApplyCurrent();}}
  void Save(){Preview();owner.SaveConfig();MessageBox.Show("تم حفظ الإعدادات.");Close();}
  void ResetAll(){int[] v={100,100,100,100,100,100,100};for(int i=0;i<7;i++)bars[i].Value=v[i];Preview();}
 }
}
static class Program{[STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new HotkeyWindow());}}
