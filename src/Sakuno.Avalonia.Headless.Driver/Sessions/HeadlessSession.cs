using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Sakuno.Avalonia.Headless.Driver.Configuration;

namespace Sakuno.Avalonia.Headless.Driver.Sessions;

public sealed class HeadlessSession : IAsyncDisposable
{
    private readonly HeadlessUnitTestSession _unitTestSession;
    private int _gate;
    private int _closed;

    private HeadlessSession(
        HeadlessUnitTestSession unitTestSession,
        IHeadlessStartupAdapter startupAdapter,
        DriverConfiguration configuration)
    {
        _unitTestSession = unitTestSession;
        StartupAdapter = startupAdapter;
        Configuration = configuration;
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public IHeadlessStartupAdapter StartupAdapter { get; }
    public DriverConfiguration Configuration { get; }
    public TopLevel InitialWindow { get; private set; } = null!;
    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public static HeadlessSession Start(IHeadlessStartupAdapter startupAdapter, DriverConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(startupAdapter);
        ArgumentNullException.ThrowIfNull(configuration);
        DriverConfiguration.ValidateCompatibility(
            Environment.Version,
            typeof(Application).Assembly.GetName().Version!);

        var unitTestSession = HeadlessUnitTestSession.StartNew(
            startupAdapter.ApplicationType,
            AvaloniaTestIsolationLevel.PerAssembly);
        var session = new HeadlessSession(unitTestSession, startupAdapter, configuration);
        try
        {
            unitTestSession.Dispatch(() =>
            {
                session.InitialWindow = startupAdapter.CreateInitialWindow()
                    ?? throw new DriverException("startup_failed", "The Startup Adapter returned no initial window.");
                if (session.InitialWindow is Window window)
                    window.Show();
                else
                    throw new DriverException("startup_failed", "The initial TopLevel must be a Window.");
                return true;
            }, CancellationToken.None).GetAwaiter().GetResult();
            return session;
        }
        catch
        {
            unitTestSession.Dispose();
            throw;
        }
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        if (Interlocked.CompareExchange(ref _gate, 1, 0) != 0)
            throw new DriverException("session_busy", "Another session operation is already active.");

        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            return await operation(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DriverException("timeout", "The operation exceeded its deadline.");
        }
        finally
        {
            Volatile.Write(ref _gate, 0);
        }
    }

    public Task<T> DispatchAsync<T>(Func<T> action, CancellationToken cancellationToken) =>
        _unitTestSession.Dispatch(action, cancellationToken);

    public Task<T> DispatchAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken) =>
        _unitTestSession.Dispatch(action, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return;
        await Task.Run(() => _unitTestSession.Dispose()).ConfigureAwait(false);
    }
}
