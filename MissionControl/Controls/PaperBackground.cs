using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MissionControl.Controls
{
    // Builds a tiling photocopied-paper texture in code: soft mottling, fine grain and a few fibres.
    public static class PaperBackground
    {
        private const int Size = 256;

        public static ImageBrush CreateBrush()
        {
            var bitmap = new WriteableBitmap(new PixelSize(Size, Size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (ILockedFramebuffer buffer = bitmap.Lock())
            {
                Marshal.Copy(BuildPixels(), 0, buffer.Address, Size * Size);
            }
            return new ImageBrush(bitmap)
            {
                TileMode = TileMode.Tile,
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                DestinationRect = new RelativeRect(0, 0, Size, Size, RelativeUnit.Absolute)
            };
        }

        private static int[] BuildPixels()
        {
            var random = new Random(14);
            double[] shade = new double[Size * Size];

            // Random coarse grid, smoothed with wrap-around so the tile has no seam.
            const int cell = 32;
            int grid = Size / cell;
            double[] coarse = new double[grid * grid];
            for (int i = 0; i < coarse.Length; i++)
            {
                coarse[i] = random.NextDouble() * 5 - 2.5;
            }

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    double gx = (double)x / cell, gy = (double)y / cell;
                    int x0 = (int)gx, y0 = (int)gy;
                    double fx = Smooth(gx - x0), fy = Smooth(gy - y0);
                    double top = Lerp(coarse[y0 * grid + x0], coarse[y0 * grid + (x0 + 1) % grid], fx);
                    double bottom = Lerp(coarse[(y0 + 1) % grid * grid + x0], coarse[(y0 + 1) % grid * grid + (x0 + 1) % grid], fx);
                    shade[y * Size + x] = Lerp(top, bottom, fy) + (random.NextDouble() - 0.5) * 5;
                }
            }

            for (int fibre = 0; fibre < 60; fibre++)
            {
                double x = random.NextDouble() * Size, y = random.NextDouble() * Size;
                double angle = random.NextDouble() * Math.PI * 2;
                double ink = random.NextDouble() < 0.5 ? -7 : 5;
                int length = random.Next(6, 22);
                for (int step = 0; step < length; step++)
                {
                    int px = ((int)x % Size + Size) % Size, py = ((int)y % Size + Size) % Size;
                    shade[py * Size + px] += ink;
                    angle += (random.NextDouble() - 0.5) * 0.5;
                    x += Math.Cos(angle);
                    y += Math.Sin(angle);
                }
            }

            int[] pixels = new int[Size * Size];
            Color paper = Ink.PaperColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                int r = Channel(paper.R + shade[i]);
                int g = Channel(paper.G + shade[i]);
                int b = Channel(paper.B + shade[i]);
                pixels[i] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
            }
            return pixels;
        }

        private static int Channel(double value) => (int)Math.Clamp(value, 0, 255);

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static double Smooth(double t) => t * t * (3 - 2 * t);
    }
}
