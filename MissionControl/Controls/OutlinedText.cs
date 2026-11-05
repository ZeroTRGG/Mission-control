using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionControl.Controls
{
    // Oversized hollow lettering, scaled so the word exactly fills the available width.
    public class OutlinedText : Control
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<OutlinedText, string>(nameof(Text), "");

        private const double ReferenceEmSize = 200;

        private Geometry? letters;
        private Rect lettersBounds;
        private double strokeWidth = 2;
        private double builtForWidth = -1;

        static OutlinedText()
        {
            AffectsMeasure<OutlinedText>(TextProperty);
        }

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
            Build(width);
            return new Size(width, lettersBounds.Height + strokeWidth);
        }

        private void Build(double width)
        {
            if (width == builtForWidth && letters != null)
            {
                return;
            }
            builtForWidth = width;
            strokeWidth = Math.Clamp(width / 240, 2, 4);

            var face = new Typeface(Faces.Display, FontStyle.Normal, FontWeight.Black);
            double measured = Format(face, ReferenceEmSize).WidthIncludingTrailingWhitespace;
            double emSize = ReferenceEmSize * (width - strokeWidth) / Math.Max(measured, 1);

            letters = Format(face, emSize).BuildGeometry(new Point(0, 0));
            if (letters != null)
            {
                lettersBounds = letters.Bounds;
                // The measured advance is a little wider than the ink, so scale once more to the ink itself.
                double correction = (width - strokeWidth) / Math.Max(lettersBounds.Width, 1);
                letters = Format(face, emSize * correction).BuildGeometry(new Point(0, 0));
                lettersBounds = letters?.Bounds ?? default;
            }
        }

        private FormattedText Format(Typeface face, double size)
        {
            return new FormattedText(Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, Ink.Black);
        }

        public override void Render(DrawingContext context)
        {
            if (letters == null)
            {
                return;
            }
            var pen = new Pen(Ink.Black, strokeWidth, lineJoin: PenLineJoin.Round);
            Matrix toTopLeft = Matrix.CreateTranslation(strokeWidth / 2 - lettersBounds.X, strokeWidth / 2 - lettersBounds.Y);
            using (context.PushTransform(toTopLeft))
            {
                context.DrawGeometry(null, pen, letters);
            }
        }
    }
}
