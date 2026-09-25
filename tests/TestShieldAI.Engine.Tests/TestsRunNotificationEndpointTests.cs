using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Endpoints;
using TestShieldAI.Api.Notifications;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class TestsRunNotificationEndpointTests
{
    private const string Spec = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets": {
              "get": {
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": { "type": "array" }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Run_NotificationsDisabled_SendsNoHttpRequest()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory(enabled: false);
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, factory.Handler.RequestCount);
    }

    [Fact]
    public async Task Run_Block_Notifies()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory();
        var client = factory.CreateClient();
        factory.Runner.ResultFactory = test => Succeed(test, "[]");
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);
        (await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null)).EnsureSuccessStatusCode();
        factory.Handler.Reset();
        factory.Runner.ResultFactory = null;

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(RegressionDecision.Block), body!.Decision);
        Assert.Equal(1, factory.Handler.RequestCount);
        Assert.Equal("Block", ReadPostedEvent(factory).GetProperty("decision").GetString());
    }

    [Fact]
    public async Task Run_Review_Notifies()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory();
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(RegressionDecision.Review), body!.Decision);
        Assert.Equal(1, factory.Handler.RequestCount);
        Assert.Equal("Review", ReadPostedEvent(factory).GetProperty("decision").GetString());
    }

    [Fact]
    public async Task Run_Safe_DoesNotNotifyByDefault()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory();
        var client = factory.CreateClient();
        factory.Runner.ResultFactory = test => Succeed(test, "[]");
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(RegressionDecision.Safe), body!.Decision);
        Assert.Equal(0, factory.Handler.RequestCount);
    }

    [Fact]
    public async Task Run_Safe_NotifiesWhenNotifyOnSafe()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory(notifyOnSafe: true);
        var client = factory.CreateClient();
        factory.Runner.ResultFactory = test => Succeed(test, "[]");
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(RegressionDecision.Safe), body!.Decision);
        Assert.Equal(1, factory.Handler.RequestCount);
        Assert.Equal("Safe", ReadPostedEvent(factory).GetProperty("decision").GetString());
    }

    [Fact]
    public async Task Run_MissingTriggerUrl_SkipsSafely()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory(triggerUrl: null);
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, factory.Handler.RequestCount);
    }

    [Fact]
    public async Task Run_PostedEvent_ContainsSummarizedFactsAndNoSensitiveFields()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory();
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var json = await response.Content.ReadAsStringAsync();
        using var runDocument = JsonDocument.Parse(json);
        var posted = ReadPostedEvent(factory);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(runDocument.RootElement.TryGetProperty("notifications", out _));
        Assert.Equal(HttpMethod.Post, factory.Handler.LastRequest!.Method);
        Assert.Equal("application/json", factory.Handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(NotificationEvent.RunCompletedEventType, posted.GetProperty("eventType").GetString());
        Assert.Equal("Review", posted.GetProperty("decision").GetString());
        Assert.Equal(project.Id.ToString(), posted.GetProperty("projectId").GetGuid().ToString());
        Assert.Equal("Pets API", posted.GetProperty("projectName").GetString());
        Assert.True(posted.TryGetProperty("risk", out var risk));
        Assert.False(string.IsNullOrWhiteSpace(risk.GetProperty("level").GetString()));
        Assert.True(posted.TryGetProperty("coverage", out var coverage));
        Assert.True(coverage.TryGetProperty("percent", out _));
        Assert.True(coverage.TryGetProperty("totalOperations", out _));
        Assert.Equal($"https://dashboard.example.test/projects/{project.Id}", posted.GetProperty("dashboardUrl").GetString());
        Assert.DoesNotContain("https://pets.example.com", factory.Handler.LastBody!, StringComparison.Ordinal);
        Assert.DoesNotContain("baseUrl", factory.Handler.LastBody!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", factory.Handler.LastBody!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_HttpFailure_StillReturns200WithoutChangingResponse()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory();
        factory.Handler.StatusCode = HttpStatusCode.BadGateway;
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(RegressionDecision.Review), body!.Decision);
        Assert.NotNull(body.Risk);
        Assert.NotNull(body.Coverage);
        Assert.Equal(1, factory.Handler.RequestCount);
    }

    [Fact]
    public async Task Run_Timeout_StillReturns200()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory(timeoutSeconds: 1);
        factory.Handler.Delay = TimeSpan.FromSeconds(5);
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Run_ThrowingNotifier_StillReturns200()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory();
        factory.ThrowingNotifier = true;
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(project.Id), null);
        var body = await response.Content.ReadFromJsonAsync<RunTestsResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(RegressionDecision.Review), body!.Decision);
        Assert.Equal(0, factory.Handler.RequestCount);
    }

    [Fact]
    public async Task Run_ProjectNotFound_DoesNotNotify()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync(TestsRunEndpoint.RouteFor(Guid.NewGuid()), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.Handler.RequestCount);
    }

    [Fact]
    public async Task Generate_DoesNotNotify()
    {
        using var factory = new TestsRunNotificationWebApplicationFactory();
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await ImportAsync(client, project.Id, Spec);

        var response = await client.PostAsync(TestsGenerateEndpoint.RouteFor(project.Id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, factory.Handler.RequestCount);
    }

    private static JsonElement ReadPostedEvent(TestsRunNotificationWebApplicationFactory factory)
    {
        Assert.False(string.IsNullOrWhiteSpace(factory.Handler.LastBody));
        return JsonDocument.Parse(factory.Handler.LastBody!).RootElement.Clone();
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            ProjectsEndpoint.Route,
            new { name = "Pets API", baseUrl = "https://pets.example.com" });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        return project;
    }

    private static async Task ImportAsync(HttpClient client, Guid projectId, string specification)
    {
        var json = JsonSerializer.Serialize(new { specification });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(SpecsImportEndpoint.RouteFor(projectId), content);
        response.EnsureSuccessStatusCode();
    }

    private static ApiTestExecutionResult Succeed(GeneratedApiTestCase test, string body) =>
        new(
            test.Kind,
            test.Method,
            test.Path,
            test.SourceMethod,
            test.SourcePath,
            test.ExpectedStatus,
            test.ExpectedStatus,
            new Dictionary<string, string>(),
            body,
            durationMs: 1,
            statusMatched: true,
            error: null);
}

