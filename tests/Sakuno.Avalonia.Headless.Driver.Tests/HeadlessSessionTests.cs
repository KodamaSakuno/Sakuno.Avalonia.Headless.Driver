using Avalonia.Controls;
using Avalonia.LogicalTree;
using Sakuno.Avalonia.Headless.Driver.Configuration;
using Sakuno.Avalonia.Headless.Driver.Sample;
using Sakuno.Avalonia.Headless.Driver.Sessions;

namespace Sakuno.Avalonia.Headless.Driver.Tests;

public sealed class HeadlessSessionTests : IClassFixture<HeadlessSessionFixture>
{
    private readonly HeadlessSessionOwner _owner;
    private readonly HeadlessSession _session;

    public HeadlessSessionTests(HeadlessSessionFixture fixture)
    {
        _owner = fixture.Owner;
        _session = fixture.Session;
    }

    [Fact]
    public async Task StatePersistsAndOnlyOneSessionIsAllowed()
    {
        await _session.DispatchAsync(() =>
        {
            var window = Assert.IsType<LoginWindow>(_session.InitialWindow);
            var username = window.GetLogicalDescendants().OfType<TextBox>().Single(x => x.Name == "UsernameTextBox");
            username.Text = "agent";
            return true;
        }, TestContext.Current.CancellationToken);

        var text = await _session.DispatchAsync(() =>
        {
            var window = Assert.IsType<LoginWindow>(_session.InitialWindow);
            return window.GetLogicalDescendants().OfType<TextBox>().Single(x => x.Name == "UsernameTextBox").Text;
        }, TestContext.Current.CancellationToken);

        Assert.Equal("agent", text);
        Assert.Throws<DriverException>(() => _owner.Start(new SampleStartupAdapter(), _session.Configuration));
    }

    [Fact]
    public async Task OverlapIsRejectedAndCancellationDoesNotRunQueuedWork()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var workRan = false;
        var active = _session.ExecuteAsync(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            workRan = true;
            return true;
        }, TimeSpan.FromSeconds(30), cancellation.Token);

        await started.Task;
        var busy = await Assert.ThrowsAsync<DriverException>(() =>
            _session.ExecuteAsync(_ => Task.FromResult(true), TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        Assert.Equal("session_busy", busy.Code);

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active);
        Assert.False(workRan);
    }

    [Fact]
    public async Task OperationDeadlineIsEnforced()
    {
        var error = await Assert.ThrowsAsync<DriverException>(() =>
            _session.ExecuteAsync(async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return true;
            }, TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken));
        Assert.Equal("timeout", error.Code);
    }
}

public sealed class HeadlessSessionFixture : IAsyncLifetime
{
    public HeadlessSessionOwner Owner { get; } = new();
    public HeadlessSession Session { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        Session = Owner.Start(new SampleStartupAdapter(), new DriverConfiguration
        {
            StartupAdapter = typeof(SampleStartupAdapter).AssemblyQualifiedName!
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await Session.DisposeAsync();
}
