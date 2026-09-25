using TestShieldAI.Api.Notifications;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class NotificationPolicyTests
{
    [Fact]
    public void ShouldNotify_Disabled_IsFalseForEveryDecision()
    {
        var options = EnabledOptions();
        options.Enabled = false;

        Assert.False(NotificationPolicy.ShouldNotify(RegressionDecision.Block, options));
        Assert.False(NotificationPolicy.ShouldNotify(RegressionDecision.Review, options));
        Assert.False(NotificationPolicy.ShouldNotify(RegressionDecision.Safe, options));
    }

    [Fact]
    public void ShouldNotify_NullOptions_IsFalse()
    {
        Assert.False(NotificationPolicy.ShouldNotify(RegressionDecision.Block, null));
    }

    [Fact]
    public void ShouldNotify_Defaults_BlockAndReviewOnly()
    {
        var options = EnabledOptions();

        Assert.True(NotificationPolicy.ShouldNotify(RegressionDecision.Block, options));
        Assert.True(NotificationPolicy.ShouldNotify(RegressionDecision.Review, options));
        Assert.False(NotificationPolicy.ShouldNotify(RegressionDecision.Safe, options));
    }

    [Fact]
    public void ShouldNotify_Safe_WhenNotifyOnSafe()
    {
        var options = EnabledOptions();
        options.NotifyOnSafe = true;

        Assert.True(NotificationPolicy.ShouldNotify(RegressionDecision.Safe, options));
    }

    [Fact]
    public void ShouldNotify_RespectsPerDecisionFlags()
    {
        var options = EnabledOptions();
        options.NotifyOnBlock = false;
        options.NotifyOnReview = false;
        options.NotifyOnSafe = true;

        Assert.False(NotificationPolicy.ShouldNotify(RegressionDecision.Block, options));
        Assert.False(NotificationPolicy.ShouldNotify(RegressionDecision.Review, options));
        Assert.True(NotificationPolicy.ShouldNotify(RegressionDecision.Safe, options));
    }

    private static NotificationOptions EnabledOptions() =>
        new()
        {
            Enabled = true,
            NotifyOnSafe = false,
            NotifyOnReview = true,
            NotifyOnBlock = true
        };
}
