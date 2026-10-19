using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WlyUsage;

internal sealed partial class ActivityCharacter : FrameworkElement
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(string), typeof(ActivityCharacter),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender, OnStatusChanged));
    private static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        "Frame", typeof(double), typeof(ActivityCharacter),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0xee, 0xf0, 0xf8));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x84, 0x90, 0xff));
    private static readonly Brush Ground = new SolidColorBrush(Color.FromRgb(0x58, 0x63, 0x80));
    public TextBlock? StatusLabel { get; init; }
    public double BuddyPhase { get; set; } = -1;
    public bool BuddyOnRight { get; set; }
    public double? PlaybackWorkCycle { get; set; }
    private string? playbackRole;
    private string EffectiveStatus => playbackRole ?? Status;
    public void SetPlaybackRole(string? role)
    {
        if (playbackRole == role) return;
        playbackRole = role;
        bool visible = EffectiveStatus.StartsWith("Trabalhando", StringComparison.Ordinal) || EffectiveStatus == "Sem atividade recente";
        Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (StatusLabel is { } label) label.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        InvalidateVisual();
    }
    public string AgentName { get; init; } = "Codex";
    private static readonly Geometry ClaudeLogo = LoadLogo("claude");
    private static readonly Geometry CodexLogo = LoadLogo("openai");
    private static readonly Brush ClaudeInk = new SolidColorBrush(Color.FromRgb(0xe3, 0xad, 0x91));

    private static Geometry LoadLogo(string name)
    {
        using var stream = typeof(ActivityCharacter).Assembly.GetManifestResourceStream($"WlyUsage.Assets.{name}.svg")!;
        var svg = System.Xml.Linq.XDocument.Load(stream);
        var geometry = Geometry.Parse(svg.Descendants().First(element => element.Name.LocalName == "path").Attribute("d")!.Value);
        geometry.Freeze();
        return geometry;
    }

    private void DrawHead(DrawingContext drawing, double x, double y)
    {
        var logo = AgentName == "Claude" ? ClaudeLogo : CodexLogo;
        var bounds = logo.Bounds;
        double scale = 14 / Math.Max(bounds.Width, bounds.Height);
        drawing.PushTransform(new MatrixTransform(scale, 0, 0, scale,
            x + (14 - bounds.Width * scale) / 2 - bounds.X * scale,
            y + (14 - bounds.Height * scale) / 2 - bounds.Y * scale));
        drawing.DrawGeometry(AgentName == "Claude" ? ClaudeInk : Ink, null, logo);
        drawing.Pop();
    }
    public string Status { get => (string)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    public ActivityCharacter()
    {
        Width = 56; Height = 48;
        Margin = new Thickness(8, 0, 8, 0);
        VerticalAlignment = VerticalAlignment.Center;
        Visibility = Visibility.Collapsed;
        Loaded += (_, _) => Animate();
        Unloaded += (_, _) => BeginAnimation(FrameProperty, null);
        IsVisibleChanged += (_, _) => Animate();
    }

    private void Animate()
    {
        if (!IsLoaded || !IsVisible) { BeginAnimation(FrameProperty, null); return; }
        double seconds = EffectiveStatus.StartsWith("Trabalhando", StringComparison.Ordinal) ? 32.4 : 12.8;
        BeginAnimation(FrameProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(seconds))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            // Refreshing local readings rebuilds cards; retain the rhythm across refreshes.
            BeginTime = TimeSpan.FromSeconds(-(DateTimeOffset.UtcNow.TimeOfDay.TotalSeconds % seconds))
        });
    }

    private static void OnStatusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var icon = (ActivityCharacter)sender;
        bool character = icon.Status.StartsWith("Trabalhando", StringComparison.Ordinal) || icon.Status == "Sem atividade recente";
        icon.Visibility = character ? Visibility.Visible : Visibility.Collapsed;
        if (icon.StatusLabel is { } label) label.Visibility = character ? Visibility.Collapsed : Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(icon, icon.Status);
        icon.Animate();
    }

    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        double frame = (double)GetValue(FrameProperty);
        var pen = new Pen(Ink, 2.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        void Path(string data, Pen? stroke = null, Brush? fill = null) => d.DrawGeometry(fill, stroke ?? pen, Geometry.Parse(data));
        // Transparent hit area makes the full status available through the tooltip.
        d.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Width, Height));
        if (BuddyPhase >= 0)
        {
            // Mirror the entire interaction so either agent can be the instigator.
            bool mirror = EffectiveStatus.StartsWith("Trabalhando", StringComparison.Ordinal) ? !BuddyOnRight : BuddyOnRight;
            if (mirror) d.PushTransform(new MatrixTransform(-1, 0, 0, 1, Width, 0));
            double scene = BuddyPhase;
            Path("M4 44 H51", new Pen(Ground, 1));
            if (scene >= 16.55 && scene < 17.5 && EffectiveStatus.StartsWith("Trabalhando", StringComparison.Ordinal))
            {
                double recoil = Math.Sin((scene - 16.55) / .95 * Math.PI);
                d.PushTransform(new RotateTransform(-recoil * 30, 20, 40));
                DrawHead(d, 11, 6);
                Path("M18 21 L20 32 L12 42 M20 32 L28 42 M18 24 L12 17 L17 12 M20 27 L29 21 L31 14");
                d.Pop();
                if (mirror) d.Pop();
                return;
            }
            if (DrawSaga(d, scene, mirror))
            {
                if (mirror) d.Pop();
                return;
            }
            if (scene >= 14.5 && scene < 17 && EffectiveStatus == "Sem atividade recente")
            {
                Path("M9 37 Q5 29 15 29 L24 34 L21 38 Z", new Pen(Accent, 1.5), Accent);
                Path("M23 34 Q31 24 40 35 L45 38 H22 Z", new Pen(Accent, 2), Accent);
                DrawHead(d, 8, 8);
                Path(scene < 16 ? "M20 29 L10 22 L6 12 M22 32 L12 26 L8 15" : "M20 29 L29 24 L39 20 M22 32 L31 27 L39 20");
                if (scene < 16)
                {
                    WorkBook.Draw(d, new Point(7, 14), .55, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                }
                if (mirror) d.Pop();
                return;
            }
            if (scene >= 9 && EffectiveStatus.StartsWith("Trabalhando", StringComparison.Ordinal))
            {
                double shake = scene < 12 ? Math.Sin(scene * 45) * .7 : 0;
                d.PushTransform(new TranslateTransform(shake, 0));
                DrawHead(d, 11, 6);
                Path("M18 21 L22 32 L10 42 M22 32 L33 42 M5 8 L2 5 M27 7 L30 4", pen);
                if (scene < 12)
                    Path("M18 23 L10 27 L12 30 M19 27 L15 33 L12 30");
                else
                    Path("M18 23 L28 24 H40 M20 28 L30 27 L40 24");
                d.Pop();
                var yell = new FormattedText(scene < 12 ? "ka… me…" : scene < 13.5 ? "HAAA!" : "hmph!",
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 8,
                    Accent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                d.DrawText(yell, new Point(7, 0));
                if (mirror) d.Pop();
                return;
            }
            if (scene >= 12.25 && scene < 14.5 && EffectiveStatus == "Sem atividade recente")
            {
                double recoil = Math.Sin(Math.Min(1, (scene - 12.25) / .5) * Math.PI / 2);
                d.PushTransform(new RotateTransform(-recoil * 25, 25, 36));
                DrawHead(d, 8, 12);
                Path("M20 26 L31 32 L41 28 M31 32 L43 40 M21 27 L15 20 M23 29 L29 19");
                d.Pop();
                for (int i = 0; i < 3; i++)
                {
                    double angle = scene * 5 + i * Math.PI * 2 / 3;
                    d.DrawEllipse(Accent, null, new Point(21 + Math.Cos(angle) * 12, 12 + Math.Sin(angle) * 5), 1.5, 1.5);
                }
                if (mirror) d.Pop();
                return;
            }
            if (EffectiveStatus.StartsWith("Trabalhando", StringComparison.Ordinal))
            {
                double wipe = scene < 2 ? Math.Sin(scene * Math.PI * 2) * 2 : 0;
                DrawHead(d, scene >= 2 ? 15 : 10, 6);
                Path("M18 21 L19 32 L12 42 M19 32 L26 42 M18 25 L11 29");
                if (scene < 2)
                {
                    d.DrawLine(pen, new Point(18, 23), new Point(25, 20));
                    d.DrawLine(pen, new Point(25, 20), new Point(18 + wipe, 9));
                    d.DrawEllipse(Accent, null, new Point(28, 13 + scene * 5), 1.4, 2);
                }
                else if (scene < 3) Path("M18 23 L27 25 L31 19");
                else if (scene < 4) Path("M18 23 L13 17 L20 9");
                else Path("M18 23 L28 20 L35 19");
                if (scene < 3)
                    Path("M43 25 L49 42 M37 28 Q43 21 49 26", new Pen(Accent, 2));
                else if (scene < 4)
                    Path("M20 8 L27 22 M14 10 Q20 3 27 7", new Pen(Accent, 2));
            }
            else
            {
                Path("M9 37 Q5 29 15 29 L24 34 L21 38 Z", new Pen(Accent, 1.5), Accent);
                Path("M23 34 Q31 24 40 35 L45 38 H22 Z", new Pen(Accent, 2), Accent);
                double wake = scene < 5.3 ? 0 : scene < 5.8 ? (scene - 5.3) / .5 : scene < 8.3 ? 1 : Math.Max(0, 1 - (scene - 8.3) / .7);
                double turn = scene >= 5.8 && scene < 8.3 ? Math.Min(1, (scene - 5.8) / .45) : 0;
                // Turn the raised head toward the thrower before replying.
                d.PushTransform(new RotateTransform(turn * 12, 16, 16));
                DrawHead(d, 8 + turn * 5, 20 - wake * 12);
                d.Pop();
                if (scene >= 5.8 && scene < 8.3)
                {
                    // Manga-style anger mark, held long enough to read at panel size.
                    double pulse = 1 + Math.Sin(scene * 12) * .08;
                    d.PushTransform(new ScaleTransform(pulse, pulse, 34, 8));
                    Path("M30 3 V7 H26 M33 3 V7 H37 M30 10 V14 H26 M33 10 V14 H37", new Pen(Accent, 1.6));
                    d.Pop();
                    Path("M22 18 L27 16 L25 21", new Pen(Ink, 1.2));
                }
                if (scene >= 5.3 && scene < 6.8)
                {
                    Path("M20 32 L25 29 L26 25");
                    if (mirror) d.Pop();
                    return;
                }
                if (wake > 0)
                {
                    Path("M20 32 L27 30.5 L32.5 28.4");
                    // Fist with separate knuckles, tucked thumb and a clearly extended middle finger.
                    d.PushOpacity(wake);
                    var fine = new Pen(Ink, 1.1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    Path("M31 28.4 L30 24 Q29.6 20.4 31.8 20.4 Q33 20.4 33 21.5 V13 Q34.2 10.6 35.4 13 V21.2 " +
                         "Q35.6 19.8 36.6 19.9 Q37.6 20 37.6 21.2 Q38.3 20.6 39 21.2 Q39.9 22 39.4 24.6 " +
                         "L38.4 27.6 Q37.6 28.6 35.4 28.6 Z", fine, Ground);
                    Path("M33 21.5 V23.2 M35.4 21.2 V23.4 M37.6 21.2 V23.2 M30.4 24.8 Q33.2 23.6 36 24.6", new Pen(Ink, .8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
                    d.Pop();
                }
                else
                {
                    var snore = new FormattedText("zzz", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 10, Accent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    d.DrawText(snore, new Point(27, 9));
                }
            }
            if (mirror) d.Pop();
            return;
        }
        if (EffectiveStatus.StartsWith("Trabalhando", StringComparison.Ordinal))
        {
            double cycle = PlaybackWorkCycle ?? frame * 32.4;
            if (cycle >= 16.2 && cycle < 22.2)
            {
                // Throw, stomp over to the abandoned tool, then carry it back.
                double tantrum = cycle - 16.2;
                double walk = tantrum < 2 ? 0 : tantrum < 4 ? (tantrum - 2) / 2 : (6 - tantrum) / 2;
                double step = Math.Sin(tantrum * 16) * 3 * Math.Sin(walk * Math.PI);
                Path("M5 44 H51", new Pen(Ground, 1));
                Path("M36 43 L39 35 L46 34 L51 43 Z", new Pen(Ground, 1.5), Ground);
                d.PushTransform(new TranslateTransform(walk * 22, -Math.Abs(step) * .4));
                DrawHead(d, 9, 7);
                Path("M16 22 L18 32");
                d.DrawLine(pen, new Point(18, 32), new Point(11 + step, 42));
                d.DrawLine(pen, new Point(18, 32), new Point(25 - step, 42));
                Path(tantrum < 2 ? "M16 23 L23 18 L29 13 M17 27 L10 24 L7 18" : "M16 23 L23 27 L28 24 M17 27 L10 29");
                d.Pop();
                double flight = Math.Min(1, tantrum / 1.4);
                double toolX = tantrum < 4 ? 24 + flight * 24 : 26 + walk * 22;
                double toolY = tantrum < 1.4 ? 19 + flight * 20 - Math.Sin(flight * Math.PI) * 17 : tantrum < 4 ? 39 : 28;
                d.PushTransform(new TranslateTransform(toolX, toolY));
                d.PushTransform(new RotateTransform(tantrum < 1.4 ? flight * 540 : tantrum < 4 ? 90 : -20));
                Path("M0 -8 V8", new Pen(Ink, 2));
                Path("M-7 -5 Q0 -11 7 -5", new Pen(Accent, 2.5));
                d.Pop(); d.Pop();
                if (tantrum < 2)
                {
                    var curse = new FormattedText("#!@", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 9, Accent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    d.DrawText(curse, new Point(27, 1));
                }
                return;
            }
            bool furious = cycle >= 22.2 && cycle < 28.2;
            double swingDuration = furious ? .45 : 1.35;
            frame = (furious ? cycle - 22.2 : cycle) % swingDuration / swingDuration;
            double swing = frame < .65 ? frame / .65 : 1 - (frame - .65) / .35;
            double bob = Math.Sin(swing * Math.PI) * (furious ? 4 : 2);
            Path("M5 44 H51", new Pen(Ground, 1));
            Path("M36 43 L39 35 L46 34 L51 43 Z", new Pen(Ground, 1.5), Ground);
            d.PushTransform(new TranslateTransform(0, bob));
            DrawHead(d, 10, 5);
            if (furious)
            {
                Path("M5 10 L2 7 M6 16 H2 M26 7 L29 4", new Pen(Accent, 1.5));
            }
            Path("M17 20 L21 30 L14 35 L11 42 M21 30 L26 40 H30");
            d.PushTransform(new RotateTransform(-65 + swing * 110, 20, 23));
            Path("M17 21 L24 24 L29 20 M20 28 L26 28 L30 22");
            Path("M28 13 L34 35", new Pen(Ink, 2.5));
            Path("M21 17 Q30 9 39 14", new Pen(Accent, 3));
            d.Pop(); d.Pop();
            if (frame > .63 && frame < .92)
            {
                double burst = (frame - .63) / .29;
                d.PushOpacity(1 - burst);
                for (int i = 0; i < (furious ? 5 : 3); i++)
                {
                    double x = 42 + (i - (furious ? 2 : 1)) * (3 + burst * 4);
                    double y = 34 - Math.Sin(burst * Math.PI) * (7 + i * 2);
                    d.DrawEllipse(Accent, null, new Point(x, y), 1.5, 1.5);
                }
                d.Pop();
            }
        }
        else if (EffectiveStatus == "Sem atividade recente")
        {
            double cycle = frame * 12.8;
            frame = cycle % 3.2 / 3.2;
            double breath = Math.Sin(frame * Math.PI * 2);
            Path("M4 40 H50 M7 39 V44 M47 39 V44", new Pen(Ground, 1.5));
            d.PushTransform(new TranslateTransform(0, breath));
            Path("M9 37 Q5 29 15 29 L24 34 L21 38 Z", new Pen(Accent, 1.5), Accent);
            Path("M23 34 Q31 24 40 35 L45 38 H22 Z", new Pen(Accent, 2), Accent);
            DrawHead(d, 8, 20);
            d.Pop();
            if (cycle > 10.4)
            {
                double puff = (cycle - 10.4) / 2.4;
                d.PushOpacity(Math.Sin(puff * Math.PI) * .85);
                // Little cloud puffs drift out from under the blanket and disappear.
                for (int i = 0; i < 3; i++)
                {
                    double drift = Math.Clamp(puff * 1.5 - i * .18, 0, 1);
                    double x = 40 + drift * 10;
                    double y = 33 - drift * 14 + i * 2;
                    double radius = 1.5 + drift * 2;
                    d.DrawEllipse(null, new Pen(Accent, 1.2), new Point(x, y), radius, radius * .7);
                }
                var prrt = new FormattedText("prrt", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 8, Accent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                d.DrawText(prrt, new Point(36, 15 - puff * 5));
                d.Pop();
            }
            for (int i = 0; i < 3; i++)
            {
                double rise = (frame + i / 3d) % 1;
                var z = new FormattedText("z", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 9 + rise * 5, Accent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                d.PushOpacity(Math.Sin(rise * Math.PI));
                d.DrawText(z, new Point(26 + rise * 17, 23 - rise * 22));
                d.Pop();
            }
        }
    }
}
