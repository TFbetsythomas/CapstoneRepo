using System.Globalization;

namespace TestShieldAI.Api.Insights;

public sealed class DeterministicInsightsAnswerer : IDeterministicInsightsAnswerer
{
    public const string NoRunAnswer = "No TestShield test run has been recorded for this project yet.";

    public string Answer(InsightsIntent intent, InsightsEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!evidence.HasLastRun)
        {
            return NoRunAnswer;
        }

        return intent switch
        {
            InsightsIntent.ExplainDecision => ExplainDecision(evidence),
            InsightsIntent.ExplainCoverage => ExplainCoverage(evidence),
            InsightsIntent.ContractDrift => ExplainContractDrift(evidence),
            InsightsIntent.FailedTests => ExplainFailedTests(evidence),
            InsightsIntent.NegativeScenarios => ExplainNegativeScenarios(evidence),
            InsightsIntent.BaselineDiff => ExplainBaselineDiff(evidence),
            _ => ExplainOverview(evidence)
        };
    }

    private static string ExplainDecision(InsightsEvidence evidence)
    {
        var decision = evidence.Decision ?? "Unknown";
        var count = evidence.FindingCount;
        var finding = evidence.Findings.FirstOrDefault();
        if (finding is null)
        {
            return $"This run is {decision} because the latest run contains {count} finding(s).";
        }

        return
            $"This run is {decision} because the latest run contains {count} finding(s). " +
            $"The highest-severity finding is {finding.Severity} on {finding.Method} {finding.Path}: {finding.Message}";
    }

    private static string ExplainCoverage(InsightsEvidence evidence)
    {
        if (evidence.Coverage is null)
        {
            return "Coverage evidence is not available for this project.";
        }

        var coverage = evidence.Coverage;
        return
            $"Coverage is {FormatPercent(coverage.Percent)}% because {coverage.CoveredOperations} of " +
            $"{coverage.TotalOperations} operations are covered.";
    }

    private static string ExplainContractDrift(InsightsEvidence evidence)
    {
        var findings = evidence.Findings;
        var operations = findings
            .Select(finding => $"{finding.Method} {finding.Path}")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var suffix = operations.Count == 0
            ? ""
            : $" The affected operations are: {string.Join(", ", operations)}.";
        return $"There are {findings.Count} contract-drift finding(s).{suffix}";
    }

    private static string ExplainFailedTests(InsightsEvidence evidence) =>
        $"The latest run has {evidence.FailedCount} failed test(s) and {evidence.ErrorCount} execution error(s).";

    private static string ExplainNegativeScenarios(InsightsEvidence evidence)
    {
        var scenarios = evidence.NegativeScenarios;
        var suffix = scenarios.Count == 0
            ? ""
            : $": {string.Join(", ", scenarios.Select(item => $"{item.Kind} {item.Method} {item.Path}"))}";
        return $"The generated suite contains {scenarios.Count} negative scenario(s){suffix}.";
    }

    private static string ExplainBaselineDiff(InsightsEvidence evidence)
    {
        if (evidence.RetrievedEvidenceIds.Contains("baseline.none", StringComparer.Ordinal))
        {
            return "No baseline has been established for this project yet.";
        }

        return
            $"The latest run differs from the previous baseline in {evidence.BaselineChanges.Count} relevant item(s).";
    }

    private static string ExplainOverview(InsightsEvidence evidence)
    {
        var decision = evidence.Decision ?? "Unknown";
        var overview =
            $"The latest run is Decision {decision} with {evidence.PassedCount} passed, " +
            $"{evidence.FailedCount} failed, and {evidence.ErrorCount} error(s).";
        if (evidence.Coverage is null)
        {
            return overview;
        }

        return $"{overview} Coverage is {FormatPercent(evidence.Coverage.Percent)}%.";
    }

    private static string FormatPercent(decimal percent) =>
        percent == decimal.Truncate(percent)
            ? decimal.Truncate(percent).ToString(CultureInfo.InvariantCulture)
            : percent.ToString("0.##", CultureInfo.InvariantCulture);
}
