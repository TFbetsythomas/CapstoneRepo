using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Ai;

public sealed class AzureOpenAiCompletionClient : IAiCompletionClient
{
    public const string ProviderName = "AzureOpenAI";

    private readonly AiOptions _options;
    private readonly IAiApiKeyAccessor _apiKeys;
    private readonly ILogger<AzureOpenAiCompletionClient> _logger;
    private readonly IAiChatCompletionTransport? _transport;

    public AzureOpenAiCompletionClient(
        AiOptions options,
        IAiApiKeyAccessor apiKeys,
        ILogger<AzureOpenAiCompletionClient>? logger = null,
        IAiChatCompletionTransport? transport = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _apiKeys = apiKeys ?? throw new ArgumentNullException(nameof(apiKeys));
        _logger = logger ?? NullLogger<AzureOpenAiCompletionClient>.Instance;
        _transport = transport;
    }

    public Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var transport = _transport ?? CreateSdkTransport();
        return ProviderAiCompletion.CompleteJsonAsync(
            ProviderName,
            _options.ModelOrDeployment,
            _options.TimeoutSeconds,
            prompt,
            transport,
            _logger,
            cancellationToken);
    }

    public Task<string> CompleteTextAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var transport = _transport ?? CreateSdkTransport();
        return ProviderAiCompletion.CompleteTextAsync(
            ProviderName,
            _options.ModelOrDeployment,
            _options.TimeoutSeconds,
            prompt,
            transport,
            _logger,
            cancellationToken);
    }

    private IAiChatCompletionTransport CreateSdkTransport()
    {
        var key = _apiKeys.GetApiKey(AiProvider.AzureOpenAI);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new AiCompletionException("Azure OpenAI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            !Uri.TryCreate(_options.Endpoint.Trim(), UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
        {
            throw new AiCompletionException("Azure OpenAI endpoint is missing or invalid.");
        }

        if (string.IsNullOrWhiteSpace(_options.ModelOrDeployment))
        {
            throw new AiCompletionException("Azure OpenAI deployment is not configured.");
        }

        var timeout = TimeSpan.FromSeconds(ProviderAiCompletion.NormalizeTimeoutSeconds(_options.TimeoutSeconds));
        var clientOptions = new AzureOpenAIClientOptions
        {
            NetworkTimeout = timeout,
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
        };

        ChatClient chatClient = new AzureOpenAIClient(
            endpoint,
            new ApiKeyCredential(key),
            clientOptions).GetChatClient(_options.ModelOrDeployment.Trim());

        return new SdkChatCompletionTransport(chatClient);
    }
}
