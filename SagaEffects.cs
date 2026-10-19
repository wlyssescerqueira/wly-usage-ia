using System;
using System.Windows;
using System.Windows.Media;

namespace WlyUsage;

internal static class SagaEffects
{
    private static readonly Brush Energy = new SolidColorBrush(Color.FromRgb(0x84, 0xb8, 0xff));
    private static readonly Brush Barrier = new SolidColorBrush(Color.FromRgb(0xff, 0x93, 0x35));

    public static void Draw(DrawingContext d, ActivityCharacter worker, ActivityCharacter sleeper, Visual surface, double phase)
    {
        if (phase < 21) return;
        double direction = worker.BuddyOnRight ? 1 : -1;
        var origin = worker.TranslatePoint(new Point(23, 19), (UIElement)surface);
        origin.X += direction * 42;
        var wall = sleeper.TranslatePoint(new Point(sleeper.BuddyOnRight ? 60 : -4, 23), (UIElement)surface);

        if (phase >= 30 && phase < 40)
        {
            double strength = Math.Clamp((phase - 30) / 1.2, 0, 1);
            d.PushOpacity(strength * (phase > 38 ? (40 - phase) / 2 : 1));
            // The tire fire spreads along the ground and rises into the blocking flames.
            var tire = sleeper.TranslatePoint(new Point(40, 39), (UIElement)surface);
            d.DrawLine(new Pen(Barrier, 3), tire, new Point(wall.X, wall.Y + 19));
            d.PushTransform(new TranslateTransform(wall.X, wall.Y));
            d.PushTransform(new ScaleTransform(1, .94 + Math.Sin(phase * 19) * .06, 0, 21));
            d.DrawGeometry(Barrier, null, Geometry.Parse("M-7 21 Q-13 12 -6 2 L-8 -8 L-2 -3 Q-7 -15 1 -23 Q0 -11 5 -7 L8 -16 Q13 -4 8 4 Q15 12 7 21 Z"));
            d.DrawGeometry(Brushes.Gold, null, Geometry.Parse("M-3 21 Q-8 12 -2 5 L0 -8 L4 3 Q9 12 3 21 Z"));
            d.Pop(); d.Pop();
            d.Pop();
        }
        if (phase >= 38) return;
        double radius = 4 + Math.Clamp((phase - 21) / 8, 0, 1) * 14;
        var center = origin;
        if (phase >= 29)
        {
            // Stop the near edge of the sphere at the barrier, never beyond it.
            double travel = Math.Clamp((phase - 29) / 5, 0, 1);
            double stoppedX = wall.X - direction * (radius + 5);
            center.X = origin.X + (stoppedX - origin.X) * travel;
            center.Y += (wall.Y - origin.Y) * travel;
            if (phase >= 34) center.X -= direction * Math.Abs(Math.Sin(phase * 18)) * 2;
        }
        d.PushOpacity(.25);
        d.DrawEllipse(Energy, null, center, radius + 4, radius + 4);
        d.Pop();
        d.DrawEllipse(Energy, new Pen(Brushes.White, 1.4), center, radius, radius);
        d.DrawEllipse(Brushes.White, null, center, radius * .65, radius * .65);
        for (int i = 0; i < 6; i++)
        {
            double angle = phase * 3 + i * Math.PI / 3;
            d.DrawLine(new Pen(Energy, 1.2),
                new Point(center.X + Math.Cos(angle) * (radius + 3), center.Y + Math.Sin(angle) * (radius + 3)),
                new Point(center.X + Math.Cos(angle) * (radius + 7), center.Y + Math.Sin(angle) * (radius + 7)));
        }
        if (phase >= 34)
        {
            for (int i = 0; i < 5; i++)
            {
                double y = wall.Y + Math.Sin(phase * 13 + i) * 18;
                d.DrawLine(new Pen(Barrier, 1.6), new Point(wall.X - direction * 5, y),
                    new Point(wall.X - direction * (10 + i * 2), y - 4));
            }
        }
    }
}
