using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WlyUsage;

internal sealed partial class ActivityCharacter
{
    private static readonly Brush KiGold = new SolidColorBrush(Color.FromRgb(0xff, 0xd3, 0x45));
    private static readonly Brush CapRed = new SolidColorBrush(Color.FromRgb(0xd6, 0x32, 0x39));
    private static readonly Brush Fire = new SolidColorBrush(Color.FromRgb(0xff, 0x93, 0x35));
    private static readonly Brush Rubber = new SolidColorBrush(Color.FromRgb(0x12, 0x14, 0x1a));

    private bool DrawSaga(DrawingContext d, double scene, bool mirrored)
    {
        bool worker = EffectiveStatus.StartsWith("Trabalhando", StringComparison.Ordinal);
        if (worker ? scene < 17.5 : scene < 29) return false;
        var pen = new Pen(Ink, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        void Path(string data, Brush? fill = null, Pen? stroke = null) => d.DrawGeometry(fill, stroke ?? pen, Geometry.Parse(data));
        void Label(string value, Point point, double size)
        {
            // Keep letters readable when the characters exchange left/right roles.
            var text = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), size, Ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            if (mirrored && worker) d.PushTransform(new ScaleTransform(-1, 1, point.X + text.Width / 2, 0));
            d.DrawText(text, point);
            if (mirrored && worker) d.Pop();
        }
        if (worker)
        {
            double power = Math.Clamp((scene - 17.5) / 3.5, 0, 1);
            if (scene < 38)
            {
                d.PushOpacity(.3 + Math.Sin(scene * 15) * .08);
                Path("M4 42 L2 29 L7 32 L5 17 L11 22 L14 5 L20 14 L25 2 L29 18 L36 11 L34 28 L41 23 L38 43 Z", KiGold, new Pen(KiGold, 1));
                d.Pop();
            }
            d.PushTransform(new TranslateTransform(scene < 21 ? Math.Sin(scene * 60) * power : 0, 0));
            if (scene >= 21)
                Path("M10 14 L7 7 L13 9 L12 1 L18 7 L21 0 L24 7 L31 2 L28 12 L24 17 Z", KiGold, new Pen(KiGold, 1));
            DrawHead(d, 12, 9);
            Path("M19 24 L22 33 L11 43 M22 33 L34 43");
            if (scene < 21) Path("M19 25 L10 31 L8 36 M20 28 L30 32 L33 37");
            else if (scene < 29) Path("M19 25 L10 17 L9 5 M20 27 L31 16 L32 5");
            else Path("M19 25 L29 27 L40 22 M20 29 L31 30 L40 22");
            d.Pop();
            if (scene < 21) Label("GRRR!", new Point(26, 0), 7);
            else if (scene >= 38) Label("?!", new Point(32, 4), 10);
            return true;
        }

        // The defender gets up, puts on the cap, then lights a tire as the orb approaches.
        // Keep the defender on the left and the burning tire on the screen-right,
        // independent of the outer transform used for the earlier exchanges.
        if (mirrored) d.PushTransform(new MatrixTransform(-1, 0, 0, 1, Width, 0));
        DrawHead(d, 9, 11);
        Path("M16 26 L20 34 L12 43 M20 34 L29 43 M16 27 L27 28 L35 23");
        double capDrop = scene < 30 ? (30 - scene) * 9 : 0;
        d.PushTransform(new TranslateTransform(0, -capDrop));
        Path("M6 13 Q6 3 16 3 Q25 3 25 13 Z M23 12 H31 L25 15 H6 Z", CapRed, new Pen(CapRed, 1));
        Label("MST", new Point(8, 5), 7);
        d.Pop();
        if (scene >= 30)
        {
            double flicker = Math.Sin(scene * 20) * 2;
            d.DrawEllipse(Rubber, new Pen(Ground, 2), new Point(40, 39), 9, 5);
            d.DrawEllipse(null, new Pen(Ground, 1), new Point(40, 39), 4, 2);
            d.PushTransform(new TranslateTransform(0, flicker));
            Path("M33 37 Q29 31 35 25 L36 30 L41 19 L44 29 L48 24 Q52 33 46 37 Z", Fire, new Pen(Fire, 1));
            Path("M37 37 L39 28 L43 33 L44 28 L46 37 Z", KiGold, new Pen(KiGold, .5));
            d.Pop();
            for (int i = 0; i < 4; i++)
            {
                double rise = (scene * .65 + i / 4d) % 1;
                d.PushOpacity((1 - rise) * .6);
                d.DrawEllipse(Ground, null, new Point(40 + Math.Sin(rise * 6 + i) * 5, 27 - rise * 23), 3 + rise * 3, 3 + rise * 4);
                d.Pop();
            }
        }
        if (mirrored) d.Pop();
        return true;
    }
}
