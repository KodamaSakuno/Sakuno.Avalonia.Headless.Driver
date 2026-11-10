using Sakuno.Avalonia.Headless.Driver.Configuration;

namespace Sakuno.Avalonia.Headless.Driver.Sessions;

public sealed class HeadlessSessionOwner
{
    private readonly Lock _lock = new();
    private HeadlessSession? _session;

    public HeadlessSession Current
    {
        get
        {
            lock (_lock)
                return _session is { IsClosed: false } session
                    ? session
                    : throw new DriverException("session_not_found", "No active Headless Session exists.");
        }
    }

    public HeadlessSession Start(IHeadlessStartupAdapter startupAdapter, DriverConfiguration configuration)
    {
        lock (_lock)
        {
            if (_session is { IsClosed: false })
                throw new DriverException("session_active", "This Host already owns an active Headless Session.");
            _session = HeadlessSession.Start(startupAdapter, configuration);
            return _session;
        }
    }

    public HeadlessSession Get(string sessionId)
    {
        var session = Current;
        if (!string.Equals(session.Id, sessionId, StringComparison.Ordinal))
            throw new DriverException("stale_session", "The session identity does not belong to this Host process.");
        return session;
    }
}
