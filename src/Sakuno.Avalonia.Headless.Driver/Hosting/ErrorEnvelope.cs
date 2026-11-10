using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace Sakuno.Avalonia.Headless.Driver.Hosting;

public sealed record ErrorEnvelope(string Code, string Message, object? Details, IReadOnlyList<string> Diagnostics);

public static class HttpErrorMapping
{
    public static (int Status, ErrorEnvelope Body) Map(Exception exception)
    {
        if (exception is BadHttpRequestException or JsonException)
            exception = new DriverException("invalid_request", "The request body is invalid or exceeds the request-size limit.");
        if (exception is DriverException driver)
        {
            var status = driver.Code switch
            {
                "invalid_request" => StatusCodes.Status400BadRequest,
                "element_not_found" or "window_not_found" or "session_not_found" => StatusCodes.Status404NotFound,
                "timeout" => StatusCodes.Status408RequestTimeout,
                "session_active" or "session_busy" or "ambiguous_element" => StatusCodes.Status409Conflict,
                "stale_session" => StatusCodes.Status410Gone,
                "element_not_actionable" or "capability_unavailable" => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status500InternalServerError
            };
            return (status, new ErrorEnvelope(driver.Code, driver.Message, Sanitize(driver.Details), []));
        }
        return (StatusCodes.Status500InternalServerError,
            new ErrorEnvelope("internal_error", "The Host could not complete the operation.", null, []));
    }

    private static object? Sanitize(object? details)
    {
        if (details is null)
            return null;
        var json = JsonSerializer.Serialize(details);
        if (json.Contains("correct-password", StringComparison.Ordinal))
            return new { redacted = true };
        return details;
    }
}
