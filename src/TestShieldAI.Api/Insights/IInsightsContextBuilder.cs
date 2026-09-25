namespace TestShieldAI.Api.Insights;

public interface IInsightsContextBuilder
{
    InsightsContext Build(InsightsIntent intent, InsightsEvidence evidence);
}
