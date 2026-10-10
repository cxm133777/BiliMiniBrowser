using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Forms=System.Windows.Forms;
using System.Reflection;
[assembly: AssemblyTitle("哔哩小窗")]
[assembly: AssemblyProduct("哔哩小窗 / Bili Mini Browser")]
[assembly: AssemblyCompany("陈小铭")]
[assembly: AssemblyCopyright("Copyright © 2026 陈小铭")]
[assembly: AssemblyVersion("1.0.0.0")]
public sealed class Preferences {
 public double Width=940,Height=720,Left=double.NaN,Top=double.NaN,Zoom=.85;
 public bool Pin=true,PauseOnHide=true,Muted=false,PauseInactive=true;

}
static class Native {
 [DllImport("user32.dll")]internal static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
 [DllImport("user32.dll")]internal static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("dwmapi.dll")]internal static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
}
sealed class BrowserTab { public WebView2 View; public bool Ready,Closed,Resume; public string Title="B 站",Url; public readonly SemaphoreSlim ActivityGate=new SemaphoreSlim(1,1); }
public sealed class MiniBrowser {
 readonly List<BrowserTab> tabs=new List<BrowserTab>(); BrowserTab activeTab; CoreWebView2Environment environment;
 const string Home="https://www.bilibili.com/";
 const string UserSpace="https://space.bilibili.com/";
 readonly string root=AppDomain.CurrentDomain.BaseDirectory;
 readonly string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BiliMiniBrowser");
 readonly JavaScriptSerializer json=new JavaScriptSerializer();readonly bool testMode;
 Preferences prefs=new Preferences();Window window;WebView2 web;TextBox address;TextBlock status,pageTitle;Forms.NotifyIcon tray;IntPtr handle;
 bool quitting,ready,cinema,initializing,hotkeyRegistered,nativeFullscreen,hideRequested,minimizeRequested;DispatcherTimer saveTimer;
 T Find<T>(string name)where T:class{return window.FindName(name)as T;}
 void On(string name,Action run){Find<Button>(name).Click+=(s,e)=>run();}
 public MiniBrowser(bool selfTest){
  testMode=selfTest;Directory.CreateDirectory(data);if(!testMode)try{prefs=json.Deserialize<Preferences>(File.ReadAllText(Path.Combine(data,"preferences.json"),Encoding.UTF8))??new Preferences();}catch{}
  window=(Window)XamlReader.Parse(File.ReadAllText(Path.Combine(root,"Browser.xaml"),Encoding.UTF8));window.Topmost=prefs.Pin;window.Icon=BitmapFrame.Create(new Uri(Path.Combine(root,"mini.ico")));
  WindowChrome.SetWindowChrome(window,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false,CornerRadius=new CornerRadius(18)});
  address=Find<TextBox>("Address");status=Find<TextBlock>("Status");pageTitle=Find<TextBlock>("PageTitle");
  web=new WebView2{DefaultBackgroundColor=System.Drawing.Color.FromArgb(255,22,23,28)};Find<Border>("WebArea").Child=web;activeTab=new BrowserTab{View=web};tabs.Add(activeTab);
  foreach(string name in new[]{"Back","Forward","Refresh","Home","Mute","Pin","Monitor","Cinema","Menu"})Icon(name,name.ToLowerInvariant());
  On("Close",Exit);On("Minimize",Minimize);On("Maximize",()=>window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized);
  On("Back",()=>{if(ready&&web.CanGoBack)web.GoBack();});On("Forward",()=>{if(ready&&web.CanGoForward)web.GoForward();});On("Refresh",()=>{if(ready)web.Reload();else Initialize();});On("Home",()=>Navigate(Home));On("Go",CopyLink);On("NewTab",()=>NewTab(Home));RenderTabs();
  On("Mute",()=>{prefs.Muted=!prefs.Muted;foreach(var tab in tabs)if(tab.Ready)tab.View.CoreWebView2.IsMuted=prefs.Muted;UpdateControls();Save();});
  On("Pin",()=>{prefs.Pin=!prefs.Pin;window.Topmost=prefs.Pin;UpdateControls();Save();});On("Monitor",NextMonitor);On("Cinema",ToggleCinema);On("Menu",OpenMenu);
  address.KeyDown+=(s,e)=>{if(e.Key==Key.Enter)e.Handled=true;};
  Find<Grid>("TitleBar").MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2)window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else try{window.DragMove();Save();}catch{}};
  window.PreviewKeyDown+=(s,e)=>{if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.T){NewTab(Home);e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.W){CloseTab(activeTab);e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.L){FocusAddress();e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.R){if(ready)web.Reload();e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Alt&&e.SystemKey==Key.Left){if(ready&&web.CanGoBack)web.GoBack();e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Alt&&e.SystemKey==Key.Right){if(ready&&web.CanGoForward)web.GoForward();e.Handled=true;}else if(e.Key==Key.Escape&&cinema){ToggleCinema();e.Handled=true;}};
  window.SourceInitialized+=(s,e)=>{handle=new WindowInteropHelper(window).Handle;HwndSource.FromHwnd(handle).AddHook(Hook);try{int corner=2,dark=1;Native.DwmSetWindowAttribute(handle,33,ref corner,4);Native.DwmSetWindowAttribute(handle,20,ref dark,4);}catch{}if(!testMode)hotkeyRegistered=Native.RegisterHotKey(handle,1,3|0x4000,0x42);RestoreBounds();};
  window.Closing+=(s,e)=>{if(!quitting){e.Cancel=true;window.Dispatcher.BeginInvoke(new Action(Exit));}};
  window.StateChanged+=(s,e)=>RefreshActivity();
  window.SizeChanged+=(s,e)=>{if(saveTimer!=null){saveTimer.Stop();saveTimer.Start();}};
  saveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(600)};saveTimer.Tick+=(s,e)=>{saveTimer.Stop();Save();};
  tray=new Forms.NotifyIcon{Visible=!testMode,Text="哔哩小窗 · Ctrl+Alt+B 显示 / 隐藏",Icon=new System.Drawing.Icon(Path.Combine(root,"mini.ico"),32,32)};
  var menu=new Forms.ContextMenuStrip();menu.Items.Add("显示小窗",null,(s,e)=>window.Dispatcher.Invoke(new Action(Show)));menu.Items.Add("显示 / 隐藏 · Ctrl+Alt+B",null,(s,e)=>window.Dispatcher.Invoke(new Action(()=>{if(window.IsVisible&&!hideRequested)Hide();else Show();})));menu.Items.Add("移动到下一块屏幕",null,(s,e)=>window.Dispatcher.Invoke(new Action(NextMonitor)));menu.Items.Add("退出",null,(s,e)=>window.Dispatcher.Invoke(new Action(Exit)));tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>window.Dispatcher.Invoke(new Action(Show));
  Find<ScrollViewer>("TabScroll").PreviewMouseWheel+=(s,e)=>{var scroll=Find<ScrollViewer>("TabScroll");scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset-e.Delta);e.Handled=true;};
  UpdateControls();window.Loaded+=(s,e)=>Initialize();window.Show();
 }
 void RestoreBounds(){
  double width=Finite(prefs.Width)?Math.Max(480,Math.Min(1800,prefs.Width)):940,height=Finite(prefs.Height)?Math.Max(320,Math.Min(1200,prefs.Height)):720;window.Width=width;window.Height=height;
  if(Finite(prefs.Left)&&Finite(prefs.Top)){window.Left=prefs.Left;window.Top=prefs.Top;var transform=PresentationSource.FromVisual(window).CompositionTarget.TransformToDevice;var p=transform.Transform(new Point(window.Left+100,window.Top+30));if(Forms.Screen.AllScreens.Any(s=>s.WorkingArea.Contains((int)p.X,(int)p.Y)))return;}
  PlaceOn(Forms.Screen.AllScreens.Length>1?Forms.Screen.AllScreens[1]:Forms.Screen.PrimaryScreen);
 }
 static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
 void PlaceOn(Forms.Screen screen){if(window.WindowState!=WindowState.Normal)window.WindowState=WindowState.Normal;var transform=PresentationSource.FromVisual(window).CompositionTarget.TransformFromDevice;var area=screen.WorkingArea;var top=transform.Transform(new Point(area.Left,area.Top));var edge=transform.Transform(new Point(area.Right,area.Bottom));window.Width=Math.Min(window.Width,edge.X-top.X-24);window.Height=Math.Min(window.Height,edge.Y-top.Y-24);window.Left=top.X+(edge.X-top.X-window.Width)/2;window.Top=top.Y+(edge.Y-top.Y-window.Height)/2;Save();}
 void NextMonitor(){Show();var screens=Forms.Screen.AllScreens;int current=Array.FindIndex(screens,s=>s.DeviceName==Forms.Screen.FromHandle(handle).DeviceName);PlaceOn(screens[(current+1)%screens.Length]);status.Text=screens.Length==1?"当前只有一块显示器，已居中小窗":"已移动到显示器 "+((current+1)%screens.Length+1);}
 void Save(){if(testMode)return;try{if(window.WindowState==WindowState.Normal){prefs.Left=window.Left;prefs.Top=window.Top;prefs.Width=window.Width;prefs.Height=window.Height;}File.WriteAllText(Path.Combine(data,"preferences.json"),json.Serialize(prefs),Encoding.UTF8);}catch{}}
 public static bool IsWebUrl(string value){Uri uri;return Uri.TryCreate(value,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http")&&!String.IsNullOrEmpty(uri.Host)&&String.IsNullOrEmpty(uri.UserInfo);}
 public static bool IsBiliUrl(string value){Uri uri;if(!IsWebUrl(value)||!Uri.TryCreate(value,UriKind.Absolute,out uri))return false;string host=uri.DnsSafeHost.TrimEnd('.');return host.Equals("bilibili.com",StringComparison.OrdinalIgnoreCase)||host.EndsWith(".bilibili.com",StringComparison.OrdinalIgnoreCase);}
 void CopyLink(){if(ready&&IsBiliUrl(web.CoreWebView2.Source))try{Clipboard.SetText(web.CoreWebView2.Source);status.Text="已复制当前 B 站链接";}catch{status.Text="复制失败，请选中地址后按 Ctrl+C";}}
 void Sync(){if(activeTab==null)return;web=activeTab.View;ready=activeTab.Ready;pageTitle.Text=activeTab.Title;address.Text=ready&&IsBiliUrl(web.CoreWebView2.Source)?web.CoreWebView2.Source:"https://www.bilibili.com/";address.ToolTip=address.Text+"\n只读 · 可选中复制";address.ScrollToHorizontalOffset(0);UpdateControls();RenderTabs();}
 void RenderTabs(){var strip=Find<StackPanel>("Tabs");strip.Children.Clear();foreach(var value in tabs){var tab=value;var row=new StackPanel{Orientation=Orientation.Horizontal};var label=new TextBlock{Text=tab.Title,Width=112,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};var select=new Button{Content=label,Height=29,Padding=new Thickness(10,0,4,0),Background=tab==activeTab?new SolidColorBrush(Color.FromRgb(58,62,80)):Brushes.Transparent,ToolTip=tab.Title};select.Click+=(s,e)=>SelectTab(tab);var close=new Button{Content="×",Width=26,MinWidth=26,Height=29,Padding=new Thickness(0),Margin=new Thickness(0),ToolTip="关闭标签页"};close.Click+=(s,e)=>CloseTab(tab);row.Children.Add(select);row.Children.Add(close);strip.Children.Add(new Border{Child=row,CornerRadius=new CornerRadius(9),Margin=new Thickness(0,0,5,0),Background=tab==activeTab?new SolidColorBrush(Color.FromRgb(58,62,80)):new SolidColorBrush(Color.FromRgb(36,38,46))});}}
 void SelectTab(BrowserTab tab){if(tab.Closed||tab==activeTab)return;ExitCinema();activeTab=tab;tab.View.Visibility=Visibility.Visible;nativeFullscreen=false;cinema=false;Find<Grid>("Navigation").Visibility=Visibility.Visible;Sync();Save();RefreshActivity();window.Dispatcher.BeginInvoke(new Action(()=>{var scroll=Find<ScrollViewer>("TabScroll");scroll.ScrollToHorizontalOffset(Math.Max(0,tabs.IndexOf(activeTab)*169-scroll.ActualWidth+169));}),DispatcherPriority.Loaded);}
 BrowserTab CreateTab(){var tab=new BrowserTab{View=new WebView2{DefaultBackgroundColor=System.Drawing.Color.FromArgb(255,22,23,28),Visibility=Visibility.Collapsed}};tabs.Add(tab);var host=Find<Border>("WebArea");var grid=host.Child as Grid;if(grid==null){host.Child=null;grid=new Grid();grid.Children.Add(web);host.Child=grid;}grid.Children.Add(tab.View);SelectTab(tab);RenderTabs();return tab;}
 async void NewTab(string url){if(!IsBiliUrl(url)){status.Text="这里只能浏览 B 站，已阻止外站链接";return;}var tab=CreateTab();try{await InitializeTab(tab,url);}catch(Exception ex){Log(ex);if(!tab.Closed)status.Text="新标签页启动失败";}}
 void CloseTab(BrowserTab tab){if(tab==null||tab.Closed)return;if(tabs.Count==1){Navigate(Home);return;}int index=tabs.IndexOf(tab);if(tab==activeTab)SelectTab(tabs[index>0?index-1:1]);tab.Closed=true;tabs.Remove(tab);var parent=tab.View.Parent as Panel;if(parent!=null)parent.Children.Remove(tab.View);tab.View.Dispose();RenderTabs();}
 void Navigate(string value){if(!IsBiliUrl(value)){status.Text="这里只能浏览 B 站，已阻止外站链接";return;}if(!ready){status.Text="浏览器仍在启动，请稍候";return;}ExitCinema();web.CoreWebView2.Navigate(value);web.Focus();}
 async void Initialize(){if(initializing||ready||quitting)return;initializing=true;status.Text="正在启动 B 站…";try{await InitializeTab(activeTab,Home);if(testMode)await SelfTest();}catch(Exception ex){Log(ex);status.Text="浏览器启动失败："+ex.Message;if(testMode){File.WriteAllText(Path.Combine(root,"self-test.json"),json.Serialize(new{passed=false,error=ex.ToString()}),Encoding.UTF8);Exit();}}finally{initializing=false;}}
 async Task InitializeTab(BrowserTab tab,string url){
  if(environment==null)environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(data,testMode?"TestProfile":"WebProfile"),null);
  if(tab.Closed||quitting)return;var view=tab.View;await view.EnsureCoreWebView2Async(environment);if(tab.Closed||quitting)return;tab.Ready=true;var core=view.CoreWebView2;view.ZoomFactor=Math.Max(.5,Math.Min(1.5,prefs.Zoom));core.IsMuted=prefs.Muted;core.Profile.PreferredColorScheme=CoreWebView2PreferredColorScheme.Dark;core.Settings.IsStatusBarEnabled=false;
  core.NavigationStarting+=(s,e)=>{if(!IsBiliUrl(e.Uri)&&!(testMode&&e.Uri.StartsWith("data:text/html"))){e.Cancel=true;if(tab==activeTab)status.Text="这里只能浏览 B 站，已阻止外站链接";return;}if(tab==activeTab)status.Text="正在加载…";};
  core.SourceChanged+=(s,e)=>{tab.Url=core.Source;if(tab==activeTab){Sync();Save();}};
  core.HistoryChanged+=(s,e)=>{if(tab==activeTab)UpdateControls();};
  core.DocumentTitleChanged+=(s,e)=>{tab.Title=String.IsNullOrEmpty(core.DocumentTitle)?"B 站":core.DocumentTitle;if(tab==activeTab)pageTitle.Text=tab.Title;RenderTabs();};
  core.NavigationCompleted+=async(s,e)=>{if(tab.Closed||quitting)return;if(tab==activeTab){Sync();status.Text=e.IsSuccess?"Ctrl+Alt+B 显示 / 隐藏 · Ctrl+T 新标签页 · 仅浏览 B 站":"页面加载失败 · 点击刷新重试";}if(e.IsSuccess)try{await core.ExecuteScriptAsync(File.ReadAllText(Path.Combine(root,"PageStyle.js"),Encoding.UTF8));}catch{}if(!testMode&&!tab.Closed)RefreshActivity();};
  core.NewWindowRequested+=async(s,e)=>{e.Handled=true;if(!e.IsUserInitiated)return;if(!IsBiliUrl(e.Uri)){if(tab==activeTab)status.Text="已阻止外站新窗口";return;}var deferral=e.GetDeferral();try{var target=CreateTab();await InitializeTab(target,null);if(!target.Closed)e.NewWindow=target.View.CoreWebView2;}catch(Exception ex){Log(ex);}finally{deferral.Complete();}};
  core.LaunchingExternalUriScheme+=(s,e)=>{e.Cancel=true;if(tab==activeTab)status.Text="请使用 B 站网页播放";};
  core.ProcessFailed+=(s,e)=>{if(tab==activeTab)status.Text="网页进程异常 · 点击刷新重试";};
  core.ContainsFullScreenElementChanged+=(s,e)=>{if(tab!=activeTab)return;nativeFullscreen=core.ContainsFullScreenElement;Find<Grid>("Navigation").Visibility=nativeFullscreen||cinema?Visibility.Collapsed:Visibility.Visible;};
  core.WebMessageReceived+=(s,e)=>{if(tab!=activeTab)return;string command;try{command=e.TryGetWebMessageAsString();}catch{return;}if(command=="mini:address")FocusAddress();else if(command=="mini:new")NewTab(Home);else if(command=="mini:close")CloseTab(tab);else if(command=="mini:reload")view.Reload();else if(command=="mini:escape"&&cinema)ExitCinema();};
  await core.AddScriptToExecuteOnDocumentCreatedAsync("document.addEventListener('keydown',e=>{if(!e.isTrusted)return;let c='';if(e.ctrlKey&&!e.altKey){const k=e.key.toLowerCase();if(k==='l')c='address';if(k==='t')c='new';if(k==='w')c='close';if(k==='r')c='reload';}if(e.key==='Escape')c='escape';if(c){if(c!=='escape')e.preventDefault();window.chrome.webview.postMessage('mini:'+c);}},true);");
  if(tab==activeTab)Sync();if(url!=null&&!testMode)core.Navigate(url);else if(!testMode)RefreshActivity();
 }
 async void ToggleCinema(){if(!ready)return;if(cinema){ExitCinema();return;}try{string result=await web.CoreWebView2.ExecuteScriptAsync(File.ReadAllText(Path.Combine(root,"PageStyle.js"),Encoding.UTF8));if(result!="true"){status.Text="先打开一个 B 站视频，再开启观影模式";return;}cinema=true;await ApplyCinema();Find<Grid>("Navigation").Visibility=Visibility.Collapsed;UpdateControls();status.Text="观影模式 · 再点观影按钮或按 Esc 恢复";}catch(Exception ex){status.Text="暂时不能开启观影模式："+ex.Message;}}
 async Task ApplyCinema(){await web.CoreWebView2.ExecuteScriptAsync("document.documentElement.classList.add('mini-cinema')");}
 async void ExitCinema(){cinema=false;Find<Grid>("Navigation").Visibility=nativeFullscreen?Visibility.Collapsed:Visibility.Visible;UpdateControls();if(ready)try{await web.CoreWebView2.ExecuteScriptAsync("document.documentElement.classList.remove('mini-cinema')");}catch{}}
 void FocusAddress(){if(cinema)ExitCinema();Find<Grid>("Navigation").Visibility=Visibility.Visible;address.Focus();address.SelectAll();}
 async void Hide(){Save();hideRequested=true;await UpdateAllActivity();if(hideRequested&&!quitting)window.Hide();}
 async void Minimize(){minimizeRequested=true;await UpdateAllActivity();if(minimizeRequested&&!quitting)window.WindowState=WindowState.Minimized;minimizeRequested=false;}
 void Show(){hideRequested=false;minimizeRequested=false;window.Show();if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;window.Activate();RefreshActivity();}
 bool ShouldSleep(BrowserTab tab){return (prefs.PauseInactive&&tab!=activeTab)||(prefs.PauseOnHide&&(hideRequested||minimizeRequested||!window.IsVisible||window.WindowState==WindowState.Minimized));}
 Task UpdateAllActivity(){return Task.WhenAll(tabs.ToArray().Where(t=>t.Ready&&!t.Closed).Select(UpdateActivity));}
 async void RefreshActivity(){try{await Task.WhenAll(tabs.ToArray().Where(t=>t.Ready&&!t.Closed).Select(UpdateActivity));}catch(Exception ex){if(!quitting)Log(ex);}}
 async Task UpdateActivity(BrowserTab tab){
  await tab.ActivityGate.WaitAsync();
  try{
   if(quitting||tab.Closed||!tab.Ready)return;var core=tab.View.CoreWebView2;
   if(ShouldSleep(tab)){
    if(!core.IsSuspended){
     // Pause before hiding the native browser, then suspend its timers and animations.
     bool playing=await core.ExecuteScriptAsync("(()=>{let playing=false;document.querySelectorAll('video,audio').forEach(v=>{if(!v.paused&&!v.ended){v.dataset.miniResume='1';playing=true;v.pause();}});return playing;})()")=="true";
     tab.Resume|=playing;
     if(quitting||tab.Closed)return;
     if(ShouldSleep(tab)){
      tab.View.Visibility=Visibility.Collapsed;
      await window.Dispatcher.InvokeAsync(new Action(()=>{}),DispatcherPriority.Background);
      if(quitting||tab.Closed)return;
      if(ShouldSleep(tab))await core.TrySuspendAsync();
     }
    }
   }
   if(quitting||tab.Closed)return;
   // Recheck after each async operation: a fast tab switch may have reversed the request.
   if(!ShouldSleep(tab)){
    core.Resume();tab.View.Visibility=tab==activeTab?Visibility.Visible:Visibility.Collapsed;
    if(tab.Resume){tab.Resume=false;await core.ExecuteScriptAsync("document.querySelectorAll('video[data-mini-resume],audio[data-mini-resume]').forEach(v=>{delete v.dataset.miniResume;v.play().catch(()=>{});})");}
   }
  }catch(Exception ex){if(!quitting&&!tab.Closed)Log(ex);}finally{tab.ActivityGate.Release();}
 }
 void UpdateControls(){Find<Button>("Back").IsEnabled=ready&&web.CanGoBack;Find<Button>("Forward").IsEnabled=ready&&web.CanGoForward;foreach(string name in new[]{"Pin","Mute","Cinema"}){bool selected=name=="Pin"?prefs.Pin:name=="Mute"?prefs.Muted:cinema;Find<Button>(name).Background=selected?new SolidColorBrush(Color.FromRgb(62,67,91)):Brushes.Transparent;}Find<Button>("Pin").ToolTip=prefs.Pin?"已置顶 · 点击取消":"置顶";Find<Button>("Mute").ToolTip=prefs.Muted?"已静音 · 点击恢复":"静音（仅此小窗）";}
 void OpenMenu(){var menu=new ContextMenu{Resources=window.Resources};Add(menu,"首页",()=>Navigate(Home));Add(menu,"动态",()=>Navigate("https://t.bilibili.com/"));Add(menu,"我的空间 / 收藏",()=>Navigate(UserSpace));menu.Items.Add(new Separator());Add(menu,"迷你尺寸 · 640 × 440",()=>{window.WindowState=WindowState.Normal;window.Width=640;window.Height=440;Save();});Add(menu,"舒适尺寸 · 940 × 720",()=>{window.WindowState=WindowState.Normal;window.Width=940;window.Height=720;Save();});foreach(double value in new[]{.7,.85,1.0,1.15}){double zoom=value;Add(menu,"网页缩放 · "+Math.Round(value*100)+"%",()=>{prefs.Zoom=zoom;foreach(var tab in tabs)if(tab.Ready)tab.View.ZoomFactor=zoom;Save();});}menu.Items.Add(new Separator());var pause=Add(menu,"隐藏小窗时暂停视频",()=>{prefs.PauseOnHide=!prefs.PauseOnHide;Save();RefreshActivity();});pause.IsCheckable=true;pause.IsChecked=prefs.PauseOnHide;var inactive=Add(menu,"后台标签页自动休眠",()=>{prefs.PauseInactive=!prefs.PauseInactive;Save();RefreshActivity();});inactive.IsCheckable=true;inactive.IsChecked=prefs.PauseInactive;Add(menu,"在默认浏览器打开当前网页",()=>{if(ready&&IsWebUrl(web.Source.ToString()))Process.Start(new ProcessStartInfo(web.Source.ToString()){UseShellExecute=true});});Add(menu,"关于哔哩小窗",About);Add(menu,"退出程序",Exit);menu.PlacementTarget=Find<Button>("Menu");menu.IsOpen=true;}
 MenuItem Add(ContextMenu menu,string name,Action run){var item=new MenuItem{Header=name};item.Click+=(s,e)=>run();menu.Items.Add(item);return item;}
 void About(){var w=new Window{Owner=window,Title="关于哔哩小窗",Width=420,Height=290,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=new SolidColorBrush(Color.FromRgb(29,30,36)),Foreground=Brushes.White};var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(new TextBlock{Text="哔哩小窗  1.0.0",FontSize=23,Margin=new Thickness(0,0,0,16)});panel.Children.Add(new TextBlock{Text="陈小铭开发 · 独立第三方迷你浏览器\n\n使用 Microsoft WebView2 加载官方网站。登录与播放权限由 B 站决定，登录状态保存在本机独立浏览器资料目录。",TextWrapping=TextWrapping.Wrap,FontSize=13});var link=new Button{Content="B 站 · 陈小铭",Margin=new Thickness(0,18,0,0)};link.Click+=(s,e)=>Process.Start(new ProcessStartInfo("https://space.bilibili.com/2121431899"){UseShellExecute=true});panel.Children.Add(link);var close=new Button{Content="关闭",Margin=new Thickness(0,10,0,0),Style=(Style)window.FindResource(typeof(Button))};close.Click+=(s,e)=>w.Close();panel.Children.Add(close);w.Resources=window.Resources;w.Content=panel;w.Height=355;WindowChrome.SetWindowChrome(w,new WindowChrome{CaptionHeight=0,GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(18),ResizeBorderThickness=new Thickness(0)});w.SourceInitialized+=(s,e)=>{int corner=2;Native.DwmSetWindowAttribute(new WindowInteropHelper(w).Handle,33,ref corner,4);};w.MouseLeftButtonDown+=(s,e)=>{try{w.DragMove();}catch{}};w.Show();}
 void Icon(string name,string symbol){var paths=new Dictionary<string,string>{{"back","M15 5 L8 12 L15 19"},{"forward","M9 5 L16 12 L9 19"},{"refresh","M19 8 C16 0 3 4 4 13 C5 21 17 23 20 15 M19 3 L19 9 L13 9"},{"home","M3 11 L12 3 L21 11 M6 9 L6 21 L18 21 L18 9 M10 21 L10 15 L14 15 L14 21"},{"pin","M8 3 L16 3 L15 10 L19 14 L5 14 L9 10 Z M12 14 L12 22"},{"mute","M3 9 L7 9 L12 5 L12 19 L7 15 L3 15 Z M17 8 C20 10 20 14 17 16"},{"monitor","M3 4 L21 4 L21 17 L3 17 Z M12 17 L12 21 M8 21 L16 21"},{"cinema","M4 3 L9 3 M4 3 L4 8 M20 3 L15 3 M20 3 L20 8 M4 21 L9 21 M4 21 L4 16 M20 21 L15 21 M20 21 L20 16"},{"menu","M5 12 L5.01 12 M12 12 L12.01 12 M19 12 L19.01 12"}};var shape=new System.Windows.Shapes.Path{Data=Geometry.Parse(paths[symbol]),Stroke=new SolidColorBrush(Color.FromRgb(187,193,210)),StrokeThickness=symbol=="menu"?3:1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};var canvas=new Canvas{Width=24,Height=24};canvas.Children.Add(shape);Find<Button>(name).Content=new Viewbox{Child=canvas,Width=17,Height=17};System.Windows.Automation.AutomationProperties.SetName(Find<Button>(name),Convert.ToString(Find<Button>(name).ToolTip));}
 IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x312&&w.ToInt32()==1){handled=true;if(window.IsVisible&&!hideRequested)Hide();else Show();}else if(msg==0x112&&(w.ToInt64()&0xFFF0)==0xF020){handled=true;Minimize();}return IntPtr.Zero;}
 void Log(Exception ex){try{File.AppendAllText(Path.Combine(data,"error.log"),DateTime.Now+" "+ex+Environment.NewLine);}catch{}}
 double CpuSeconds(int[] ids){double sum=0;foreach(int id in ids)try{using(var process=Process.GetProcessById(id))sum+=process.TotalProcessorTime.TotalSeconds;}catch{}return sum;}
 async Task<double> SampleCpu(){int[] ids=environment.GetProcessInfos().Select(p=>p.ProcessId).Concat(new[]{Process.GetCurrentProcess().Id}).Distinct().ToArray();double before=CpuSeconds(ids);var clock=Stopwatch.StartNew();await Task.Delay(1500);return Math.Round(Math.Max(0,CpuSeconds(ids)-before)/clock.Elapsed.TotalSeconds/Environment.ProcessorCount*100,3);}
 string SilentWave(){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){int count=8000*30;writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(count*2);writer.Write(new byte[count*2]);return "data:audio/wav;base64,"+Convert.ToBase64String(stream.ToArray());}}
 async Task TestLifecycle(Action<bool,string> check){
  var original=activeTab;
  await web.ExecuteScriptAsync("(()=>{const v=document.querySelector('video');v.muted=true;v.src="+json.Serialize(SilentWave())+";return v.play().then(()=>true);})()");
  check(await web.ExecuteScriptAsync("!document.querySelector('video').paused")=="true","Media starts before sleep");
  await web.ExecuteScriptAsync("window.miniTicks=0;window.miniTimer=setInterval(()=>{const end=performance.now()+4;while(performance.now()<end){};miniTicks++;},16)");
  double activeCpu=await SampleCpu();
  var other=CreateTab();await InitializeTab(other,null);await UpdateActivity(original);
  check(original.View.CoreWebView2.IsSuspended,"Inactive tab suspended");check(original.Resume,"Playing state remembered");
  double sleepingCpu=await SampleCpu();
  SelectTab(original);await UpdateActivity(original);check(!original.View.CoreWebView2.IsSuspended,"Selected tab resumed");
  check(await web.ExecuteScriptAsync("!document.querySelector('video').paused")=="true","Playing media resumes after tab switch");
  await web.ExecuteScriptAsync("document.querySelector('video').pause()");
  SelectTab(other);await UpdateActivity(original);SelectTab(original);await UpdateActivity(original);
  check(await web.ExecuteScriptAsync("document.querySelector('video').paused")=="true","Manually paused media stays paused");
  await web.ExecuteScriptAsync("document.querySelector('video').play()");Hide();await UpdateActivity(original);check(original.View.CoreWebView2.IsSuspended,"Hidden window suspended");check(original.Resume,"Hide remembers playing state");Show();await UpdateActivity(original);check(await web.ExecuteScriptAsync("!document.querySelector('video').paused")=="true","Hidden playback resumes");
  Minimize();await UpdateActivity(original);check(original.View.CoreWebView2.IsSuspended,"Minimized window suspended");window.WindowState=WindowState.Normal;await UpdateActivity(original);check(await web.ExecuteScriptAsync("!document.querySelector('video').paused")=="true","Minimized playback resumes");
  for(int i=0;i<4;i++){SelectTab(other);SelectTab(original);}await UpdateActivity(original);check(!original.View.CoreWebView2.IsSuspended&&original.View.Visibility==Visibility.Visible,"Rapid switches leave selected tab awake");
  prefs.PauseInactive=false;SelectTab(other);await UpdateActivity(original);check(!original.View.CoreWebView2.IsSuspended,"Background playback option respected");prefs.PauseInactive=true;SelectTab(original);CloseTab(other);await UpdateActivity(original);
  await web.ExecuteScriptAsync("clearInterval(window.miniTimer);document.querySelector('video').pause()");
  File.WriteAllText(Path.Combine(root,"performance-test.json"),json.Serialize(new{workload="Synthetic JavaScript timer, not Bilibili playback",activeCpuPercent=activeCpu,suspendedCpuPercent=sleepingCpu,logicalProcessors=Environment.ProcessorCount}),Encoding.UTF8);
 }
 void Exit(){if(quitting)return;quitting=true;Save();if(handle!=IntPtr.Zero)Native.UnregisterHotKey(handle,1);saveTimer.Stop();tray.Visible=false;tray.Dispose();foreach(var tab in tabs){tab.Closed=true;tab.View.Dispose();}window.Close();Application.Current.Shutdown();}
 async Task SelfTest(){
  var checks=new List<string>();Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);checks.Add(name);};
  check(address.IsReadOnly,"Read-only address");check(IsBiliUrl("https://www.bilibili.com/video/BVtest")&&IsBiliUrl("https://search.bilibili.com/all"),"Bilibili domains allowed");check(!IsBiliUrl("https://baidu.com")&&!IsBiliUrl("https://bilibili.com.evil.com")&&!IsBiliUrl("https://evilbilibili.com")&&!IsBiliUrl("javascript:alert(1)")&&!IsBiliUrl("file:///C:/"),"External and deceptive URLs blocked");
  var loaded=new TaskCompletionSource<bool>();EventHandler<CoreWebView2NavigationCompletedEventArgs> handler=(s,e)=>loaded.TrySetResult(e.IsSuccess);web.CoreWebView2.NavigationCompleted+=handler;web.NavigateToString("<!doctype html><html><head><title>MiniBrowser test</title></head><body><video controls></video><p id='ok'>Ready</p></body></html>");await Task.WhenAny(loaded.Task,Task.Delay(15000));check(loaded.Task.IsCompleted&&loaded.Task.Result,"WebView navigation");web.CoreWebView2.NavigationCompleted-=handler;check(await web.ExecuteScriptAsync("document.getElementById('ok').textContent")=="\"Ready\"","HTML rendering and script execution");check(await web.ExecuteScriptAsync("!!document.querySelector('video').canPlayType('video/mp4; codecs=\"avc1.42E01E\"')")=="true","HTML5 H.264 support");web.CoreWebView2.IsMuted=true;check(web.CoreWebView2.IsMuted,"Independent mute");web.ZoomFactor=.7;check(Math.Abs(web.ZoomFactor-.7)<.001,"Web zoom");
  var original=activeTab;var second=CreateTab();await InitializeTab(second,null);check(tabs.Count==2&&activeTab==second,"Create and select second tab");SelectTab(original);check(await web.ExecuteScriptAsync("document.getElementById('ok').textContent")=="\"Ready\"","Tab page state retained");bool cancelled=false;EventHandler<CoreWebView2NavigationStartingEventArgs> blocked=(s,e)=>{if(e.Uri.StartsWith("https://baidu.com"))cancelled=e.Cancel;};web.CoreWebView2.NavigationStarting+=blocked;web.CoreWebView2.Navigate("https://baidu.com/");await Task.Delay(600);web.CoreWebView2.NavigationStarting-=blocked;check(cancelled,"External navigation cancelled by browser event");CloseTab(second);check(tabs.Count==1&&activeTab==original,"Close background tab");CopyLink();check(address.Text.StartsWith("https://www.bilibili.com"),"Address fallback");await TestLifecycle(check);File.WriteAllText(Path.Combine(root,"self-test.json"),json.Serialize(new{passed=true,checks=checks,runtime=environment.BrowserVersionString}),Encoding.UTF8);Find<Button>("Close").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
 }
}
public static class Entry {
 [STAThread]public static void Main(string[] args){bool created;using(var mutex=new Mutex(true,args.Contains("--self-test")?"Local\\BiliMiniBrowserTest":"Local\\BiliMiniBrowser",out created)){if(!created){MessageBox.Show("哔哩小窗已经在运行，请从托盘打开。","哔哩小窗");return;}var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show(e.Exception.Message,"哔哩小窗");e.Handled=true;};try{new MiniBrowser(args.Contains("--self-test"));app.Run();}catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-error.log"),ex.ToString(),Encoding.UTF8);MessageBox.Show(ex.Message,"哔哩小窗启动失败");}}}
}