public sealed class TestsRunNotificationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"testshield-notify-{Guid.NewGuid():N}.db");
    private readonly bool _enabled;
    private readonly bool _notifyOnSafe;
    private readonly string? _triggerUrl;
    private readonly int _timeoutSeconds;

    public TestsRunNotificationWebApplicationFactory(
        bool enabled = true,
        bool notifyOnSafe = false,
        string? triggerUrl = "https://power-automate.test/trigger?sig=test-sig",
        int timeoutSeconds = 10)
    {
        _enabled = enabled;
        _notifyOnSafe = notifyOnSafe;
        _triggerUrl = triggerUrl;
        _timeoutSeconds = timeoutSeconds;
    }

    public RecordingHttpMessageHandler Handler { get; } = new();

    public bool ThrowingNotifier { get; set; }

    public StubApiTestRunner Runner => (StubApiTestRunner)Services.GetRequiredService<IApiTestRunner>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TestShield", $"Data Source={_dbPath}");
        builder.UseSetting("Notifications:Enabled", _enabled ? "true" : "false");
        builder.UseSetting("Notifications:NotifyOnSafe", _notifyOnSafe ? "true" : "false");
        builder.UseSetting("Notifications:TimeoutSeconds", _timeoutSeconds.ToString());
        builder.UseSetting("Notifications:DashboardBaseUrl", "https://dashboard.example.test");
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IApiTestRunner)).ToList())
            {
                services.Remove(descriptor);
            }

            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(INotificationTriggerUrlAccessor)).ToList())
            {
                services.Remove(descriptor);
            }

            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IRunCompletedNotifier)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IApiTestRunner, StubApiTestRunner>();
            services.AddSingleton<INotificationTriggerUrlAccessor>(new StubNotificationTriggerUrlAccessor(_triggerUrl));

            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(PowerAutomateHttpNotifier)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddTransient(provider => new PowerAutomateHttpNotifier(
                new HttpClient(Handler, disposeHandler: false)
                {
                    Timeout = TimeSpan.FromSeconds(_timeoutSeconds > 0 ? _timeoutSeconds : 10)
                },
                provider.GetRequiredService<IOptions<NotificationOptions>>(),
                provider.GetRequiredService<INotificationTriggerUrlAccessor>(),
                provider.GetRequiredService<ILogger<PowerAutomateHttpNotifier>>()));
            services.AddTransient<IRunCompletedNotifier>(provider =>
            {
                if (ThrowingNotifier)
                {
                    return new ThrowingRunCompletedNotifier();
                }

                return _enabled
                    ? provider.GetRequiredService<PowerAutomateHttpNotifier>()
                    : provider.GetRequiredService<NoOpRunCompletedNotifier>();
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}

public sealed class ThrowingRunCompletedNotifier : IRunCompletedNotifier
{
    public Task NotifyAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("notification boom");
}
