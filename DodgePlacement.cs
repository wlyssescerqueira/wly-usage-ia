using System;
using System.Linq;
using System.Windows;

namespace WlyUsage;

internal static class DodgePlacement
{
    // Alterna para a borda oposta; alternativas: topo, direita, esquerda e rodapé.
    public static Point? Choose(Rect screen, Rect panel, Point cursor)
    {
        double left = screen.Left + 12, right = screen.Right - panel.Width - 12;
        double top = screen.Top + 12, bottom = screen.Bottom - panel.Height - 12;
        if (right < left || bottom < top) return null;
        var currentEdge = new[] {
            (Edge: 0, Distance: Math.Abs(panel.Top - screen.Top)),
            (Edge: 1, Distance: Math.Abs(screen.Right - panel.Right)),
            (Edge: 2, Distance: Math.Abs(panel.Left - screen.Left)),
            (Edge: 3, Distance: Math.Abs(screen.Bottom - panel.Bottom))
        }.OrderBy(edge => edge.Distance).First().Edge;
        int opposite = currentEdge switch { 0 => 3, 3 => 0, 1 => 2, _ => 1 };
        var candidates = new[] {
            (Edge: 0, Position: new Point((left + right) / 2, top)),
            (Edge: 1, Position: new Point(right, (top + bottom) / 2)),
            (Edge: 2, Position: new Point(left, (top + bottom) / 2)),
            (Edge: 3, Position: new Point((left + right) / 2, bottom))
        };
        return candidates.Where(candidate =>
        {
            var occupied = new Rect(candidate.Position, panel.Size); occupied.Inflate(48, 48);
            return !occupied.Contains(cursor);
        }).OrderBy(candidate => candidate.Edge == opposite ? -1 : candidate.Edge)
          .Select(candidate => (Point?)candidate.Position).FirstOrDefault();
    }
    public static void Verify()
    {
        foreach (var screen in new[] { new Rect(0, 0, 1920, 1080), new Rect(-1920, 0, 1920, 1080), new Rect(0, 0, 800, 600) })
        {
            var panel = new Rect(screen.Left + 12, screen.Top + 12, 680, 76);
            var cursor = new Point(panel.Left + 340, panel.Top + 38);
            var chosen = Choose(screen, panel, cursor) ?? throw new InvalidOperationException("Nenhuma posição livre");
            var result = new Rect(chosen, panel.Size);
            if (!screen.Contains(result)) throw new InvalidOperationException("Painel fora do monitor");
            result.Inflate(48, 48);
            if (result.Contains(cursor)) throw new InvalidOperationException("Painel continua sobre o mouse");
            double centerX = screen.Left + (screen.Width - 680) / 2;
            double centerY = screen.Top + (screen.Height - 76) / 2;
            var positions = new[] {
                (From: new Point(centerX, screen.Top + 12), To: new Point(centerX, screen.Bottom - 76 - 12)),
                (From: new Point(centerX, screen.Bottom - 76 - 12), To: new Point(centerX, screen.Top + 12)),
                (From: new Point(screen.Right - 680 - 12, centerY), To: new Point(screen.Left + 12, centerY)),
                (From: new Point(screen.Left + 12, centerY), To: new Point(screen.Right - 680 - 12, centerY))
            };
            foreach (var position in positions)
            {
                var from = new Rect(position.From, panel.Size);
                var mouse = new Point(from.Left + from.Width / 2, from.Top + from.Height / 2);
                var expectedArea = new Rect(position.To, panel.Size); expectedArea.Inflate(48, 48);
                // Em telas estreitas, a borda oposta pode continuar sob o mouse: deve usar outra borda.
                if (!expectedArea.Contains(mouse) && Choose(screen, from, mouse) != position.To)
                    throw new InvalidOperationException("Borda oposta não foi priorizada");
            }
        }
        if (Choose(new Rect(0, 0, 300, 100), new Rect(0, 0, 680, 76), new Point(10, 10)) != null)
            throw new InvalidOperationException("Monitor pequeno aceitou painel maior");
    }
}
