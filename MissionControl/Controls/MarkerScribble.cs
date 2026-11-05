using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionControl.Controls
{
    public enum ScribbleKind
    {
        Circle,
        Underline
    }

    // A red felt-tip stroke that can draw itself in.
    public class MarkerScribble : Control
    {
        public static readonly StyledProperty<double> ProgressProperty =
            AvaloniaProperty.Register<MarkerScribble, double>(nameof(Progress), 1);

        private const int Segments = 96;

        private readonly ScribbleKind kind;
        private readonly int seed;
        private readonly Pen pen = new(Ink.Marker, 3.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        static MarkerScribble()
        {
            AffectsRender<MarkerScribble>(ProgressProperty);
        }

        public MarkerScribble(ScribbleKind kind, int seed = 7)
        {
            this.kind = kind;
            this.seed = seed;
            IsHitTestVisible = false;
        }

        public double Progress
        {
            get => GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        // Rubs the stroke out and draws it again.
        public void Replay()
        {
            Transitions = null;
            Progress = 0;
            Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = ProgressProperty,
                    Duration = TimeSpan.FromMilliseconds(650),
                    Easing = new CubicEaseOut()
                }
            };
            Progress = 1;
        }

        public override void Render(DrawingContext context)
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0 || Progress <= 0)
            {
                return;
            }

            List<Point> points = kind == ScribbleKind.Circle ? CirclePoints() : UnderlinePoints();
            int count = Math.Clamp((int)Math.Ceiling(points.Count * Progress), 2, points.Count);

            var stroke = new StreamGeometry();
            using (StreamGeometryContext line = stroke.Open())
            {
                line.BeginFigure(points[0], false);
                for (int i = 1; i < count; i++)
                {
                    line.LineTo(points[i]);
                }
                line.EndFigure(false);
            }
            context.DrawGeometry(null, pen, stroke);
        }

        // One and a bit turns of a wobbly ellipse, overshooting where the pen started.
        private List<Point> CirclePoints()
        {
            var random = new Random(seed);
            double phase1 = random.NextDouble() * Math.PI * 2;
            double phase2 = random.NextDouble() * Math.PI * 2;
            double cx = Bounds.Width / 2, cy = Bounds.Height / 2;
            double rx = Bounds.Width / 2 - 4, ry = Bounds.Height / 2 - 4;

            var points = new List<Point>(Segments + 1);
            for (int i = 0; i <= Segments; i++)
            {
                double t = (double)i / Segments;
                double angle = -2.1 + t * Math.PI * 2 * 1.14;
                double wobble = 1 + 0.025 * Math.Sin(angle * 3 + phase1) + 0.015 * Math.Sin(angle * 7 + phase2);
                double drift = 1 - 0.06 * t;
                points.Add(new Point(
                    cx + Math.Cos(angle) * rx * wobble * drift,
                    cy + Math.Sin(angle) * ry * wobble * drift - 3 * t));
            }
            return points;
        }

        // There and back again, the way a hurried editor underlines a word.
        private List<Point> UnderlinePoints()
        {
            var random = new Random(seed);
            double phase = random.NextDouble() * Math.PI * 2;
            double w = Bounds.Width - 4, h = Bounds.Height;

            var points = new List<Point>(Segments + 1);
            for (int i = 0; i <= Segments; i++)
            {
                double t = (double)i / Segments;
                double along = t < 0.62 ? t / 0.62 : 1 - (t - 0.62) / 0.38 * 0.8;
                double y = h * 0.35 + (t < 0.62 ? 0 : h * 0.4) + Math.Sin(along * 9 + phase) * 1.4 - along * 2;
                points.Add(new Point(2 + along * w, y));
            }
            return points;
        }
    }
}
