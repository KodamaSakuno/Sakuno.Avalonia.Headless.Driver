using Avalonia.Controls;
using Sakuno.Avalonia.Headless.Driver.Automation;
using Sakuno.Avalonia.Headless.Driver.Sample;
using Sakuno.Avalonia.Headless.Driver.Sessions;

namespace Sakuno.Avalonia.Headless.Driver.Tests;

[Collection("Headless")]
public sealed class SemanticAutomationTests
{
    private readonly HeadlessSession _session;
    private readonly SemanticAutomation _automation;

    public SemanticAutomationTests(HeadlessSessionFixture fixture)
    {
        _session = fixture.Session;
        _automation = new SemanticAutomation(_session);
    }

    [Fact]
    public async Task SnapshotContainsBoundedSemanticsAndSupportsSubtrees()
    {
        var snapshot = await _automation.SnapshotAsync(null, null, 4, 500, TestContext.Current.CancellationToken);
        var nodes = Flatten(snapshot.Roots);
        var username = Assert.Single(nodes, x => x.AutomationId == "UsernameTextBox");
        Assert.Equal("TextBox", username.ControlType);
        Assert.True(username.IsVisible);
        Assert.NotNull(username.Bounds);
        Assert.Contains(nodes, x => x.ControlType == "TextBlock" && x.Name is not null);

        var truncated = await _automation.SnapshotAsync(null, null, 1, 5, TestContext.Current.CancellationToken);
        Assert.True(truncated.Truncated);
        var branch = Flatten(truncated.Roots).First(x => x.IsTruncated);
        var expanded = await _automation.SnapshotAsync(null, branch.Path, 4, 500, TestContext.Current.CancellationToken);
        Assert.Equal(branch.Path, expanded.Roots[0].Path);
    }

    [Fact]
    public async Task QueriesAreDeterministicAndRejectAmbiguityAndHiddenTargets()
    {
        var query = await _automation.QueryAsync(new ElementQuery { AutomationId = "LoginButton" }, TestContext.Current.CancellationToken);
        Assert.Single(query.Matches);
        Assert.Equal("LoginButton", query.Matches[0].AutomationId);

        var ambiguous = await Assert.ThrowsAsync<DriverException>(() => _session.DispatchAsync(() =>
        {
            _automation.ResolveSingle(new ElementQuery { ControlType = "Button" });
            return true;
        }, TestContext.Current.CancellationToken));
        Assert.Equal("ambiguous_element", ambiguous.Code);

        var hidden = await _automation.QueryAsync(new ElementQuery { AutomationId = "HiddenButton" }, TestContext.Current.CancellationToken);
        Assert.Empty(hidden.Matches);
        var included = await _automation.QueryAsync(new ElementQuery { AutomationId = "HiddenButton", IncludeHidden = true }, TestContext.Current.CancellationToken);
        Assert.Single(included.Matches);

        var contains = await _automation.QueryAsync(new ElementQuery
        {
            AutomationId = "StatusText",
            Text = "authenticated",
            TextMatch = TextMatchKind.Contains,
            ControlType = "TextBlock"
        }, TestContext.Current.CancellationToken);
        Assert.Single(contains.Matches);

        var unrealized = await _automation.QueryAsync(new ElementQuery
        {
            WindowId = (await _automation.SnapshotAsync(null, null, 1, 10, TestContext.Current.CancellationToken)).Windows[0].Id,
            Text = "Virtual item 1000"
        }, TestContext.Current.CancellationToken);
        Assert.Empty(unrealized.Matches);
    }

    [Fact]
    public async Task DialogsAreTrackedScopedAndRemovedWhenClosed()
    {
        var window = Assert.IsType<LoginWindow>(_session.InitialWindow);
        Window? dialog = null;
        await _session.DispatchAsync(() =>
        {
            dialog = new Window
            {
                Title = "Observation dialog",
                Content = new Button { Name = "LoginButton", Content = "Dialog login" }
            };
            dialog.Show(window);
            return true;
        }, TestContext.Current.CancellationToken);

        var snapshot = await _automation.SnapshotAsync(null, null, 4, 500, TestContext.Current.CancellationToken);
        Assert.Equal(2, snapshot.Windows.Count);
        var dialogInfo = Assert.Single(snapshot.Windows, x => x.Title == "Observation dialog");
        var scoped = await _automation.QueryAsync(new ElementQuery
        {
            WindowId = dialogInfo.Id,
            AutomationId = "LoginButton"
        }, TestContext.Current.CancellationToken);
        Assert.Single(scoped.Matches);

        await _session.DispatchAsync(() =>
        {
            dialog!.Close();
            return true;
        }, TestContext.Current.CancellationToken);
        snapshot = await _automation.SnapshotAsync(null, null, 4, 500, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(snapshot.Windows, x => x.Id == dialogInfo.Id);
    }

    private static IReadOnlyList<SemanticNode> Flatten(IEnumerable<SemanticNode> roots) =>
        roots.SelectMany(root => new[] { root }.Concat(Flatten(root.Children))).ToArray();
}
