using System.Text.Json.Serialization;

namespace Sakuno.Avalonia.Headless.Driver.Automation.Actions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ClickAction), "click")]
[JsonDerivedType(typeof(FocusAction), "focus")]
[JsonDerivedType(typeof(ToggleAction), "toggle")]
[JsonDerivedType(typeof(SelectAction), "select")]
[JsonDerivedType(typeof(ScrollAction), "scroll")]
[JsonDerivedType(typeof(TextAction), "text")]
[JsonDerivedType(typeof(ClearTextAction), "clear")]
[JsonDerivedType(typeof(KeyAction), "key")]
[JsonDerivedType(typeof(PointerAction), "pointer")]
public abstract record UiAction;
public abstract record TargetedAction : UiAction
{
    public required ElementQuery Query { get; init; }
}
public sealed record ClickAction : TargetedAction;
public sealed record FocusAction : TargetedAction;
public sealed record ToggleAction : TargetedAction;
public sealed record SelectAction : TargetedAction
{
    public required string Value { get; init; }
}
public sealed record ScrollAction : TargetedAction
{
    public double DeltaX { get; init; }
    public double DeltaY { get; init; }
}
public enum TextMode { Replace, Append }
public sealed record TextAction : TargetedAction
{
    public required string Text { get; init; }
    public TextMode Mode { get; init; }
}
public sealed record ClearTextAction : TargetedAction;
public sealed record KeyAction : UiAction
{
    public required string WindowId { get; init; }
    public required string Key { get; init; }
    public IReadOnlyList<string> Modifiers { get; init; } = [];
}
public sealed record PointerAction : UiAction
{
    public required string WindowId { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public string Event { get; init; } = "click";
    public string Button { get; init; } = "left";
    public IReadOnlyList<string> Modifiers { get; init; } = [];
}
public sealed record ActionResponse(long BeforeObservationId, long AfterObservationId, SemanticNode Target, bool InputExecuted, bool UiQueueProcessed);
