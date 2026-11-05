using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Travelling;

namespace MissionControl.Press
{
    // Turns route data into newspaper text. Every statement comes from the map or the
    // traveler: there is no data for weather or travel times, so none is printed.
    public static class FrontPageWriter
    {
        private const int MaxSkippedLinesShown = 5;

        public static List<string> StopsOf(Traveler traveler)
        {
            var stops = new List<string>();
            for (int i = 0; i < traveler.GetStopCount(); i++)
            {
                stops.Add(traveler[i]);
            }
            return stops;
        }

        public static List<Leg> LegsOf(Traveler traveler, CityGraph? map)
        {
            List<string> stops = StopsOf(traveler);
            var legs = new List<Leg>();
            for (int i = 0; i + 1 < stops.Count; i++)
            {
                legs.Add(new Leg(stops[i], stops[i + 1], map?.GetRoadDistance(stops[i], stops[i + 1])));
            }
            return legs;
        }

        public static FrontPage Write(Traveler? traveler, CityGraph? map, DateTime now)
        {
            string dateline = now.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture);
            List<string> mapCorrections = MapCorrections(map);

            if (traveler == null)
            {
                return map == null
                    ? NothingToReport(dateline)
                    : MapOnly(map, dateline, mapCorrections);
            }

            List<string> stops = StopsOf(traveler);
            if (stops.Count == 0)
            {
                return NoRoute(traveler, map, dateline, mapCorrections);
            }
            if (stops.Count == 1)
            {
                return SingleStop(traveler, stops[0], map, dateline, mapCorrections);
            }
            return Route(traveler, map, stops, dateline, now.DayOfWeek, mapCorrections);
        }

        private static FrontPage NothingToReport(string dateline) => new()
        {
            Dateline = dateline,
            Headline = "NOTHING TO REPORT",
            Standfirst = "Mission Control has no map on its desk, so no route can be filed today.",
            ArticleTitle = "How to File",
            Article = new List<string>
            {
                "First, load a map from the desk. A map is a text file with one road per line, written as City - City,km. The sample map will do for a trial run.",
                "Next, sign in a traveler with a name and a starting city.",
                "Finally, choose a destination and plan the route. The story prints itself on this page."
            }
        };

        private static FrontPage MapOnly(CityGraph map, string dateline, List<string> corrections)
        {
            List<string> cities = map.GetCities();
            int roads = cities.Sum(c => map.GetRoads(c).Count) / 2;
            return new FrontPage
            {
                Dateline = dateline,
                Headline = $"NEW MAP ARRIVES: {Count(cities.Count, "CITY", "CITIES")}, {Count(roads, "ROAD", "ROADS")}",
                Standfirst = "No traveler has signed in yet. Sign one in at the desk to file the first route.",
                ArticleTitle = "On the Map",
                Article = new List<string> { $"The new map covers {JoinNames(cities)}." },
                Corrections = corrections
            };
        }

        private static FrontPage NoRoute(Traveler traveler, CityGraph? map, string dateline, List<string> corrections)
        {
            string name = traveler.GetName();
            string where = traveler.GetLocation() ?? "an unknown city";
            return new FrontPage
            {
                Dateline = dateline,
                Headline = Upper($"{name} waits in {where}"),
                Standfirst = $"No route has been filed. Choose a destination at the desk to send {name} on the way.",
                ClassifiedsTitle = $"Roads Out of {where}",
                Classifieds = ClassifiedsFor(where, map),
                Corrections = corrections,
                FilingLine = traveler.GetTravelSummary()
            };
        }

        private static FrontPage SingleStop(Traveler traveler, string stop, CityGraph? map, string dateline, List<string> corrections)
        {
            string name = traveler.GetName();
            return new FrontPage
            {
                Dateline = dateline,
                Headline = Upper($"{name} goes nowhere: already in {stop}"),
                Standfirst = $"The route has a single stop, {stop}, so there are no roads to report.",
                StopCount = 1,
                TotalKm = 0,
                ClassifiedsTitle = $"Roads Out of {stop}",
                Classifieds = ClassifiedsFor(stop, map),
                Corrections = corrections,
                FilingLine = traveler.GetTravelSummary()
            };
        }

