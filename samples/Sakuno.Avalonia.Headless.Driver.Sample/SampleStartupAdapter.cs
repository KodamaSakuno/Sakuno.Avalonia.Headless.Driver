using Avalonia.Controls;

namespace Sakuno.Avalonia.Headless.Driver.Sample;

public sealed class SampleStartupAdapter : IHeadlessStartupAdapter
{
    public Type ApplicationType => typeof(App);

    public TopLevel CreateInitialWindow() => new LoginWindow();
}
