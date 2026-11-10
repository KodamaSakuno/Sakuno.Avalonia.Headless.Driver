using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using global::Avalonia;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Console;
using Sakuno.Avalonia.Headless.Driver.Automation;
using Sakuno.Avalonia.Headless.Driver.Automation.Actions;
using Sakuno.Avalonia.Headless.Driver.Automation.Waits;
using Sakuno.Avalonia.Headless.Driver.Configuration;
using Sakuno.Avalonia.Headless.Driver.Sessions;

namespace Sakuno.Avalonia.Headless.Driver.Hosting;

public static class HeadlessHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        HeadlessSession? session = null;
        try
        {
            var configuration = DriverConfiguration.Load(GetConfigurationPath(args));
            var adapter = StartupAdapterResolver.Resolve(configuration.StartupAdapter);
            var owner = new HeadlessSessionOwner();
            session = owner.Start(adapter, configuration);
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var activity = new HostActivity();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, 0);
                options.Limits.MaxRequestBodySize = 64 * 1024;
            });
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
            builder.Services.AddSingleton(owner);
            builder.Services.AddSingleton(new SessionService(owner));
            builder.Services.AddSingleton(activity);

            var app = builder.Build();
            app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error
                    ?? new InvalidOperationException("Unknown failure");
                var (status, body) = HttpErrorMapping.Map(exception);
                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(body);
            }));
            app.Use(async (context, next) =>
            {
                if (!context.Request.Path.StartsWithSegments("/v1"))
                {
                    await next();
                    return;
                }
                var expected = "Bearer " + token;
                var supplied = context.Request.Headers.Authorization.ToString();
                if (!FixedTimeEquals(expected, supplied))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new ErrorEnvelope("unauthorized", "A valid bearer token is required.", null, []));
                    return;
                }
                activity.Touch();
                await next();
            });

            MapEndpoints(app);
            await app.StartAsync().ConfigureAwait(false);
            var address = app.Urls.Single();
            var readiness = new
            {
                url = address,
                token,
                processId = Environment.ProcessId,
                protocolVersion = "1.0"
            };
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(readiness)).ConfigureAwait(false);
            await Console.Out.FlushAsync().ConfigureAwait(false);

            var idleTask = MonitorIdle(app, session, activity, configuration.IdleTimeout);
            await app.WaitForShutdownAsync().ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
            await idleTask.ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            if (session is not null)
                await session.DisposeAsync().ConfigureAwait(false);
            var message = exception is DriverConfigurationException or DriverException
                ? exception.Message
                : "The Headless Host could not start.";
            await Console.Error.WriteLineAsync(message).ConfigureAwait(false);
            return 1;
        }
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/v1/capabilities", () => new
        {
            hostVersion = "1.0.0",
            protocolVersion = "1.0",
            avaloniaVersion = typeof(Application).Assembly.GetName().Version!.ToString(3),
            features = new[] { "semanticTree", "elementQuery", "actions", "waits", "screenshots" }
        }).WithTags("Discovery");

        app.MapPost("/v1/sessions", async (SessionService service, CancellationToken cancellationToken) =>
            await service.ClaimAsync(cancellationToken)).WithTags("Sessions");
        app.MapGet("/v1/sessions/{sessionId}/snapshot", async (
            string sessionId,
            [FromQuery] string? windowId,
            [FromQuery] string? subtreePath,
            [FromQuery] int? maxDepth,
            [FromQuery] int? maxNodes,
            SessionService service,
            CancellationToken cancellationToken) =>
            await service.Automation(service.Get(sessionId)).SnapshotAsync(windowId, subtreePath, maxDepth, maxNodes, cancellationToken)).WithTags("Observations");
        app.MapPost("/v1/sessions/{sessionId}/query", async (
            string sessionId,
            ElementQuery query,
            SessionService service,
            CancellationToken cancellationToken) =>
            await service.Automation(service.Get(sessionId)).QueryAsync(query, cancellationToken)).WithTags("Observations");
        app.MapPost("/v1/sessions/{sessionId}/actions", async (
            string sessionId,
            UiAction action,
            SessionService service,
            CancellationToken cancellationToken) =>
            await service.Actions(service.Get(sessionId)).ExecuteAsync(action, cancellationToken)).WithTags("Actions");
        app.MapPost("/v1/sessions/{sessionId}/wait", async (
            string sessionId,
            WaitRequest request,
            SessionService service,
            CancellationToken cancellationToken) =>
            await service.Waits(service.Get(sessionId)).WaitAsync(request, cancellationToken)).WithTags("Waits");
        app.MapGet("/v1/sessions/{sessionId}/windows/{windowId}/screenshot", async (
            string sessionId,
            string windowId,
            SessionService service,
            HttpResponse response,
            CancellationToken cancellationToken) =>
        {
            var screenshot = await service.Visual(service.Get(sessionId)).CaptureAsync(windowId, cancellationToken);
            response.Headers["X-Pixel-Width"] = screenshot.PixelWidth.ToString();
            response.Headers["X-Pixel-Height"] = screenshot.PixelHeight.ToString();
            response.Headers["X-Render-Scaling"] = screenshot.RenderScaling.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.File(screenshot.Png, "image/png", fileDownloadName: null, enableRangeProcessing: false);
        }).WithTags("Observations");
        app.MapDelete("/v1/sessions/{sessionId}", async (string sessionId, SessionService service, IHostApplicationLifetime lifetime) =>
        {
            var session = service.Get(sessionId);
            await session.DisposeAsync();
            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                lifetime.StopApplication();
            });
            return Results.NoContent();
        }).WithTags("Sessions");
    }

    private static async Task MonitorIdle(WebApplication app, HeadlessSession session, HostActivity activity, TimeSpan? idleTimeout)
    {
        if (idleTimeout is null)
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try
        {
            while (await timer.WaitForNextTickAsync(app.Lifetime.ApplicationStopping))
            {
                if (activity.Elapsed >= idleTimeout)
                {
                    await session.DisposeAsync();
                    app.Lifetime.StopApplication();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string GetConfigurationPath(string[] args)
    {
        if (args.Length != 2 || args[0] != "--config" || string.IsNullOrWhiteSpace(args[1]))
            throw new DriverConfigurationException("Usage: HeadlessHost --config <local-configuration.json>");
        return args[1];
    }

    private static bool FixedTimeEquals(string expected, string supplied)
    {
        var left = System.Text.Encoding.UTF8.GetBytes(expected);
        var right = System.Text.Encoding.UTF8.GetBytes(supplied);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
