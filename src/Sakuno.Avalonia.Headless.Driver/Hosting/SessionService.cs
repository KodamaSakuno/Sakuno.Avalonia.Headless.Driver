using Sakuno.Avalonia.Headless.Driver.Automation;
using Sakuno.Avalonia.Headless.Driver.Automation.Actions;
using Sakuno.Avalonia.Headless.Driver.Automation.Waits;
using Sakuno.Avalonia.Headless.Driver.Sessions;

namespace Sakuno.Avalonia.Headless.Driver.Hosting;

public sealed class SessionService(HeadlessSessionOwner owner)
{
    private int _claimed;
    private SemanticAutomation? _automation;
    private ActionExecutor? _actions;
    private ConditionWaiter? _waits;
    private VisualObservation? _visual;

    public HeadlessSession Session => owner.Current;

    public async Task<SessionResponse> ClaimAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _claimed, 1, 0) != 0)
            throw new DriverException("session_active", "This Host already has an active claimed session.");
        var session = Session;
        var windows = await session.DispatchAsync(() => Automation(session).Windows, cancellationToken);
        return new SessionResponse(session.Id, windows);
    }

    public SemanticAutomation Automation(HeadlessSession session) => _automation ??= new(session);
    public ActionExecutor Actions(HeadlessSession session) => _actions ??= new(session, Automation(session));
    public ConditionWaiter Waits(HeadlessSession session) => _waits ??= new(session, Automation(session));
    public VisualObservation Visual(HeadlessSession session) => _visual ??= new(session, Automation(session));

    public HeadlessSession Get(string id) => owner.Get(id);
}
