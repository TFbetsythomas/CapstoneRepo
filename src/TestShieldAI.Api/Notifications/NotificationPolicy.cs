using TestShieldAI.Engine;

namespace TestShieldAI.Api.Notifications;

public static class NotificationPolicy
{
    public static bool ShouldNotify(RegressionDecision decision, NotificationOptions? options)
    {
        if (options is null || !options.Enabled)
        {
            return false;
        }

        return decision switch
        {
            RegressionDecision.Block => options.NotifyOnBlock,
            RegressionDecision.Review => options.NotifyOnReview,
            RegressionDecision.Safe => options.NotifyOnSafe,
            _ => false
        };
    }
}
