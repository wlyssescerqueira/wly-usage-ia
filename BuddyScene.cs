using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WlyUsage;

// One shared timeline coordinates both characters and the projectile between cards.
internal sealed class BuddyScene : FrameworkElement
{
    private readonly Grid agents;
    private static readonly DependencyProperty ClockProperty = DependencyProperty.Register(
        "Clock", typeof(double), typeof(BuddyScene), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly Brush ToolInk = new SolidColorBrush(Color.FromRgb(0x84, 0x90, 0xff));

    public BuddyScene(Grid agents)
    {
        this.agents = agents; IsHitTestVisible = false;
        Loaded += (_, _) => BeginAnimation(ClockProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(60))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromSeconds(-(DateTimeOffset.UtcNow.TimeOfDay.TotalSeconds % 60))
        });
        Unloaded += (_, _) => BeginAnimation(ClockProperty, null);
    }

    private static IEnumerable<ActivityCharacter> Characters(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ActivityCharacter character) yield return character;
            else foreach (var nested in Characters(child)) yield return nested;
        }
    }

    protected override void OnRender(DrawingContext drawing)
    {
        var characters = Characters(agents).ToArray();
        foreach (var character in characters) character.BuddyPhase = -1;
        var worker = characters.FirstOrDefault(c => c.Status.StartsWith("Trabalhando", StringComparison.Ordinal));
        var sleeper = characters.FirstOrDefault(c => c.Status == "Sem atividade recente");
        double phase = (double)GetValue(ClockProperty) * 60 - 36;
        // Fixed phases make the existing --preview useful for inspecting the whole scene.
        if (Environment.GetCommandLineArgs().Contains("--preview"))
        {
            var arg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--buddy-phase="));
            if (arg != null && double.TryParse(arg.Split('=')[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double sample)) phase = sample;
        }
        if (worker != null && sleeper != null && phase >= 0 && phase < 22)
        {
            var start = worker.TranslatePoint(new Point(30, 18), this);
            var end = sleeper.TranslatePoint(new Point(16, 24), this);
            worker.BuddyPhase = sleeper.BuddyPhase = phase;
            worker.BuddyOnRight = end.X > start.X; sleeper.BuddyOnRight = start.X > end.X;
            if (phase >= 16 && phase < 17.5)
            {
                // The sleeper returns a work booklet instead of another weapon.
                double flight = (phase - 16) / 1.5;
                var hand = sleeper.TranslatePoint(new Point(sleeper.BuddyOnRight ? 17 : 39, 20), this);
                var recipient = worker.TranslatePoint(new Point(worker.BuddyOnRight ? 22 : 34, 24), this);
                var point = new Point(hand.X + (recipient.X - hand.X) * flight,
                    hand.Y + (recipient.Y - hand.Y) * flight - Math.Sin(flight * Math.PI) * 6);
                drawing.PushTransform(new TranslateTransform(point.X, point.Y));
                drawing.PushTransform(new RotateTransform(Math.Sin(flight * Math.PI * 2) * 18));
                WorkBook.Draw(drawing, new Point(), 1, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                drawing.Pop(); drawing.Pop();
            }
            if (phase >= 9 && phase < 12)
            {
                var charge = worker.TranslatePoint(new Point(worker.BuddyOnRight ? 12 : 44, 29), this);
                double power = (phase - 9) / 3;
                drawing.DrawEllipse(null, new Pen(ToolInk, 1.5), charge, 4 + power * 7, 4 + power * 7);
                drawing.DrawEllipse(Brushes.White, null, charge, 2 + power * 3, 2 + power * 3);
                for (int i = 0; i < 6; i++)
                {
                    double angle = i * Math.PI / 3 + phase * 4;
                    drawing.DrawLine(new Pen(ToolInk, 1),
                        new Point(charge.X + Math.Cos(angle) * 13, charge.Y + Math.Sin(angle) * 13),
                        new Point(charge.X + Math.Cos(angle) * 18, charge.Y + Math.Sin(angle) * 18));
                }
            }
            if (phase >= 12 && phase < 13.5)
            {
                var origin = worker.TranslatePoint(new Point(worker.BuddyOnRight ? 40 : 16, 24), this);
                double travel = Math.Min(1, (phase - 12) / .35);
                var target = new Point(origin.X + (end.X - origin.X) * travel, end.Y);
                double width = 8 + Math.Sin((phase - 12) * 22) * 2;
                drawing.DrawLine(new Pen(ToolInk, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, origin, target);
                drawing.DrawLine(new Pen(Brushes.White, 3), origin, target);
                drawing.DrawEllipse(ToolInk, null, target, 10, 13);
                drawing.DrawEllipse(Brushes.White, null, target, 4, 7);
            }
            if (phase >= 4 && phase < 5.3)
            {
                double flight = (phase - 4) / 1.3;
                var point = new Point(start.X + (end.X - start.X) * flight,
                    start.Y + (end.Y - start.Y) * flight - Math.Sin(flight * Math.PI) * 12);
                drawing.PushTransform(new TranslateTransform(point.X, point.Y));
                drawing.PushTransform(new RotateTransform(flight * 720));
                drawing.DrawLine(new Pen(Brushes.White, 2), new Point(0, -8), new Point(0, 8));
                drawing.DrawRoundedRectangle(ToolInk, null, new Rect(-7, -9, 14, 6), 1, 1);
                drawing.Pop(); drawing.Pop();
            }
            if (phase >= 5.3 && phase < 5.7)
            {
                for (int i = 0; i < 5; i++)
                {
                    double angle = i * Math.PI * 2 / 5;
                    drawing.DrawLine(new Pen(ToolInk, 1.5),
                        new Point(end.X + Math.Cos(angle) * 9, end.Y + Math.Sin(angle) * 9),
                        new Point(end.X + Math.Cos(angle) * 14, end.Y + Math.Sin(angle) * 14));
                }
            }
        }
        foreach (var character in characters) character.InvalidateVisual();
    }
}
