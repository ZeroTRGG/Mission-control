using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionControl.Controls
{
    // A page section under a 2px rule. On wide pages the tag sits in the first of three
    // grid columns and the content fills the other two, as on the reference; otherwise the tag stacks on top.
    public class SectionPanel : DrawnPanel
    {
        private const double WideFrom = 860;
        private const double GridGap = 40;
        private const double PaddingY = 28;
        private const double StackGap = 14;

        private readonly Control tag;
        private readonly Control content;
        private readonly IBrush rule;

        public SectionPanel(Control tag, Control content, IBrush? rule = null)
        {
            this.tag = tag;
            this.content = content;
            this.rule = rule ?? Ink.Black;
            Children.Add(tag);
            Children.Add(content);
        }

        private static double TagWidth(double width) => (width - GridGap * 2) / 3;

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
            if (width >= WideFrom)
            {
                double tagWidth = TagWidth(width);
                tag.Measure(new Size(tagWidth, double.PositiveInfinity));
                content.Measure(new Size(width - tagWidth - GridGap, double.PositiveInfinity));
                return new Size(width, Math.Max(tag.DesiredSize.Height, content.DesiredSize.Height) + PaddingY * 2);
            }

            tag.Measure(new Size(width, double.PositiveInfinity));
            content.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, tag.DesiredSize.Height + StackGap + content.DesiredSize.Height + PaddingY * 2);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double width = finalSize.Width;
            if (width >= WideFrom)
            {
                double tagWidth = TagWidth(width);
                tag.Arrange(new Rect(0, PaddingY, tagWidth, tag.DesiredSize.Height));
                content.Arrange(new Rect(tagWidth + GridGap, PaddingY, width - tagWidth - GridGap, content.DesiredSize.Height));
            }
            else
            {
                tag.Arrange(new Rect(0, PaddingY, width, tag.DesiredSize.Height));
                content.Arrange(new Rect(0, PaddingY + tag.DesiredSize.Height + StackGap, width, content.DesiredSize.Height));
            }
            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.DrawLine(new Pen(rule, 2), new Point(0, 1), new Point(Bounds.Width, 1));
        }
    }

    // A 2px dotted rule between rows.
    public class DottedRule : Control
    {
        private static readonly Pen Dots = new(Ink.Black, 2, new DashStyle(new double[] { 0, 2 }, 0), PenLineCap.Round);

        public DottedRule()
        {
            Height = 2;
        }

        public override void Render(DrawingContext context)
        {
            context.DrawLine(Dots, new Point(1, 1), new Point(Bounds.Width, 1));
        }
    }
}
