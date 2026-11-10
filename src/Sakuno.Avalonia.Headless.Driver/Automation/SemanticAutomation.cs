using global::Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Sakuno.Avalonia.Headless.Driver.Configuration;
using Sakuno.Avalonia.Headless.Driver.Sessions;

namespace Sakuno.Avalonia.Headless.Driver.Automation;

public sealed class SemanticAutomation(HeadlessSession session)
{
    private readonly TopLevelRegistry _windows = new(session);
    private long _snapshotId;

    public IReadOnlyList<WindowInfo> Windows => _windows.GetWindows().Select(x => x.Info).ToArray();

    public Task<SnapshotResponse> SnapshotAsync(
        string? windowId,
        string? subtreePath,
        int? maxDepth,
        int? maxNodes,
        CancellationToken cancellationToken) =>
        session.ExecuteAsync(token => session.DispatchAsync(() =>
        {
            var configuration = session.Configuration.Snapshot;
            var depth = maxDepth ?? configuration.MaxDepth;
            var nodes = maxNodes ?? configuration.MaxNodes;
            if (depth is < 1 or > 32 || nodes is < 1 or > 10000)
                throw new DriverException("invalid_request", "Snapshot limits exceed the supported bounds.");

            var selected = windowId is null
                ? _windows.GetWindows()
                : _windows.GetWindows().Where(x => x.Info.Id == windowId).ToArray();
            if (windowId is not null && selected.Count == 0)
                throw new DriverException("window_not_found", "The requested window is not active.", new { windowId });

            var remaining = nodes;
            var truncated = false;
            var roots = new List<SemanticNode>();
            foreach (var (topLevel, _) in selected)
            {
                Control root = topLevel;
                var rootPath = topLevel is Window ? "0" : "0";
                if (subtreePath is not null)
                {
                    root = ResolveSubtree(topLevel, subtreePath);
                    rootPath = subtreePath;
                }
                roots.Add(CreateNode(root, topLevel, rootPath, depth, configuration, ref remaining, ref truncated));
            }
            return new SnapshotResponse
            {
                SnapshotId = Interlocked.Increment(ref _snapshotId),
                SessionId = session.Id,
                Windows = Windows,
                Roots = roots,
                Truncated = truncated
            };
        }, token), session.Configuration.ActionTimeout, cancellationToken);

    public Task<QueryResponse> QueryAsync(ElementQuery query, CancellationToken cancellationToken) =>
        session.ExecuteAsync(token => session.DispatchAsync(() =>
        {
            var matches = FindControls(query, 100);
            return new QueryResponse(
                Interlocked.Increment(ref _snapshotId),
                matches.Select(x => CreateNode(x.Control, x.TopLevel, x.Path, 0, session.Configuration.Snapshot)).ToArray());
        }, token), session.Configuration.ActionTimeout, cancellationToken);

    internal Control ResolveSingle(ElementQuery query)
    {
        var matches = FindControls(query, 101);
        if (matches.Count == 0)
            throw new DriverException("element_not_found", "No observable control matches the Element Query.", new { query });
        if (matches.Count > 1)
            throw new DriverException("ambiguous_element", "The Element Query matches more than one control.", new
            {
                candidates = matches.Take(10).Select(x => CreateNode(x.Control, x.TopLevel, x.Path, 0, session.Configuration.Snapshot)).ToArray(),
                truncated = matches.Count > 10
            });
        return matches[0].Control;
    }

    internal TopLevel ResolveWindow(Control control) =>
        TopLevel.GetTopLevel(control)
        ?? throw new DriverException("element_not_actionable", "The control is not attached to an active TopLevel.");

    internal SemanticNode Describe(Control control)
    {
        var topLevel = ResolveWindow(control);
        return CreateNode(control, topLevel, "0", 0, session.Configuration.Snapshot);
    }

