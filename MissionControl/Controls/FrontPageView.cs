using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MissionControl.Press;

namespace MissionControl.Controls
{
    // Prints a FrontPage. The route only ever appears here as text: there is no map.
    public class FrontPageView : UserControl
    {
        private static readonly double TiltRise = Math.Tan(4.75 * Math.PI / 180) / 2;

        private readonly StackPanel root = new();
        private readonly Panel mastheadHost = new();
        private readonly TextBlock dailyDispatch;
        private readonly TextBlock datelineDate;
        private readonly Grid dateline;
        private readonly StackPanel story = new();

        private MarkerScribble? totalCircle;
        private MarkerScribble? headlineUnderline;
        private TextBlock? headline;
        private TextBlock? standfirst;
        private string? lastStoryKey;

        public FrontPageView()
        {
            dailyDispatch = new TextBlock
            {
                Text = "DAILY DISPATCH",
                FontFamily = Faces.Felt,
                Foreground = Ink.Marker,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false
            };
            mastheadHost.Children.Add(new HalftoneBlock());
            mastheadHost.Children.Add(new OutlinedText { Text = "MISSION CONTROL", VerticalAlignment = VerticalAlignment.Top });
            mastheadHost.Children.Add(dailyDispatch);
            mastheadHost.RenderTransform = new SkewTransform(0, -4.75);

            datelineDate = Print.Text("", 17, FontWeight.Bold);
            dateline = BuildDateline();

            root.Children.Add(mastheadHost);
            root.Children.Add(new Border
            {
                BorderBrush = Ink.Black,
                BorderThickness = new Thickness(0, 2),
                Padding = new Thickness(0, 5),
                Child = dateline
            });
            root.Children.Add(story);
            Content = root;
        }

        private Grid BuildDateline()
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*") };
            TextBlock left = Print.Text("VOL. XIV", 17, FontWeight.Bold);
            TextBlock right = Print.Text("ROUTE DESK EDITION", 17, FontWeight.Bold);
            right.TextAlignment = TextAlignment.Right;
            Grid.SetColumn(datelineDate, 1);
            Grid.SetColumn(right, 2);
            grid.Children.Add(left);
            grid.Children.Add(datelineDate);
            grid.Children.Add(right);
            return grid;
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyWidth(e.NewSize.Width);
        }

        private void ApplyWidth(double width)
        {
            double side = width < 640 ? 16 : width < 1024 ? 28 : 40;
            root.Margin = new Thickness(side, 0, side, 28);
            double inner = Math.Max(width - side * 2, 1);

            // The tilt lifts one end of the masthead and drops the other, so leave room for both.
            double rise = inner * TiltRise;
            dailyDispatch.FontSize = Math.Clamp(inner / 11, 26, 104);
            double letterHeight = inner * 0.135;
            mastheadHost.Height = letterHeight + dailyDispatch.FontSize * 0.62;
            mastheadHost.Margin = new Thickness(0, rise + 20, 0, rise + 12);
            dailyDispatch.Margin = new Thickness(0, 0, 0, -dailyDispatch.FontSize * 0.42);

            if (headline != null)
            {
                headline.FontSize = Math.Clamp(inner / 13, 36, 92);
                headline.LineHeight = headline.FontSize * 1.02;
            }
            if (standfirst != null)
            {
                double size = Math.Clamp(inner / 42, 20, 28);
                standfirst.FontSize = size;
                standfirst.LineHeight = size * 1.3;
            }

            bool narrow = inner < 420;
            foreach (TextBlock block in dateline.Children.OfType<TextBlock>().Where(b => b != datelineDate))
            {
                block.IsVisible = !narrow;
            }
        }

