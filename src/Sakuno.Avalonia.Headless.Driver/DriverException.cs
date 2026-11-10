namespace Sakuno.Avalonia.Headless.Driver;

public class DriverException(string code, string message, object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public object? Details { get; } = details;
}