    private IReadOnlyList<(Control Control, TopLevel TopLevel, string Path)> FindControls(ElementQuery query, int limit)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.AutomationId is null && query.Name is null && query.Text is null && query.Role is null && query.ControlType is null)
            throw new DriverException("invalid_request", "At least one Element Query criterion is required.");

        var windows = query.WindowId is null
            ? _windows.GetWindows()
            : _windows.GetWindows().Where(x => x.Info.Id == query.WindowId).ToArray();
        if (query.WindowId is not null && windows.Count == 0)
            throw new DriverException("window_not_found", "The requested window is not active.", new { query.WindowId });

        var results = new List<(Control, TopLevel, string)>();
        foreach (var (topLevel, _) in windows)
        {
            var root = query.SubtreePath is null ? topLevel : ResolveSubtree(topLevel, query.SubtreePath);
            Visit(root, query.SubtreePath ?? "0", topLevel);
            if (results.Count >= limit)
                break;
        }
        return results;

        void Visit(Control control, string path, TopLevel topLevel)
        {
            if (results.Count >= limit)
                return;
            if (Matches(control, query))
                results.Add((control, topLevel, path));
            var index = 0;
            foreach (var child in control.GetLogicalChildren().OfType<Control>())
            {
                Visit(child, $"{path}/{index++}", topLevel);
                if (results.Count >= limit)
                    return;
            }
        }
    }

    private static bool Matches(Control control, ElementQuery query)
    {
        if (!query.IncludeHidden && !control.IsEffectivelyVisible)
            return false;
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        var automationId = peer?.GetAutomationId() ?? AutomationProperties.GetAutomationId(control) ?? control.Name;
        var name = peer?.GetName() ?? AutomationProperties.GetName(control);
        var role = peer?.GetAutomationControlType().ToString();
        var value = GetValue(control);
        return MatchesText(automationId, query.AutomationId, TextMatchKind.Exact)
            && MatchesText(name, query.Name, query.NameMatch)
            && MatchesText(value, query.Text, query.TextMatch)
            && MatchesText(role, query.Role, TextMatchKind.Exact)
            && MatchesText(control.GetType().Name, query.ControlType, TextMatchKind.Exact);
    }

    private static bool MatchesText(string? actual, string? expected, TextMatchKind kind) =>
        expected is null || actual is not null && (kind == TextMatchKind.Contains
            ? actual.Contains(expected, StringComparison.Ordinal)
            : string.Equals(actual, expected, StringComparison.Ordinal));

    private static Control ResolveSubtree(TopLevel topLevel, string path)
    {
        Control current = topLevel;
        if (path == "0" || string.IsNullOrEmpty(path))
            return current;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments[0] != "0")
            throw new DriverException("invalid_request", "A subtree path must begin at root 0.");
        foreach (var segment in segments.Skip(1))
        {
            if (!int.TryParse(segment, out var index) || index < 0)
                throw new DriverException("invalid_request", "The subtree path is invalid.");
            var child = current.GetLogicalChildren().OfType<Control>().ElementAtOrDefault(index);
            if (child is null)
                throw new DriverException("element_not_found", "The requested subtree is no longer observable.");
            current = child;
        }
        return current;
    }

    private SemanticNode CreateNode(
        Control control,
        TopLevel topLevel,
        string path,
        int depth,
        SnapshotConfiguration configuration,
        ref int remaining,
        ref bool truncated)
    {
        if (remaining <= 0)
        {
            truncated = true;
            return new SemanticNode { Path = path, ControlType = control.GetType().Name, IsTruncated = true };
        }
        remaining--;
        var children = new List<SemanticNode>();
        if (depth > 0)
        {
            var index = 0;
            foreach (var child in control.GetLogicalChildren().OfType<Control>())
            {
                if (remaining <= 0)
                {
                    truncated = true;
                    break;
                }
                children.Add(CreateNode(child, topLevel, $"{path}/{index++}", depth - 1, configuration, ref remaining, ref truncated));
            }
        }
        else if (control.GetLogicalChildren().OfType<Control>().Any())
        {
            truncated = true;
        }
        return CreateNode(control, topLevel, path, 0, configuration) with
        {
            Children = children,
            IsTruncated = depth == 0 && control.GetLogicalChildren().OfType<Control>().Any()
        };
    }

    private SemanticNode CreateNode(Control control, TopLevel topLevel, string path, int depth, SnapshotConfiguration configuration)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        var automationId = peer?.GetAutomationId() ?? AutomationProperties.GetAutomationId(control) ?? control.Name;
        var name = peer?.GetName() ?? AutomationProperties.GetName(control);
        var origin = control.TranslatePoint(new global::Avalonia.Point(0, 0), topLevel);
        return new SemanticNode
        {
            Path = path,
            ControlType = control.GetType().Name,
            Name = Limit(name, configuration.MaxTextLength),
            AutomationId = Limit(automationId, configuration.MaxTextLength),
            Role = peer?.GetAutomationControlType().ToString(),
            Value = Limit(GetValue(control), configuration.MaxTextLength),
            IsVisible = control.IsEffectivelyVisible,
            IsEnabled = control.IsEffectivelyEnabled,
            IsFocused = control.IsFocused,
            IsSelected = control is SelectingItemsControl selecting ? selecting.SelectedIndex >= 0 : null,
            IsToggled = control is ToggleButton toggle ? toggle.IsChecked : null,
            Bounds = origin is { } point
                ? new BoundsInfo(point.X, point.Y, control.Bounds.Width, control.Bounds.Height)
                : null
        };
    }

    private static string? GetValue(Control control) => control switch
    {
        TextBox textBox when textBox.PasswordChar != '\0' => "********",
        TextBox textBox => textBox.Text,
        TextBlock textBlock => textBlock.Text,
        ContentControl content when content.Content is string text => text,
        SelectingItemsControl selecting when selecting.SelectedItem is string text => text,
        RangeBase range => range.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => null
    };

    private static string? Limit(string? value, int maximum) =>
        value is null || value.Length <= maximum ? value : value[..maximum] + "…";
}
