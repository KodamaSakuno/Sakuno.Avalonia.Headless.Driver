using System.Net;
using System.Text;
using System.Text.Json;
using Sakuno.Avalonia.Headless.Driver.Hosting;

namespace Sakuno.Avalonia.Headless.Driver.Tests;

public sealed class HttpAcceptanceTests
{
    [Fact]
    public async Task RealHttpWorkflowSupportsSecurityPersistenceAndRecovery()
    {
        await using var host = await HttpProcess.StartAsync();
        var unauthenticated = await new HttpClient { BaseAddress = new Uri(host.Url) }.GetAsync("/v1/capabilities");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        var documentation = await host.Client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, documentation.StatusCode);

        var capabilities = await host.SendAsync(HttpMethod.Get, "/v1/capabilities");
        Assert.Equal("12.1.6", capabilities.RootElement.GetProperty("avaloniaVersion").GetString());
        var sessionId = await host.ClaimSessionAsync();
        var second = await host.Client.PostAsync("/v1/sessions", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var contractPath = Path.Combine(AppContext.BaseDirectory, "openapi", "v1.json");
        using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(contractPath));
        var paths = contract.RootElement.GetProperty("paths");
        Assert.Equal(8, paths.EnumerateObject().Count());
        Assert.Equal("binary", paths.GetProperty("/v1/sessions/{sessionId}/windows/{windowId}/screenshot")
            .GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("image/png").GetProperty("schema").GetProperty("format").GetString());

        var snapshot = await host.SendAsync(HttpMethod.Get, $"/v1/sessions/{sessionId}/snapshot");
        Assert.True(snapshot.RootElement.GetProperty("roots").GetArrayLength() > 0);
        var required = contract.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("Snapshot")
            .GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        foreach (var field in required)
            Assert.True(snapshot.RootElement.TryGetProperty(field!, out _), field);
        await host.SendAsync(HttpMethod.Post, $"/v1/sessions/{sessionId}/actions", Action("UsernameTextBox", "agent"));
        await host.SendAsync(HttpMethod.Post, $"/v1/sessions/{sessionId}/actions", Action("PasswordTextBox", "bad-password"));
        await host.SendAsync(HttpMethod.Post, $"/v1/sessions/{sessionId}/actions", """{"type":"click","query":{"automationId":"LoginButton"}}""");
        await host.SendAsync(HttpMethod.Post, $"/v1/sessions/{sessionId}/wait", """
            {"query":{"automationId":"StatusText"},"property":"text","comparison":"equals","expected":"Authentication failed"}
            """);
        var passwordResponse = await host.SendAsync(HttpMethod.Post, $"/v1/sessions/{sessionId}/actions", Action("PasswordTextBox", "correct-password"));
        Assert.DoesNotContain("correct-password", passwordResponse.RootElement.GetRawText());
        await host.SendAsync(HttpMethod.Post, $"/v1/sessions/{sessionId}/actions", """{"type":"click","query":{"automationId":"LoginButton"}}""");
        var wait = await host.SendAsync(HttpMethod.Post, $"/v1/sessions/{sessionId}/wait", """
            {"query":{"automationId":"StatusText"},"property":"text","comparison":"equals","expected":"Authenticated"}
            """);
        Assert.Single(wait.RootElement.GetProperty("matches").EnumerateArray());

        var image = await host.Client.GetAsync($"/v1/sessions/{sessionId}/windows/w1/screenshot");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.True(image.Headers.Contains("X-Pixel-Width"));
        Assert.True(image.Headers.Contains("X-Pixel-Height"));
        Assert.True(image.Headers.Contains("X-Render-Scaling"));
        Assert.True((await image.Content.ReadAsByteArrayAsync()).Length > 100);

