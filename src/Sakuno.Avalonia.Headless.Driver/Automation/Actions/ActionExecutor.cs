using global::Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Sakuno.Avalonia.Headless.Driver.Sessions;

namespace Sakuno.Avalonia.Headless.Driver.Automation.Actions;

public sealed class ActionExecutor(HeadlessSession session, SemanticAutomation automation)
{
    public Task<ActionResponse> ExecuteAsync(UiAction action, CancellationToken cancellationToken) =>
        session.ExecuteAsync(token => session.DispatchAsync(() => Execute(action), token), session.Configuration.ActionTimeout, cancellationToken);

    private ActionResponse Execute(UiAction action)
    {
        var before = automation.LastObservationId;
        Control target;
        switch (action)
        {
            case TargetedAction targeted:
                target = automation.ResolveSingle(targeted.Query);
                ValidateTarget(target);
                target.BringIntoView();
                target.UpdateLayout();
                ExecuteTargeted(target, targeted);
                break;
            case KeyAction key:
                var keyWindow = automation.ResolveWindow(key.WindowId);
                if (!Enum.TryParse<Key>(key.Key, false, out var parsedKey))
                    throw new DriverException("invalid_request", "The key name is not supported.");
                keyWindow.KeyPress(parsedKey, ParseModifiers(key.Modifiers), ParsePhysicalKey(key.Key), null);
                target = keyWindow;
                break;
            case PointerAction pointer:
                var pointerWindow = automation.ResolveWindow(pointer.WindowId);
                var point = new Point(pointer.X, pointer.Y);
                var modifiers = ParseModifiers(pointer.Modifiers);
                var button = ParseButton(pointer.Button);
                switch (pointer.Event)
                {
                    case "move": pointerWindow.MouseMove(point, modifiers); break;
                    case "down": pointerWindow.MouseDown(point, button, modifiers); break;
                    case "up": pointerWindow.MouseUp(point, button, modifiers); break;
                    case "click": Click(pointerWindow, point, button, modifiers); break;
                    default: throw new DriverException("invalid_request", "Pointer event must be move, down, up, or click.");
                }
                target = pointerWindow;
                break;
            default:
                throw new DriverException("invalid_request", "The action type is not supported.");
        }
        return new ActionResponse(before, automation.NextObservationId(), automation.Describe(target), true, true);
    }

    private void ExecuteTargeted(Control target, TargetedAction action)
    {
        var topLevel = automation.ResolveWindow(target);
        var point = Center(target, topLevel);
        switch (action)
        {
            case ClickAction:
                Click(topLevel, point, MouseButton.Left, RawInputModifiers.None);
                break;
            case FocusAction:
                if (!target.Focus())
                    throw new DriverException("element_not_actionable", "The target cannot receive focus.");
                break;
            case ToggleAction when target is ToggleButton:
                Click(topLevel, point, MouseButton.Left, RawInputModifiers.None);
                break;
            case ToggleAction:
                throw new DriverException("element_not_actionable", "The target does not support toggling.");
            case SelectAction select when target is SelectingItemsControl selecting:
                Select(selecting, select.Value, topLevel);
                break;
            case SelectAction:
                throw new DriverException("element_not_actionable", "The target does not support selection.");
            case ScrollAction scroll:
                topLevel.MouseWheel(point, new Vector(scroll.DeltaX, scroll.DeltaY), RawInputModifiers.None);
                break;
            case TextAction text when target is TextBox:
                target.Focus();
                if (text.Mode == TextMode.Replace)
                    SelectAll(topLevel);
                topLevel.KeyTextInput(text.Text);
                break;
            case TextAction:
                throw new DriverException("element_not_actionable", "The target does not accept text input.");
            case ClearTextAction when target is TextBox:
                target.Focus();
                SelectAll(topLevel);
                topLevel.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                break;
            case ClearTextAction:
                throw new DriverException("element_not_actionable", "The target does not accept text input.");
            default:
                throw new DriverException("invalid_request", "The targeted action is not supported.");
        }
    }

    private static void Select(SelectingItemsControl target, string value, TopLevel topLevel)
    {
        var items = target.Items.Cast<object?>().ToArray();
        var index = Array.FindIndex(items, item => string.Equals(item?.ToString(), value, StringComparison.Ordinal));
        if (index < 0)
            throw new DriverException("element_not_found", "The requested selection is not available.");
        if (index > 10000)
            throw new DriverException("invalid_request", "Selection exceeds the bounded input sequence.");
        Click(topLevel, Center(target, topLevel), MouseButton.Left, RawInputModifiers.None);
        topLevel.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
        for (var current = 0; current < index; current++)
            topLevel.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        topLevel.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
    }

    private static void ValidateTarget(Control target)
    {
        if (!target.IsEffectivelyVisible || !target.IsEffectivelyEnabled)
            throw new DriverException("element_not_actionable", "The target is hidden or disabled.", new
            {
                target.IsEffectivelyVisible,
                target.IsEffectivelyEnabled
            });
    }

    private static Point Center(Control target, TopLevel topLevel)
    {
        var origin = target.TranslatePoint(new Point(0, 0), topLevel)
            ?? throw new DriverException("element_not_actionable", "The target bounds are unavailable.");
        return new Point(origin.X + target.Bounds.Width / 2, origin.Y + target.Bounds.Height / 2);
    }

    private static void Click(TopLevel topLevel, Point point, MouseButton button, RawInputModifiers modifiers)
    {
        topLevel.MouseMove(point, modifiers);
        topLevel.MouseDown(point, button, modifiers);
        topLevel.MouseUp(point, button, modifiers);
    }

    private static void SelectAll(TopLevel topLevel) =>
        topLevel.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);

    private static PhysicalKey ParsePhysicalKey(string key) =>
        Enum.TryParse<PhysicalKey>(key, false, out var physical) ? physical : PhysicalKey.None;

    private static MouseButton ParseButton(string button) => button switch
    {
        "left" => MouseButton.Left,
        "right" => MouseButton.Right,
        "middle" => MouseButton.Middle,
        _ => throw new DriverException("invalid_request", "Mouse button must be left, right, or middle.")
    };

    private static RawInputModifiers ParseModifiers(IEnumerable<string> modifiers)
    {
        var result = RawInputModifiers.None;
        foreach (var modifier in modifiers)
        {
            if (!Enum.TryParse<RawInputModifiers>(modifier, false, out var parsed))
                throw new DriverException("invalid_request", "A raw input modifier is not supported.");
            result |= parsed;
        }
        return result;
    }
}
