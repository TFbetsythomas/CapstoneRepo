using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TestShieldAI.Api.Notifications;

public sealed class PowerAutomateHttpNotifier : IRunCompletedNotifier
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly NotificationOptions _options;
    private readonly INotificationTriggerUrlAccessor _triggerUrl;
    private readonly ILogger<PowerAutomateHttpNotifier> _logger;

    public PowerAutomateHttpNotifier(
        HttpClient httpClient,
        IOptions<NotificationOptions> options,
        INotificationTriggerUrlAccessor triggerUrl,
        ILogger<PowerAutomateHttpNotifier> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new NotificationOptions();
        _triggerUrl = triggerUrl ?? throw new ArgumentNullException(nameof(triggerUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient.Timeout = TimeSpan.FromSeconds(EffectiveTimeoutSeconds());
    }

    public async Task NotifyAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var url = _triggerUrl.GetHttpTriggerUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("Power Automate notification skipped because the trigger URL is not configured.");
            return;
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                url,
                notificationEvent,
                JsonOptions,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Power Automate notification failed.");
            }
        }
        catch (Exception)
        {
            _logger.LogWarning("Power Automate notification failed.");
        }
    }

    private int EffectiveTimeoutSeconds() =>
        _options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 10;
}
