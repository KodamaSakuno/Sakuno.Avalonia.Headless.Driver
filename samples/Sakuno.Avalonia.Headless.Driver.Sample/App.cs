using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace Sakuno.Avalonia.Headless.Driver.Sample;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            });
}
