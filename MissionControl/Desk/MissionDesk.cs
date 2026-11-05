using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MissionControl.Press;
using Travelling;

namespace MissionControl.Desk
{
    // A problem the person at the desk caused and can fix. The message is printed on the page.
    public sealed class DeskException : Exception
    {
        public DeskException(string message, Exception? inner = null) : base(message, inner) { }
    }

    // All application state and every action a button can trigger. It knows nothing about
    // the UI, so each click handler is one call into this class.
    public sealed class MissionDesk
    {
        private const int MaxUndoSteps = 50;

        private sealed class Entry
        {
            public Traveler Traveler;
            public bool Unsaved;

            public Entry(Traveler traveler, bool unsaved)
            {
                Traveler = traveler;
                Unsaved = unsaved;
            }
        }

        private readonly List<Entry> entries = new();
        private readonly List<(int Index, Traveler Before, bool WasUnsaved)> undo = new();
        private readonly Logger<string> log = new();
        private readonly string logPath;

        public MissionDesk(string? logPath = null)
        {
            this.logPath = logPath ?? Path.Combine(AppContext.BaseDirectory, "log.txt");
        }

        public event Action? Changed;

        public CityGraph? Map { get; private set; }
        public string? MapName { get; private set; }
        public int ActiveIndex { get; private set; } = -1;

        // What went wrong with the last action, printed on the page as an erratum.
        public string? Erratum { get; private set; }

        public IReadOnlyList<Traveler> Travelers => entries.Select(e => e.Traveler).ToList();
        public Traveler? Active => ActiveIndex >= 0 ? entries[ActiveIndex].Traveler : null;
        public bool ActiveIsUnsaved => ActiveIndex >= 0 && entries[ActiveIndex].Unsaved;
        public bool HasUnsavedChanges => entries.Any(e => e.Unsaved);
        public bool CanUndo => undo.Count > 0;
        public IReadOnlyList<string> Cities => Map?.GetCities() ?? new List<string>();

        public bool IsUnsaved(int index) => entries[index].Unsaved;

        public FrontPage WriteFrontPage(DateTime now) => FrontPageWriter.Write(Active, Map, now);

        // ---- The guards every button goes through ----

        public bool Attempt(Action action)
        {
            Erratum = null;
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                Report(ex);
                return false;
            }
            finally
            {
                Changed?.Invoke();
            }
        }

        public async Task<bool> AttemptAsync(Func<Task> action)
        {
            Erratum = null;
            try
            {
                await action();
                return true;
            }
            catch (Exception ex)
            {
                Report(ex);
                return false;
            }
            finally
            {
                Changed?.Invoke();
            }
        }

        // Also the backstop for anything that escapes the guards above.
        public void Report(Exception ex)
        {
            Erratum = ex is DeskException ? ex.Message : $"Mission Control hit an unexpected fault: {ex.Message}";
            Log($"ERROR {ex.GetType().Name}: {ex.Message}");
        }

        // ---- Map ----

        public void LoadMap(string path)
        {
            CityGraph map = ReadFile(path, () => CityGraph.LoadFromFile(path));
            if (map.GetCities().Count == 0)
            {
                string why = map.SkippedLines.Count > 0 ? $" {map.SkippedLines[0]}." : "";
                throw new DeskException($"{Path.GetFileName(path)} has no roads Mission Control can read, so the map on the desk stays.{why}");
            }
            Map = map;
            MapName = Path.GetFileName(path);
            Log($"Loaded map {MapName} with {map.GetCities().Count} cities and {map.SkippedLines.Count} unreadable lines");
        }

        // ---- Travelers ----

