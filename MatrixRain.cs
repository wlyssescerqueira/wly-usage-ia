using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace WlyUsage;

// Indicador decorativo: não exibe tokens nem conteúdo privado das conversas.
internal sealed class MatrixRain : FrameworkElement
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Typeface font = new("Consolas");
    private readonly SolidColorBrush trail = new(Color.FromRgb(49, 166, 99));
    private readonly SolidColorBrush head = new(Color.FromRgb(166, 255, 193));
    private const string Glyphs = "01アイウエカキクケサシスセタチツテナニヌネ";
    private int frame;
    private bool active;

    public MatrixRain()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        Focusable = false;
        Visibility = Visibility.Collapsed;
        timer.Tick += (_, _) => { frame++; InvalidateVisual(); };
        IsVisibleChanged += (_, _) => UpdateTimer();
        Loaded += (_, _) => UpdateTimer();
        Unloaded += (_, _) => timer.Stop();
    }

    public void SetWorking(bool working)
    {
        active = working;
        Visibility = working ? Visibility.Visible : Visibility.Collapsed;
        UpdateTimer();
        InvalidateVisual();
    }

    private void UpdateTimer()
    {
        if (active && IsVisible && IsLoaded && SystemParameters.ClientAreaAnimation) timer.Start();
        else timer.Stop();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (!active) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        for (int column = 0; column * 14 < ActualWidth; column++)
        {
            int step = frame / (1 + column % 3);
            double y = (step * 5 + column * 37) % (ActualHeight + 100);
            for (int row = 0; row < 9; row++)
            {
                var glyph = Glyphs[(step + column * 7 + row * 3) % Glyphs.Length].ToString();
                var text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    font, 12, row == 0 ? head : trail, dpi);
                drawing.PushOpacity(row == 0 ? .95 : .8 - row * .08);
                drawing.DrawText(text, new Point(column * 14, y - row * 14));
                drawing.Pop();
            }
        }
    }
}
