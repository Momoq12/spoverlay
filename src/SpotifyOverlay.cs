using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

public class SpotifyOverlay {
 static string Dir=AppDomain.CurrentDomain.BaseDirectory;
 Window window; Grid root; ImageBrush cover, gif; TextBlock song,artist; Button play,previous,next; ProgressBar progress;
 FrameworkElement scene,tint,layout; Border artOnly; Forms.NotifyIcon tray;
 DispatcherTimer poll,animation; BitmapSource[] frames; int[] delays; int frame; bool busy,closing,coverMode;
 GlobalSystemMediaTransportControlsSessionManager manager;
 GlobalSystemMediaTransportControlsSession session;
 string coverKey=""; BitmapSource cachedCover;
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint point);
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct NativePoint{public int X,Y;}
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle,out NativeRect rect);
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct NativeRect{public int Left,Top,Right,Bottom;}
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] static extern int GetWindowLong(IntPtr handle,int index);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] static extern int SetWindowLong(IntPtr handle,int index,int value);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr handle,int id,uint modifiers,uint key);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr handle,int id);
 bool clickThrough,hotkeyRegistered;IntPtr nativeHandle;System.Windows.Interop.HwndSource nativeSource;MenuItem clickOption;Forms.ToolStripMenuItem trayClickOption;
 const int HotkeyId=28431;
 static Mutex mutex; DispatcherTimer hoverTimer; DateTime hoverTime=DateTime.UtcNow; double titleDistance; bool dragging; Point dragStart; double dragLeft,dragTop;
 double transparency,foregroundTransparency; Window settingsWindow;
 Grid titleViewport; StackPanel titleLoop; TextBlock titleFirst,titleSecond; TranslateTransform titleShift=new TranslateTransform(); bool titleHover; string titleText="";
 [STAThread] public static void Main(string[] args) {
  bool first; mutex=new Mutex(true,"Local\\NeonSpotifyOverlayApp",out first);
  if(!first){MessageBox.Show("Quit the running overlay from its tray icon, then open this updated version.","spoverlay");return;}
  try {new SpotifyOverlay().Run(args.Contains("--check"));}
  catch(Exception ex){File.WriteAllText(Path.Combine(Dir,"app-error.txt"),ex.ToString()); if(!args.Contains("--check"))MessageBox.Show("The overlay could not start. Details are in app-error.txt.","Spotify Overlay"); Environment.ExitCode=1;}
 }
 static BitmapSource AppIcon(){using(var icon=Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location)){var image=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;}}
 static UIElement TransportIcon(bool playing){
  string data=playing?"M 4,3 L 7,3 L 7,17 L 4,17 Z M 13,3 L 16,3 L 16,17 L 13,17 Z":"M 5,3 L 17,10 L 5,17 Z";
  return new System.Windows.Shapes.Path{Data=Geometry.Parse(data),Fill=Brushes.White,Stroke=Brushes.White,StrokeThickness=1.5,StrokeLineJoin=PenLineJoin.Round,Width=18,Height=18,Stretch=Stretch.Uniform};
 }
 T Find<T>(string name) where T:class {return window.FindName(name) as T;}
 void Run(bool check) {
  var app=new Application();
  using(var input=File.OpenRead(Path.Combine(Dir,"Overlay.xaml")))window=(Window)XamlReader.Load(input);
  window.Icon=AppIcon();
  window.SourceInitialized+=(s,e)=>{nativeHandle=new System.Windows.Interop.WindowInteropHelper(window).Handle;nativeSource=System.Windows.Interop.HwndSource.FromHwnd(nativeHandle);nativeSource.AddHook(WindowMessage);hotkeyRegistered=RegisterHotKey(nativeHandle,HotkeyId,0x4003,0xBE);};
  try{double value;if(double.TryParse(File.ReadAllText(Path.Combine(Dir,"transparency.txt")),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value))transparency=Math.Max(0,Math.Min(100,value));}catch{}
  window.Opacity=1;if(scene!=null)scene.Opacity=1-transparency/100;if(tint!=null)tint.Opacity=1-transparency/100;
  root=Find<Grid>("Root");cover=Find<ImageBrush>("Cover");gif=Find<ImageBrush>("GifBrush");song=Find<TextBlock>("Song");artist=Find<TextBlock>("Artist");
  SetupTitle();try{double value;if(double.TryParse(File.ReadAllText(Path.Combine(Dir,"foreground-transparency.txt")),out value))foregroundTransparency=Math.Max(0,Math.Min(100,value));}catch{}
  play=Find<Button>("Play");play.Content=TransportIcon(false);previous=Find<Button>("Previous");next=Find<Button>("Next");progress=Find<ProgressBar>("Progress");scene=Find<FrameworkElement>("Scene");tint=Find<FrameworkElement>("Tint");layout=Find<FrameworkElement>("Layout");ApplyTransparency(transparency,false);ApplyForegroundTransparency(foregroundTransparency,false);
  window.Left=SystemParameters.WorkArea.Right-window.Width-24;window.Top=SystemParameters.WorkArea.Bottom-window.Height-24;
  try{var xy=File.ReadAllLines(Path.Combine(Dir,"app-position.txt"));double x,y;if(double.TryParse(xy[0],out x)&&double.TryParse(xy[1],out y)){if(!double.IsNaN(x)&&!double.IsInfinity(x)&&!double.IsNaN(y)&&!double.IsInfinity(y)&&new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight).IntersectsWith(new Rect(x,y,window.Width,window.Height))){window.Left=x;window.Top=y;}}}catch{}
  Find<Button>("Close").Click+=(s,e)=>window.Close();
  Find<Button>("Minimize").Click+=(s,e)=>{window.Hide();};
  window.MouseLeftButtonDown+=(s,e)=>{DependencyObject d=e.OriginalSource as DependencyObject;while(d!=null){if(d is Button)return;d=VisualTreeHelper.GetParent(d);}dragging=true;dragStart=CursorDips();dragLeft=window.Left;dragTop=window.Top;window.CaptureMouse();e.Handled=true;};
  window.MouseMove+=(s,e)=>{AdvanceTitle(0,true);if(!dragging)return;var cursor=CursorDips();window.Left=dragLeft+cursor.X-dragStart.X;window.Top=dragTop+cursor.Y-dragStart.Y;};
  window.MouseLeftButtonUp+=(s,e)=>{if(!dragging)return;dragging=false;window.ReleaseMouseCapture();};window.LostMouseCapture+=(s,e)=>dragging=false;
  play.Click+=async(s,e)=>await Command(0);previous.Click+=async(s,e)=>await Command(-1);next.Click+=async(s,e)=>await Command(1);
  artOnly=new Border{Margin=new Thickness(12),CornerRadius=new CornerRadius(14),Visibility=Visibility.Collapsed};
  var onlyBrush=new ImageBrush{Stretch=Stretch.UniformToFill};artOnly.Background=onlyBrush;
  System.Windows.Data.BindingOperations.SetBinding(onlyBrush,ImageBrush.ImageSourceProperty,new System.Windows.Data.Binding("ImageSource"){Source=cover});root.Children.Insert(2,artOnly);ApplyForegroundTransparency(foregroundTransparency,false);
  var menu=new ContextMenu();clickOption=new MenuItem{Header="Click through (Ctrl+Alt+.)",IsCheckable=true};clickOption.Click+=(s,e)=>ToggleClickThrough();menu.Items.Add(clickOption);var coverOption=new MenuItem{Header="Album cover only",IsCheckable=true};coverOption.Click+=(s,e)=>SetCoverMode(coverOption.IsChecked);menu.Items.Add(coverOption);
  var pin=new MenuItem{Header="Always on top",IsCheckable=true,IsChecked=true};pin.Click+=(s,e)=>window.Topmost=pin.IsChecked;menu.Items.Add(pin);
  var settings=new MenuItem{Header="Transparency..."};settings.Click+=(s,e)=>OpenSettings();menu.Items.Add(settings);
  var exit=new MenuItem{Header="Quit"};exit.Click+=(s,e)=>window.Close();menu.Items.Add(exit);window.ContextMenu=menu;
  tray=new Forms.NotifyIcon{Icon=Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),Text="spoverlay - scroll 3",Visible=!check};
  var trayMenu=new Forms.ContextMenuStrip();trayClickOption=new Forms.ToolStripMenuItem("Click through (Ctrl+Alt+.)");trayClickOption.Click+=(s,e)=>ToggleClickThrough();trayMenu.Items.Add(trayClickOption);trayMenu.Items.Add("Show overlay",null,(s,e)=>Restore());trayMenu.Items.Add("Album cover only",null,(s,e)=>{coverOption.IsChecked=!coverMode;SetCoverMode(!coverMode);Restore();});trayMenu.Items.Add("Transparency...",null,(s,e)=>OpenSettings());trayMenu.Items.Add("Reset position",null,(s,e)=>{window.Left=SystemParameters.WorkArea.Right-window.Width-24;window.Top=SystemParameters.WorkArea.Bottom-window.Height-24;Restore();});trayMenu.Items.Add("Quit",null,(s,e)=>window.Close());tray.ContextMenuStrip=trayMenu;tray.DoubleClick+=(s,e)=>Restore();
  poll=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};poll.Tick+=async(s,e)=>await Poll();
  animation=new DispatcherTimer();animation.Tick+=(s,e)=>{if(frames==null||!window.IsVisible)return;frame=(frame+1)%frames.Length;gif.ImageSource=frames[frame];animation.Interval=TimeSpan.FromMilliseconds(delays[frame]);};
  window.Closed+=(s,e)=>{closing=true;if(hotkeyRegistered)UnregisterHotKey(nativeHandle,HotkeyId);if(nativeSource!=null)nativeSource.RemoveHook(WindowMessage);if(settingsWindow!=null)settingsWindow.Close();poll.Stop();animation.Stop();if(hoverTimer!=null)hoverTimer.Stop();tray.Visible=false;tray.Dispose();try{File.WriteAllLines(Path.Combine(Dir,"app-position.txt"),new[]{window.Left.ToString(),window.Top.ToString()});}catch{}};
  if(check){double oldForeground=foregroundTransparency;ApplyForegroundTransparency(100,false);if(layout.Opacity!=0||artOnly.Opacity!=0||Find<Button>("Close").Opacity!=0)throw new Exception("Foreground transparency failed");ApplyForegroundTransparency(0,false);if(layout.Opacity!=1)throw new Exception("Foreground reset failed");nativeHandle=new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();ApplyClickStyle(true);if(!clickThrough)throw new Exception("Click through did not enable");foreach(var control in new[]{previous,play,next,Find<Button>("Minimize"),Find<Button>("Close")})if(control.Visibility!=Visibility.Hidden)throw new Exception("Locked control remained visible");ApplyClickStyle(false);if(clickThrough)throw new Exception("Click through did not disable");foreach(var control in new[]{previous,play,next,Find<Button>("Minimize"),Find<Button>("Close")})if(control.Visibility!=Visibility.Visible)throw new Exception("Unlocked control remained hidden");if(hotkeyRegistered)UnregisterHotKey(nativeHandle,HotkeyId);if(nativeSource!=null)nativeSource.RemoveHook(WindowMessage);double original=transparency;ApplyTransparency(0,false);if(scene.Opacity!=1||tint.Opacity!=1||window.Opacity!=1)throw new Exception("Zero background transparency failed");ApplyTransparency(100,false);if(scene.Opacity!=0||tint.Opacity!=0||window.Opacity!=1||layout.Opacity!=1||artOnly.Opacity!=1)throw new Exception("Background-only transparency failed");ApplyTransparency(original,false);var probe=AlbumReader.TestMemory().GetAwaiter().GetResult();if(probe.Length!=6||probe[0]!=71)throw new Exception("Native artwork stream test failed");LoadFrames();SetCoverMode(true);if(window.Width!=184)throw new Exception("Cover mode failed");SetCoverMode(false);RenderPreview();string originalTitle=titleText;SetTitle("Where the Leaves No Longer Grow - full song title scrolling test");titleHover=true;RefreshTitle();if(titleLoop.Visibility!=Visibility.Visible||song.Visibility!=Visibility.Hidden)throw new Exception("Long title hover failed");AdvanceTitle(0.5,true);root.UpdateLayout();if(titleFirst.ActualWidth<=titleViewport.ActualWidth||titleLoop.ActualHeight<=0)throw new Exception("Scrolling title was not laid out");if(titleShift.X>=-1)throw new Exception("Title animation did not move");SaveHoverPreview();titleHover=false;RefreshTitle();if(titleLoop.Visibility!=Visibility.Collapsed||song.Visibility!=Visibility.Visible||titleShift.X!=0)throw new Exception("Title reset failed");SetTitle("Short");titleHover=true;RefreshTitle();if(titleLoop.Visibility!=Visibility.Collapsed)throw new Exception("Short title should not scroll");titleHover=false;SetTitle(originalTitle);tray.Dispose();File.WriteAllText(Path.Combine(Dir,"verification.txt"),"Native interface loaded; native click-through style toggle passed; transparency endpoints passed; long-title hover and reset passed; cover mode passed; "+frames.Length+" GIF frames decoded. Native artwork stream test passed. No PowerShell runtime dependency.\r\n");return;}
  window.ContentRendered+=async(s,e)=>{await Task.Run((Action)LoadFrames);if(closing)return;gif.ImageSource=frames[0];animation.Interval=TimeSpan.FromMilliseconds(delays[0]);animation.Start();poll.Start();await Poll();};
  app.Run(window);
 }
 void ApplyTransparency(double value,bool save){
  transparency=Math.Max(0,Math.Min(100,value));window.Opacity=1;if(scene!=null)scene.Opacity=1-transparency/100;if(tint!=null)tint.Opacity=1-transparency/100;
  if(save)try{File.WriteAllText(Path.Combine(Dir,"transparency.txt"),transparency.ToString(System.Globalization.CultureInfo.InvariantCulture));}catch{}
 }
 void ApplyForegroundTransparency(double value,bool save){
  foregroundTransparency=Math.Max(0,Math.Min(100,value));double opacity=1-foregroundTransparency/100;
  if(layout!=null)layout.Opacity=opacity;if(artOnly!=null)artOnly.Opacity=opacity;
  var close=Find<Button>("Close");var minimize=Find<Button>("Minimize");if(close!=null)close.Opacity=opacity;if(minimize!=null)minimize.Opacity=opacity;
  if(save)try{File.WriteAllText(Path.Combine(Dir,"foreground-transparency.txt"),foregroundTransparency.ToString());}catch{}
 }
 void OpenSettings(){
  if(settingsWindow!=null){settingsWindow.Activate();return;}
  settingsWindow=new Window{Width=220,Height=84,WindowStyle=WindowStyle.None,AllowsTransparency=true,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Topmost=true,ShowInTaskbar=false,Background=Brushes.Transparent};
  var slider=new Slider{Minimum=0,Maximum=100,Value=transparency,TickFrequency=1,IsSnapToTickEnabled=true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(16,0,16,0),Foreground=new SolidColorBrush(Color.FromRgb(232,184,249))};
  System.Windows.Automation.AutomationProperties.SetName(slider,"Background transparency");
  slider.ValueChanged+=(s,e)=>ApplyTransparency(slider.Value,true);
  slider.ToolTip="Background";var foregroundSlider=new Slider{Minimum=0,Maximum=100,Value=foregroundTransparency,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(16,8,16,0),ToolTip="Artwork, text and controls"};System.Windows.Automation.AutomationProperties.SetName(foregroundSlider,"Artwork text and controls transparency");foregroundSlider.ValueChanged+=(s,e)=>ApplyForegroundTransparency(foregroundSlider.Value,true);var sliders=new StackPanel{VerticalAlignment=VerticalAlignment.Center};sliders.Children.Add(slider);sliders.Children.Add(foregroundSlider);
  settingsWindow.Content=new Border{CornerRadius=new CornerRadius(12),Background=new SolidColorBrush(Color.FromRgb(29,20,36)),BorderBrush=new SolidColorBrush(Color.FromRgb(73,52,83)),BorderThickness=new Thickness(1),Child=sliders};
  settingsWindow.Deactivated+=(s,e)=>{if(settingsWindow!=null)settingsWindow.Close();};
  settingsWindow.KeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape)settingsWindow.Close();};
  settingsWindow.Closed+=(s,e)=>settingsWindow=null;settingsWindow.Show();
 }
 void SetupTitle(){
  var parent=(StackPanel)song.Parent;int index=parent.Children.IndexOf(song);parent.Children.Remove(song);
  titleViewport=new Grid{ClipToBounds=true,Height=26};titleViewport.Children.Add(song);
  var canvas=new Canvas{IsHitTestVisible=false};
  titleLoop=new StackPanel{Orientation=Orientation.Horizontal,RenderTransform=titleShift,Visibility=Visibility.Collapsed};
  titleFirst=new TextBlock{FontFamily=song.FontFamily,FontSize=song.FontSize,FontWeight=song.FontWeight,Foreground=song.Foreground,Margin=new Thickness(0,0,36,0)};
  titleSecond=new TextBlock{FontFamily=song.FontFamily,FontSize=song.FontSize,FontWeight=song.FontWeight,Foreground=song.Foreground};
  titleLoop.Children.Add(titleFirst);titleLoop.Children.Add(titleSecond);canvas.Children.Add(titleLoop);titleViewport.Children.Add(canvas);parent.Children.Insert(index,titleViewport);
  titleViewport.SizeChanged+=(s,e)=>RefreshTitle();window.MouseEnter+=(s,e)=>AdvanceTitle(0,true);window.MouseLeave+=(s,e)=>AdvanceTitle(0,false);
  hoverTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};hoverTimer.Tick+=(s,e)=>{var now=DateTime.UtcNow;double elapsed=Math.Min(0.1,(now-hoverTime).TotalSeconds);hoverTime=now;NativePoint cursor;bool over=false;if(window.IsVisible&&GetCursorPos(out cursor)){NativeRect bounds;if(GetWindowRect(new System.Windows.Interop.WindowInteropHelper(window).Handle,out bounds))over=cursor.X>=bounds.Left&&cursor.Y>=bounds.Top&&cursor.X<bounds.Right&&cursor.Y<bounds.Bottom;}AdvanceTitle(elapsed,over);};hoverTimer.Start();
  SetTitle(song.Text);
 }
 void SetTitle(string text){text=text??"";if(titleText==text)return;titleText=text;song.Text=text;titleFirst.Text=text;titleSecond.Text=text;RefreshTitle();}
 void RefreshTitle(){
  titleShift.BeginAnimation(TranslateTransform.XProperty,null);titleShift.X=0;
  var measured=new FormattedText(titleText,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(song.FontFamily,song.FontStyle,song.FontWeight,song.FontStretch),song.FontSize,song.Foreground,VisualTreeHelper.GetDpi(window).PixelsPerDip);
  double width=Math.Ceiling(measured.WidthIncludingTrailingWhitespace);titleDistance=width+36;titleFirst.Width=width;titleSecond.Width=width;titleLoop.Width=titleDistance+width;titleLoop.Height=26;
  bool scroll=titleHover&&titleViewport.ActualWidth>0&&width>titleViewport.ActualWidth+1;
  song.Visibility=scroll?Visibility.Hidden:Visibility.Visible;titleLoop.Visibility=scroll?Visibility.Visible:Visibility.Collapsed;
 }
 void AdvanceTitle(double seconds,bool over){
  if(titleHover!=over){titleHover=over;RefreshTitle();}
  if(titleLoop.Visibility==Visibility.Visible&&titleDistance>0)titleShift.X=-((-titleShift.X+seconds*40)%titleDistance);
 }
 Point CursorDips(){NativePoint cursor;GetCursorPos(out cursor);var source=PresentationSource.FromVisual(window);return source.CompositionTarget.TransformFromDevice.Transform(new Point(cursor.X,cursor.Y));}
 IntPtr WindowMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam,ref bool handled){
  if(message==0x312&&wParam.ToInt32()==HotkeyId){ToggleClickThrough();handled=true;}return IntPtr.Zero;
 }
 void ApplyClickStyle(bool enabled){
  int style=GetWindowLong(nativeHandle,-20);
  SetWindowLong(nativeHandle,-20,enabled?(style|0x20):(style&~0x20));
  if(((GetWindowLong(nativeHandle,-20)&0x20)!=0)!=enabled)throw new Exception("Windows did not apply click-through mode");
  foreach(var button in new[]{previous,play,next,Find<Button>("Minimize"),Find<Button>("Close")})button.Visibility=enabled?Visibility.Hidden:Visibility.Visible;
  clickThrough=enabled;if(clickOption!=null)clickOption.IsChecked=enabled;if(trayClickOption!=null)trayClickOption.Checked=enabled;
 }
 void ToggleClickThrough(){
  if(!clickThrough&&!hotkeyRegistered){MessageBox.Show("Ctrl+Alt+. is unavailable. Another app may be using it. Close that app and restart this overlay.","spoverlay");if(clickOption!=null)clickOption.IsChecked=false;return;}
  try{ApplyClickStyle(!clickThrough);}catch(Exception ex){Log("click-through-error.txt",ex);}
 }
 void Restore(){window.Show();window.WindowState=WindowState.Normal;window.Activate();}
 void SetCoverMode(bool value){coverMode=value;scene.Visibility=tint.Visibility=layout.Visibility=value?Visibility.Collapsed:Visibility.Visible;artOnly.Visibility=value?Visibility.Visible:Visibility.Collapsed;window.Width=value?184:420;window.Height=value?184:132;}
 void LoadFrames(){
  using(var image=Drawing.Image.FromFile(Path.Combine(Dir,"background.gif"))){var dimension=new Drawing.Imaging.FrameDimension(image.FrameDimensionsList[0]);int n=image.GetFrameCount(dimension);frames=new BitmapSource[n];delays=new int[n];byte[] timing=null;try{timing=image.GetPropertyItem(0x5100).Value;}catch{}
   for(int i=0;i<n;i++){image.SelectActiveFrame(dimension,i);using(var bitmap=new Drawing.Bitmap(image))using(var memory=new MemoryStream()){bitmap.Save(memory,Drawing.Imaging.ImageFormat.Png);frames[i]=Decode(memory.ToArray(),500);}delays[i]=timing!=null&&timing.Length>=i*4+4?Math.Max(20,BitConverter.ToInt32(timing,i*4)*10):100;}
  }
 }
 static BitmapSource Decode(byte[] bytes,int width){using(var stream=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=width;image.StreamSource=stream;image.EndInit();image.Freeze();return image;}}
 static async Task<T> Timeout<T>(Task<T> task){if(await Task.WhenAny(task,Task.Delay(4000))!=task)throw new TimeoutException("Windows media did not respond");return await task;}
 class Snapshot {public string Title,Artist;public bool Playing,Prev,Next,Toggle;public double Percent;public BitmapSource Art;public bool Missing;}
 async Task<Snapshot> Read(){
  if(manager==null)manager=await Timeout(System.WindowsRuntimeSystemExtensions.AsTask<GlobalSystemMediaTransportControlsSessionManager>(GlobalSystemMediaTransportControlsSessionManager.RequestAsync()));
  var active=manager.GetSessions().FirstOrDefault(s=>s.SourceAppUserModelId.IndexOf("Spotify",StringComparison.OrdinalIgnoreCase)>=0);session=active;
  if(active==null){coverKey="";cachedCover=null;return new Snapshot{Missing=true};}
  var props=await Timeout(System.WindowsRuntimeSystemExtensions.AsTask<GlobalSystemMediaTransportControlsSessionMediaProperties>(active.TryGetMediaPropertiesAsync()));var info=active.GetPlaybackInfo();
  var result=new Snapshot{Title=props.Title,Artist=props.Artist,Playing=info.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,Prev=info.Controls.IsPreviousEnabled,Next=info.Controls.IsNextEnabled,Toggle=info.Controls.IsPlayPauseToggleEnabled};
  string key=props.Title+"|"+props.Artist+"|"+props.AlbumTitle;
  if(key!=coverKey){cachedCover=null;coverKey=key;}
  if(cachedCover==null&&props.Thumbnail!=null){try{var bytes=await Timeout(AlbumReader.Read(props.Thumbnail));cachedCover=Decode(bytes,400);}catch(Exception ex){Log("artwork-error.txt",ex);}}
  result.Art=cachedCover;
  var t=active.GetTimelineProperties();double duration=(t.EndTime-t.StartTime).TotalSeconds;double elapsed=(t.Position-t.StartTime).TotalSeconds;if(result.Playing)elapsed+=Math.Max(0,(DateTimeOffset.Now-t.LastUpdatedTime).TotalSeconds);result.Percent=duration>0?Math.Max(0,Math.Min(100,elapsed/duration*100)):0;return result;
 }
 async Task Poll(){if(busy||closing)return;busy=true;try{var data=await Task.Run((Func<Task<Snapshot>>)Read);if(closing)return;SetTitle(data.Missing?"Ready when you are":data.Title);artist.Text=data.Missing?"Play a song on this PC":data.Artist;cover.ImageSource=data.Art;progress.Value=data.Percent;play.Content=TransportIcon(data.Playing);previous.IsEnabled=data.Prev;next.IsEnabled=data.Next;play.IsEnabled=data.Toggle;}catch(Exception ex){manager=null;Log("last-error.txt",ex);if(!closing)artist.Text="Waiting for Windows media";}finally{busy=false;}}
 async Task Command(int action){var active=session;if(active==null)return;try{await Task.Run(async()=>{var op=action<0?active.TrySkipPreviousAsync():action>0?active.TrySkipNextAsync():active.TryTogglePlayPauseAsync();await Timeout(System.WindowsRuntimeSystemExtensions.AsTask<bool>(op));});await Poll();}catch(Exception ex){Log("controls-error.txt",ex);}}
 static void Log(string file,Exception ex){try{File.WriteAllText(Path.Combine(Dir,file),ex.ToString());}catch{}}
 void SaveHoverPreview(){root.UpdateLayout();var bitmap=new RenderTargetBitmap(420,132,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(Dir,"hover-preview.png")))encoder.Save(file);}
 void RenderPreview(){root.Measure(new Size(420,132));root.Arrange(new Rect(0,0,420,132));root.UpdateLayout();gif.ImageSource=frames[0];root.UpdateLayout();var bitmap=new RenderTargetBitmap(420,132,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(Dir,"preview.png")))encoder.Save(f);}
}


















