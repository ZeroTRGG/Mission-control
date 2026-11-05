using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionControl.Controls
{
    // A printer's dot screen on a 45 degree grid, fading out from the middle.
    // The dots show through the hollow masthead letters.
    public class HalftoneBlock : Control
    {
        private const double Pitch = 7;
        private static readonly double Rotate = Math.PI / 4;

        private StreamGeometry? dots;
        private Size builtFor;

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            dots = null;
        }

        public override void Render(DrawingContext context)
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }
            if (dots == null || builtFor != Bounds.Size)
            {
                builtFor = Bounds.Size;
                dots = BuildDots(Bounds.Size);
            }
            context.DrawGeometry(Ink.Black, null, dots);
        }

        private static StreamGeometry BuildDots(Size size)
        {
            var geometry = new StreamGeometry();
            using StreamGeometryContext pen = geometry.Open();

            double cx = size.Width * 0.5, cy = size.Height * 0.5;
            double rx = size.Width * 0.5, ry = size.Height * 0.72;
            double reach = Math.Sqrt(size.Width * size.Width + size.Height * size.Height);
            double cos = Math.Cos(Rotate), sin = Math.Sin(Rotate);

            for (double u = -reach; u < reach; u += Pitch)
            {
                for (double v = -reach; v < reach; v += Pitch)
                {
                    double x = cx + u * cos - v * sin;
                    double y = cy + u * sin + v * cos;
                    if (x < 0 || y < 0 || x > size.Width || y > size.Height)
                    {
                        continue;
                    }

                    double dx = (x - cx) / rx, dy = (y - cy) / ry;
                    double strength = 1 - Math.Sqrt(dx * dx + dy * dy);
                    if (strength <= 0.04)
                    {
                        continue;
                    }
                    AddDot(pen, x, y, Pitch * 0.36 * Math.Pow(strength, 0.75));
                }
            }
            return geometry;
        }

        // Eight straight sides look round at this size and keep thousands of dots cheap to draw.
        private static void AddDot(StreamGeometryContext pen, double x, double y, double radius)
        {
            pen.BeginFigure(new Point(x + radius, y), true);
            for (int i = 1; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                pen.LineTo(new Point(x + radius * Math.Cos(angle), y + radius * Math.Sin(angle)));
            }
            pen.EndFigure(true);
        }
    }
}
