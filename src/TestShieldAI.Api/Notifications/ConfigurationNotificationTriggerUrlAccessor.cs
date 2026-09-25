using Microsoft.Extensions.Configuration;

namespace TestShieldAI.Api.Notifications;

public sealed class ConfigurationNotificationTriggerUrlAccessor : INotificationTriggerUrlAccessor
{
    public const string EnvironmentVariable = "POWER_AUTOMATE_HTTP_TRIGGER_URL";
    public const string UserSecretsConfigurationKey = "Notifications:HttpTriggerUrl";

    private readonly IConfiguration _configuration;
    private readonly Func<string, string?> _environment;

    public ConfigurationNotificationTriggerUrlAccessor(IConfiguration configuration)
        : this(configuration, Environment.GetEnvironmentVariable)
    {
    }

    public ConfigurationNotificationTriggerUrlAccessor(
        IConfiguration configuration,
        Func<string, string?> environment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public string? GetHttpTriggerUrl()
    {
        return FirstNonEmpty(
            _environment(EnvironmentVariable),
            _configuration[EnvironmentVariable],
            _configuration[UserSecretsConfigurationKey]);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
