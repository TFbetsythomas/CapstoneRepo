using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestShieldAI.Api.Notifications;

namespace TestShieldAI.Engine.Tests;

public class PowerAutomateHttpNotifierTests
{
    private const string TriggerUrl = "https://power-automate.test/workflows/demo/triggers/manual/run?sig=super-secret-sig";

    [Fact]
    public async Task NotifyAsync_Disabled_SendsNoRequest()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifier = CreateNotifier(handler, enabled: false, triggerUrl: TriggerUrl);

        await notifier.NotifyAsync(SampleEvent(), CancellationToken.None);

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task NotifyAsync_MissingTriggerUrl_SendsNoRequest()
    {
        var handler = new RecordingHttpMessageHandler();
        var logger = new CollectingLogger<PowerAutomateHttpNotifier>();
        var notifier = CreateNotifier(handler, enabled: true, triggerUrl: null, logger);

        await notifier.NotifyAsync(SampleEvent(), CancellationToken.None);

        Assert.Equal(0, handler.RequestCount);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("sig=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NotifyAsync_PostsJsonWithApplicationJson()
    {
        var handler = new RecordingHttpMessageHandler { StatusCode = HttpStatusCode.Accepted };
        var notifier = CreateNotifier(handler, enabled: true, triggerUrl: TriggerUrl);

        await notifier.NotifyAsync(SampleEvent(), CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(handler.LastBody));
        using var document = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(NotificationEvent.RunCompletedEventType, document.RootElement.GetProperty("eventType").GetString());
        Assert.Equal("Block", document.RootElement.GetProperty("decision").GetString());
        Assert.True(document.RootElement.TryGetProperty("risk", out _));
        Assert.True(document.RootElement.TryGetProperty("coverage", out _));
        Assert.Equal(TriggerUrl, handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task NotifyAsync_HttpError_DoesNotThrow()
    {
        var handler = new RecordingHttpMessageHandler { StatusCode = HttpStatusCode.BadRequest };
        var notifier = CreateNotifier(handler, enabled: true, triggerUrl: TriggerUrl);

        await notifier.NotifyAsync(SampleEvent(), CancellationToken.None);
    }

    [Fact]
    public async Task NotifyAsync_ServerError_DoesNotThrow()
    {
        var handler = new RecordingHttpMessageHandler { StatusCode = HttpStatusCode.InternalServerError };
        var notifier = CreateNotifier(handler, enabled: true, triggerUrl: TriggerUrl);

        await notifier.NotifyAsync(SampleEvent(), CancellationToken.None);
    }

    [Fact]
    public async Task NotifyAsync_Timeout_DoesNotThrow()
    {
        var handler = new RecordingHttpMessageHandler { Delay = TimeSpan.FromSeconds(5) };
        var notifier = CreateNotifier(handler, enabled: true, triggerUrl: TriggerUrl, timeoutSeconds: 1);

        await notifier.NotifyAsync(SampleEvent(), CancellationToken.None);
    }

    [Fact]
    public async Task NotifyAsync_NetworkException_DoesNotThrow()
    {
        var handler = new RecordingHttpMessageHandler { Exception = new HttpRequestException("network down") };
        var notifier = CreateNotifier(handler, enabled: true, triggerUrl: TriggerUrl);

        await notifier.NotifyAsync(SampleEvent(), CancellationToken.None);
    }

    [Fact]
    public async Task NotifyAsync_DoesNotLogTriggerUrlOrSignature()
    {
        var handler = new RecordingHttpMessageHandler { StatusCode = HttpStatusCode.BadGateway };
        var logger = new CollectingLogger<PowerAutomateHttpNotifier>();
        var notifier = CreateNotifier(handler, enabled: true, triggerUrl: TriggerUrl, logger: logger);

        await notifier.NotifyAsync(SampleEvent(), CancellationToken.None);

        Assert.All(logger.Messages, message =>
        {
            Assert.DoesNotContain(TriggerUrl, message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sig=", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("super-secret-sig", message, StringComparison.Ordinal);
            Assert.DoesNotContain("power-automate.test", message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task NoOp_DoesNothing()
    {
        await new NoOpRunCompletedNotifier().NotifyAsync(SampleEvent(), CancellationToken.None);
    }

    private static PowerAutomateHttpNotifier CreateNotifier(
        RecordingHttpMessageHandler handler,
        bool enabled,
        string? triggerUrl,
        CollectingLogger<PowerAutomateHttpNotifier>? logger = null,
        int timeoutSeconds = 10)
    {
        var options = Options.Create(new NotificationOptions
        {
            Enabled = enabled,
            TimeoutSeconds = timeoutSeconds
        });
        return new PowerAutomateHttpNotifier(
            new HttpClient(handler),
            options,
            new StubNotificationTriggerUrlAccessor(triggerUrl),
            logger ?? new CollectingLogger<PowerAutomateHttpNotifier>());
    }

    private static NotificationEvent SampleEvent() =>
        new()
        {
            SchemaVersion = NotificationEvent.CurrentSchemaVersion,
            EventType = NotificationEvent.RunCompletedEventType,
            CompletedAt = DateTimeOffset.UtcNow,
            ProjectId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            ProjectName = "Pets API",
            Decision = "Block",
            Risk = new NotificationRisk
            {
                Level = "High",
                Title = "High risk",
                Summary = "Something broke.",
                Recommendations = ["Fix it."]
            },
            Coverage = new NotificationCoverage
            {
                Percent = 50m,
                TotalOperations = 2,
                CoveredOperations = 1,
                UncoveredOperations = 1
            },
            Results = new NotificationResults { Passed = 0, Failed = 1, Error = 0 },
            Findings = new NotificationFindings
            {
                Count = 1,
                Items =
                [
                    new NotificationFinding
                    {
                        Category = "Regression",
                        Severity = "High",
                        Method = "GET",
                        Path = "/pets",
                        Message = "status changed"
                    }
                ]
            },
            Ai = new NotificationAiSummary { Enabled = false, WarningCount = 0, Warnings = [] }
        };
}

public sealed class StubNotificationTriggerUrlAccessor : INotificationTriggerUrlAccessor
{
    private readonly string? _url;

    public StubNotificationTriggerUrlAccessor(string? url) => _url = url;

    public string? GetHttpTriggerUrl() => _url;
}

public sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    public int RequestCount { get; private set; }

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.Accepted;

    public TimeSpan? Delay { get; set; }

    public Exception? Exception { get; set; }

    public void Reset()
    {
        RequestCount = 0;
        LastRequest = null;
        LastBody = null;
        StatusCode = HttpStatusCode.Accepted;
        Delay = null;
        Exception = null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequest = request;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        if (Delay is { } delay)
        {
            await Task.Delay(delay, cancellationToken);
        }

        if (Exception is not null)
        {
            throw Exception;
        }

        return new HttpResponseMessage(StatusCode);
    }
}

public sealed class CollectingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
        if (exception is not null)
        {
            Messages.Add(exception.ToString());
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
