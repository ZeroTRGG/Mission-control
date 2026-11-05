using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Travelling
{
    public class InlineJsonConverter : JsonConverter<List<string>>
    {
        public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var list = new List<string>();

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.String)
                    {
                        throw new JsonException();
                    }
                    var item = reader.GetString();
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
            }
            else if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            else
            {
                throw new JsonException();
            }
            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            IEnumerable<string> items = value.Select(city => JsonSerializer.Serialize(city, options));
            writer.WriteRawValue($"[{string.Join(", ", items)}]");
        }
    }

    public class Traveler : ICloneable
    {
        [JsonPropertyName("name")]
        [JsonInclude]
        private string name;

        [JsonPropertyName("currentLocation")]
        [JsonInclude]
        private string? currentLocation;

        [JsonPropertyName("route")]
        [JsonConverter(typeof(InlineJsonConverter))]
        [JsonInclude]
        private List<string> route = new List<string> { };

        public Traveler(string name)
        {
            this.name = name;
        }

        public void SetLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("A location cannot be blank.", nameof(location));
            currentLocation = CityToUpper(location);
        }

        public string CityToUpper(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return "";
            }

            int x = 0;
            char[] array = city.Trim().ToCharArray();
            array[0] = char.ToUpper(array[x]);
            foreach (char c in array)
            {
                if (x + 1 < array.Length && (c == ' ' && array[x + 1] != ' ' || c == '-' && array[x + 1] != '-' || c == '_' && array[x + 1] != '_'))
                {
                    array[x + 1] = char.ToUpper(array[x + 1]);
                }
                x++;
            }
            string str = new string(array);
            return str;
        }

        public void AddCity(string city)
        {
            ArgumentNullException.ThrowIfNull(city);
            if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("A city cannot be blank.", nameof(city));
            string UpperCity = CityToUpper(city);
            route.Add(UpperCity);
        }

        public string GetRoute()
        {
            string rout = string.Join(" -> ", route);
            return rout;
        }

        public string? GetLocation()
        {
            return currentLocation;
        }

        public string GetName()
        {
            return name;
        }

        public override string ToString()
        {
            return $"Traveler: {GetName()} | Location: {GetLocation()} | Route: {GetRoute()}";
        }

        public void ClearRoute()
        {
            route.Clear();
        }

        public int GetStopCount()
        {
            return route.Count();
        }

        public bool HasCity(string city)
        {
            return route.Contains(CityToUpper(city), StringComparer.OrdinalIgnoreCase);
        }

        public void SortRoute()
        {
            route.Sort();
        }

        public bool RemoveCity(string city)
        {
            int index = route.FindIndex(stop => string.Equals(stop, CityToUpper(city), StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return false;
            }
            route.RemoveAt(index);
            return true;
        }

        // RemoveCity only finds a city's first visit, so route editing works by position.
        public void RemoveStopAt(int index)
        {
            route.RemoveAt(index);
        }

        public void MoveStop(int from, int to)
        {
            if (to < 0 || to >= route.Count) throw new ArgumentOutOfRangeException(nameof(to));
            string city = route[from];
            route.RemoveAt(from);
            route.Insert(to, city);
        }

        public string GetNextStop()
        {
            if (route.Count() == 0)
            {
                return null!;
            }
            else return route[0];
        }

        public string this[int index]
        {
            get => route[index];
        }

        public override bool Equals(object? obj)
        {
            return obj is Traveler other && this == other;
        }

        public static bool operator ==(Traveler? a, Traveler? b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (a is null || b is null)
            {
                return false;
            }
            return a.currentLocation == b.currentLocation && a.name == b.name;
        }

        public static bool operator !=(Traveler? a, Traveler? b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(name, currentLocation);
        }

        public object Clone()
        {
            return new Traveler(name)
            {
                route = this.route.ToList(),
                currentLocation = this.currentLocation
            };
        }

        public virtual string GetTravelSummary()
        {
            return $"Traveler: {GetName()} | Location: {GetLocation()} | Route: {GetRoute()}";
        }

        public void SaveToFile(string filePath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string output = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, output);
        }

        public static Traveler LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException();
            }
            try
            {
                string jsonString = File.ReadAllText(filePath);
                Traveler? trave = JsonSerializer.Deserialize<Traveler>(jsonString);
                if (trave == null || string.IsNullOrWhiteSpace(trave.name) || trave.route == null
                    || trave.route.Any(string.IsNullOrWhiteSpace))
                {
                    throw new FileLoadException("Invalid travel data");
                }
                return trave;
            }
            catch (JsonException)
            {
                throw new FileLoadException("Invalid travel data");
            }
        }

        public bool PlanRouteTo(string destination, CityGraph map)
        {
            string? startCity = !string.IsNullOrEmpty(currentLocation)
                ? currentLocation
                : (route.Count > 0 ? route[0] : null);

            if (string.IsNullOrEmpty(startCity) || string.IsNullOrEmpty(destination) || map == null)
            {
                return false;
            }

            List<string> shortestPath = map.FindShortestPath(startCity, destination);
            if (shortestPath == null || shortestPath.Count == 0)
            {
                return false;
            }

            route.Clear();
            foreach (string city in shortestPath)
            {
                AddCity(city);
            }
            return true;
        }
    }
}
