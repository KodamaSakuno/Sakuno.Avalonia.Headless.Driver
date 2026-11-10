using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sakuno.Avalonia.Headless.Driver.Configuration;

public sealed record DriverConfiguration
{
    public int Version { get; init; } = 1;
    public required string StartupAdapter { get; init; }
    public bool Rendering { get; init; } = true;
    public int ActionTimeoutSeconds { get; init; } = 5;
    public int WaitTimeoutSeconds { get; init; } = 10;
    public int? IdleTimeoutSeconds { get; init; } = 600;
    public SnapshotConfiguration Snapshot { get; init; } = new();

    public TimeSpan ActionTimeout => TimeSpan.FromSeconds(ActionTimeoutSeconds);
    public TimeSpan WaitTimeout => TimeSpan.FromSeconds(WaitTimeoutSeconds);
    public TimeSpan? IdleTimeout => IdleTimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null;

    public static DriverConfiguration Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        var configuration = JsonSerializer.Deserialize<DriverConfiguration>(stream, SerializerOptions)
            ?? throw new DriverConfigurationException("Configuration must contain a JSON object.");
        configuration.Validate();
        return configuration;
    }

    public void Validate()
    {
        if (Version != 1)
            throw new DriverConfigurationException("Only configuration version 1 is supported.");
        if (string.IsNullOrWhiteSpace(StartupAdapter) || StartupAdapter.Length > 1024)
            throw new DriverConfigurationException("A bounded Startup Adapter type name is required.");
        if (ActionTimeoutSeconds is < 1 or > 60 || WaitTimeoutSeconds is < 1 or > 60)
            throw new DriverConfigurationException("Action and wait timeouts must be between 1 and 60 seconds.");
        if (IdleTimeoutSeconds is < 1 or > 86400)
            throw new DriverConfigurationException("Idle timeout must be null or between 1 and 86400 seconds.");
        Snapshot.Validate();
    }

    public static void ValidateCompatibility(Version runtime, Version avalonia)
    {
        if (runtime.Major != 10)
            throw new DriverConfigurationException("The Headless Host requires .NET 10.");
        if (avalonia.Major != 12 || avalonia.Minor != 1)
            throw new DriverConfigurationException("The Headless Host requires Avalonia 12.1.x.");
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
}

public sealed record SnapshotConfiguration
{
    public int MaxDepth { get; init; } = 4;
    public int MaxNodes { get; init; } = 500;
    public int MaxTextLength { get; init; } = 500;

    internal void Validate()
    {
        if (MaxDepth is < 1 or > 32)
            throw new DriverConfigurationException("Snapshot depth must be between 1 and 32.");
        if (MaxNodes is < 1 or > 10000)
            throw new DriverConfigurationException("Snapshot node count must be between 1 and 10000.");
        if (MaxTextLength is < 16 or > 10000)
            throw new DriverConfigurationException("Snapshot text length must be between 16 and 10000.");
    }
}

public sealed class DriverConfigurationException(string message) : Exception(message);
