using System.Text.Json;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Notifications;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Api.TestsRun;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class NotificationEventComposerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Compose_IncludesDecisionRiskCoverageAndCounts()
    {
        var project = Project();
        var execution = Execution(
            decision: RegressionDecision.Block,
            findings: [Finding(RegressionFindingSeverity.High, "GET /pets broke")],
            outcomes:
            [
                ContractValidationOutcome.Passed,
                ContractValidationOutcome.Failed,
                ContractValidationOutcome.Error
            ]);

        var notification = new NotificationEventComposer().Compose(
            project,
            execution,
            new NotificationOptions { Enabled = true, MaxFindings = 5 },
            new DateTimeOffset(2026, 9, 17, 8, 30, 0, TimeSpan.Zero));

        Assert.Equal(NotificationEvent.CurrentSchemaVersion, notification.SchemaVersion);
        Assert.Equal(NotificationEvent.RunCompletedEventType, notification.EventType);
        Assert.Equal(project.Id, notification.ProjectId);
        Assert.Equal("Pets API", notification.ProjectName);
        Assert.Equal(nameof(RegressionDecision.Block), notification.Decision);
        Assert.Equal(nameof(RiskSummaryLevel.High), notification.Risk.Level);
        Assert.Equal("High risk", notification.Risk.Title);
        Assert.Equal("Something broke.", notification.Risk.Summary);
        Assert.Equal(["Fix the contract."], notification.Risk.Recommendations);
        Assert.Equal(50m, notification.Coverage.Percent);
        Assert.Equal(2, notification.Coverage.TotalOperations);
        Assert.Equal(1, notification.Coverage.CoveredOperations);
        Assert.Equal(1, notification.Coverage.UncoveredOperations);
        Assert.Equal(1, notification.Results.Passed);
        Assert.Equal(1, notification.Results.Failed);
        Assert.Equal(1, notification.Results.Error);
        Assert.Equal(1, notification.Findings.Count);
        Assert.Equal("GET /pets broke", Assert.Single(notification.Findings.Items).Message);
        Assert.True(notification.Ai.Enabled);
        Assert.Null(notification.DashboardUrl);
    }

    [Fact]
    public void Compose_CapsFindingsAndPrefersHigherSeverity()
    {
        var findings = new List<RegressionFinding>
        {
            Finding(RegressionFindingSeverity.Medium, "medium-1", "/a"),
            Finding(RegressionFindingSeverity.Medium, "medium-2", "/b"),
            Finding(RegressionFindingSeverity.High, "high-1", "/c"),
            Finding(RegressionFindingSeverity.Medium, "medium-3", "/d"),
            Finding(RegressionFindingSeverity.High, "high-2", "/e"),
            Finding(RegressionFindingSeverity.Medium, "medium-4", "/f")
        };

        var notification = new NotificationEventComposer().Compose(
            Project(),
            Execution(RegressionDecision.Review, findings),
            new NotificationOptions { MaxFindings = 3 },
            DateTimeOffset.UtcNow);

        Assert.Equal(6, notification.Findings.Count);
        Assert.Equal(3, notification.Findings.Items.Count);
        Assert.Equal(["high-1", "high-2", "medium-1"], notification.Findings.Items.Select(item => item.Message).ToList());
        Assert.All(
            notification.Findings.Items.Take(2),
            item => Assert.Equal(nameof(RegressionFindingSeverity.High), item.Severity));
    }

    [Fact]
    public void Compose_CapsAiWarnings()
    {
        var warnings = Enumerable.Range(1, 9).Select(index => $"warning-{index}").ToList();
        var execution = Execution(
            RegressionDecision.Review,
            [],
            ai: new AiRunSummaryDto
            {
                Enabled = true,
                GeneratedCount = 9,
                AcceptedCount = 0,
                WarningCount = warnings.Count,
                Warnings = warnings
            });

        var notification = new NotificationEventComposer().Compose(
            Project(),
            execution,
            new NotificationOptions(),
            DateTimeOffset.UtcNow);

        Assert.Equal(9, notification.Ai.WarningCount);
        Assert.Equal(NotificationEventComposer.MaxAiWarnings, notification.Ai.Warnings.Count);
        Assert.Equal(["warning-1", "warning-2", "warning-3", "warning-4", "warning-5"], notification.Ai.Warnings);
    }

    [Fact]
    public void Compose_DashboardUrl_OnlyWhenConfigured()
    {
        var project = Project();
        var composer = new NotificationEventComposer();
        var execution = Execution(RegressionDecision.Safe, []);

        var withoutUrl = composer.Compose(project, execution, new NotificationOptions(), DateTimeOffset.UtcNow);
        var withBlank = composer.Compose(
            project,
            execution,
            new NotificationOptions { DashboardBaseUrl = "  " },
            DateTimeOffset.UtcNow);
        var withUrl = composer.Compose(
            project,
            execution,
            new NotificationOptions { DashboardBaseUrl = "https://dashboard.example.test/" },
            DateTimeOffset.UtcNow);

        Assert.Null(withoutUrl.DashboardUrl);
        Assert.Null(withBlank.DashboardUrl);
        Assert.Equal($"https://dashboard.example.test/projects/{project.Id}", withUrl.DashboardUrl);
    }

    [Fact]
    public void Compose_OmitsSensitiveFields()
    {
        var project = Project();
        var execution = Execution(
            RegressionDecision.Block,
            [Finding(RegressionFindingSeverity.High, "status changed")],
            outcomes: [ContractValidationOutcome.Failed],
            headers: new Dictionary<string, string> { ["Authorization"] = "Bearer secret-token" },
            body: """{"password":"hunter2"}""");

        var json = JsonSerializer.Serialize(
            new NotificationEventComposer().Compose(
                project,
                execution,
                new NotificationOptions { DashboardBaseUrl = "https://dashboard.example.test" },
                DateTimeOffset.UtcNow),
            JsonOptions);

        Assert.DoesNotContain(project.BaseUrl, json, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer secret-token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", json, StringComparison.Ordinal);
        Assert.DoesNotContain("openapi", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("power-automate", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sig=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("baseUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"decision\":\"Block\"", json, StringComparison.Ordinal);
        Assert.Contains("\"coverage\"", json, StringComparison.Ordinal);
    }

    private static ProjectRecord Project() =>
        new()
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Name = "Pets API",
            BaseUrl = "https://pets.example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static TestsRunExecution Execution(
        RegressionDecision decision,
        IReadOnlyList<RegressionFinding> findings,
        IReadOnlyList<ContractValidationOutcome>? outcomes = null,
        AiRunSummaryDto? ai = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null)
    {
        outcomes ??= [ContractValidationOutcome.Passed];
        var results = outcomes.Select(outcome => Case(outcome, headers, body)).ToList();
        return new TestsRunExecution(
            results,
            new RegressionDetectionResult(findings, decision),
            [],
            promoteBaseline: false,
            [],
            ai ?? new AiRunSummaryDto
            {
                Enabled = true,
                GeneratedCount = 0,
                AcceptedCount = 0,
                WarningCount = 0,
                Warnings = []
            },
            new CoverageCalculationResult(
                2,
                1,
                1,
                50m,
                0,
                new CoverageScenarioCounts(1, 0, 0, 0, 0),
                []),
            new RiskSummaryResult(
                RiskSummaryLevel.High,
                "High risk",
                "Something broke.",
                ["Contract drifted."],
                ["Fix the contract."]));
    }

    private static TestsRunCaseResult Case(
        ContractValidationOutcome outcome,
        IReadOnlyDictionary<string, string>? headers,
        string? body)
    {
        var test = new GeneratedApiTestCase(
            GeneratedApiTestKind.HappyPath,
            "GET",
            "/pets",
            "/pets",
            [],
            requestBody: """{"secret":"do-not-notify"}""",
            expectedStatus: 200,
            expectedResponseSchema: null,
            "GET",
            "/pets");
        var execution = new ApiTestExecutionResult(
            test.Kind,
            test.Method,
            test.Path,
            test.SourceMethod,
            test.SourcePath,
            test.ExpectedStatus,
            200,
            headers ?? new Dictionary<string, string>(),
            body,
            durationMs: 1,
            statusMatched: outcome is ContractValidationOutcome.Passed,
            error: null);
        var schemaValid = outcome is ContractValidationOutcome.Passed
            ? true
            : outcome is ContractValidationOutcome.Failed ? false : (bool?)null;
        return new TestsRunCaseResult(
            test,
            execution,
            new ContractValidationResult(outcome, statusValid: true, schemaValid, []));
    }

    private static RegressionFinding Finding(
        RegressionFindingSeverity severity,
        string message,
        string path = "/pets") =>
        new(
            RegressionFindingCategory.Regression,
            "TEST",
            severity,
            GeneratedApiTestKind.HappyPath,
            "GET",
            path,
            "expected",
            "current",
            jsonPath: null,
            message);
}
