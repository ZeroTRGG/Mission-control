using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace MissionControl.Controls
{
    // Ink colours taken from the reference site's stylesheet.
    public static class Ink
    {
        public static readonly Color PaperColor = Color.Parse("#F1ECE0");
        public static readonly IBrush Paper = new ImmutableSolidColorBrush(PaperColor);
        public static readonly IBrush Black = new ImmutableSolidColorBrush(Color.Parse("#333333"));
        public static readonly IBrush Marker = new ImmutableSolidColorBrush(Color.Parse("#C44536"));
    }

    public static class Faces
    {
        public static readonly FontFamily Body = new("avares://MissionControl/Assets/Fonts#Fira Sans Condensed");
        public static readonly FontFamily Display = new("avares://MissionControl/Assets/Fonts#Fira Sans Extra Condensed");

        // Permanent Marker has Latin letters only, so it is used for fixed English text and never for city names.
        public static readonly FontFamily Felt = new("avares://MissionControl/Assets/Fonts#Permanent Marker");
    }

    // Small builders so the page reads as a list of parts rather than property settings.
    public static class Print
    {
        public static SelectableTextBlock Text(string text, double size = 18, FontWeight weight = FontWeight.Medium, IBrush? ink = null)
        {
            return new SelectableTextBlock
            {
                Text = text,
                FontFamily = Faces.Body,
                FontSize = size,
                FontWeight = weight,
                LineHeight = size * 1.333,
                Foreground = ink ?? Ink.Black,
                TextWrapping = TextWrapping.Wrap,
                SelectionBrush = Ink.Marker,
                SelectionForegroundBrush = Ink.Paper
            };
        }

        // White on ink, the reference's reverse-video section tag.
        public static Border Tag(string text, IBrush? ink = null, double size = 32)
        {
            return new Border
            {
                Background = ink ?? Ink.Black,
                Padding = new Thickness(8, 1, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = text.ToUpperInvariant(),
                    FontFamily = Faces.Display,
                    FontWeight = FontWeight.Bold,
                    FontSize = size,
                    Foreground = Ink.Paper,
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }
    }
}
