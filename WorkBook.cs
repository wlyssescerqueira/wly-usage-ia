using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WlyUsage;

internal static class WorkBook
{
    private static readonly Brush Cover = new SolidColorBrush(Color.FromRgb(0x16, 0x35, 0x58));
    private static readonly Brush Edge = new SolidColorBrush(Color.FromRgb(0x39, 0x58, 0x78));
    private static readonly Brush Gold = new SolidColorBrush(Color.FromRgb(0xe3, 0xc4, 0x7b));

    // A miniature of the familiar blue paper work booklet, shared by hand and flight.
    public static void Draw(DrawingContext d, Point center, double scale, double pixelsPerDip)
    {
        d.PushTransform(new TranslateTransform(center.X, center.Y));
        d.PushTransform(new ScaleTransform(scale, scale));
        d.DrawRoundedRectangle(Cover, new Pen(Edge, 1), new Rect(-12, -16, 24, 32), 1.5, 1.5);
        d.DrawLine(new Pen(Edge, .7), new Point(-9, -14), new Point(-9, 14));
        d.DrawLine(new Pen(Gold, .5), new Point(-7, 13), new Point(9, 13));
        d.DrawGeometry(null, new Pen(Gold, .6), Geometry.Parse("M0 -12 L1 -9 L4 -9 L2 -7 L3 -4 L0 -6 L-3 -4 L-2 -7 L-4 -9 L-1 -9 Z M-5 -8 Q-6 -3 0 -2 Q6 -3 5 -8"));
        void Label(string value, double size, double y)
        {
            var text = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), size, Gold, pixelsPerDip);
            d.DrawText(text, new Point(1 - text.Width / 2, y));
        }
        Label("CARTEIRA", 3.5, 0);
        Label("DE TRABALHO", 3.1, 4);
        Label("E PREVIDÊNCIA SOCIAL", 1.8, 9);
        d.Pop(); d.Pop();
    }
}
