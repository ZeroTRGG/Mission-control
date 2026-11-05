using System.Collections.Specialized;
using Avalonia.Controls;

namespace MissionControl.Controls
{
    // A panel that can also draw its own rules. Avalonia's Panel seals Render, so this hosts children itself.
    public abstract class DrawnPanel : Control
    {
        public Avalonia.Controls.Controls Children { get; } = new();

        protected DrawnPanel()
        {
            Children.CollectionChanged += OnChildrenChanged;
        }

        private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (Control child in e.OldItems)
                {
                    LogicalChildren.Remove(child);
                    VisualChildren.Remove(child);
                }
            }
            if (e.NewItems != null)
            {
                foreach (Control child in e.NewItems)
                {
                    LogicalChildren.Add(child);
                    VisualChildren.Add(child);
                }
            }
            InvalidateMeasure();
        }
    }
}
