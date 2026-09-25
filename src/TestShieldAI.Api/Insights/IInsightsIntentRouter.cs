namespace TestShieldAI.Api.Insights;

public interface IInsightsIntentRouter
{
    InsightsIntent Route(string? question);
}
