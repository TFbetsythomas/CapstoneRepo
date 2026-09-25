using TestShieldAI.Api.Insights;

namespace TestShieldAI.Engine.Tests;

public class DeterministicInsightsAnswererTests
{
    private readonly DeterministicInsightsAnswerer _answerer = new();

    [Fact]
    public void Answer_NoLastRun_ReturnsNoRunMessage()
    {
        var evidence = new InsightsEvidence
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = "Pets API",
            HasLastRun = false
        };

        var answer = _answerer.Answer(InsightsIntent.ExplainDecision, evidence);

        Assert.Equal(DeterministicInsightsAnswerer.NoRunAnswer, answer);
    }

    [Fact]
    public void Answer_ExplainDecision_UsesPersistedDecision()
    {
        var evidence = new InsightsEvidence
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = "Pets API",
            HasLastRun = true,
            Decision = "Block",
            FindingCount = 1,
            Findings =
            [
                new InsightsFindingEvidence
                {
                    Category = "Regression",
                    Severity = "High",
                    Code = "VALIDATION_REGRESSED",
                    Method = "GET",
                    Path = "/pets",
                    Message = "status changed"
                }
            ]
        };

        var answer = _answerer.Answer(InsightsIntent.ExplainDecision, evidence);

        Assert.Contains("This run is Block", answer, StringComparison.Ordinal);
        Assert.Contains("1 finding(s)", answer, StringComparison.Ordinal);
        Assert.Contains("High", answer, StringComparison.Ordinal);
        Assert.Contains("GET /pets", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Review", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Safe", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_ExplainCoverage_UsesCoverageCounts()
    {
        var evidence = new InsightsEvidence
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = "Pets API",
            HasLastRun = true,
            Decision = "Safe",
            Coverage = new InsightsCoverageEvidence
            {
                Percent = 75m,
                TotalOperations = 4,
                CoveredOperations = 3,
                UncoveredOperations = 1
            }
        };

        var answer = _answerer.Answer(InsightsIntent.ExplainCoverage, evidence);

        Assert.Equal("Coverage is 75% because 3 of 4 operations are covered.", answer);
    }
}
