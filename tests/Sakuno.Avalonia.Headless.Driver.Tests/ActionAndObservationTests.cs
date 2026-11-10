using System.Text.Json;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Sakuno.Avalonia.Headless.Driver.Automation;
using Sakuno.Avalonia.Headless.Driver.Automation.Actions;
using Sakuno.Avalonia.Headless.Driver.Automation.Waits;
using Sakuno.Avalonia.Headless.Driver.Sample;
using Sakuno.Avalonia.Headless.Driver.Sessions;

namespace Sakuno.Avalonia.Headless.Driver.Tests;

[Collection("Headless")]
public sealed class ActionAndObservationTests
{
    private readonly HeadlessSession _session;
    private readonly SemanticAutomation _automation;
    private readonly ActionExecutor _actions;

    public ActionAndObservationTests(HeadlessSessionFixture fixture)
    {
        _session = fixture.Session;
        _automation = new SemanticAutomation(_session);
        _actions = new ActionExecutor(_session, _automation);
    }

    [Fact]
    public async Task InputActionsUseUiPathsAndTextIsNotEchoed()
    {
        var username = new ElementQuery { AutomationId = "UsernameTextBox" };
        var password = new ElementQuery { AutomationId = "PasswordTextBox" };
        await _actions.ExecuteAsync(new TextAction { Query = username, Text = "wrong", Mode = TextMode.Replace }, TestContext.Current.CancellationToken);
        var response = await _actions.ExecuteAsync(new TextAction { Query = username, Text = "agent", Mode = TextMode.Replace }, TestContext.Current.CancellationToken);
        var secretResponse = await _actions.ExecuteAsync(new TextAction { Query = password, Text = "correct-password", Mode = TextMode.Replace }, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("correct-password", JsonSerializer.Serialize(secretResponse));
        Assert.True(response.InputExecuted);
        Assert.True(response.UiQueueProcessed);

        await _actions.ExecuteAsync(new ClickAction { Query = new ElementQuery { AutomationId = "LoginButton" } }, TestContext.Current.CancellationToken);
        var status = await _automation.QueryAsync(new ElementQuery { AutomationId = "StatusText" }, TestContext.Current.CancellationToken);
        Assert.Equal("Authenticated", status.Matches[0].Value);

        var passwordNode = (await _automation.QueryAsync(new ElementQuery { AutomationId = "PasswordTextBox" }, TestContext.Current.CancellationToken)).Matches[0];
        Assert.Equal("********", passwordNode.Value);

        await _actions.ExecuteAsync(new TextAction { Query = username, Text = "-suffix", Mode = TextMode.Append }, TestContext.Current.CancellationToken);
        await _actions.ExecuteAsync(new ClearTextAction { Query = username }, TestContext.Current.CancellationToken);
        var cleared = await _automation.QueryAsync(username, TestContext.Current.CancellationToken);
        Assert.True(string.IsNullOrEmpty(cleared.Matches[0].Value));

        await _session.DispatchAsync(() =>
        {
            var window = Assert.IsType<LoginWindow>(_session.InitialWindow);
            window.GetLogicalDescendants().OfType<ToggleSwitch>().Single().IsChecked = false;
            var outerScroll = Assert.IsType<ScrollViewer>(window.Content);
            outerScroll.Offset = new global::Avalonia.Vector(0, 10000);
            outerScroll.UpdateLayout();
            return true;
        }, TestContext.Current.CancellationToken);
        await _actions.ExecuteAsync(new ToggleAction { Query = new ElementQuery { AutomationId = "ToggleSwitch" } }, TestContext.Current.CancellationToken);
        var toggle = await _automation.QueryAsync(new ElementQuery { AutomationId = "ToggleSwitch" }, TestContext.Current.CancellationToken);
        Assert.True(toggle.Matches[0].IsToggled);

        await _actions.ExecuteAsync(new SelectAction
        {
            Query = new ElementQuery { AutomationId = "SelectionComboBox" },
            Value = "Second item"
        }, TestContext.Current.CancellationToken);
        var selection = await _automation.QueryAsync(new ElementQuery { AutomationId = "SelectionComboBox" }, TestContext.Current.CancellationToken);
        Assert.Equal("Second item", selection.Matches[0].Value);

        var hidden = await Assert.ThrowsAsync<DriverException>(() => _actions.ExecuteAsync(
            new ClickAction { Query = new ElementQuery { AutomationId = "HiddenButton", IncludeHidden = true } }, TestContext.Current.CancellationToken));
        Assert.Equal("element_not_actionable", hidden.Code);
    }

    [Fact]
    public async Task KeyPointerAndScrollActionsAreSupported()
    {
        var snapshot = await _automation.SnapshotAsync(null, null, 1, 10, TestContext.Current.CancellationToken);
        var windowId = snapshot.Windows[0].Id;
        await _actions.ExecuteAsync(new FocusAction { Query = new ElementQuery { AutomationId = "UsernameTextBox" } }, TestContext.Current.CancellationToken);
        await _actions.ExecuteAsync(new KeyAction { WindowId = windowId, Key = "A", Modifiers = ["Control"] }, TestContext.Current.CancellationToken);
        await _actions.ExecuteAsync(new PointerAction { WindowId = windowId, X = 10, Y = 10 }, TestContext.Current.CancellationToken);
        await _actions.ExecuteAsync(new ScrollAction
        {
            Query = new ElementQuery { AutomationId = "SampleScrollViewer" },
            DeltaY = -50
        }, TestContext.Current.CancellationToken);
        var offset = await _session.DispatchAsync(() => _session.InitialWindow.GetLogicalDescendants()
            .OfType<ScrollViewer>().Single(x => x.Name == "SampleScrollViewer").Offset.Y, TestContext.Current.CancellationToken);
        Assert.True(offset >= 0);
    }

    [Fact]
    public async Task WaitsObserveBusinessCompletionAndTimeOut()
    {
        var waiter = new ConditionWaiter(_session, _automation);
        await _actions.ExecuteAsync(new ClickAction { Query = new ElementQuery { AutomationId = "DelayedLoginButton" } }, TestContext.Current.CancellationToken);
        var completed = await waiter.WaitAsync(new WaitRequest
        {
            Query = new ElementQuery { AutomationId = "StatusText" },
            Property = "text",
            Expected = "Delayed authentication completed"
        }, TestContext.Current.CancellationToken);
        Assert.Single(completed.Matches);

        var timeout = await Assert.ThrowsAsync<DriverException>(() => waiter.WaitAsync(new WaitRequest
        {
            Query = new ElementQuery { AutomationId = "StatusText" },
            Property = "text",
            Expected = "Never completed",
            TimeoutSeconds = 1
        }, TestContext.Current.CancellationToken));
        Assert.Equal("timeout", timeout.Code);
        await Assert.ThrowsAsync<DriverException>(() => new ConditionWaiter(_session, _automation).WaitAsync(new WaitRequest
        {
            Query = new ElementQuery { AutomationId = "StatusText" },
            TimeoutSeconds = 61
        }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScreenshotReturnsPngAndMetadata()
    {
        var visual = new VisualObservation(_session, _automation);
        var snapshot = await _automation.SnapshotAsync(null, null, 1, 10, TestContext.Current.CancellationToken);
        var screenshot = await visual.CaptureAsync(snapshot.Windows[0].Id, TestContext.Current.CancellationToken);
        Assert.NotEmpty(screenshot.Png);
        Assert.Equal([0x89, 0x50, 0x4e, 0x47], screenshot.Png.Take(4).ToArray());
        Assert.True(screenshot.PixelWidth > 0);
        Assert.True(screenshot.PixelHeight > 0);
        Assert.True(screenshot.RenderScaling > 0);
    }
}
