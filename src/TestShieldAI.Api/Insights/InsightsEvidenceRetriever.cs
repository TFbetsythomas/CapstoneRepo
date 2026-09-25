using TestShieldAI.Api.OpenApi;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Insights;

public sealed class InsightsEvidenceRetriever : IInsightsEvidenceRetriever
{
    private readonly IProjectTestRunStore _lastRuns;
    private readonly IProjectBaselineStore _baselines;
    private readonly IOpenApiSpecificationStore _specifications;
    private readonly IOpenApiIngestor _ingestor;
    private readonly IApiTestGenerator _generator;
    private readonly ICoverageCalculator _coverage;
    private readonly IRiskSummaryBuilder _risk;

    public InsightsEvidenceRetriever(
        IProjectTestRunStore lastRuns,
        IProjectBaselineStore baselines,
        IOpenApiSpecificationStore specifications,
        IOpenApiIngestor ingestor,
        IApiTestGenerator generator,
        ICoverageCalculator coverage,
        IRiskSummaryBuilder risk)
    {
        _lastRuns = lastRuns;
        _baselines = baselines;
        _specifications = specifications;
        _ingestor = ingestor;
        _generator = generator;
        _coverage = coverage;
        _risk = risk;
    }

    public async Task<InsightsEvidence> RetrieveAsync(
        ProjectRecord project,
        InsightsIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var lastRun = await _lastRuns.FindByProjectIdAsync(project.Id, cancellationToken);
        if (lastRun is null)
        {
            return new InsightsEvidence
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                HasLastRun = false,
                RetrievedEvidenceIds = []
            };
        }

        var retrieved = new List<string> { "lastRun.decision" };
        var results = await LoadResultsIfNeededAsync(project.Id, intent, cancellationToken);
        var findings = SelectFindings(intent, results, retrieved);
        var failedTests = SelectFailedTests(intent, results, retrieved);
        var coverage = await LoadCoverageIfNeededAsync(project.Id, intent, retrieved, cancellationToken);
        var negatives = await LoadNegativesIfNeededAsync(project.Id, intent, retrieved, cancellationToken);
        var baselineChanges = await LoadBaselineChangesIfNeededAsync(
            project.Id,
            intent,
            results,
            retrieved,
            cancellationToken);
        var risk = BuildRiskIfNeeded(intent, lastRun.Decision, findings, coverage?.Calculation, retrieved);

