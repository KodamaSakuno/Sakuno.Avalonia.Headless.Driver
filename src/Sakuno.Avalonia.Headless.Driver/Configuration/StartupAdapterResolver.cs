namespace Sakuno.Avalonia.Headless.Driver.Configuration;

public static class StartupAdapterResolver
{
    public static IHeadlessStartupAdapter Resolve(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName) || typeName.Length > 1024)
            throw new DriverConfigurationException("A bounded Startup Adapter type name is required.");

        var type = Type.GetType(typeName, throwOnError: false, ignoreCase: false);
        if (type is null)
            throw new DriverConfigurationException("The configured Startup Adapter type is not available in a referenced assembly.");
        if (type.IsAbstract || type.IsInterface || !typeof(IHeadlessStartupAdapter).IsAssignableFrom(type))
            throw new DriverConfigurationException("The configured type must implement IHeadlessStartupAdapter.");
        if (type.GetConstructor(Type.EmptyTypes) is null)
            throw new DriverConfigurationException("The Startup Adapter must have a public parameterless constructor.");

        try
        {
            return (IHeadlessStartupAdapter)Activator.CreateInstance(type)!;
        }
        catch (Exception exception)
        {
            throw new DriverConfigurationException($"The Startup Adapter could not be constructed: {exception.GetBaseException().Message}");
        }
    }
}
