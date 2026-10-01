using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhalePet;

public sealed class PetCanvas : FrameworkElement
{
    private readonly BitmapSource[] sprites;
    private Mood mood;
    private Mood previousMood;
    private DateTimeOffset changedAt = DateTimeOffset.MinValue;
    public string Message { get; set; } = "你好呀～右键菜单里可以喂米饭！";
    public DateTimeOffset MessageUntil { get; set; } = DateTimeOffset.UtcNow.AddSeconds(6);
    public PetCanvas()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WhalePet.states.png") ?? throw new FileNotFoundException("缺少角色表情素材");
        BitmapImage sheet = new();
        sheet.BeginInit(); sheet.CacheOption = BitmapCacheOption.OnLoad; sheet.StreamSource = stream; sheet.EndInit(); sheet.Freeze();
        sprites = new BitmapSource[6];
        int split = sheet.PixelHeight / 2 + 3;
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
            {
                CroppedBitmap crop = new(sheet, new Int32Rect(col * sheet.PixelWidth / 3, row == 0 ? 0 : split, sheet.PixelWidth / 3, row == 0 ? split : sheet.PixelHeight - split));
                crop.Freeze(); sprites[row * 3 + col] = crop;
            }
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }
    public void SetMood(Mood value, bool instant = false)
    {
        if (value != mood) { previousMood = mood; mood = value; changedAt = instant ? DateTimeOffset.MinValue : DateTimeOffset.UtcNow; }
        if (instant) changedAt = DateTimeOffset.MinValue;
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        double side = Math.Min(Math.Max(0, ActualWidth - 16), Math.Max(0, ActualHeight - 65));
        Rect rect = new((ActualWidth - side) / 2, ActualHeight - side - 4, side, side);
        double progress = Math.Clamp((now - changedAt).TotalSeconds / .22, 0, 1);
        if (progress < 1) { dc.PushOpacity(1 - progress); dc.DrawImage(sprites[(int)previousMood], rect); dc.Pop(); }
        dc.PushOpacity(progress); dc.DrawImage(sprites[(int)mood], rect); dc.Pop();
        string symbol = mood switch { Mood.Bored => "⋯", Mood.Hungry => "🍚", Mood.Shy => "♡", Mood.Angry => "💢", Mood.Eating => "♪", _ => "" };
        if (symbol.Length > 0)
        {
            double pulse = .6 + .4 * Math.Sin(now.ToUnixTimeMilliseconds() / 500.0);
            dc.PushOpacity(pulse); DrawText(dc, symbol, new Point(ActualWidth - 36, 60), 22, Brushes.DeepPink, 32); dc.Pop();
        }
        if (now < MessageUntil || mood is Mood.Hungry or Mood.Bored)
        {
            string text = now < MessageUntil ? Message : mood == Mood.Hungry ? "肚子饿啦…右键菜单喂饭 🍚" : "有点无聊…陪我玩一下嘛～";
            Rect bubble = new(5, 8, Math.Max(0, ActualWidth - 10), 46);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(247, 255, 255, 255)), null, bubble, 14, 14);
            DrawText(dc, text, new Point(13, 19), 12, new SolidColorBrush(Color.FromRgb(41, 64, 112)), Math.Max(1, ActualWidth - 26), true);
        }
    }
    private void DrawText(DrawingContext dc, string text, Point position, double size, Brush brush, double width, bool center = false)
    {
        FormattedText formatted = new(text, System.Globalization.CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { MaxTextWidth = width, MaxTextHeight = 38, Trimming = TextTrimming.CharacterEllipsis, TextAlignment = center ? TextAlignment.Center : TextAlignment.Left };
        dc.DrawText(formatted, position);
    }
}
