namespace TestShieldAI.Api.Insights;

public interface IInsightsAssistant
{
    Task<InsightsAssistantResult> AnswerAsync(
        string question,
        InsightsIntent intent,
        InsightsContext context,
        CancellationToken cancellationToken = default);
}
