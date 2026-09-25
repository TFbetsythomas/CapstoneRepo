using System.Text.Json.Serialization;

namespace TestShieldAI.Api.Notifications;

public sealed class NotificationEvent
{
    public const string CurrentSchemaVersion = "1.0";

    public const string RunCompletedEventType = "testshield.run.completed";

    public required string SchemaVersion { get; init; }

    public required string EventType { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public required Guid ProjectId { get; init; }

    public required string ProjectName { get; init; }

    public required string Decision { get; init; }

    public required NotificationRisk Risk { get; init; }

    public required NotificationCoverage Coverage { get; init; }

    public required NotificationResults Results { get; init; }

    public required NotificationFindings Findings { get; init; }

    public required NotificationAiSummary Ai { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DashboardUrl { get; init; }
}

public sealed class NotificationRisk
{
    public required string Level { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required IReadOnlyList<string> Recommendations { get; init; }
}

public sealed class NotificationCoverage
{
    public required decimal Percent { get; init; }

    public required int TotalOperations { get; init; }

    public required int CoveredOperations { get; init; }

    public required int UncoveredOperations { get; init; }
}

public sealed class NotificationResults
{
    public required int Passed { get; init; }

    public required int Failed { get; init; }

    public required int Error { get; init; }
}

public sealed class NotificationFinding
{
    public required string Category { get; init; }

    public required string Severity { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string Message { get; init; }
}

public sealed class NotificationFindings
{
    public required int Count { get; init; }

    public required IReadOnlyList<NotificationFinding> Items { get; init; }
}

public sealed class NotificationAiSummary
{
    public required bool Enabled { get; init; }

    public required int WarningCount { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
