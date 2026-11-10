using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Sakuno.Avalonia.Headless.Driver.Sessions;

namespace Sakuno.Avalonia.Headless.Driver.Automation;

public sealed class TopLevelRegistry(HeadlessSession session)
{
    private readonly Dictionary<TopLevel, string> _ids = new(ReferenceEqualityComparer.Instance);
    private int _nextId;

    public IReadOnlyList<(TopLevel TopLevel, WindowInfo Info)> GetWindows()
    {
        var topLevels = new List<TopLevel>();
        Add(session.InitialWindow);
        AddOwnedWindows(session.InitialWindow);
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            foreach (var window in desktop.Windows)
            {
                Add(window);
                AddOwnedWindows(window);
            }

        var popups = topLevels.OfType<Window>()
            .SelectMany(window => window.GetLogicalDescendants().OfType<Popup>())
            .Where(popup => popup.IsOpen && popup.Child is not null)
            .Select(popup => TopLevel.GetTopLevel(popup.Child!) as PopupRoot)
            .OfType<PopupRoot>();
        foreach (var popup in popups)
            Add(popup);

        var active = topLevels.Where(x => x.IsVisible).Distinct().ToArray();
        foreach (var stale in _ids.Keys.Where(key => !active.Contains(key, ReferenceEqualityComparer.Instance)).ToArray())
            _ids.Remove(stale);

        return active.Select((topLevel, index) =>
        {
            if (!_ids.TryGetValue(topLevel, out var id))
                _ids[topLevel] = id = $"w{++_nextId}";
            var title = topLevel is Window window ? window.Title ?? string.Empty : "Popup";
            var kind = topLevel is PopupRoot ? "popup" : "window";
            return (topLevel, new WindowInfo(id, title, kind, topLevel.ClientSize.Width, topLevel.ClientSize.Height, topLevel.RenderScaling));
        }).ToArray();

        void Add(TopLevel? topLevel)
        {
            if (topLevel is not null && !topLevels.Contains(topLevel, ReferenceEqualityComparer.Instance))
                topLevels.Add(topLevel);
        }

        void AddOwnedWindows(TopLevel topLevel)
        {
            if (topLevel is not Window window)
                return;
            foreach (var owned in window.OwnedWindows)
            {
                Add(owned);
                AddOwnedWindows(owned);
            }
        }
    }

    public TopLevel Resolve(string windowId)
    {
        var match = GetWindows().SingleOrDefault(x => x.Info.Id == windowId);
        if (match.TopLevel is null)
            throw new DriverException("window_not_found", "The requested window is not active.", new { windowId });
        return match.TopLevel;
    }
}
