using TestShieldAI.Api.Persistence;
using TestShieldAI.Api.TestsRun;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Notifications;

public sealed class NotificationEventComposer
{
    public const int MaxAiWarnings = 5;

    public NotificationEvent Compose(
        ProjectRecord project,
        TestsRunExecution execution,
        NotificationOptions options,
        DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(execution);
        options ??= new NotificationOptions();

        var outcomes = execution.Results.Select(item => item.Validation.Outcome).ToList();
        var maxFindings = options.MaxFindings < 0 ? 0 : options.MaxFindings;
        var findings = execution.Detection.Findings ?? [];

        return new NotificationEvent
        {
            SchemaVersion = NotificationEvent.CurrentSchemaVersion,
            EventType = NotificationEvent.RunCompletedEventType,
            CompletedAt = completedAt,
            ProjectId = project.Id,
            ProjectName = project.Name,
            Decision = execution.Detection.Decision.ToString(),
            Risk = new NotificationRisk
            {
                Level = execution.Risk.Level.ToString(),
                Title = execution.Risk.Title,
                Summary = execution.Risk.Summary,
                Recommendations = execution.Risk.Recommendations
            },
            Coverage = new NotificationCoverage
            {
                Percent = execution.Coverage.CoveragePercent,
                TotalOperations = execution.Coverage.TotalOperations,
                CoveredOperations = execution.Coverage.CoveredOperations,
                UncoveredOperations = execution.Coverage.UncoveredOperations
            },
            Results = new NotificationResults
            {
                Passed = outcomes.Count(outcome => outcome is ContractValidationOutcome.Passed),
                Failed = outcomes.Count(outcome => outcome is ContractValidationOutcome.Failed),
                Error = outcomes.Count(outcome => outcome is ContractValidationOutcome.Error)
            },
            Findings = new NotificationFindings
            {
                Count = findings.Count,
                Items = findings
                    .OrderBy(finding => finding.Severity)
                    .ThenBy(finding => finding.Category)
                    .Take(maxFindings)
                    .Select(ToFinding)
                    .ToList()
            },
            Ai = new NotificationAiSummary
            {
                Enabled = execution.Ai.Enabled,
                WarningCount = execution.Ai.WarningCount,
                Warnings = execution.Ai.Warnings.Take(MaxAiWarnings).ToList()
            },
            DashboardUrl = DashboardUrl(options.DashboardBaseUrl, project.Id)
        };
    }

    private static NotificationFinding ToFinding(RegressionFinding finding) =>
        new()
        {
            Category = finding.Category.ToString(),
            Severity = finding.Severity.ToString(),
            Method = finding.Method,
            Path = finding.Path,
            Message = finding.Message
        };

    private static string? DashboardUrl(string? dashboardBaseUrl, Guid projectId)
    {
        if (string.IsNullOrWhiteSpace(dashboardBaseUrl))
        {
            return null;
        }

        return $"{dashboardBaseUrl.Trim().TrimEnd('/')}/projects/{projectId}";
    }
}
