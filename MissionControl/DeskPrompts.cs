using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using MissionControl.Controls;

namespace MissionControl
{
    // Everything that stops to ask the person a question, kept behind an interface so
    // tests can answer without opening a native file dialog.
    public interface IDeskPrompts
    {
        Task<string?> PickMapFileAsync();
        Task<string?> PickTravelerFileAsync();
        Task<string?> PickSaveFileAsync(string suggestedName);
        Task<bool> ConfirmDiscardAsync(string message);
    }

    public sealed class WindowPrompts : IDeskPrompts
    {
        private static readonly FilePickerFileType MapFiles = new("Map files") { Patterns = new[] { "*.txt", "*.map" } };
        private static readonly FilePickerFileType TravelerFiles = new("Traveler files") { Patterns = new[] { "*.json" } };
        private static readonly FilePickerFileType AllFiles = new("All files") { Patterns = new[] { "*" } };

        private readonly Window owner;

        public WindowPrompts(Window owner)
        {
            this.owner = owner;
        }

        public Task<string?> PickMapFileAsync() => PickFileAsync("Load a map", MapFiles);

        public Task<string?> PickTravelerFileAsync() => PickFileAsync("Load a traveler", TravelerFiles);

        private async Task<string?> PickFileAsync(string title, FilePickerFileType type)
        {
            IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[] { type, AllFiles }
            });
            return files.FirstOrDefault()?.TryGetLocalPath();
        }

        public async Task<string?> PickSaveFileAsync(string suggestedName)
        {
            IStorageFile? file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save traveler",
                SuggestedFileName = suggestedName,
                DefaultExtension = "json",
                FileTypeChoices = new[] { TravelerFiles }
            });
            return file?.TryGetLocalPath();
        }

        public Task<bool> ConfirmDiscardAsync(string message)
        {
            return new ConfirmDiscardWindow(message).ShowDialog<bool>(owner);
        }
    }

    public sealed class ConfirmDiscardWindow : Window
    {
        public ConfirmDiscardWindow(string message)
        {
            Title = "Unsaved changes";
            Width = 420;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = PaperBackground.CreateBrush();

            var discard = new Button { Content = "Discard changes" };
            var keep = new Button { Content = "Keep working", IsDefault = true, IsCancel = true };
            keep.Classes.Add("primary");
            discard.Click += (_, _) => Close(true);
            keep.Click += (_, _) => Close(false);

            var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
            Grid.SetColumn(keep, 2);
            buttons.Children.Add(discard);
            buttons.Children.Add(keep);

            var body = new StackPanel { Margin = new Thickness(20), Spacing = 16, HorizontalAlignment = HorizontalAlignment.Stretch };
            body.Children.Add(Print.Text(message, 19));
            body.Children.Add(buttons);
            Content = body;
        }
    }

    public static class FileNames
    {
        // Turns a traveler's name into a safe default file name.
        public static string ForTraveler(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return (safe.Length == 0 ? "traveler" : safe) + ".json";
        }
    }
}
