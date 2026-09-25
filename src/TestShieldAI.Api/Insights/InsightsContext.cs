namespace TestShieldAI.Api.Insights;

public sealed class InsightsContext
{
    public const string NoDecision = "NONE";

    public const string PersistedDecisionSource = "Persisted TestShield run decision";

    public const string NoRunSource = "No persisted TestShield run";

    public required InsightsIntent Intent { get; init; }

    public required string ProjectName { get; init; }

    public string? Decision { get; init; }

    public DateTimeOffset? LastRunCompletedAt { get; init; }

    public int? PassedCount { get; init; }

    public int? FailedCount { get; init; }

    public int? ErrorCount { get; init; }

    public int? FindingCount { get; init; }

    public required string AuthoritativeDecision { get; init; }

    public required string AuthoritativeDecisionSource { get; init; }

    public required string AuthoritativeDecisionStatement { get; init; }

    public IReadOnlyList<string> SummaryFacts { get; init; } = [];

    public InsightsRiskEvidence? Risk { get; init; }

    public InsightsCoverageEvidence? Coverage { get; init; }

    public IReadOnlyList<InsightsFindingEvidence> Findings { get; init; } = [];

    public IReadOnlyList<InsightsFailedTestEvidence> FailedTests { get; init; } = [];

    public IReadOnlyList<InsightsCoverageGapEvidence> CoverageGaps { get; init; } = [];

    public IReadOnlyList<InsightsNegativeScenarioEvidence> NegativeScenarios { get; init; } = [];

    public IReadOnlyList<InsightsBaselineChangeEvidence> BaselineChanges { get; init; } = [];
}
