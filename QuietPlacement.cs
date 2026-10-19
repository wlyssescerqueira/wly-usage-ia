using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Point = System.Windows.Point;

namespace WlyUsage;

internal static class QuietPlacement
{
    // Captura apenas quando solicitado. Bitmap e pixels vivem só em memória; nada é gravado ou enviado.
    public static Point? CaptureAndChoose(Rect area, Rect panel, Point cursor, Rectangle pixels)
    {
        try
        {
            using var capture = new Bitmap(pixels.Width, pixels.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(capture))
                graphics.CopyFromScreen(pixels.Location, System.Drawing.Point.Empty, pixels.Size, CopyPixelOperation.SourceCopy);
            int width = Math.Min(800, pixels.Width);
            int height = Math.Max(1, (int)Math.Round(pixels.Height * (double)width / pixels.Width));
            using var small = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(small)) graphics.DrawImage(capture, 0, 0, width, height);
            var data = small.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var rgb = new byte[data.Stride * height];
            try { Marshal.Copy(data.Scan0, rgb, 0, rgb.Length); }
            finally { small.UnlockBits(data); }
            var gray = new byte[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    int offset = y * data.Stride + x * 4;
                    gray[y * width + x] = (byte)((rgb[offset + 2] * 77 + rgb[offset + 1] * 150 + rgb[offset] * 29) >> 8);
                }
            return Choose(gray, width, height, area, panel, cursor);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or ExternalException or ArgumentException or InvalidOperationException)
        { return null; }
    }

    internal static Point? Choose(byte[] gray, int width, int height, Rect area, Rect panel, Point cursor)
    {
        double left = area.Left + 12, top = area.Top + 12;
        double right = area.Right - panel.Width - 12, bottom = area.Bottom - panel.Height - 12;
        if (right < left || bottom < top || width < 2 || height < 2) return null;
        Point? best = null; double bestScore = double.MaxValue;
        // Passos curtos no alto permitem descer abaixo de barras de título, abas e ferramentas.
        var rows = Enumerable.Range(0, 9).Select(row => top + (bottom - top) * row / 8)
            .Concat(new[] { 32d, 56d, 80d, 104d, 128d }.Select(offset => area.Top + offset))
            .Where(y => y >= top && y <= bottom).Distinct().OrderBy(y => y);
        foreach (double y in rows) for (int column = 0; column <= 6; column++)
            {
                var candidate = new Rect(new Point(left + (right - left) * column / 6, y), panel.Size);
                var safe = candidate; safe.Inflate(64, 64);
                if (safe.Contains(cursor)) continue;
                // Evita avaliar o próprio painel se algum método de captura não respeitar a exclusão.
                if (candidate.IntersectsWith(panel)) continue;
                int x0 = Math.Clamp((int)((candidate.Left - area.Left) / area.Width * width), 0, width - 2);
                int y0 = Math.Clamp((int)((candidate.Top - area.Top) / area.Height * height), 0, height - 2);
                int x1 = Math.Clamp((int)Math.Ceiling((candidate.Right - area.Left) / area.Width * width), x0 + 1, width - 1);
                int y1 = Math.Clamp((int)Math.Ceiling((candidate.Bottom - area.Top) / area.Height * height), y0 + 1, height - 1);
                double density = EdgeDensity(gray, width, x0, y0, x1, y1, out double busiestTile);
                // Uma região só é aceita se for homogênea inclusive nos pequenos blocos internos.
                if (density > .065 || busiestTile > .24) continue;
                // Centro superior seguro primeiro, inclusive abaixo das barras do aplicativo.
                // Depois as demais bordas; o miolo continua como alternativa.
                bool upperCenter = column == 3 && y <= Math.Min(bottom, area.Top + 128);
                double edgePreference = upperCenter ? 0 : y == top || y == bottom || column == 0 || column == 6 ? 1 : 2;
                double score = density + busiestTile * .12 + edgePreference + (upperCenter ? (y - top) * .00001 : 0);
                if (score < bestScore) { bestScore = score; best = candidate.TopLeft; }
            }
        return best;
    }
    private static double EdgeDensity(byte[] pixels, int stride, int x0, int y0, int x1, int y1, out double busiest)
    {
        int edges = 0, samples = 0; busiest = 0;
        for (int ty = y0; ty < y1; ty += 8) for (int tx = x0; tx < x1; tx += 16)
            {
                int tileEdges = 0, tileSamples = 0;
                for (int y = ty; y < Math.Min(ty + 8, y1); y++) for (int x = tx; x < Math.Min(tx + 16, x1); x++)
                    {
                        int value = pixels[y * stride + x];
                        if (Math.Abs(value - pixels[y * stride + x + 1]) > 14 || Math.Abs(value - pixels[(y + 1) * stride + x]) > 14) tileEdges++;
                        tileSamples++;
                    }
                edges += tileEdges; samples += tileSamples;
                busiest = Math.Max(busiest, (double)tileEdges / tileSamples);
            }
        return samples == 0 ? 1 : (double)edges / samples;
    }
    public static void Verify()
    {
        const int width = 800, height = 450;
        var area = new Rect(-1920, 0, 1920, 1080);
        var panel = new Rect(-1300, 500, 680, 76);
        var cursor = new Point(-1000, 530);
        var blank = Enumerable.Repeat((byte)220, width * height).ToArray();
        var position = Choose(blank, width, height, area, panel, cursor) ?? throw new InvalidOperationException("Área vazia rejeitada");
        var result = new Rect(position, panel.Size);
        if (!area.Contains(result) || result.Contains(cursor)) throw new InvalidOperationException("Posição insegura");
        // Bordas levemente marcadas continuam preferíveis ao centro completamente vazio.
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) blank[y * width + x] = (byte)(x % 100 == 0 ? 190 : 220);
        for (int y = 100; y < 350; y++) for (int x = 150; x < 650; x++) blank[y * width + x] = 220;
        var edge = Choose(blank, width, height, area, panel, cursor) ?? throw new InvalidOperationException("Bordas seguras rejeitadas");
        if (edge.X != area.Left + 12 && edge.X != area.Right - panel.Width - 12 && edge.Y != area.Top + 12 && edge.Y != area.Bottom - panel.Height - 12)
            throw new InvalidOperationException("Centro priorizado sobre borda segura");
        var noisy = new byte[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) noisy[y * width + x] = (byte)((x + y) % 2 * 255);
        if (Choose(noisy, width, height, area, panel, cursor) != null) throw new InvalidOperationException("Área densa aceita");
        var centerOnly = (byte[])noisy.Clone();
        for (int y = 100; y < 350; y++) for (int x = 150; x < 650; x++) centerOnly[y * width + x] = 220;
        if (Choose(centerOnly, width, height, area, panel, cursor) == null) throw new InvalidOperationException("Centro seguro rejeitado quando bordas estão ocupadas");
        // Só uma faixa superior fica limpa: ela deve vencer o restante da tela.
        Array.Fill(noisy, (byte)220, 0, width * 45);
        if (Choose(noisy, width, height, area, panel, cursor)?.Y != area.Top + 12) throw new InvalidOperationException("Faixa vazia não priorizada");
        // Barra superior ocupada: centraliza logo abaixo dela, em vez de ir para um canto.
        var toolbar = Enumerable.Repeat((byte)220, width * height).ToArray();
        for (int y = 0; y < 40; y++) for (int x = 0; x < width; x++) toolbar[y * width + x] = (byte)((x + y) % 2 * 255);
        var belowToolbar = Choose(toolbar, width, height, area, panel, cursor) ?? throw new InvalidOperationException("Área abaixo da barra rejeitada");
        if (belowToolbar.X != area.Left + (area.Width - panel.Width) / 2 || belowToolbar.Y < 96 || belowToolbar.Y > 128)
            throw new InvalidOperationException("Centro abaixo da barra não priorizado");
    }
}
