namespace Sakuno.Avalonia.Headless.Driver.Hosting;

public sealed class HostActivity
{
    private long _lastActivity = Environment.TickCount64;
    public void Touch() => Interlocked.Exchange(ref _lastActivity, Environment.TickCount64);
    public TimeSpan Elapsed => TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastActivity));
}
