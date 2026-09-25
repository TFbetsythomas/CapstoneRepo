namespace TestShieldAI.Api.Insights;

public sealed class InsightsIntentRouter : IInsightsIntentRouter
{
    public InsightsIntent Route(string? question)
    {
        var text = question?.Trim() ?? "";
        if (text.Length == 0)
        {
            return InsightsIntent.RunOverview;
        }

        if (ContainsAny(text, "contract drift", "contract-drift"))
        {
            return InsightsIntent.ContractDrift;
        }

        if (ContainsAny(text, "baseline"))
        {
            return InsightsIntent.BaselineDiff;
        }

        if (ContainsAny(text, "negative scenario", "negative scenarios", "negative tests"))
        {
            return InsightsIntent.NegativeScenarios;
        }

        if (ContainsAny(text, "coverage"))
        {
            return InsightsIntent.ExplainCoverage;
        }

        if (ContainsAny(text, "failed", "failing", "failure"))
        {
            return InsightsIntent.FailedTests;
        }

        if (ContainsAny(text, "blocked", "block", "why was this run", "decision"))
        {
            return InsightsIntent.ExplainDecision;
        }

        return InsightsIntent.RunOverview;
    }

    private static bool ContainsAny(string text, params string[] phrases) =>
        phrases.Any(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase));
}
