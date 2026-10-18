using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WlyUsage;

// Small vector cartoons stay sharp at every Windows display scale.
internal sealed class ActivityCharacter : FrameworkElement
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(string), typeof(ActivityCharacter),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender, OnStatusChanged));

    public string Status
    {
        get => (string)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public ActivityCharacter()
    {
        Width = 32;
        Height = 24;
        Margin = new Thickness(8, 0, 0, 0);
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
    }

    private static void OnStatusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var icon = (ActivityCharacter)sender;
        icon.Visibility = icon.Status.StartsWith("Trabalhando", StringComparison.Ordinal)
            || icon.Status == "Sem atividade recente" ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        var ink = new SolidColorBrush(Color.FromRgb(0xa1, 0xa9, 0xbd));
        var accent = new SolidColorBrush(Color.FromRgb(0x84, 0x90, 0xff));
        var pen = new Pen(ink, 1.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        void Path(string data, Pen stroke) => drawing.DrawGeometry(null, stroke, Geometry.Parse(data));

        if (Status.StartsWith("Trabalhando", StringComparison.Ordinal))
        {
            // Hard hat, bent knees, both hands on the pickaxe, and a chipped rock.
            drawing.DrawEllipse(null, pen, new Point(10, 7), 3, 3);
            Path("M6 6 Q10 0 14 6 M5 6 H15 M10 2 V4", pen);
            Path("M10 10 L13 15 L8 18 L6 22 M13 15 L17 21 H20 M11 11 L17 12 L22 8 M13 14 L19 14 L23 9", pen);
            Path("M19 3 L28 17 M15 6 Q21 0 28 3", new Pen(accent, 1.7));
            Path("M23 22 L25 18 L29 19 L31 22 Z M29 13 L31 11 M24 16 L23 14", pen);
        }
        else if (Status == "Sem atividade recente")
        {
            // Slumped over a pillow with closed eyes and a tiny snore.
            Path("M2 20 H29 M3 19 Q2 14 8 15 L13 18 M13 18 L19 15 L24 19 M18 19 L23 22 H28", pen);
            drawing.DrawEllipse(null, pen, new Point(9, 12), 4, 3.5);
            Path("M6 12 L8 13 L9 12 M11 12 L12 13 M5 17 L13 18", pen);
            var zzz = new FormattedText("zZz", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 9, accent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawing.DrawText(zzz, new Point(16, 0));
        }
    }
}
