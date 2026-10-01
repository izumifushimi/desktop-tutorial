using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace WhalePet;

public sealed class PetWindow : Window
{
    private readonly Storage storage;
    private readonly Settings settings;
    private readonly PetState state;
    private readonly PetCanvas canvas;
    private readonly DispatcherTimer timer;
    private readonly Forms.NotifyIcon tray;
    private bool dragging;
    private bool moved;
    private Point downScreen;
    private Point downWindow;
    private Mood? preview;
    private DateTimeOffset previewUntil;

    public PetWindow(Storage storage)
    {
        this.storage = storage;
        settings = storage.Load();
        state = new PetState(DateTimeOffset.UtcNow, settings.Hungry, settings.LastMealCheck);
        timer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TimeSpan.FromMilliseconds(100) };
        Title = "鲸鱼娘桌宠";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        Width = double.IsFinite(settings.Width) ? Math.Clamp(settings.Width, 150, 380) : 250;
        Height = Width + 65;
        canvas = new PetCanvas(); Content = canvas;
        Loaded += (_, _) =>
        {
            ResetPosition();
            if (settings.X is double x && settings.Y is double y && double.IsFinite(x) && double.IsFinite(y))
            {
                double minX = SystemParameters.VirtualScreenLeft, minY = SystemParameters.VirtualScreenTop;
                double maxX = minX + SystemParameters.VirtualScreenWidth, maxY = minY + SystemParameters.VirtualScreenHeight;
                if (x + Width > minX + 80 && x < maxX - 80 && y + Height > minY + 80 && y < maxY - 80) { Left = x; Top = y; }
            }
            if (!settings.StartupConfigured)
            {
                TryStartup(true); settings.StartupConfigured = true; Save();
            }
        };
        PreviewMouseLeftButtonDown += OnLeftDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnLeftUp;
        LostMouseCapture += (_, _) => dragging = false;
        PreviewMouseRightButtonUp += (_, e) => { ContextMenu = CreateMenu(); ContextMenu.PlacementTarget = this; ContextMenu.IsOpen = true; e.Handled = true; };
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Information, Text = "鲸鱼娘桌宠 · 右键菜单", Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) { Dispatcher.Invoke(() => { Show(); Activate(); }); } };
        tray.ContextMenuStrip = CreateTrayMenu();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Closed += (_, _) => { timer.Stop(); SystemEvents.PowerModeChanged -= OnPowerModeChanged; SavePosition(); Save(); tray.Visible = false; tray.Dispose(); };
        timer.Tick += (_, _) => Tick(); timer.Start(); Tick();
    }
    private void Tick()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (state.Update(now)) { Save(); Speak("到饭点啦，肚子咕咕叫…🍚"); }
        Mood current = state.GetMood(now, Platform.IdleSeconds());
        bool instant = preview.HasValue && now < previewUntil;
        if (!instant) preview = null;
        canvas.SetMood(current, instant);
    }
    private void Speak(string text) { canvas.Message = text; canvas.MessageUntil = DateTimeOffset.UtcNow.AddSeconds(4); canvas.InvalidateVisual(); }
    private void Touch()
    {
        preview = null; state.Touch(DateTimeOffset.UtcNow);
        Speak(state.Touches >= 20 ? "哼！头发都要被你摸乱啦！" : state.Touches >= 5 ? "嘿嘿…有点不好意思了～" : "摸摸头，今天也要加油呀～"); Tick();
    }
    private void Feed() { preview = null; state.Feed(DateTimeOffset.UtcNow); Save(); Speak("啊呜～米饭最好吃了！🍚"); Tick(); }
    private void PreviewMood(Mood mood, string title)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow; preview = mood; previewUntil = now.AddSeconds(4); state.Preview(mood, now);
        canvas.SetMood(mood, true); Speak("表情预览：" + title);
    }
    private ContextMenu CreateMenu()
    {
        ContextMenu menu = new();
        void Add(string title, Action action) { MenuItem i = new() { Header = title }; i.Click += (_, _) => action(); menu.Items.Add(i); }
        Add("🍚 喂米饭", Feed); Add("🐳 打开 DeepSeek 应用", OpenDeepSeek);
        Add("小一点", () => Resize(.85)); Add("大一点", () => Resize(1.15)); Add("回到右下角", () => { ResetPosition(); SavePosition(); });
        MenuItem previews = new() { Header = "预览表情" };
        string[] names = { "正常", "无聊", "饥饿", "害羞", "嗔怒", "吃饭" };
        for (int n = 0; n < names.Length; n++) { Mood m = (Mood)n; string title = names[n]; MenuItem i = new() { Header = title }; i.Click += (_, _) => PreviewMood(m, title); previews.Items.Add(i); }
        menu.Items.Add(previews); menu.Items.Add(new Separator());
        bool enabled = IsStartupEnabled(); Add(enabled ? "关闭登录自启动" : "启用登录自启动", () => TryStartup(!enabled)); Add("退出鲸鱼娘", Close);
        return menu;
    }
    private Forms.ContextMenuStrip CreateTrayMenu()
    {
        Forms.ContextMenuStrip menu = new();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            void Add(string title, Action action) { menu.Items.Add(title, null, (_, _) => Dispatcher.Invoke(action)); }
            Add("🍚 喂米饭", Feed); Add("🐳 打开 DeepSeek 应用", OpenDeepSeek); Add("回到右下角", () => { ResetPosition(); SavePosition(); });
            Forms.ToolStripMenuItem previews = new("预览表情"); string[] names = { "正常", "无聊", "饥饿", "害羞", "嗔怒", "吃饭" };
            for (int n = 0; n < names.Length; n++) { Mood mood = (Mood)n; string name = names[n]; previews.DropDownItems.Add(name, null, (_, _) => Dispatcher.Invoke(() => PreviewMood(mood, name))); }
            menu.Items.Add(previews); Add("小一点", () => Resize(.85)); Add("大一点", () => Resize(1.15));
            bool enabled = IsStartupEnabled(); Add(enabled ? "关闭登录自启动" : "启用登录自启动", () => TryStartup(!enabled)); Add("退出鲸鱼娘", Close);
        };
        return menu;
    }
    private bool IsStartupEnabled() { try { return Platform.StartupEnabled(); } catch (Exception e) { storage.Log(e); return false; } }
    private void TryStartup(bool enabled)
    {
        try { Platform.SetStartup(enabled); Speak(enabled ? "已启用登录自启动" : "已关闭登录自启动"); }
        catch (Exception e) { storage.Log(e); Speak("无法修改自启动，请检查系统设置"); }
    }
    private void OpenDeepSeek()
    {
        try { if (!Platform.OpenDeepSeek()) Speak("未找到应用，可将 DeepSeek 快捷方式放在桌面"); }
        catch (Exception e) { storage.Log(e); Speak("DeepSeek 应用打开失败"); }
    }
    private void OnLeftDown(object sender, MouseButtonEventArgs e)
    {
        dragging = true; moved = false; downScreen = PointToScreen(e.GetPosition(this)); downWindow = new Point(Left, Top); CaptureMouse(); e.Handled = true;
    }
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
        Point p = PointToScreen(e.GetPosition(this)); Vector delta = p - downScreen; DpiScale dpi = VisualTreeHelper.GetDpi(this);
        if (delta.Length >= 3) moved = true;
        if (moved) { Left = downWindow.X + delta.X / dpi.DpiScaleX; Top = downWindow.Y + delta.Y / dpi.DpiScaleY; }
    }
    private void OnLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (!dragging) return; bool wasMoved = moved; dragging = false; ReleaseMouseCapture();
        if (wasMoved) SavePosition(); else Touch(); e.Handled = true;
    }
    private void Resize(double factor) { Width = Math.Clamp(Width * factor, 150, 380); Height = Width + 65; SavePosition(); }
    private void ResetPosition() { Rect area = SystemParameters.WorkArea; Left = area.Right - Width - 30; Top = area.Bottom - Height - 10; }
    private void SavePosition() { settings.Width = Width; settings.X = Left; settings.Y = Top; Save(); }
    private void Save() { settings.Hungry = state.Hungry; settings.LastMealCheck = state.LastMealCheck; storage.Save(settings); }
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(Tick); }
}
