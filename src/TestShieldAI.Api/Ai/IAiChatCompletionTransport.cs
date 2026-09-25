namespace TestShieldAI.Api.Ai;

public interface IAiChatCompletionTransport
{
    Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken);

    Task<string> CompleteTextAsync(string prompt, CancellationToken cancellationToken) =>
        CompleteJsonAsync(prompt, cancellationToken);
}

