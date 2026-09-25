using Microsoft.Extensions.Configuration;
using TestShieldAI.Api.Notifications;

namespace TestShieldAI.Engine.Tests;

public class ConfigurationNotificationTriggerUrlAccessorTests
{
    [Fact]
    public void GetHttpTriggerUrl_EnvironmentVariable_TakesPrecedence()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationNotificationTriggerUrlAccessor.UserSecretsConfigurationKey] = "https://secrets.test/trigger?sig=secret",
                [ConfigurationNotificationTriggerUrlAccessor.EnvironmentVariable] = "https://config-env.test/trigger?sig=config"
            })
            .Build();
        var accessor = new ConfigurationNotificationTriggerUrlAccessor(
            configuration,
            name => name == ConfigurationNotificationTriggerUrlAccessor.EnvironmentVariable
                ? "https://env.test/trigger?sig=env"
                : null);

        Assert.Equal("https://env.test/trigger?sig=env", accessor.GetHttpTriggerUrl());
    }

    [Fact]
    public void GetHttpTriggerUrl_UserSecrets_WhenEnvironmentMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationNotificationTriggerUrlAccessor.UserSecretsConfigurationKey] = " https://secrets.test/trigger?sig=secret "
            })
            .Build();
        var accessor = new ConfigurationNotificationTriggerUrlAccessor(configuration, _ => null);

        Assert.Equal("https://secrets.test/trigger?sig=secret", accessor.GetHttpTriggerUrl());
    }

    [Fact]
    public void GetHttpTriggerUrl_Blank_ReturnsNull()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationNotificationTriggerUrlAccessor.UserSecretsConfigurationKey] = "  "
            })
            .Build();
        var accessor = new ConfigurationNotificationTriggerUrlAccessor(configuration, _ => " ");

        Assert.Null(accessor.GetHttpTriggerUrl());
    }
}
