using TestShieldAI.Engine;

namespace TestShieldAI.Api.Insights;

public sealed class InsightsContextBuilder : IInsightsContextBuilder
{
    public InsightsContext Build(InsightsIntent intent, InsightsEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var (decisionValue, source, statement) = AuthoritativeDecision(evidence);
        var findings = IncludeFindings(intent)
            ? SelectFindings(intent, evidence.Findings)
            : [];
        var failedTests = IncludeFailedTests(intent)
            ? SelectFailedTests(evidence.FailedTests)
            : [];
        var coverage = IncludeCoverage(intent) ? CopyCoverage(evidence.Coverage) : null;
        var gaps = IncludeCoverage(intent)
            ? SelectGaps(evidence.CoverageGaps)
            : [];
        var negatives = IncludeNegatives(intent)
            ? SelectNegatives(evidence.NegativeScenarios)
            : [];
        var baselineChanges = IncludeBaseline(intent)
            ? SelectBaselineChanges(evidence.BaselineChanges)
            : [];
        var risk = IncludeRisk(intent) ? CopyRisk(evidence.Risk) : null;

        return new InsightsContext
        {
            Intent = intent,
            ProjectName = evidence.ProjectName,
            Decision = evidence.HasLastRun ? evidence.Decision : null,
            LastRunCompletedAt = evidence.HasLastRun ? evidence.LastRunCompletedAt : null,
            PassedCount = evidence.HasLastRun ? evidence.PassedCount : null,
            FailedCount = evidence.HasLastRun ? evidence.FailedCount : null,
            ErrorCount = evidence.HasLastRun ? evidence.ErrorCount : null,
            FindingCount = evidence.HasLastRun ? evidence.FindingCount : null,
            AuthoritativeDecision = decisionValue,
            AuthoritativeDecisionSource = source,
            AuthoritativeDecisionStatement = statement,
            SummaryFacts = SummaryFacts(intent, evidence, coverage, findings, failedTests, negatives, baselineChanges),
            Risk = risk,
            Coverage = coverage,
            Findings = findings,
            FailedTests = failedTests,
            CoverageGaps = gaps,
            NegativeScenarios = negatives,
            BaselineChanges = baselineChanges
        };
    }

    private static (string Decision, string Source, string Statement) AuthoritativeDecision(InsightsEvidence evidence)
    {
        if (!evidence.HasLastRun || string.IsNullOrWhiteSpace(evidence.Decision))
        {
            return (
                InsightsContext.NoDecision,
                InsightsContext.NoRunSource,
                $"""
                AUTHORITATIVE_DECISION: {InsightsContext.NoDecision}
                SOURCE: {InsightsContext.NoRunSource}
                """);
        }

        return (
            evidence.Decision,
            InsightsContext.PersistedDecisionSource,
            $"""
            AUTHORITATIVE_DECISION: {evidence.Decision}
            SOURCE: {InsightsContext.PersistedDecisionSource}
            """);
    }

    private static bool IncludeFindings(InsightsIntent intent) =>
        intent is InsightsIntent.ExplainDecision or InsightsIntent.ContractDrift or InsightsIntent.RunOverview;

    private static bool IncludeFailedTests(InsightsIntent intent) =>
        intent is InsightsIntent.FailedTests;

    private static bool IncludeCoverage(InsightsIntent intent) =>
        intent is InsightsIntent.ExplainCoverage or InsightsIntent.RunOverview;

    private static bool IncludeNegatives(InsightsIntent intent) =>
        intent is InsightsIntent.NegativeScenarios;

    private static bool IncludeBaseline(InsightsIntent intent) =>
        intent is InsightsIntent.BaselineDiff;

    private static bool IncludeRisk(InsightsIntent intent) =>
        intent is InsightsIntent.ExplainDecision or InsightsIntent.RunOverview;

