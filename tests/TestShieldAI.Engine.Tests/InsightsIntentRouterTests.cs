using TestShieldAI.Api.Insights;

namespace TestShieldAI.Engine.Tests;

public class InsightsIntentRouterTests
{
    private readonly InsightsIntentRouter _router = new();

    [Theory]
    [InlineData("Why was this run blocked?", InsightsIntent.ExplainDecision)]
    [InlineData("  why was this run blocked?  ", InsightsIntent.ExplainDecision)]
    [InlineData("Why is coverage 75%?", InsightsIntent.ExplainCoverage)]
    [InlineData("What changed from the previous baseline?", InsightsIntent.BaselineDiff)]
    [InlineData("Which operations have contract drift?", InsightsIntent.ContractDrift)]
    [InlineData("What tests failed?", InsightsIntent.FailedTests)]
    [InlineData("What tests failed in the latest run?", InsightsIntent.FailedTests)]
    [InlineData("What negative scenarios exist?", InsightsIntent.NegativeScenarios)]
    [InlineData("Show me the run overview", InsightsIntent.RunOverview)]
    [InlineData("hello", InsightsIntent.RunOverview)]
    [InlineData("", InsightsIntent.RunOverview)]
    [InlineData("   ", InsightsIntent.RunOverview)]
    public void Route_UsesKeywordMatchingAndFallback(string question, InsightsIntent expected)
    {
        Assert.Equal(expected, _router.Route(question));
    }
}
