using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Sakuno.Avalonia.Headless.Driver.Tests;

public sealed class HttpProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _errors = new();

    private HttpProcess(Process process, string url, string token)
    {
        _process = process;
        Url = url;
        Token = token;
        Client = new HttpClient { BaseAddress = new Uri(url) };
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _ = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
                _errors.AppendLine(line);
        });
    }

    public string Url { get; }
    public string Token { get; }
    public string SessionId { get; private set; } = string.Empty;
    public HttpClient Client { get; }
    public string Diagnostics => _errors.ToString();

    public static async Task<HttpProcess> StartAsync(int? idleSeconds = null)
    {
        var directory = AppContext.BaseDirectory;
        var host = Path.Combine(directory, "Sakuno.Avalonia.Headless.Driver.Sample.Host.dll");
        var config = Path.Combine(directory, "headless-driver.json");
        if (idleSeconds is not null)
        {
            var json = await File.ReadAllTextAsync(config);
            config = Path.GetTempFileName();
            await File.WriteAllTextAsync(config, json.Replace("\"idleTimeoutSeconds\": 600", $"\"idleTimeoutSeconds\": {idleSeconds}"));
        }
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(host);
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add(config);
        var process = new Process { StartInfo = startInfo };
        process.Start();
        var lineTask = process.StandardOutput.ReadLineAsync();
        var completed = await Task.WhenAny(lineTask, Task.Delay(TimeSpan.FromSeconds(30)));
        if (completed != lineTask || await lineTask is not { Length: > 0 } line)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("Host readiness timed out.");
        }
        using var readiness = JsonDocument.Parse(line);
        var result = new HttpProcess(process,
            readiness.RootElement.GetProperty("url").GetString()!,
            readiness.RootElement.GetProperty("token").GetString()!);
        Assert.NotEqual(Environment.ProcessId, readiness.RootElement.GetProperty("processId").GetInt32());
        Assert.Equal("1.0", readiness.RootElement.GetProperty("protocolVersion").GetString());
        return result;
    }

    public async Task<string> ClaimSessionAsync()
    {
        var response = await Client.PostAsync("/v1/sessions", null);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        SessionId = json.RootElement.GetProperty("sessionId").GetString()!;
        return SessionId;
    }

    public async Task<JsonDocument> SendAsync(HttpMethod method, string path, string? json = null)
    {
        using var content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await Client.SendAsync(new HttpRequestMessage(method, path) { Content = content });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    public async Task WaitForExitAsync()
    {
        await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(_process.HasExited);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (!_process.HasExited)
            _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync();
        _process.Dispose();
    }
}
