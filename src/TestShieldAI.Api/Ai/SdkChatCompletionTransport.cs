using OpenAI.Chat;

namespace TestShieldAI.Api.Ai;

internal sealed class SdkChatCompletionTransport : IAiChatCompletionTransport
{
    private const string JsonSystemMessage =
        "Return JSON only. Do not wrap the response in markdown. Do not include commentary.";

    private readonly ChatClient _chatClient;

    public SdkChatCompletionTransport(ChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    }

    public async Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken)
    {
        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(JsonSystemMessage),
                new UserChatMessage(prompt)
            ],
            new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            },
            cancellationToken).ConfigureAwait(false);

        return ReadText(completion);
    }

    public async Task<string> CompleteTextAsync(string prompt, CancellationToken cancellationToken)
    {
        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            [new UserChatMessage(prompt)],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ReadText(completion);
    }

    private static string ReadText(ChatCompletion completion)
    {
        if (completion.Content is null || completion.Content.Count == 0)
        {
            throw new AiCompletionException("The AI provider returned an empty completion.");
        }

        return completion.Content[0].Text ?? "";
    }
}
