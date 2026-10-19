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
            d.DrawEllipse(null, new Pen(Barrier, 3), wall, 5, 23);
            d.DrawEllipse(null, new Pen(Brushes.White, 1), wall, 2, 21);
            for (int i = 0; i < 5; i++)
            {
                double y = wall.Y - 19 + ((phase * 15 + i * 8) % 38);
                d.DrawLine(new Pen(Barrier, 1.4), new Point(wall.X - 3, y), new Point(wall.X + 4, y - 3));
            }
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
