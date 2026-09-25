using TestShieldAI.Api.Persistence;

namespace TestShieldAI.Api.Insights;

public interface IInsightsEvidenceRetriever
{
    Task<InsightsEvidence> RetrieveAsync(
        ProjectRecord project,
        InsightsIntent intent,
        CancellationToken cancellationToken = default);
}
