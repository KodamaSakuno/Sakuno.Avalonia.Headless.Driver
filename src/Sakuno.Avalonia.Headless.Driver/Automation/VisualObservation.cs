using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Sakuno.Avalonia.Headless.Driver.Sessions;

namespace Sakuno.Avalonia.Headless.Driver.Automation;

public sealed record ScreenshotResult(byte[] Png, int PixelWidth, int PixelHeight, double RenderScaling);

public sealed class VisualObservation(HeadlessSession session, SemanticAutomation automation)
{
    public Task<ScreenshotResult> CaptureAsync(string windowId, CancellationToken cancellationToken) =>
        session.ExecuteAsync(token => session.DispatchAsync(() =>
        {
            if (!session.Configuration.Rendering)
                throw new DriverException("capability_unavailable", "Rendering is disabled for this Host.");
            var window = automation.ResolveWindow(windowId);
            using var bitmap = window.CaptureRenderedFrame()
                ?? throw new DriverException("capture_unavailable", "No rendered frame is available for the window.");
            using var stream = new MemoryStream();
            bitmap.Save(stream, new PngBitmapEncoderOptions());
            return new ScreenshotResult(stream.ToArray(), bitmap.PixelSize.Width, bitmap.PixelSize.Height, window.RenderScaling);
        }, token), session.Configuration.ActionTimeout, cancellationToken);
}
