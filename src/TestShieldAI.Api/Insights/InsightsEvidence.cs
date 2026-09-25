using TestShieldAI.Engine;

namespace TestShieldAI.Api.Insights;

public sealed class InsightsEvidence
{
    public required Guid ProjectId { get; init; }

    public required string ProjectName { get; init; }

    public bool HasLastRun { get; init; }

    public string? Decision { get; init; }

    public DateTimeOffset? LastRunCompletedAt { get; init; }

    public int PassedCount { get; init; }

    public int FailedCount { get; init; }

    public int ErrorCount { get; init; }

    public int FindingCount { get; init; }

    public InsightsRiskEvidence? Risk { get; init; }

    public InsightsCoverageEvidence? Coverage { get; init; }

    public IReadOnlyList<InsightsCoverageGapEvidence> CoverageGaps { get; init; } = [];

    public IReadOnlyList<InsightsFindingEvidence> Findings { get; init; } = [];

    public IReadOnlyList<InsightsFailedTestEvidence> FailedTests { get; init; } = [];

    public IReadOnlyList<InsightsNegativeScenarioEvidence> NegativeScenarios { get; init; } = [];

    public IReadOnlyList<InsightsBaselineChangeEvidence> BaselineChanges { get; init; } = [];

    public IReadOnlyList<string> RetrievedEvidenceIds { get; init; } = [];
}

public sealed class InsightsRiskEvidence
{
    public required string Level { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }
}

public sealed class InsightsCoverageEvidence
{
    public required decimal Percent { get; init; }

    public required int TotalOperations { get; init; }

    public required int CoveredOperations { get; init; }

    public required int UncoveredOperations { get; init; }
}

public sealed class InsightsCoverageGapEvidence
{
    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string Reason { get; init; }
}

public sealed class InsightsFindingEvidence
{
    public required string Category { get; init; }

    public required string Severity { get; init; }

    public required string Code { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string Message { get; init; }
}

public sealed class InsightsFailedTestEvidence
{
    public required string Kind { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string Outcome { get; init; }

    public required int ExpectedStatus { get; init; }

    public int? ActualStatus { get; init; }
}

public sealed class InsightsNegativeScenarioEvidence
{
    public required string Kind { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required int ExpectedStatus { get; init; }
}

public sealed class InsightsBaselineChangeEvidence
{
    public required string Change { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string Detail { get; init; }
}

public static class InsightsEvidenceLimits
{
    public const int MaxFindings = 12;

    public const int MaxFailedTests = 15;

    public const int MaxCoverageGaps = 10;

    public const int MaxNegativeScenarios = 15;

    public const int MaxBaselineChanges = 20;

    public const int MaxSummaryFacts = 10;

    public static int SeverityRank(string? severity)
    {
        if (Enum.TryParse<RegressionFindingSeverity>(severity, ignoreCase: true, out var parsed))
        {
            return (int)parsed;
        }

        return int.MaxValue;
    }
}
