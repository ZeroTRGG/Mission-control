using System.Collections.Generic;

namespace MissionControl.Press
{
    // One road between two stops. Km is null when the map has no such road.
    public sealed record Leg(string From, string To, int? Km);

    // RunningKm is null once the route has broken, because the total is then unknown.
    public sealed record ItineraryRow(int Number, string From, string To, int? Km, int? RunningKm);

    public sealed record Classified(string City, int Km);

    // Everything printed on the page, as plain data.
    public sealed class FrontPage
    {
        public string Dateline { get; init; } = "";
        public string Headline { get; init; } = "";
        public string Standfirst { get; init; } = "";

        // The route told in prose. This is the page's replacement for a map.
        public string ArticleTitle { get; init; } = "";
        public List<string> Article { get; init; } = new();

        public List<ItineraryRow> Itinerary { get; init; } = new();

        // Null means no total can honestly be printed.
        public int? TotalKm { get; init; }
        public int StopCount { get; init; }
        public int LegCount { get; init; }
        public string? LongestLeg { get; init; }

        public string ClassifiedsTitle { get; init; } = "";
        public List<Classified> Classifieds { get; init; } = new();

        public List<string> Corrections { get; init; } = new();
        public string? FilingLine { get; init; }

        public bool HasRoute => Itinerary.Count > 0;
    }
}