        public void Show(FrontPage page, string? erratum)
        {
            datelineDate.Text = page.Dateline.ToUpperInvariant();
            story.Children.Clear();
            totalCircle = null;
            headlineUnderline = null;

            if (erratum != null)
            {
                story.Children.Add(Erratum(erratum));
            }

            story.Children.Add(Lead(page));

            if (page.Article.Count > 0)
            {
                story.Children.Add(new SectionPanel(Print.Tag(page.ArticleTitle), Article(page.Article)));
            }
            if (page.HasRoute)
            {
                story.Children.Add(new SectionPanel(Print.Tag("By the Numbers"), Numbers(page)));
                story.Children.Add(new SectionPanel(Print.Tag("Itinerary"), Itinerary(page.Itinerary)));
            }
            if (page.Classifieds.Count > 0)
            {
                story.Children.Add(new SectionPanel(Print.Tag(page.ClassifiedsTitle), Classifieds(page.Classifieds)));
            }
            if (page.Corrections.Count > 0)
            {
                story.Children.Add(new SectionPanel(Print.Tag("Corrections", Ink.Marker), Corrections(page.Corrections), Ink.Marker));
            }
            story.Children.Add(Footer(page));

            ApplyWidth(Bounds.Width);

            // Only draw the marker in when the story actually changed, not on every refresh.
            string key = page.Headline + page.TotalKm;
            if (key != lastStoryKey)
            {
                lastStoryKey = key;
                totalCircle?.Replay();
                headlineUnderline?.Replay();
            }
        }

        private static Control Erratum(string message)
        {
            var body = new StackPanel { Spacing = 2 };
            body.Children.Add(new TextBlock
            {
                Text = "ERRATUM",
                FontFamily = Faces.Felt,
                FontSize = 30,
                Foreground = Ink.Marker,
                RenderTransform = new RotateTransform(-1.2)
            });
            body.Children.Add(Print.Text(message, 20));
            return new Border
            {
                Name = "ErratumBox",
                BorderBrush = Ink.Marker,
                BorderThickness = new Thickness(3),
                Padding = new Thickness(16, 10, 16, 14),
                Margin = new Thickness(0, 22, 0, 0),
                Child = body
            };
        }

        private Control Lead(FrontPage page)
        {
            headline = new SelectableTextBlock
            {
                Name = "Headline",
                Text = page.Headline,
                FontFamily = Faces.Display,
                FontWeight = FontWeight.Black,
                Foreground = Ink.Black,
                TextWrapping = TextWrapping.Wrap,
                SelectionBrush = Ink.Marker,
                SelectionForegroundBrush = Ink.Paper
            };
            headlineUnderline = new MarkerScribble(ScribbleKind.Underline)
            {
                Height = 12,
                Width = 260,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 8)
            };
            standfirst = Print.Text(page.Standfirst);
            standfirst.Name = "Standfirst";

