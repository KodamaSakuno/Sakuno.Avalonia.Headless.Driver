namespace Sakuno.Avalonia.Headless.Driver.Automation;

public sealed record WindowInfo(string Id, string Title, string Kind, double Width, double Height, double RenderScaling);
public sealed record BoundsInfo(double X, double Y, double Width, double Height);

public sealed record SemanticNode
{
    public required string Path { get; init; }
    public required string ControlType { get; init; }
    public string? Name { get; init; }
    public string? AutomationId { get; init; }
    public string? Role { get; init; }
    public string? Value { get; init; }
    public bool IsVisible { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsFocused { get; init; }
    public bool? IsSelected { get; init; }
    public bool? IsToggled { get; init; }
    public BoundsInfo? Bounds { get; init; }
    public bool IsTruncated { get; init; }
    public IReadOnlyList<SemanticNode> Children { get; init; } = [];
}

public sealed record SnapshotResponse
{
    public required long SnapshotId { get; init; }
    public required string SessionId { get; init; }
    public required IReadOnlyList<WindowInfo> Windows { get; init; }
    public required IReadOnlyList<SemanticNode> Roots { get; init; }
    public bool Truncated { get; init; }
}

public enum TextMatchKind { Exact, Contains }

public sealed record ElementQuery
{
    public string? WindowId { get; init; }
    public string? AutomationId { get; init; }
    public string? Name { get; init; }
    public TextMatchKind NameMatch { get; init; }
    public string? Text { get; init; }
    public TextMatchKind TextMatch { get; init; }
    public string? Role { get; init; }
    public string? ControlType { get; init; }
    public bool IncludeHidden { get; init; }
    public string? SubtreePath { get; init; }
}

public sealed record QueryResponse(long SnapshotId, IReadOnlyList<SemanticNode> Matches, bool Truncated = false);

public sealed record SessionResponse(string SessionId, IReadOnlyList<WindowInfo> Windows);