        private static FrontPage Route(Traveler traveler, CityGraph? map, List<string> stops, string dateline, DayOfWeek day, List<string> corrections)
        {
            string name = traveler.GetName();
            string first = stops[0];
            string last = stops[^1];
            List<Leg> legs = LegsOf(traveler, map);
            bool broken = legs.Any(l => l.Km == null);

            if (map == null)
            {
                corrections.Add("No map is loaded, so these distances cannot be confirmed.");
            }
            else
            {
                corrections.AddRange(legs.Where(l => l.Km == null).Select(l => $"No road links {l.From} and {l.To}."));
            }

            string? location = traveler.GetLocation();
            if (location != null && !string.Equals(location, first, StringComparison.OrdinalIgnoreCase))
            {
                corrections.Add($"The route starts in {first}, but {name} is in {location}.");
            }

            int? total = broken ? null : legs.Sum(l => l.Km!.Value);
            Leg? firstBreak = legs.FirstOrDefault(l => l.Km == null);

            string headline;
            string standfirst;
            if (map == null)
            {
                headline = $"{name}'s route: {first} to {last}";
                standfirst = "With no map on the desk, Mission Control cannot confirm a single kilometre of it.";
            }
            else if (firstBreak != null)
            {
                int known = legs.Where(l => l.Km != null).Sum(l => l.Km!.Value);
                headline = $"Route broken: no road from {firstBreak.From} to {firstBreak.To}";
                standfirst = $"Mission Control can account for {known} km of {name}'s route. Mend the route at the desk to fill the gaps.";
            }
            else
            {
                headline = $"{name} leaves {first} for {last}: {total} km";
                standfirst = RouteStandfirst(map, stops, total!.Value, day);
            }

            Leg? longest = broken ? null : legs.MaxBy(l => l.Km);
            return new FrontPage
            {
                Dateline = dateline,
                Headline = Upper(headline),
                Standfirst = standfirst,
                ArticleTitle = "The Road",
                Article = TellTheRoad(name, legs),
                Itinerary = BuildItinerary(legs),
                TotalKm = total,
                StopCount = stops.Count,
                LegCount = legs.Count,
                LongestLeg = longest == null ? null : $"{longest.From} to {longest.To}, {longest.Km} km",
                ClassifiedsTitle = $"Roads Out of {last}",
                Classifieds = ClassifiedsFor(last, map),
                Corrections = corrections,
                FilingLine = traveler.GetTravelSummary()
            };
        }

        private static string RouteStandfirst(CityGraph map, List<string> stops, int total, DayOfWeek day)
        {
            List<string> via = stops.Skip(1).Take(stops.Count - 2).ToList();
            int shortest = map.GetPathDistance(map.FindShortestPath(stops[0], stops[^1]));
            int extra = total - shortest;

            if (extra > 0)
            {
                string by = via.Count == 0 ? "The route" : $"The route through {JoinNames(via)}";
                return $"{by} runs {extra} km longer than the shortest road, Mission Control noted on {day}.";
            }
            if (via.Count == 0)
            {
                return $"A single road links the two cities, and it is the shortest way, Mission Control confirmed on {day}.";
            }
            return $"The shortest road runs through {JoinNames(via)}, Mission Control confirmed on {day}.";
        }

        // One paragraph per leg, so the route reads as a story from first stop to last.
        private static List<string> TellTheRoad(string name, List<Leg> legs)
        {
            var paragraphs = new List<string>();
            int? running = 0;

            for (int i = 0; i < legs.Count; i++)
            {
                Leg leg = legs[i];
                bool isFirst = i == 0;
                bool isLast = i == legs.Count - 1;

                if (leg.Km == null)
                {
                    running = null;
                    string opening = isFirst ? $"{name} sets out from {leg.From}, but the report stops there" : $"The report stops at {leg.From}";
                    paragraphs.Add($"{opening}: this desk has no road on record to {leg.To}.");
                    continue;
                }

                running += leg.Km;
                if (isFirst && isLast)
                {
                    paragraphs.Add($"{name} sets out from {leg.From}. One road, {leg.Km} km long, runs all the way to {leg.To}.");
                }
                else if (isFirst)
                {
                    paragraphs.Add($"{name} sets out from {leg.From}. The first leg runs {leg.Km} km to {leg.To}.");
                }
                else if (isLast)
                {
                    string close = running == null ? "" : $", ending the journey at {running} km";
                    paragraphs.Add($"The final {leg.Km} km carry {name} from {leg.From} into {leg.To}{close}.");
                }
                else
                {
                    string close = running == null ? "" : $", bringing the total to {running} km";
                    paragraphs.Add($"From {leg.From} the road runs {leg.Km} km to {leg.To}{close}.");
                }
            }
            return paragraphs;
        }

        private static List<ItineraryRow> BuildItinerary(List<Leg> legs)
        {
            var rows = new List<ItineraryRow>();
            int? running = 0;
            for (int i = 0; i < legs.Count; i++)
            {
                running = legs[i].Km == null ? null : running + legs[i].Km;
                rows.Add(new ItineraryRow(i + 1, legs[i].From, legs[i].To, legs[i].Km, running));
            }
            return rows;
        }

        private static List<Classified> ClassifiedsFor(string city, CityGraph? map)
        {
            if (map == null)
            {
                return new List<Classified>();
            }
            return map.GetRoads(city).Select(r => new Classified(r.Target.Name1, r.Distance)).ToList();
        }

        private static List<string> MapCorrections(CityGraph? map)
        {
            var corrections = new List<string>();
            if (map == null)
            {
                return corrections;
            }
            corrections.AddRange(map.SkippedLines.Take(MaxSkippedLinesShown).Select(line => $"The map could not be fully read. {line}."));
            int hidden = map.SkippedLines.Count - MaxSkippedLinesShown;
            if (hidden > 0)
            {
                corrections.Add($"{hidden} more unreadable map {(hidden == 1 ? "line was" : "lines were")} left out.");
            }
            return corrections;
        }

        private static string Upper(string text) => text.ToUpper(CultureInfo.InvariantCulture);

        private static string Count(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

        private static string JoinNames(List<string> names)
        {
            if (names.Count <= 1)
            {
                return string.Concat(names);
            }
            return $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
        }
    }
}