            var lead = new StackPanel { Margin = new Thickness(0, 26, 0, 30) };
            lead.Children.Add(headline);
            lead.Children.Add(headlineUnderline);
            lead.Children.Add(standfirst);
            return lead;
        }

        private static Control Article(List<string> paragraphs)
        {
            var columns = new ColumnFlowPanel();
            foreach (string paragraph in paragraphs)
            {
                columns.Children.Add(Print.Text(paragraph, 19));
            }
            return columns;
        }

        private Control Numbers(FrontPage page)
        {
            string total = page.TotalKm == null ? "?" : $"{page.TotalKm} km";
            var figure = new TextBlock
            {
                Name = "TotalKm",
                Text = total,
                FontFamily = Faces.Display,
                FontWeight = FontWeight.Black,
                FontSize = 72,
                LineHeight = 76,
                Foreground = Ink.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(34, 14, 34, 16)
            };
            totalCircle = new MarkerScribble(ScribbleKind.Circle);

            var circled = new Panel { HorizontalAlignment = HorizontalAlignment.Left };
            circled.Children.Add(figure);
            circled.Children.Add(totalCircle);

            var left = new StackPanel { Spacing = 2 };
            left.Children.Add(circled);
            left.Children.Add(Print.Text(page.TotalKm == null ? "Total unknown until the route is whole" : "Total distance", 17, FontWeight.Bold));

            var stats = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            AddStat(stats, "Stops", page.StopCount.ToString());
            stats.Children.Add(new DottedRule());
            AddStat(stats, "Legs", page.LegCount.ToString());
            if (page.LongestLeg != null)
            {
                stats.Children.Add(new DottedRule());
                stats.Children.Add(Print.Text("Longest leg", 16, FontWeight.Bold));
                stats.Children.Add(Print.Text(page.LongestLeg, 19));
            }

            var columns = new ColumnFlowPanel { BlockGap = 24 };
            columns.Children.Add(left);
            columns.Children.Add(stats);
            return columns;
        }

        private static void AddStat(StackPanel stats, string label, string value)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(Print.Text(label, 16, FontWeight.Bold));
            TextBlock number = Print.Text(value, 20, FontWeight.Bold);
            Grid.SetColumn(number, 1);
            row.Children.Add(number);
            stats.Children.Add(row);
        }

        private static Control Itinerary(List<ItineraryRow> rows)
        {
            var list = new StackPanel { Spacing = 8 };
            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*,80,80") };
            AddCell(header, 1, "ROAD", TextAlignment.Left, FontWeight.Bold, 14);
            AddCell(header, 2, "KM", TextAlignment.Right, FontWeight.Bold, 14);
            AddCell(header, 3, "RUNNING", TextAlignment.Right, FontWeight.Bold, 14);
            list.Children.Add(header);

            foreach (ItineraryRow row in rows)
            {
                list.Children.Add(new DottedRule());
                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*,80,80") };
                bool missing = row.Km == null;
                IBrush ink = missing ? Ink.Marker : Ink.Black;
                AddCell(grid, 0, row.Number.ToString(), TextAlignment.Left, FontWeight.Bold, 18, ink);
                AddCell(grid, 1, $"{row.From} → {row.To}", TextAlignment.Left, FontWeight.Medium, 18, ink);
                AddCell(grid, 2, row.Km?.ToString() ?? "no road", TextAlignment.Right, FontWeight.Medium, 18, ink);
                AddCell(grid, 3, row.RunningKm?.ToString() ?? "–", TextAlignment.Right, FontWeight.Bold, 18, ink);
                list.Children.Add(grid);
            }
            return list;
        }

        private static void AddCell(Grid grid, int column, string text, TextAlignment align, FontWeight weight, double size, IBrush? ink = null)
        {
            TextBlock cell = Print.Text(text, size, weight, ink);
            cell.TextAlignment = align;
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }

        private static Control Classifieds(List<Classified> classifieds)
        {
            var columns = new ColumnFlowPanel { BlockGap = 8, MinColumnWidth = 180 };
            foreach (Classified ad in classifieds)
            {
                var entry = new StackPanel { Spacing = 8 };
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                row.Children.Add(Print.Text(ad.City, 18, FontWeight.Bold));
                TextBlock km = Print.Text($"{ad.Km} km", 18);
                Grid.SetColumn(km, 1);
                row.Children.Add(km);
                entry.Children.Add(row);
                entry.Children.Add(new DottedRule());
                columns.Children.Add(entry);
            }
            return columns;
        }

        private static Control Corrections(List<string> corrections)
        {
            var list = new StackPanel { Spacing = 10 };
            foreach (string correction in corrections)
            {
                list.Children.Add(Print.Text(correction, 18));
            }
            return list;
        }

        private static Control Footer(FrontPage page)
        {
            var footer = new StackPanel { Spacing = 12 };
            var rule = new Border { Height = 2, Background = Ink.Black };
            footer.Children.Add(rule);
            if (page.FilingLine != null)
            {
                footer.Children.Add(Print.Text($"Filed: {page.FilingLine}", 15));
            }
            footer.Children.Add(new TextBlock
            {
                Text = "PRINTED WITHOUT A SINGLE MAP",
                FontFamily = Faces.Felt,
                FontSize = 24,
                Foreground = Ink.Marker,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            });
            return footer;
        }
    }
}
