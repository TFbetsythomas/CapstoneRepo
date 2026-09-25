namespace TestShieldAI.Api.Contracts;

public sealed class AskInsightsRequest
{
    public string? Question { get; init; }
}

public sealed class AskInsightsResponse
{
    public const string DeterministicSource = "deterministic";

    public const string LlmSource = "llm";

    public required string Question { get; init; }

    public required string Intent { get; init; }

    public required string Answer { get; init; }

    public required bool AssistantAvailable { get; init; }

    public required string AnswerSource { get; init; }

    public string? Warning { get; init; }

    public required InsightsEvidenceDto Evidence { get; init; }
}

public sealed class InsightsEvidenceDto
{
    public string? Decision { get; init; }

    public DateTimeOffset? LastRunCompletedAt { get; init; }

    public required IReadOnlyList<string> Retrieved { get; init; }
}
