namespace TestShieldAI.Engine;

public interface IAiCompletionClient
{
    Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken = default);

    Task<string> CompleteTextAsync(string prompt, CancellationToken cancellationToken = default) =>
        CompleteJsonAsync(prompt, cancellationToken);
}

