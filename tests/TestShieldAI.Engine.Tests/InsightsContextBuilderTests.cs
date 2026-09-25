using System.Text.Json;
using TestShieldAI.Api.Insights;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class InsightsContextBuilderTests
{
    private readonly InsightsContextBuilder _builder = new();
    private readonly InsightsContextFormatter _formatter = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Build_CopiesPersistedDecisionExactly()
    {
        var evidence = RunEvidence(decision: "Block", findings: [Finding("Regression", "High", "/a")]);

        var context = _builder.Build(InsightsIntent.ExplainDecision, evidence);

        Assert.Equal("Block", context.Decision);
        Assert.Equal("Block", context.AuthoritativeDecision);
        Assert.Equal(InsightsContext.PersistedDecisionSource, context.AuthoritativeDecisionSource);
        Assert.Contains("AUTHORITATIVE_DECISION: Block", context.AuthoritativeDecisionStatement, StringComparison.Ordinal);
        Assert.Contains("SOURCE: Persisted TestShield run decision", context.AuthoritativeDecisionStatement, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DoesNotCalculateANewDecision()
    {
        var evidence = RunEvidence(
            decision: "Safe",
            findings: [Finding("Regression", "High", "/pets")],
            failedCount: 4);

        var context = _builder.Build(InsightsIntent.ExplainDecision, evidence);

        Assert.Equal("Safe", context.Decision);
        Assert.Equal("Safe", context.AuthoritativeDecision);
        Assert.DoesNotContain("Block", context.AuthoritativeDecisionStatement, StringComparison.Ordinal);
        Assert.DoesNotContain("Review", context.AuthoritativeDecisionStatement, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NoRun_UsesAuthoritativeNone()
    {
        var evidence = new InsightsEvidence
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = "Pets API",
            HasLastRun = false,
            Decision = "Block"
        };

        var context = _builder.Build(InsightsIntent.ExplainDecision, evidence);

        Assert.Null(context.Decision);
        Assert.Equal(InsightsContext.NoDecision, context.AuthoritativeDecision);
        Assert.Equal(InsightsContext.NoRunSource, context.AuthoritativeDecisionSource);
        Assert.Contains("AUTHORITATIVE_DECISION: NONE", context.AuthoritativeDecisionStatement, StringComparison.Ordinal);
        Assert.Contains("SOURCE: No persisted TestShield run", context.AuthoritativeDecisionStatement, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_CapsFindingsAt12()
    {
        var findings = Enumerable.Range(0, 20)
            .Select(index => Finding("Regression", "Medium", $"/p{index}"))
            .ToList();
        findings[5] = Finding("Regression", "High", "/high");

        var context = _builder.Build(InsightsIntent.ExplainDecision, RunEvidence("Block", findings));

        Assert.Equal(InsightsEvidenceLimits.MaxFindings, context.Findings.Count);
        Assert.Equal("/high", context.Findings[0].Path);
        Assert.All(context.Findings.Take(1), finding => Assert.Equal("High", finding.Severity));
    }

    [Fact]
    public void Build_CapsFailedTestsAt15_AndPrefersFailedBeforeError()
    {
        var tests = Enumerable.Range(0, 10)
            .Select(index => FailedTest(nameof(ContractValidationOutcome.Error), $"/e{index}"))
            .Concat(Enumerable.Range(0, 10).Select(index => FailedTest(nameof(ContractValidationOutcome.Failed), $"/f{index}")))
            .ToList();

        var context = _builder.Build(
            InsightsIntent.FailedTests,
            RunEvidence("Review", failedTests: tests));

        Assert.Equal(InsightsEvidenceLimits.MaxFailedTests, context.FailedTests.Count);
        Assert.All(context.FailedTests.Take(10), test => Assert.Equal(nameof(ContractValidationOutcome.Failed), test.Outcome));
        Assert.All(context.FailedTests.Skip(10), test => Assert.Equal(nameof(ContractValidationOutcome.Error), test.Outcome));
    }

    [Fact]
    public void Build_CapsCoverageGapsAt10()
    {
        var gaps = Enumerable.Range(0, 16)
            .Select(index => new InsightsCoverageGapEvidence
            {
                Method = "GET",
                Path = $"/gap{index}",
                Reason = "NO_DETERMINISTIC_TEST"
            })
            .ToList();

        var context = _builder.Build(
            InsightsIntent.ExplainCoverage,
            RunEvidence("Safe", coverage: Coverage(75m, 4, 3, 1), gaps: gaps));

        Assert.Equal(InsightsEvidenceLimits.MaxCoverageGaps, context.CoverageGaps.Count);
    }

    [Fact]
    public void Build_CapsNegativeScenariosAt15()
    {
        var scenarios = Enumerable.Range(0, 21)
            .Select(index => new InsightsNegativeScenarioEvidence
            {
                Kind = nameof(GeneratedApiTestKind.NegativeMissingRequiredBody),
                Method = "POST",
                Path = $"/n{index}",
                ExpectedStatus = 400
            })
            .ToList();

        var context = _builder.Build(
            InsightsIntent.NegativeScenarios,
            RunEvidence("Safe", negatives: scenarios));

        Assert.Equal(InsightsEvidenceLimits.MaxNegativeScenarios, context.NegativeScenarios.Count);
    }

    [Fact]
    public void Build_CapsBaselineChangesAt20()
    {
        var changes = Enumerable.Range(0, 28)
            .Select(index => new InsightsBaselineChangeEvidence
            {
                Change = "removed",
                Method = "GET",
                Path = $"/b{index}",
                Detail = "A baselined test is not present in the latest run."
            })
            .ToList();

        var context = _builder.Build(
            InsightsIntent.BaselineDiff,
            RunEvidence("Review", baselineChanges: changes));

        Assert.Equal(InsightsEvidenceLimits.MaxBaselineChanges, context.BaselineChanges.Count);
    }

    [Fact]
    public void Build_ExplainDecision_IncludesDecisionFindingsAndRisk()
    {
        var evidence = RunEvidence(
            "Block",
            findings: [Finding("Regression", "High", "/pets")],
            risk: new InsightsRiskEvidence
            {
                Level = "High",
                Title = "Release blocked",
                Summary = "A previously passing test has regressed."
            },
            failedCount: 1);

        var context = _builder.Build(InsightsIntent.ExplainDecision, evidence);

        Assert.Equal("Block", context.Decision);
        Assert.NotNull(context.Risk);
        Assert.Equal("High", context.Risk.Level);
        Assert.NotEmpty(context.Findings);
        Assert.Contains(context.SummaryFacts, fact => fact.Contains("failed 1", StringComparison.Ordinal));
        Assert.Empty(context.CoverageGaps);
        Assert.Empty(context.NegativeScenarios);
        Assert.Empty(context.BaselineChanges);
    }

    [Fact]
    public void Build_ExplainCoverage_IncludesCoverageAndGaps()
    {
        var context = _builder.Build(
            InsightsIntent.ExplainCoverage,
            RunEvidence(
                "Safe",
                coverage: Coverage(75m, 4, 3, 1),
                gaps:
                [
                    new InsightsCoverageGapEvidence
                    {
                        Method = "GET",
                        Path = "/health",
                        Reason = "NO_DETERMINISTIC_TEST"
                    }
                ],
                findings: [Finding("Regression", "High", "/pets")]));

        Assert.NotNull(context.Coverage);
        Assert.Equal(75m, context.Coverage.Percent);
        Assert.Equal("/health", Assert.Single(context.CoverageGaps).Path);
        Assert.Empty(context.Findings);
        Assert.Contains(context.SummaryFacts, fact => fact.Contains("Coverage is 75%", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_ContractDrift_IncludesOnlyContractDriftFindings()
    {
        var context = _builder.Build(
            InsightsIntent.ContractDrift,
            RunEvidence(
                "Review",
                findings:
                [
                    Finding("Regression", "High", "/pets"),
                    Finding("ContractDrift", "Medium", "/orders")
                ]));

        var finding = Assert.Single(context.Findings);
        Assert.Equal("ContractDrift", finding.Category);
        Assert.Equal("/orders", finding.Path);
        Assert.Empty(context.FailedTests);
    }

    [Fact]
    public void Build_FailedTests_IncludesFailedAndErrorTests()
    {
        var context = _builder.Build(
            InsightsIntent.FailedTests,
            RunEvidence(
                "Review",
                failedTests:
                [
                    FailedTest(nameof(ContractValidationOutcome.Error), "/timeout"),
                    FailedTest(nameof(ContractValidationOutcome.Failed), "/pets")
                ]));

        Assert.Equal(2, context.FailedTests.Count);
        Assert.Equal("/pets", context.FailedTests[0].Path);
        Assert.Equal(200, context.FailedTests[0].ExpectedStatus);
        Assert.Equal(500, context.FailedTests[0].ActualStatus);
        Assert.Empty(context.Findings);
    }

    [Fact]
    public void Build_NegativeScenarios_IncludesGeneratedNegatives()
    {
        var context = _builder.Build(
            InsightsIntent.NegativeScenarios,
            RunEvidence(
                "Safe",
                negatives:
                [
                    new InsightsNegativeScenarioEvidence
                    {
                        Kind = nameof(GeneratedApiTestKind.NegativeMissingRequiredBody),
                        Method = "POST",
                        Path = "/pets",
                        ExpectedStatus = 400
                    }
                ]));

        var scenario = Assert.Single(context.NegativeScenarios);
        Assert.Equal("POST", scenario.Method);
        Assert.Equal("/pets", scenario.Path);
        Assert.Equal(400, scenario.ExpectedStatus);
        Assert.Empty(context.Findings);
    }

    [Fact]
    public void Build_BaselineDiff_IncludesBaselineChanges()
    {
        var context = _builder.Build(
            InsightsIntent.BaselineDiff,
            RunEvidence(
                "Review",
                baselineChanges:
                [
                    new InsightsBaselineChangeEvidence
                    {
                        Change = "outcome",
                        Method = "GET",
                        Path = "/pets",
                        Detail = "Outcome changed from Passed to Failed."
                    }
                ]));

        var change = Assert.Single(context.BaselineChanges);
        Assert.Equal("outcome", change.Change);
        Assert.Empty(context.Findings);
        Assert.Null(context.Coverage);
    }

    [Fact]
    public void Build_RunOverview_IncludesSummaryFacts()
    {
        var context = _builder.Build(
            InsightsIntent.RunOverview,
            RunEvidence(
                "Block",
                findings: [Finding("Regression", "High", "/pets")],
                coverage: Coverage(100m, 1, 1, 0),
                risk: new InsightsRiskEvidence
                {
                    Level = "High",
                    Title = "Release blocked",
                    Summary = "A previously passing test has regressed."
                }));

        Assert.Equal("Block", context.Decision);
        Assert.NotNull(context.Risk);
        Assert.NotNull(context.Coverage);
        Assert.NotEmpty(context.Findings);
        Assert.NotEmpty(context.SummaryFacts);
        Assert.True(context.SummaryFacts.Count <= InsightsEvidenceLimits.MaxSummaryFacts);
        Assert.Contains(context.SummaryFacts, fact => fact.Contains("Persisted decision is Block", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_AndFormat_OmitUnsafeFields()
    {
        var evidence = RunEvidence(
            "Block",
            findings: [Finding("Regression", "High", "/pets")],
            retrieved: ["lastRun.decision", "https://logic.azure.com/trigger?sig=super-secret"]);
        evidence = new InsightsEvidence
        {
            ProjectId = evidence.ProjectId,
            ProjectName = evidence.ProjectName,
            HasLastRun = true,
            Decision = evidence.Decision,
            LastRunCompletedAt = evidence.LastRunCompletedAt,
            PassedCount = 3,
            FailedCount = 1,
            ErrorCount = 0,
            FindingCount = 1,
            Risk = evidence.Risk,
            Coverage = evidence.Coverage,
            Findings = evidence.Findings,
            RetrievedEvidenceIds = evidence.RetrievedEvidenceIds
        };

        var context = _builder.Build(InsightsIntent.ExplainDecision, evidence);
        var json = JsonSerializer.Serialize(context, JsonOptions);
        var formatted = _formatter.Format(context);

        AssertSafe(json);
        AssertSafe(formatted);
        Assert.DoesNotContain(evidence.ProjectId.ToString(), formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://logic.azure.com/trigger?sig=super-secret", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("RetrievedEvidenceIds", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("HasLastRun", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_DoesNotSerializeArbitraryEvidenceJson()
    {
        var evidence = RunEvidence("Block", findings: [Finding("Regression", "High", "/pets")]);
        var context = _builder.Build(InsightsIntent.ExplainDecision, evidence);
        var formatted = _formatter.Format(context);
        var evidenceJson = JsonSerializer.Serialize(evidence, JsonOptions);

        Assert.StartsWith("TESTSHIELD_INSIGHTS_CONTEXT", formatted, StringComparison.Ordinal);
        Assert.Contains("END_CONTEXT", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("retrievedEvidenceIds", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain(evidenceJson, formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("\"findings\":", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IsDeterministic()
    {
        var evidence = RunEvidence(
            "Block",
            findings: [Finding("Regression", "High", "/pets"), Finding("ContractDrift", "Medium", "/orders")],
            coverage: Coverage(100m, 2, 2, 0));
        var context = _builder.Build(InsightsIntent.RunOverview, evidence);

        Assert.Equal(_formatter.Format(context), _formatter.Format(context));
    }

    private static void AssertSafe(string text)
    {
        Assert.DoesNotContain("baseUrl", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://pets.example.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API_KEY", text, StringComparison.Ordinal);
        Assert.DoesNotContain("requestBody", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("responseBody", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"headers\"", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openapi", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("swagger", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("webhook", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sqlite", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("App_Data", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("POWER_AUTOMATE", text, StringComparison.OrdinalIgnoreCase);
    }

    private static InsightsEvidence RunEvidence(
        string decision,
        IReadOnlyList<InsightsFindingEvidence>? findings = null,
        IReadOnlyList<InsightsFailedTestEvidence>? failedTests = null,
        IReadOnlyList<InsightsCoverageGapEvidence>? gaps = null,
        IReadOnlyList<InsightsNegativeScenarioEvidence>? negatives = null,
        IReadOnlyList<InsightsBaselineChangeEvidence>? baselineChanges = null,
        InsightsCoverageEvidence? coverage = null,
        InsightsRiskEvidence? risk = null,
        int failedCount = 0,
        IReadOnlyList<string>? retrieved = null) =>
        new()
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = "Pets API",
            HasLastRun = true,
            Decision = decision,
            LastRunCompletedAt = new DateTimeOffset(2026, 9, 18, 8, 0, 0, TimeSpan.Zero),
            PassedCount = 3,
            FailedCount = failedCount,
            ErrorCount = 0,
            FindingCount = findings?.Count ?? 0,
            Risk = risk,
            Coverage = coverage,
            CoverageGaps = gaps ?? [],
            Findings = findings ?? [],
            FailedTests = failedTests ?? [],
            NegativeScenarios = negatives ?? [],
            BaselineChanges = baselineChanges ?? [],
            RetrievedEvidenceIds = retrieved ?? ["lastRun.decision"]
        };

    private static InsightsCoverageEvidence Coverage(decimal percent, int total, int covered, int uncovered) =>
        new()
        {
            Percent = percent,
            TotalOperations = total,
            CoveredOperations = covered,
            UncoveredOperations = uncovered
        };

    private static InsightsFindingEvidence Finding(string category, string severity, string path) =>
        new()
        {
            Category = category,
            Severity = severity,
            Code = "TEST",
            Method = "GET",
            Path = path,
            Message = "status changed"
        };

    private static InsightsFailedTestEvidence FailedTest(string outcome, string path) =>
        new()
        {
            Kind = nameof(GeneratedApiTestKind.HappyPath),
            Method = "GET",
            Path = path,
            Outcome = outcome,
            ExpectedStatus = 200,
            ActualStatus = 500
        };
}
