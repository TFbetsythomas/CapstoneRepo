using TestShieldAI.Engine;

namespace TestShieldAI.Api.Ai;

public sealed class NoOpAiCompletionClient : IAiCompletionClient
{
    /// <summary>
    /// Empty JSON array. Slice C does not call a model; callers must treat this as no completion.
    /// </summary>
    public const string EmptyJsonResponse = "[]";

    public Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken = default)
    {
        _ = prompt;
        _ = cancellationToken;
        return Task.FromResult(EmptyJsonResponse);
    }

    public Task<string> CompleteTextAsync(string prompt, CancellationToken cancellationToken = default)
    {
        _ = prompt;
        _ = cancellationToken;
        return Task.FromResult(string.Empty);
    }
}
