using Avalonia.Controls;
using Sakuno.Avalonia.Headless.Driver;
using Sakuno.Avalonia.Headless.Driver.Configuration;

namespace Sakuno.Avalonia.Headless.Driver.Tests;

public sealed class DriverConfigurationTests
{
    [Fact]
    public void ValidSettingsAreAccepted()
    {
        var configuration = new DriverConfiguration
        {
            StartupAdapter = typeof(TestStartupAdapter).AssemblyQualifiedName!,
            IdleTimeoutSeconds = null
        };

        configuration.Validate();
        Assert.Equal(TimeSpan.FromSeconds(5), configuration.ActionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), configuration.WaitTimeout);
        Assert.Null(configuration.IdleTimeout);
    }

    [Fact]
    public void UnknownAndInvalidAdapterTypesAreRejected()
    {
        Assert.Throws<DriverConfigurationException>(() => StartupAdapterResolver.Resolve("Missing.Adapter, Missing.Assembly"));
        Assert.Throws<DriverConfigurationException>(() => StartupAdapterResolver.Resolve(typeof(string).AssemblyQualifiedName!));
        Assert.Throws<DriverConfigurationException>(() => StartupAdapterResolver.Resolve(typeof(AbstractAdapter).AssemblyQualifiedName!));
    }

    [Fact]
    public void CompiledAdapterTypeIsResolved()
    {
        var adapter = StartupAdapterResolver.Resolve(typeof(TestStartupAdapter).AssemblyQualifiedName!);
        Assert.IsType<TestStartupAdapter>(adapter);
    }

    [Theory]
    [InlineData(0, 10, 600)]
    [InlineData(61, 10, 600)]
    [InlineData(5, 61, 600)]
    [InlineData(5, 10, 0)]
    public void InvalidLimitsAreRejected(int action, int wait, int idle)
    {
        var configuration = new DriverConfiguration
        {
            StartupAdapter = typeof(TestStartupAdapter).AssemblyQualifiedName!,
            ActionTimeoutSeconds = action,
            WaitTimeoutSeconds = wait,
            IdleTimeoutSeconds = idle
        };
        Assert.Throws<DriverConfigurationException>(configuration.Validate);
    }

    [Fact]
    public void UnknownConfigurationMembersAreRejected()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"version":1,"startupAdapter":"adapter","assemblyPath":"other.dll"}""");
            Assert.ThrowsAny<System.Text.Json.JsonException>(() => DriverConfiguration.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(9, 12, 1)]
    [InlineData(10, 11, 3)]
    [InlineData(10, 12, 2)]
    public void UnsupportedRuntimeAndPackageCombinationsAreRejected(int runtime, int major, int minor)
    {
        Assert.Throws<DriverConfigurationException>(() =>
            DriverConfiguration.ValidateCompatibility(new Version(runtime, 0), new Version(major, minor, 0)));
    }

    public sealed class TestStartupAdapter : IHeadlessStartupAdapter
    {
        public Type ApplicationType => typeof(TestStartupAdapter);
        public TopLevel CreateInitialWindow() => new Window();
    }

    public abstract class AbstractAdapter : IHeadlessStartupAdapter
    {
        public Type ApplicationType => typeof(AbstractAdapter);
        public TopLevel CreateInitialWindow() => new Window();
    }
}
