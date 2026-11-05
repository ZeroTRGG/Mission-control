using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MissionControl.Controls;
using MissionControl.Desk;
using MissionControl.Press;
using Travelling;

namespace MissionControl
{
    public partial class MainWindow : Window
    {
        private const double StackBelowWidth = 1000;

        private readonly MissionDesk desk;
        private readonly Func<DateTime> clock;
        private IDeskPrompts prompts;

        private CityGraph? mapShownInLists;
        private bool refreshing;
        private bool closeConfirmed;

        public MainWindow() : this(new MissionDesk(), null, () => DateTime.Now)
        {
        }

        public MainWindow(MissionDesk desk, IDeskPrompts? prompts, Func<DateTime> clock)
        {
            this.desk = desk;
            this.clock = clock;
            this.prompts = prompts ?? new WindowPrompts(this);

            InitializeComponent();
            Background = PaperBackground.CreateBrush();

            desk.Changed += Refresh;
            Dispatcher.UIThread.UnhandledException += OnUnhandledUiException;
            Closing += OnClosing;
            Closed += (_, _) => Dispatcher.UIThread.UnhandledException -= OnUnhandledUiException;
            Refresh();
        }

        // ---- Layout ----

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyLayout(e.NewSize.Width);
        }

        private void ApplyLayout(double width)
        {
            bool stacked = width < StackBelowWidth;
            Root.ColumnDefinitions = new ColumnDefinitions(stacked ? "*" : "372,*");
            Root.RowDefinitions = new RowDefinitions(stacked ? "2*,3*" : "*");
            Grid.SetColumn(PaperScroll, stacked ? 0 : 1);
            Grid.SetRow(PaperScroll, stacked ? 1 : 0);
            DeskPane.BorderThickness = stacked ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 2, 0);
        }

        // ---- Showing the desk's state ----

        private void Refresh()
        {
            refreshing = true;
            try
            {
                RefreshCityLists();
                RefreshRoster();
                RefreshStops();
                RefreshButtons();
                Paper.Show(desk.WriteFrontPage(clock()), desk.Erratum);
                if (desk.Erratum != null)
                {
                    PaperScroll.Offset = default;
                }
            }
            finally
            {
                refreshing = false;
            }
        }

        private void RefreshCityLists()
        {
            if (desk.Map == mapShownInLists)
            {
                return;
            }
            mapShownInLists = desk.Map;
            List<string> cities = desk.Cities.ToList();
            StartBox.ItemsSource = cities;
            DestinationBox.ItemsSource = cities;
            AddStopBox.ItemsSource = cities;
            MapStatus.Text = desk.Map == null
                ? "No map on the desk."
                : $"{desk.MapName}: {cities.Count} {(cities.Count == 1 ? "city" : "cities")}";
        }

        private void RefreshRoster()
        {
            var names = new List<string>();
            for (int i = 0; i < desk.Travelers.Count; i++)
            {
                Traveler traveler = desk.Travelers[i];
                names.Add($"{traveler.GetName()}, {traveler.GetLocation()}{(desk.IsUnsaved(i) ? " (unsaved)" : "")}");
            }
            // Replacing identical items would drop keyboard focus while arrowing through the list.
            if (Roster.ItemsSource is not List<string> shown || !shown.SequenceEqual(names))
            {
                Roster.ItemsSource = names;
            }
            Roster.SelectedIndex = desk.ActiveIndex;
            Roster.IsVisible = names.Count > 0;
        }

        private void RefreshStops()
        {
            StopList.Children.Clear();
            Traveler? traveler = desk.Active;
            int count = traveler?.GetStopCount() ?? 0;
            NoStopsNote.IsVisible = count == 0;
            if (traveler == null || count == 0)
            {
                return;
            }

            List<Leg> legs = FrontPageWriter.LegsOf(traveler, desk.Map);
            for (int i = 0; i < count; i++)
            {
                StopList.Children.Add(StopRow(i, count, traveler[i], i < legs.Count ? legs[i] : null));
            }
        }

        private Control StopRow(int index, int count, string city, Leg? legToNext)
        {
            var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock
            {
                Text = $"{index + 1}. {city}",
                FontSize = 17,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            if (legToNext != null && desk.Map != null)
            {
                bool broken = legToNext.Km == null;
                name.Children.Add(new TextBlock
                {
                    Text = broken ? "no road to the next stop" : $"{legToNext.Km} km to the next stop",
                    FontSize = 14,
                    Foreground = broken ? Ink.Marker : Ink.Black,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,4,Auto,4,Auto"), Margin = new Thickness(0, 2) };
            row.Children.Add(name);
            AddRowButton(row, 1, "↑", $"Move {city} up", index > 0, () => MoveStop(index, -1), $"up{index}");
            AddRowButton(row, 3, "↓", $"Move {city} down", index < count - 1, () => MoveStop(index, 1), $"down{index}");
            AddRowButton(row, 5, "×", $"Remove {city}", true, () => desk.Attempt(() => desk.RemoveStop(index)), $"remove{index}");
            return row;
        }

        private static void AddRowButton(Grid row, int column, string glyph, string label, bool enabled, Action click, string name)
        {
            var button = new Button { Content = glyph, IsEnabled = enabled, Name = name };
            button.Classes.Add("stop");
            AutomationProperties.SetName(button, label);
            ToolTip.SetTip(button, label);
            button.Click += (_, _) => click();
            Grid.SetColumn(button, column);
            row.Children.Add(button);
        }

        // Rebuilding the rows would lose keyboard focus, so put it back on the moved stop.
        private void MoveStop(int index, int delta)
        {
            desk.Attempt(() => desk.MoveStop(index, delta));
            string direction = delta < 0 ? "up" : "down";
            int moved = index + delta;
            Button? button = StopList.GetVisualDescendantButtons().FirstOrDefault(b => b.Name == direction + moved && b.IsEnabled)
                ?? StopList.GetVisualDescendantButtons().FirstOrDefault(b => b.Name == "remove" + moved);
            button?.Focus();
        }

        private void RefreshButtons()
        {
            UndoButton.IsEnabled = desk.CanUndo;
            ClearButton.IsEnabled = desk.Active?.GetStopCount() > 0;
            MendButton.IsEnabled = desk.Active?.GetStopCount() > 0;
            SaveTravelerButton.IsEnabled = desk.Active != null;
            RemoveTravelerButton.IsEnabled = desk.Active != null;
        }

        // ---- Map ----

        private async void OnLoadMap(object? sender, RoutedEventArgs e)
        {
            await desk.AttemptAsync(async () =>
            {
                string? path = await prompts.PickMapFileAsync();
                if (path != null)
                {
                    desk.LoadMap(path);
                }
            });
        }

        private void OnUseSampleMap(object? sender, RoutedEventArgs e)
        {
            desk.Attempt(() => desk.LoadMap(Path.Combine(AppContext.BaseDirectory, "Assets", "sample-map.txt")));
        }

        // ---- Travelers ----

        private void OnSignIn(object? sender, RoutedEventArgs e) => SignIn();

        private void OnNameKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SignIn();
            }
        }

        private void SignIn()
        {
            bool created = desk.Attempt(() => desk.CreateTraveler(NameBox.Text ?? "", StartBox.SelectedItem as string ?? ""));
            if (created)
            {
                NameBox.Text = "";
                StartBox.SelectedItem = null;
            }
        }

        private void OnRosterSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!refreshing && Roster.SelectedIndex >= 0)
            {
                desk.Attempt(() => desk.SelectTraveler(Roster.SelectedIndex));
            }
        }

        private async void OnRemoveTraveler(object? sender, RoutedEventArgs e)
        {
            await desk.AttemptAsync(async () =>
            {
                Traveler? traveler = desk.Active;
                if (traveler == null)
                {
                    return;
                }
                if (!desk.ActiveIsUnsaved || await prompts.ConfirmDiscardAsync($"{traveler.GetName()} has unsaved changes. Remove anyway?"))
                {
                    desk.RemoveActive();
                }
            });
        }

        private async void OnLoadTraveler(object? sender, RoutedEventArgs e)
        {
            await desk.AttemptAsync(async () =>
            {
                string? path = await prompts.PickTravelerFileAsync();
                if (path == null)
                {
                    return;
                }
                Traveler? replaced = desk.UnsavedTravelerReplacedBy(path);
                if (replaced == null || await prompts.ConfirmDiscardAsync($"Loading this file replaces {replaced.GetName()}, who has unsaved changes. Continue?"))
                {
                    desk.LoadTraveler(path);
                }
            });
        }

        private async void OnSaveTraveler(object? sender, RoutedEventArgs e)
        {
            await desk.AttemptAsync(async () =>
            {
                Traveler? traveler = desk.Active;
                if (traveler == null)
                {
                    throw new DeskException("Sign in a traveler first.");
                }
                string? path = await prompts.PickSaveFileAsync(FileNames.ForTraveler(traveler.GetName()));
                if (path != null)
                {
                    desk.SaveActive(path);
                }
            });
        }

        // ---- Route ----

        private void OnPlanRoute(object? sender, RoutedEventArgs e)
        {
            desk.Attempt(() => desk.PlanRoute(DestinationBox.SelectedItem as string ?? ""));
        }

        private void OnAddStop(object? sender, RoutedEventArgs e)
        {
            bool added = desk.Attempt(() => desk.AddStop(AddStopBox.SelectedItem as string ?? ""));
            if (added)
            {
                AddStopBox.SelectedItem = null;
            }
        }

        private void OnMendRoute(object? sender, RoutedEventArgs e) => desk.Attempt(desk.MendRoute);

        private void OnUndo(object? sender, RoutedEventArgs e) => desk.Attempt(desk.Undo);

        private void OnClearRoute(object? sender, RoutedEventArgs e) => desk.Attempt(desk.ClearRoute);

        // ---- Backstops ----

        private void OnUnhandledUiException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            desk.Report(e.Exception);
            Refresh();
        }

        private async void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (closeConfirmed || !desk.HasUnsavedChanges)
            {
                return;
            }

            e.Cancel = true;
            await desk.AttemptAsync(async () =>
            {
                if (await prompts.ConfirmDiscardAsync("Some travelers have unsaved changes. Close Mission Control anyway?"))
                {
                    closeConfirmed = true;
                    Close();
                }
            });
        }
    }

    internal static class StopListExtensions
    {
        public static IEnumerable<Button> GetVisualDescendantButtons(this StackPanel list)
        {
            return list.Children.OfType<Grid>().SelectMany(row => row.Children.OfType<Button>());
        }
    }
}
