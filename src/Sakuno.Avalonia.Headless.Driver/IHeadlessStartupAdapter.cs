using Avalonia.Controls;

namespace Sakuno.Avalonia.Headless.Driver;

public interface IHeadlessStartupAdapter
{
    Type ApplicationType { get; }
    TopLevel CreateInitialWindow();
}
