namespace TestShieldAI.Api.Insights;

public interface IDeterministicInsightsAnswerer
{
    string Answer(InsightsIntent intent, InsightsEvidence evidence);
}
