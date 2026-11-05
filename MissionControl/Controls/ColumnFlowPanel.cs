using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionControl.Controls
{
    // Newspaper columns: whole blocks flow down one column and into the next, in reading
    // order, split so the columns finish at about the same height. Column count follows width.
    public class ColumnFlowPanel : DrawnPanel
    {
        public double MinColumnWidth { get; set; } = 220;
        public int MaxColumns { get; set; } = 2;
        public double ColumnGap { get; set; } = 40;
        public double BlockGap { get; set; } = 14;

        private int columns = 1;
        private double columnWidth;
        private List<int> columnStarts = new() { 0 };

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? MinColumnWidth : availableSize.Width;
            int fit = (int)Math.Floor((width + ColumnGap) / (MinColumnWidth + ColumnGap));
            columns = Math.Clamp(fit, 1, Math.Max(1, Math.Min(MaxColumns, Children.Count)));
            columnWidth = (width - ColumnGap * (columns - 1)) / columns;

            foreach (Control child in Children)
            {
                child.Measure(new Size(columnWidth, double.PositiveInfinity));
            }

            double[] heights = Children.Select(c => c.DesiredSize.Height).ToArray();
            columnStarts = SplitIntoColumns(heights, columns, BlockGap);

            double tallest = 0;
            for (int c = 0; c < columnStarts.Count; c++)
            {
                int end = c + 1 < columnStarts.Count ? columnStarts[c + 1] : heights.Length;
                tallest = Math.Max(tallest, HeightOf(heights, columnStarts[c], end, BlockGap));
            }
            return new Size(width, tallest);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            for (int c = 0; c < columnStarts.Count; c++)
            {
                int end = c + 1 < columnStarts.Count ? columnStarts[c + 1] : Children.Count;
                double y = 0;
                double x = c * (columnWidth + ColumnGap);
                for (int i = columnStarts[c]; i < end; i++)
                {
                    Control child = Children[i];
                    child.Arrange(new Rect(x, y, columnWidth, child.DesiredSize.Height));
                    y += child.DesiredSize.Height + BlockGap;
                }
            }
            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var rule = new Pen(Ink.Black, 1);
            for (int c = 1; c < columnStarts.Count; c++)
            {
                double x = c * (columnWidth + ColumnGap) - ColumnGap / 2;
                context.DrawLine(rule, new Point(x, 0), new Point(x, Bounds.Height));
            }
        }

        private static double HeightOf(double[] heights, int start, int end, double gap)
        {
            double total = 0;
            for (int i = start; i < end; i++)
            {
                total += heights[i];
            }
            return total + gap * Math.Max(0, end - start - 1);
        }

        // Picks the break points that make the tallest column as short as possible.
        // Returns the index of the first block in each non-empty column.
        internal static List<int> SplitIntoColumns(double[] heights, int columns, double gap)
        {
            int n = heights.Length;
            if (n == 0 || columns <= 1)
            {
                return new List<int> { 0 };
            }

            double[,] best = new double[columns + 1, n + 1];
            int[,] from = new int[columns + 1, n + 1];
            for (int i = 1; i <= n; i++)
            {
                best[1, i] = HeightOf(heights, 0, i, gap);
            }

            for (int c = 2; c <= columns; c++)
            {
                for (int i = 1; i <= n; i++)
                {
                    best[c, i] = double.MaxValue;
                    for (int split = c - 1; split < i; split++)
                    {
                        double tallest = Math.Max(best[c - 1, split], HeightOf(heights, split, i, gap));
                        if (tallest < best[c, i])
                        {
                            best[c, i] = tallest;
                            from[c, i] = split;
                        }
                    }
                }
            }

            var starts = new List<int>();
            int end = n;
            for (int c = Math.Min(columns, n); c >= 2; c--)
            {
                end = from[c, end];
                starts.Insert(0, end);
            }
            starts.Insert(0, 0);
            return starts;
        }
    }
}
