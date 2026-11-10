using Sakuno.Avalonia.Headless.Driver.Sessions;

namespace Sakuno.Avalonia.Headless.Driver.Automation.Waits;

public sealed record WaitRequest
{
    public required ElementQuery Query { get; init; }
    public string Property { get; init; } = "existence";
    public string Comparison { get; init; } = "equals";
    public string? Expected { get; init; } = "true";
    public int? TimeoutSeconds { get; init; }
}

public sealed record WaitResponse(long ObservationId, IReadOnlyList<SemanticNode> Matches);

public sealed class ConditionWaiter(HeadlessSession session, SemanticAutomation automation)
{
    public Task<WaitResponse> WaitAsync(WaitRequest request, CancellationToken cancellationToken)
    {
        var seconds = request.TimeoutSeconds ?? (int)session.Configuration.WaitTimeout.TotalSeconds;
        if (seconds < 1 || seconds > 60 || seconds > session.Configuration.WaitTimeout.TotalSeconds)
            throw new DriverException("invalid_request", "Wait timeout must be positive and within the configured 60-second ceiling.");
        return session.ExecuteAsync(async token =>
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var result = await session.DispatchAsync(() => Evaluate(request), token).ConfigureAwait(false);
                if (result is not null)
                    return new WaitResponse(automation.NextObservationId(), result);
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }, TimeSpan.FromSeconds(seconds), cancellationToken);
    }

    private IReadOnlyList<SemanticNode>? Evaluate(WaitRequest request)
    {
        var nodes = automation.Find(request.Query);
        if (request.Property.Equals("existence", StringComparison.Ordinal))
        {
            var exists = nodes.Count > 0;
            return Compare(exists.ToString(), request) ? nodes : null;
        }
        var matches = nodes.Where(node => Compare(GetProperty(node, request.Property), request)).ToArray();
        return matches.Length > 0 ? matches : null;
    }

    private static string? GetProperty(SemanticNode node, string property) => property switch
    {
        "visibility" => node.IsVisible.ToString(),
        "enablement" => node.IsEnabled.ToString(),
        "focus" => node.IsFocused.ToString(),
        "selection" => node.IsSelected?.ToString(),
        "text" or "value" => node.Value,
        _ => throw new DriverException("invalid_request", "The wait property is not supported.")
    };

    private static bool Compare(string? actual, WaitRequest request) => request.Comparison switch
    {
        "equals" => string.Equals(actual, request.Expected, StringComparison.Ordinal),
        "notEquals" => !string.Equals(actual, request.Expected, StringComparison.Ordinal),
        "contains" => actual?.Contains(request.Expected ?? string.Empty, StringComparison.Ordinal) == true,
        _ => throw new DriverException("invalid_request", "The wait comparison is not supported.")
    };
}
