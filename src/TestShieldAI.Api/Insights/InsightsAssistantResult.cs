namespace TestShieldAI.Api.Insights;

public sealed class InsightsAssistantResult
{
    public const string UnavailableWarning = "AI insights assistant is not available.";

    public required bool Succeeded { get; init; }

    public string? Answer { get; init; }

    public string? Warning { get; init; }

    public static InsightsAssistantResult Success(string answer) =>
        new()
        {
            Succeeded = true,
            Answer = answer,
            Warning = null
        };

    public static InsightsAssistantResult Failure(string? warning = null) =>
        new()
        {
            Succeeded = false,
            Answer = null,
            Warning = string.IsNullOrWhiteSpace(warning) ? UnavailableWarning : warning
        };
}
