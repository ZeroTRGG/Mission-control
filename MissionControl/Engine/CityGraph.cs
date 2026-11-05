using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Travelling
{
    public class TNode
    {
        public string Name1 { get; set; } = "";
    }

    public class TEdge
    {
        public TNode Target { get; set; }

        public int Distance { get; set; }

        public TEdge(TNode target, int distance)
        {
            Target = target;
            Distance = distance;
        }
    }

    public class CityGraph
    {
        private List<string> edgeLines = new List<string>();
        private List<string> skippedLines = new List<string>();
        private Dictionary<TNode, List<TEdge>> adjacencyList = new();
        private Dictionary<string, TNode> nodeLookup = new Dictionary<string, TNode>(StringComparer.OrdinalIgnoreCase);

        // Lines LoadFromFile could not read, each with its line number and the reason.
        public IReadOnlyList<string> SkippedLines => skippedLines;

        public override string ToString()
        {
            return string.Join("\n", edgeLines);
        }

        public void AddEdge(TNode node, TNode target, string distance)
        {
            int parsedDistance = int.Parse(distance);
            if (parsedDistance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "A road cannot have a negative distance.");
            }

            if (!adjacencyList.ContainsKey(node))
            {
                adjacencyList[node] = new List<TEdge>();
            }
            adjacencyList[node].Add(new TEdge(target, parsedDistance));

            if (!adjacencyList.ContainsKey(target))
            {
                adjacencyList[target] = new List<TEdge>();
            }
            adjacencyList[target].Add(new TEdge(node, parsedDistance));

            edgeLines.Add($"{node.Name1}-{target.Name1},{distance}");
            edgeLines.Add($"{target.Name1}-{node.Name1},{distance}");
        }

        public TNode GetOrCreateNode(string node)
        {
            if (!nodeLookup.ContainsKey(node))
            {
                nodeLookup[node] = new TNode { Name1 = node };
            }
            return nodeLookup[node];
        }

        // Loads a bidirectional graph from a file and returns an instance.
        // Lines read "City-City,km", or "City - City,km" when a name has a hyphen.
        public static CityGraph LoadFromFile(string filePath)
        {
            CityGraph myGraph = new CityGraph();
            string[] readText = File.ReadAllLines(filePath);
            for (int i = 0; i < readText.Length; i++)
            {
                string s = readText[i].Trim();
                if (s.Length == 0 || s.StartsWith('#'))
                {
                    continue;
                }

                string? problem = TryParseRoad(s, out string from, out string to, out string distance);
                if (problem != null)
                {
                    myGraph.skippedLines.Add($"Line {i + 1}: {problem} (\"{s}\")");
                    continue;
                }
                myGraph.AddEdge(myGraph.GetOrCreateNode(from), myGraph.GetOrCreateNode(to), distance);
            }
            return myGraph;
        }

        // Returns null when the line is a valid road, otherwise why it is not.
        private static string? TryParseRoad(string line, out string from, out string to, out string distance)
        {
            from = to = distance = "";

            int comma = line.LastIndexOf(',');
            if (comma < 0)
            {
                return "no distance after a comma";
            }
            distance = line.Substring(comma + 1).Trim();
            string names = line.Substring(0, comma);

            if (!int.TryParse(distance, out int km))
            {
                return "the distance is not a whole number";
            }
            if (km < 0)
            {
                return "the distance is negative";
            }

            string[] cities = names.Contains(" - ") ? names.Split(" - ") : names.Split('-');
            if (cities.Length != 2)
            {
                return names.Contains('-')
                    ? "too many hyphens, write hyphenated names as \"City-Name - Other\""
                    : "it does not name two cities";
            }

            from = cities[0].Trim();
            to = cities[1].Trim();
            if (from.Length == 0 || to.Length == 0)
            {
                return "a city name is blank";
            }
            return null;
        }

        public List<string> GetCities()
        {
            return nodeLookup.Values
                .Select(n => n.Name1)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        // The roads leaving a city, nearest first. Duplicate roads count once, at their shortest.
        public List<TEdge> GetRoads(string city)
        {
            if (string.IsNullOrWhiteSpace(city)
                || !nodeLookup.TryGetValue(city.Trim(), out TNode? node)
                || !adjacencyList.TryGetValue(node, out List<TEdge>? edges))
            {
                return new List<TEdge>();
            }

            return edges
                .Where(e => e.Target != node)
                .GroupBy(e => e.Target)
                .Select(g => g.MinBy(e => e.Distance)!)
                .OrderBy(e => e.Distance)
                .ThenBy(e => e.Target.Name1, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        // The length of the direct road between two cities, or null when there is none.
        public int? GetRoadDistance(string from, string to)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)
                || !nodeLookup.TryGetValue(from.Trim(), out TNode? start)
                || !nodeLookup.TryGetValue(to.Trim(), out TNode? end)
                || !adjacencyList.TryGetValue(start, out List<TEdge>? edges))
            {
                return null;
            }

            List<TEdge> roads = edges.Where(e => e.Target == end).ToList();
            return roads.Count == 0 ? null : roads.Min(e => e.Distance);
        }

        public List<string> FindShortestPath(string from, string to)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)
                || !nodeLookup.TryGetValue(from.Trim(), out TNode? startNode)
                || !nodeLookup.TryGetValue(to.Trim(), out TNode? targetNode))
            {
                return new List<string>();
            }

            if (startNode == targetNode)
            {
                return new List<string> { startNode.Name1 };
            }

            var distances = new Dictionary<TNode, int>();
            var parents = new Dictionary<TNode, TNode>();
            var priorityQueue = new PriorityQueue<TNode, int>();

            distances[startNode] = 0;
            priorityQueue.Enqueue(startNode, 0);

            while (priorityQueue.TryDequeue(out TNode? current, out int queuedDistance))
            {
                if (current == targetNode)
                {
                    break;
                }
                if (queuedDistance > distances[current])
                {
                    continue;
                }
                if (!adjacencyList.TryGetValue(current, out List<TEdge>? edges))
                {
                    continue;
                }
                foreach (TEdge edge in edges)
                {
                    TNode neighbor = edge.Target;
                    int dist = distances[current] + edge.Distance;

                    if (!distances.ContainsKey(neighbor) || dist < distances[neighbor])
                    {
                        distances[neighbor] = dist;
                        parents[neighbor] = current;

                        priorityQueue.Enqueue(neighbor, dist);
                    }
                }
            }

            if (!parents.ContainsKey(targetNode))
            {
                return new List<string>();
            }

            var paths = new List<string>();
            TNode? currentPathNode = targetNode;
            while (currentPathNode != null)
            {
                paths.Add(currentPathNode.Name1);
                currentPathNode = parents.TryGetValue(currentPathNode, out var next) ? next : default;
            }
            paths.Reverse();
            return paths;
        }

        // Returns 0 when any leg has no road. Use GetRoadDistance to tell that from a real 0.
        public int GetPathDistance(List<string> path)
        {
            if (path == null || path.Count < 2)
            {
                return 0;
            }

            int distance = 0;
            for (int x = 0; x < path.Count - 1; x++)
            {
                int? road = GetRoadDistance(path[x], path[x + 1]);
                if (road == null)
                {
                    return 0;
                }
                distance += road.Value;
            }
            return distance;
        }
    }
}