        var close = await host.Client.DeleteAsync($"/v1/sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);
        await host.WaitForExitAsync();
        Assert.DoesNotContain(host.Token, host.Diagnostics);

        await using var restarted = await HttpProcess.StartAsync();
        var stale = await restarted.Client.GetAsync($"/v1/sessions/{sessionId}/snapshot");
        Assert.Equal(HttpStatusCode.Gone, stale.StatusCode);
        Assert.Equal("stale_session", (await JsonDocument.ParseAsync(await stale.Content.ReadAsStreamAsync())).RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ErrorsConcurrencyAndIdleExpirationAreEnforced()
    {
        await using var host = await HttpProcess.StartAsync();
        var sessionId = await host.ClaimSessionAsync();
        using var invalidClient = new HttpClient { BaseAddress = new Uri(host.Url) };
        invalidClient.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await invalidClient.GetAsync("/v1/capabilities")).StatusCode);

        using var badRequest = new StringContent("""{"type":"text","query":{"automationId":"UsernameTextBox"},"text":"x","assemblyPath":"other.dll"}""", Encoding.UTF8, "application/json");
        var rejected = await host.Client.PostAsync($"/v1/sessions/{sessionId}/actions", badRequest);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var missing = await host.Client.PostAsync($"/v1/sessions/{sessionId}/actions", new StringContent("""{"type":"click","query":{"automationId":"Missing"}}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var disabled = await host.Client.PostAsync($"/v1/sessions/{sessionId}/actions", new StringContent("""{"type":"click","query":{"automationId":"DisabledButton"}}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, disabled.StatusCode);
        var ambiguousAction = await host.Client.PostAsync($"/v1/sessions/{sessionId}/actions", new StringContent("""{"type":"click","query":{"controlType":"Button"}}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Conflict, ambiguousAction.StatusCode);
        Assert.Equal("ambiguous_element", (await JsonDocument.ParseAsync(await ambiguousAction.Content.ReadAsStreamAsync())).RootElement.GetProperty("code").GetString());

        await host.SendAsync(HttpMethod.Post, $"/v1/sessions/{sessionId}/actions", """{"type":"click","query":{"automationId":"DelayedLoginButton"}}""");
        using var waitContent = new StringContent("""{"query":{"automationId":"StatusText"},"property":"text","expected":"Never","timeoutSeconds":1}""", Encoding.UTF8, "application/json");
        using var cancellation = new CancellationTokenSource(200);
        var waiting = host.Client.PostAsync($"/v1/sessions/{sessionId}/wait", waitContent, cancellation.Token);
        await Task.Delay(50);
        var busy = await host.Client.GetAsync($"/v1/sessions/{sessionId}/snapshot");
        Assert.Equal(HttpStatusCode.Conflict, busy.StatusCode);
        Assert.Equal("session_busy", (await JsonDocument.ParseAsync(await busy.Content.ReadAsStreamAsync())).RootElement.GetProperty("code").GetString());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        await Task.Delay(1200, TestContext.Current.CancellationToken);

        var timeout = await host.Client.PostAsync($"/v1/sessions/{sessionId}/wait", new StringContent("""{"query":{"automationId":"StatusText"},"property":"text","expected":"Never","timeoutSeconds":1}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestTimeout, timeout.StatusCode);
        await host.SendAsync(HttpMethod.Post, $"/v1/sessions/{sessionId}/wait", """{"query":{"automationId":"StatusText"},"property":"text","expected":"Delayed authentication completed"}""");
        var ambiguous = await host.Client.PostAsync($"/v1/sessions/{sessionId}/query", new StringContent("""{"controlType":"Button"}""", Encoding.UTF8, "application/json"));
        ambiguous.EnsureSuccessStatusCode();
        var mapped500 = HttpErrorMapping.Map(new InvalidOperationException("sensitive"));
        Assert.Equal(500, mapped500.Status);
        Assert.DoesNotContain("sensitive", mapped500.Body.Message);

        await using var idle = await HttpProcess.StartAsync(1);
        await idle.WaitForExitAsync();
    }

    private static string Action(string id, string text) =>
        JsonSerializer.Serialize(new { type = "text", query = new { automationId = id }, text, mode = "replace" });
}
