using System.Globalization;
using System.Text;

namespace TestShieldAI.Api.Insights;

public sealed class InsightsContextFormatter
{
    public string Format(InsightsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new StringBuilder();
        builder.AppendLine("TESTSHIELD_INSIGHTS_CONTEXT");
        builder.AppendLine();
        builder.AppendLine("INTENT:");
        builder.AppendLine(context.Intent.ToString());
        builder.AppendLine();
        builder.AppendLine("PROJECT:");
        builder.AppendLine(context.ProjectName);
        builder.AppendLine();
        builder.AppendLine("AUTHORITATIVE_DECISION:");
        builder.AppendLine(context.AuthoritativeDecision);
        builder.AppendLine();
        builder.AppendLine("SOURCE:");
        builder.AppendLine(context.AuthoritativeDecisionSource);

        if (context.LastRunCompletedAt is { } completedAt)
        {
            builder.AppendLine();
            builder.AppendLine("COMPLETED_AT:");
            builder.AppendLine(completedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        if (context.SummaryFacts.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("SUMMARY:");
            foreach (var fact in context.SummaryFacts)
            {
                builder.Append("- ");
                builder.AppendLine(fact);
            }
        }

        if (context.Risk is not null)
        {
            builder.AppendLine();
            builder.AppendLine("RISK:");
            builder.AppendLine($"{context.Risk.Level}: {context.Risk.Title}");
            builder.AppendLine(context.Risk.Summary);
        }

        if (context.PassedCount is not null || context.FailedCount is not null || context.ErrorCount is not null)
        {
            builder.AppendLine();
            builder.AppendLine("RUN:");
            builder.AppendLine($"Passed: {context.PassedCount?.ToString(CultureInfo.InvariantCulture) ?? "0"}");
            builder.AppendLine($"Failed: {context.FailedCount?.ToString(CultureInfo.InvariantCulture) ?? "0"}");
            builder.AppendLine($"Errors: {context.ErrorCount?.ToString(CultureInfo.InvariantCulture) ?? "0"}");
        }
        if (context.FailedTests.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("FAILED_TESTS:");
            foreach (var test in context.FailedTests)
            {
                builder.Append("- ");
                builder.Append(test.Outcome);
                builder.Append(' ');
                builder.Append(test.Kind);
                builder.Append(' ');
                builder.Append(test.Method);
                builder.Append(' ');
                builder.Append(test.Path);
                builder.Append(" expected ");
                builder.Append(test.ExpectedStatus.ToString(CultureInfo.InvariantCulture));
                if (test.ActualStatus is { } actual)
                {
                    builder.Append(" actual ");
                    builder.Append(actual.ToString(CultureInfo.InvariantCulture));
                }

                builder.AppendLine();
            }
        }

        if (context.Findings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("FINDINGS:");
            foreach (var finding in context.Findings)
            {
                builder.Append("- [");
                builder.Append(finding.Severity);
                builder.Append("] ");
                builder.Append(finding.Category);
                builder.Append(' ');
                builder.Append(finding.Method);
                builder.Append(' ');
                builder.Append(finding.Path);
                builder.Append(": ");
                builder.AppendLine(finding.Message);
            }
        }

        if (context.Coverage is not null)
        {
            builder.AppendLine();
            builder.AppendLine("COVERAGE:");
            builder.AppendLine($"{FormatPercent(context.Coverage.Percent)}%");
            builder.AppendLine(
                $"{context.Coverage.CoveredOperations.ToString(CultureInfo.InvariantCulture)} of {context.Coverage.TotalOperations.ToString(CultureInfo.InvariantCulture)} operations covered");
        }

        if (context.CoverageGaps.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("COVERAGE_GAPS:");
            foreach (var gap in context.CoverageGaps)
            {
                builder.Append("- ");
                builder.Append(gap.Method);
                builder.Append(' ');
                builder.Append(gap.Path);
                builder.Append(": ");
                builder.AppendLine(gap.Reason);
            }
        }

        if (context.NegativeScenarios.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("NEGATIVE_SCENARIOS:");
            foreach (var scenario in context.NegativeScenarios)
            {
                builder.Append("- ");
                builder.Append(scenario.Kind);
                builder.Append(' ');
                builder.Append(scenario.Method);
                builder.Append(' ');
                builder.Append(scenario.Path);
                builder.Append(" expected ");
                builder.AppendLine(scenario.ExpectedStatus.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (context.BaselineChanges.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("BASELINE_CHANGES:");
            foreach (var change in context.BaselineChanges)
            {
                builder.Append("- ");
                builder.Append(change.Change);
                builder.Append(' ');
                builder.Append(change.Method);
                builder.Append(' ');
                builder.Append(change.Path);
                builder.Append(": ");
                builder.AppendLine(change.Detail);
            }
        }

        builder.AppendLine();
        builder.Append("END_CONTEXT");
        return builder.ToString();
    }

    private static string FormatPercent(decimal percent) =>
        percent == decimal.Truncate(percent)
            ? decimal.Truncate(percent).ToString(CultureInfo.InvariantCulture)
            : percent.ToString("0.##", CultureInfo.InvariantCulture);
}