    private static IReadOnlyList<InsightsFindingEvidence> SelectFindings(
        InsightsIntent intent,
        IReadOnlyList<InsightsFindingEvidence> findings)
    {
        var selected = findings ?? [];
        if (intent is InsightsIntent.ContractDrift)
        {
            selected = selected
                .Where(finding => string.Equals(
                    finding.Category,
                    nameof(RegressionFindingCategory.ContractDrift),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return selected
            .OrderBy(finding => InsightsEvidenceLimits.SeverityRank(finding.Severity))
            .ThenBy(finding => finding.Method, StringComparer.Ordinal)
            .ThenBy(finding => finding.Path, StringComparer.Ordinal)
            .Take(InsightsEvidenceLimits.MaxFindings)
            .Select(CopyFinding)
            .ToList();
    }

    private static IReadOnlyList<InsightsFailedTestEvidence> SelectFailedTests(
        IReadOnlyList<InsightsFailedTestEvidence> tests) =>
        (tests ?? [])
            .OrderBy(OutcomeRank)
            .ThenBy(test => test.Method, StringComparer.Ordinal)
            .ThenBy(test => test.Path, StringComparer.Ordinal)
            .Take(InsightsEvidenceLimits.MaxFailedTests)
            .Select(CopyFailedTest)
            .ToList();

    private static IReadOnlyList<InsightsCoverageGapEvidence> SelectGaps(
        IReadOnlyList<InsightsCoverageGapEvidence> gaps) =>
        (gaps ?? [])
            .Take(InsightsEvidenceLimits.MaxCoverageGaps)
            .Select(CopyGap)
            .ToList();

    private static IReadOnlyList<InsightsNegativeScenarioEvidence> SelectNegatives(
        IReadOnlyList<InsightsNegativeScenarioEvidence> scenarios) =>
        (scenarios ?? [])
            .Take(InsightsEvidenceLimits.MaxNegativeScenarios)
            .Select(CopyNegative)
            .ToList();

    private static IReadOnlyList<InsightsBaselineChangeEvidence> SelectBaselineChanges(
        IReadOnlyList<InsightsBaselineChangeEvidence> changes) =>
        (changes ?? [])
            .Take(InsightsEvidenceLimits.MaxBaselineChanges)
            .Select(CopyBaselineChange)
            .ToList();

    private static IReadOnlyList<string> SummaryFacts(
        InsightsIntent intent,
        InsightsEvidence evidence,
        InsightsCoverageEvidence? coverage,
        IReadOnlyList<InsightsFindingEvidence> findings,
        IReadOnlyList<InsightsFailedTestEvidence> failedTests,
        IReadOnlyList<InsightsNegativeScenarioEvidence> negatives,
        IReadOnlyList<InsightsBaselineChangeEvidence> baselineChanges)
    {
        var facts = new List<string>();
        if (!evidence.HasLastRun)
        {
            facts.Add("No persisted TestShield run exists for this project.");
            return facts.Take(InsightsEvidenceLimits.MaxSummaryFacts).ToList();
        }

        if (intent is InsightsIntent.ExplainDecision or InsightsIntent.RunOverview)
        {
            Add(facts, $"Persisted decision is {evidence.Decision}.");
            Add(facts, $"Finding count is {evidence.FindingCount}.");
            Add(facts, $"Passed {evidence.PassedCount}, failed {evidence.FailedCount}, errors {evidence.ErrorCount}.");
            if (evidence.Risk is not null)
            {
                Add(facts, $"Risk is {evidence.Risk.Level}: {evidence.Risk.Title}.");
            }
        }

        if (intent is InsightsIntent.ExplainCoverage or InsightsIntent.RunOverview)
        {
            if (coverage is not null)
            {
                Add(facts, $"Coverage is {coverage.Percent}% ({coverage.CoveredOperations} of {coverage.TotalOperations} operations).");
                Add(facts, $"Uncovered operations: {coverage.UncoveredOperations}.");
            }
        }

        if (intent is InsightsIntent.ContractDrift)
        {
            Add(facts, $"Contract-drift findings included: {findings.Count}.");
        }

        if (intent is InsightsIntent.FailedTests)
        {
            Add(facts, $"Failed tests included: {failedTests.Count}.");
        }

        if (intent is InsightsIntent.NegativeScenarios)
        {
            Add(facts, $"Negative scenarios included: {negatives.Count}.");
        }

        if (intent is InsightsIntent.BaselineDiff)
        {
            Add(facts, $"Baseline changes included: {baselineChanges.Count}.");
        }

        return facts.Take(InsightsEvidenceLimits.MaxSummaryFacts).ToList();
    }

    private static void Add(List<string> facts, string fact)
    {
        if (facts.Count < InsightsEvidenceLimits.MaxSummaryFacts)
        {
            facts.Add(fact);
        }
    }

    private static int OutcomeRank(InsightsFailedTestEvidence test)
    {
        if (string.Equals(test.Outcome, nameof(ContractValidationOutcome.Failed), StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(test.Outcome, nameof(ContractValidationOutcome.Error), StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static InsightsFindingEvidence CopyFinding(InsightsFindingEvidence finding) =>
        new()
        {
            Category = finding.Category,
            Severity = finding.Severity,
            Code = finding.Code,
            Method = finding.Method,
            Path = finding.Path,
            Message = finding.Message
        };

    private static InsightsFailedTestEvidence CopyFailedTest(InsightsFailedTestEvidence test) =>
        new()
        {
            Kind = test.Kind,
            Method = test.Method,
            Path = test.Path,
            Outcome = test.Outcome,
            ExpectedStatus = test.ExpectedStatus,
            ActualStatus = test.ActualStatus
        };

    private static InsightsCoverageGapEvidence CopyGap(InsightsCoverageGapEvidence gap) =>
        new()
        {
            Method = gap.Method,
            Path = gap.Path,
            Reason = gap.Reason
        };

    private static InsightsNegativeScenarioEvidence CopyNegative(InsightsNegativeScenarioEvidence scenario) =>
        new()
        {
            Kind = scenario.Kind,
            Method = scenario.Method,
            Path = scenario.Path,
            ExpectedStatus = scenario.ExpectedStatus
        };

    private static InsightsBaselineChangeEvidence CopyBaselineChange(InsightsBaselineChangeEvidence change) =>
        new()
        {
            Change = change.Change,
            Method = change.Method,
            Path = change.Path,
            Detail = change.Detail
        };

    private static InsightsCoverageEvidence? CopyCoverage(InsightsCoverageEvidence? coverage) =>
        coverage is null
            ? null
            : new InsightsCoverageEvidence
            {
                Percent = coverage.Percent,
                TotalOperations = coverage.TotalOperations,
                CoveredOperations = coverage.CoveredOperations,
                UncoveredOperations = coverage.UncoveredOperations
            };

    private static InsightsRiskEvidence? CopyRisk(InsightsRiskEvidence? risk) =>
        risk is null
            ? null
            : new InsightsRiskEvidence
            {
                Level = risk.Level,
                Title = risk.Title,
                Summary = risk.Summary
            };
}