        return new InsightsEvidence
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            HasLastRun = true,
            Decision = lastRun.Decision,
            LastRunCompletedAt = lastRun.CompletedAt,
            PassedCount = lastRun.PassedCount,
            FailedCount = lastRun.FailedCount,
            ErrorCount = lastRun.ErrorCount,
            FindingCount = lastRun.FindingCount,
            Risk = risk,
            Coverage = coverage?.Summary,
            CoverageGaps = coverage?.Gaps ?? [],
            Findings = findings,
            FailedTests = failedTests,
            NegativeScenarios = negatives,
            BaselineChanges = baselineChanges,
            RetrievedEvidenceIds = retrieved
        };
    }

    private async Task<PersistedTestRunResults?> LoadResultsIfNeededAsync(
        Guid projectId,
        InsightsIntent intent,
        CancellationToken cancellationToken)
    {
        if (intent is not (
            InsightsIntent.ExplainDecision or
            InsightsIntent.ContractDrift or
            InsightsIntent.FailedTests or
            InsightsIntent.BaselineDiff or
            InsightsIntent.RunOverview))
        {
            return null;
        }

        return await _lastRuns.GetResultsAsync(projectId, cancellationToken);
    }

    private static IReadOnlyList<InsightsFindingEvidence> SelectFindings(
        InsightsIntent intent,
        PersistedTestRunResults? results,
        List<string> retrieved)
    {
        if (intent is not (
            InsightsIntent.ExplainDecision or
            InsightsIntent.ContractDrift or
            InsightsIntent.RunOverview))
        {
            return [];
        }

        var items = results?.Findings ?? [];
        if (intent is InsightsIntent.ContractDrift)
        {
            items = items
                .Where(item => string.Equals(
                    item.Category,
                    nameof(RegressionFindingCategory.ContractDrift),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        retrieved.Add("lastRun.findings");
        return items
            .OrderBy(item => InsightsEvidenceLimits.SeverityRank(item.Severity))
            .ThenBy(item => item.Method, StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .Take(InsightsEvidenceLimits.MaxFindings)
            .Select(item => new InsightsFindingEvidence
            {
                Category = item.Category,
                Severity = item.Severity,
                Code = item.Code,
                Method = item.Method,
                Path = item.Path,
                Message = item.Message
            })
            .ToList();
    }

    private static IReadOnlyList<InsightsFailedTestEvidence> SelectFailedTests(
        InsightsIntent intent,
        PersistedTestRunResults? results,
        List<string> retrieved)
    {
        if (intent is not (InsightsIntent.FailedTests or InsightsIntent.RunOverview) || results is null)
        {
            return [];
        }

        retrieved.Add("lastRun.failedTests");
        return results.Tests
            .Where(test => IsFailedOrError(test.Outcome))
            .Take(InsightsEvidenceLimits.MaxFailedTests)
            .Select(test => new InsightsFailedTestEvidence
            {
                Kind = test.Kind,
                Method = test.SourceMethod,
                Path = test.SourcePath,
                Outcome = test.Outcome,
                ExpectedStatus = test.ExpectedStatus,
                ActualStatus = test.ActualStatus
            })
            .ToList();
    }

    private async Task<CoverageLoad?> LoadCoverageIfNeededAsync(
            Guid projectId,
            InsightsIntent intent,
            List<string> retrieved,
            CancellationToken cancellationToken)
    {
        if (intent is not (InsightsIntent.ExplainCoverage or InsightsIntent.RunOverview))
        {
            return null;
        }

        var generated = await GenerateCurrentTestsAsync(projectId, cancellationToken);
        if (generated is null)
        {
            return null;
        }

        var result = _coverage.Calculate(generated.Value.Operations, generated.Value.Tests);
        retrieved.Add("coverage");
        var gaps = result.Gaps
            .Take(InsightsEvidenceLimits.MaxCoverageGaps)
            .Select(gap => new InsightsCoverageGapEvidence
            {
                Method = gap.Method,
                Path = gap.Path,
                Reason = gap.Reason
            })
            .ToList();
        if (gaps.Count > 0)
        {
            retrieved.Add("coverage.gaps");
        }

        return new CoverageLoad(
            new InsightsCoverageEvidence
            {
                Percent = result.CoveragePercent,
                TotalOperations = result.TotalOperations,
                CoveredOperations = result.CoveredOperations,
                UncoveredOperations = result.UncoveredOperations
            },
            gaps,
            result);
    }

    private async Task<IReadOnlyList<InsightsNegativeScenarioEvidence>> LoadNegativesIfNeededAsync(
        Guid projectId,
        InsightsIntent intent,
        List<string> retrieved,
        CancellationToken cancellationToken)
    {
        if (intent is not InsightsIntent.NegativeScenarios)
        {
            return [];
        }

        var generated = await GenerateCurrentTestsAsync(projectId, cancellationToken);
        if (generated is null)
        {
            return [];
        }

        retrieved.Add("generated.negativeScenarios");
        return generated.Value.Tests
            .Where(IsNegative)
            .Take(InsightsEvidenceLimits.MaxNegativeScenarios)
            .Select(test => new InsightsNegativeScenarioEvidence
            {
                Kind = test.Kind.ToString(),
                Method = test.SourceMethod,
                Path = test.SourcePath,
                ExpectedStatus = test.ExpectedStatus
            })
            .ToList();
    }

    private async Task<IReadOnlyList<InsightsBaselineChangeEvidence>> LoadBaselineChangesIfNeededAsync(
        Guid projectId,
        InsightsIntent intent,
        PersistedTestRunResults? results,
        List<string> retrieved,
        CancellationToken cancellationToken)
    {
        if (intent is not InsightsIntent.BaselineDiff)
        {
            return [];
        }

        var snapshots = await _baselines.GetSnapshotsAsync(projectId, cancellationToken);
        if (snapshots.Count == 0)
        {
            retrieved.Add("baseline.none");
            return [];
        }

        retrieved.Add("baseline.changes");

        var current = results?.Tests ?? [];
        var baselineById = snapshots
            .GroupBy(Identity)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var currentById = current
            .GroupBy(Identity)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var changes = new List<InsightsBaselineChangeEvidence>();
        foreach (var (id, baseline) in baselineById)
        {
            if (!currentById.TryGetValue(id, out var currentTest))
            {
                changes.Add(new InsightsBaselineChangeEvidence
                {
                    Change = "removed",
                    Method = baseline.SourceMethod,
                    Path = baseline.SourcePath,
                    Detail = "A baselined test is not present in the latest run."
                });
                continue;
            }

            if (baseline.ExpectedStatus != currentTest.ExpectedStatus)
            {
                changes.Add(new InsightsBaselineChangeEvidence
                {
                    Change = "expectedStatus",
                    Method = baseline.SourceMethod,
                    Path = baseline.SourcePath,
                    Detail =
                        $"Expected status changed from {baseline.ExpectedStatus} to {currentTest.ExpectedStatus}."
                });
            }

            var currentOutcome = currentTest.Outcome;
            var baselineOutcome = baseline.Outcome.ToString();
            if (!string.Equals(baselineOutcome, currentOutcome, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new InsightsBaselineChangeEvidence
                {
                    Change = "outcome",
                    Method = baseline.SourceMethod,
                    Path = baseline.SourcePath,
                    Detail = $"Outcome changed from {baselineOutcome} to {currentOutcome}."
                });
            }
        }

        foreach (var (id, currentTest) in currentById)
        {
            if (baselineById.ContainsKey(id))
            {
                continue;
            }

            changes.Add(new InsightsBaselineChangeEvidence
            {
                Change = "added",
                Method = currentTest.SourceMethod,
                Path = currentTest.SourcePath,
                Detail = "A test in the latest run is not present in the baseline."
            });
        }

        return changes.Take(InsightsEvidenceLimits.MaxBaselineChanges).ToList();
    }

    private InsightsRiskEvidence? BuildRiskIfNeeded(
        InsightsIntent intent,
        string decision,
        IReadOnlyList<InsightsFindingEvidence> findings,
        CoverageCalculationResult? coverage,
        List<string> retrieved)
    {
        if (intent is not (InsightsIntent.ExplainDecision or InsightsIntent.RunOverview))
        {
            return null;
        }

        if (!Enum.TryParse<RegressionDecision>(decision, ignoreCase: true, out var parsed))
        {
            return null;
        }

        retrieved.Add("risk");
        var detection = new RegressionDetectionResult(
            findings.Select(ToFinding).ToList(),
            parsed);
        var risk = _risk.Build(detection, coverage, testsExecuted: true);
        return new InsightsRiskEvidence
        {
            Level = risk.Level.ToString(),
            Title = risk.Title,
            Summary = risk.Summary
        };
    }

    private async Task<(IReadOnlyList<ImportedOperation> Operations, IReadOnlyList<GeneratedApiTestCase> Tests)?>
        GenerateCurrentTestsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var records = await _specifications.ListByProjectIdAsync(projectId, cancellationToken);
        var current = CurrentOpenApiSpecificationSelector.SelectLatestPerSpecKey(records, _ingestor);
        if (current.Count == 0)
        {
            return null;
        }

        var operations = current
            .SelectMany(specification => specification.Import.Operations)
            .ToList();
        return (operations, _generator.Generate(operations));
    }

    private static bool IsFailedOrError(string outcome) =>
        string.Equals(outcome, nameof(ContractValidationOutcome.Failed), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(outcome, nameof(ContractValidationOutcome.Error), StringComparison.OrdinalIgnoreCase);

    private static bool IsNegative(GeneratedApiTestCase test) =>
        test.Kind is GeneratedApiTestKind.NegativeMissingRequiredBody or GeneratedApiTestKind.AiNegative;

    private static string Identity(RegressionTestSnapshot snapshot) =>
        string.Join(
            '|',
            OpenApiSpecKey.ComparisonKey(snapshot.SpecKey),
            snapshot.Kind.ToString(),
            snapshot.SourceMethod,
            snapshot.SourcePath,
            snapshot.ScenarioKey ?? "");

    private static string Identity(PersistedTestRunCase test) =>
        string.Join(
            '|',
            OpenApiSpecKey.ComparisonKey(test.SpecKey),
            test.Kind,
            test.SourceMethod,
            test.SourcePath,
            test.ScenarioKey ?? "");

    private static RegressionFinding ToFinding(InsightsFindingEvidence finding)
    {
        Enum.TryParse<RegressionFindingCategory>(finding.Category, ignoreCase: true, out var category);
        Enum.TryParse<RegressionFindingSeverity>(finding.Severity, ignoreCase: true, out var severity);
        return new RegressionFinding(
            category,
            finding.Code,
            severity,
            kind: null,
            finding.Method,
            finding.Path,
            expected: "",
            current: "",
            jsonPath: null,
            finding.Message);
    }

    private sealed record CoverageLoad(
        InsightsCoverageEvidence Summary,
        IReadOnlyList<InsightsCoverageGapEvidence> Gaps,
        CoverageCalculationResult Calculation);
}