        public void CreateTraveler(string name, string location)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DeskException("A traveler needs a name.");
            }
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new DeskException(Map == null
                    ? $"Load a map first, then choose a starting city for {name.Trim()}."
                    : $"{name.Trim()} needs a starting city.");
            }

            var traveler = new Traveler(name.Trim());
            traveler.SetLocation(RequireOnMap(location));
            if (entries.Any(e => e.Traveler == traveler))
            {
                throw new DeskException($"{traveler.GetName()} is already signed in at {traveler.GetLocation()}.");
            }

            entries.Add(new Entry(traveler, unsaved: true));
            ActiveIndex = entries.Count - 1;
            Log($"Created traveler {traveler.GetName()} in {traveler.GetLocation()}");
        }

        public void SelectTraveler(int index)
        {
            if (index < 0 || index >= entries.Count)
            {
                return;
            }
            ActiveIndex = index;
        }

        public void RemoveActive()
        {
            Traveler traveler = RequireActive();
            entries.RemoveAt(ActiveIndex);
            ActiveIndex = Math.Min(ActiveIndex, entries.Count - 1);
            undo.Clear();
            Log($"Removed traveler {traveler.GetName()}");
        }

        public void SaveActive(string path)
        {
            Traveler traveler = RequireActive();
            ReadFile(path, () =>
            {
                traveler.SaveToFile(path);
                return true;
            });
            entries[ActiveIndex].Unsaved = false;
            Log($"Saved traveler {traveler.GetName()} to {Path.GetFileName(path)}");
        }

        // The traveler a load would overwrite if it has unsaved work, so the UI can warn first.
        public Traveler? UnsavedTravelerReplacedBy(string path)
        {
            Traveler incoming = ReadTraveler(path);
            return entries.FirstOrDefault(e => e.Traveler == incoming && e.Unsaved)?.Traveler;
        }

        public void LoadTraveler(string path)
        {
            Traveler traveler = ReadTraveler(path);
            int existing = entries.FindIndex(e => e.Traveler == traveler);
            if (existing >= 0)
            {
                entries[existing] = new Entry(traveler, unsaved: false);
                ActiveIndex = existing;
                undo.Clear();
            }
            else
            {
                entries.Add(new Entry(traveler, unsaved: false));
                ActiveIndex = entries.Count - 1;
            }
            Log($"Loaded traveler {traveler.GetName()} from {Path.GetFileName(path)}");
        }

        private Traveler ReadTraveler(string path)
        {
            Traveler traveler = ReadFile(path, () => Traveler.LoadFromFile(path));
            if (traveler.GetLocation() == null)
            {
                if (traveler.GetStopCount() == 0)
                {
                    throw new DeskException($"{Path.GetFileName(path)} does not say where {traveler.GetName()} is.");
                }
                traveler.SetLocation(traveler[0]);
            }
            return traveler;
        }

        // ---- Route ----

        public void PlanRoute(string destination)
        {
            Traveler traveler = RequireActive();
            if (Map == null)
            {
                throw new DeskException("Load a map before planning a route.");
            }
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new DeskException("Choose a destination first.");
            }
            string target = RequireOnMap(destination);
            string start = traveler.GetLocation() ?? "";
            if (!Cities.Contains(start, StringComparer.OrdinalIgnoreCase))
            {
                throw new DeskException($"{start} is not on this map, so no route can start there.");
            }

            Snapshot();
            if (!traveler.PlanRouteTo(target, Map))
            {
                DropSnapshot();
                throw new DeskException($"No road connects {start} and {target}, directly or through other cities.");
            }
            MarkEdited();
            Log($"Traveler {traveler.GetName()} planned route {traveler.GetRoute()}");
        }

        public void AddStop(string city)
        {
            Traveler traveler = RequireActive();
            if (string.IsNullOrWhiteSpace(city))
            {
                throw new DeskException("Choose a city to add.");
            }
            string stop = RequireOnMap(city);
            Snapshot();
            traveler.AddCity(stop);
            MarkEdited();
            Log($"Traveler {traveler.GetName()} added stop {stop}");
        }

        public void RemoveStop(int index)
        {
            Traveler traveler = RequireActive();
            if (index < 0 || index >= traveler.GetStopCount())
            {
                return;
            }
            Snapshot();
            string stop = traveler[index];
            traveler.RemoveStopAt(index);
            MarkEdited();
            Log($"Traveler {traveler.GetName()} removed stop {stop}");
        }

        public void MoveStop(int index, int delta)
        {
            Traveler traveler = RequireActive();
            int target = index + delta;
            if (index < 0 || index >= traveler.GetStopCount() || target < 0 || target >= traveler.GetStopCount())
            {
                return;
            }
            Snapshot();
            traveler.MoveStop(index, target);
            MarkEdited();
            Log($"Traveler {traveler.GetName()} moved a stop: {traveler.GetRoute()}");
        }

        public void ClearRoute()
        {
            Traveler traveler = RequireActive();
            if (traveler.GetStopCount() == 0)
            {
                return;
            }
            Snapshot();
            traveler.ClearRoute();
            MarkEdited();
            Log($"Traveler {traveler.GetName()} cleared the route");
        }

        // Fills every gap in the route with the shortest road, keeping the chosen stops in order.
        public void MendRoute()
        {
            Traveler traveler = RequireActive();
            if (Map == null)
            {
                throw new DeskException("Load a map before mending a route.");
            }

            List<string> stops = FrontPageWriter.StopsOf(traveler);
            if (stops.Count == 0)
            {
                throw new DeskException("There is no route to mend yet.");
            }

            string? location = traveler.GetLocation();
            if (location != null && !string.Equals(location, stops[0], StringComparison.OrdinalIgnoreCase))
            {
                stops.Insert(0, location);
            }

            var mended = new List<string> { stops[0] };
            var unreachable = new List<string>();
            for (int i = 0; i + 1 < stops.Count; i++)
            {
                if (Map.GetRoadDistance(stops[i], stops[i + 1]) != null)
                {
                    mended.Add(stops[i + 1]);
                    continue;
                }
                List<string> path = Map.FindShortestPath(stops[i], stops[i + 1]);
                if (path.Count == 0)
                {
                    unreachable.Add($"{stops[i]} and {stops[i + 1]}");
                    mended.Add(stops[i + 1]);
                    continue;
                }
                mended.AddRange(path.Skip(1));
            }

            if (!mended.SequenceEqual(FrontPageWriter.StopsOf(traveler)))
            {
                Snapshot();
                traveler.ClearRoute();
                mended.ForEach(traveler.AddCity);
                MarkEdited();
                Log($"Traveler {traveler.GetName()} mended route: {traveler.GetRoute()}");
            }

            if (unreachable.Count > 0)
            {
                throw new DeskException($"No road at all connects {string.Join("; ", unreachable)}, so that gap stays open.");
            }
        }

        public void Undo()
        {
            if (undo.Count == 0)
            {
                return;
            }
            (int index, Traveler before, bool wasUnsaved) = undo[^1];
            undo.RemoveAt(undo.Count - 1);
            entries[index].Traveler = before;
            entries[index].Unsaved = wasUnsaved;
            ActiveIndex = index;
            Log($"Undid an edit: {before.GetName()} is back to {before.GetRoute()}");
        }

        // ---- Helpers ----

        private Traveler RequireActive()
        {
            return Active ?? throw new DeskException("Sign in a traveler first.");
        }

        // Returns the map's own spelling of a city. Without a map, any name is accepted.
        private string RequireOnMap(string city)
        {
            string trimmed = city.Trim();
            if (Map == null)
            {
                return trimmed;
            }
            string? onMap = Map.GetCities().FirstOrDefault(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase));
            return onMap ?? throw new DeskException($"{trimmed} is not on this map.");
        }

        private void Snapshot()
        {
            undo.Add((ActiveIndex, (Traveler)entries[ActiveIndex].Traveler.Clone(), entries[ActiveIndex].Unsaved));
            if (undo.Count > MaxUndoSteps)
            {
                undo.RemoveAt(0);
            }
        }

        private void DropSnapshot() => undo.RemoveAt(undo.Count - 1);

        private void MarkEdited() => entries[ActiveIndex].Unsaved = true;

        private static T ReadFile<T>(string path, Func<T> read)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new DeskException("No file was chosen.");
            }
            try
            {
                return read();
            }
            catch (FileLoadException ex)
            {
                throw new DeskException($"{Path.GetFileName(path)} is not valid travel data.", ex);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                throw new DeskException($"There is no file at {path}.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new DeskException($"{path} could not be opened: it is a folder, or you do not have permission.", ex);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
            {
                throw new DeskException($"{path} could not be used: {ex.Message}", ex);
            }
        }

        // Logging must never take the app down, so a failed write only keeps the entry for the next try.
        private void Log(string message)
        {
            log.Add($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} {message}");
            try
            {
                log.Flush(logPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
        }
    }
}
