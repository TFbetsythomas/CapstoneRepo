using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Ai;

public sealed class OpenAiCompletionClient : IAiCompletionClient
{
    public const string ProviderName = "OpenAI";

    private readonly AiOptions _options;
    private readonly IAiApiKeyAccessor _apiKeys;
    private readonly ILogger<OpenAiCompletionClient> _logger;
    private readonly IAiChatCompletionTransport? _transport;

    public OpenAiCompletionClient(
        AiOptions options,
        IAiApiKeyAccessor apiKeys,
        ILogger<OpenAiCompletionClient>? logger = null,
        IAiChatCompletionTransport? transport = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _apiKeys = apiKeys ?? throw new ArgumentNullException(nameof(apiKeys));
        _logger = logger ?? NullLogger<OpenAiCompletionClient>.Instance;
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
        var key = _apiKeys.GetApiKey(AiProvider.OpenAI);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new AiCompletionException("OpenAI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.ModelOrDeployment))
        {
            throw new AiCompletionException("OpenAI model is not configured.");
        }

        var timeout = TimeSpan.FromSeconds(ProviderAiCompletion.NormalizeTimeoutSeconds(_options.TimeoutSeconds));
        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = timeout,
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
        };

        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            if (!Uri.TryCreate(_options.Endpoint.Trim(), UriKind.Absolute, out var endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
            {
                throw new AiCompletionException("OpenAI endpoint is missing or invalid.");
            }

            clientOptions.Endpoint = endpoint;
        }

        ChatClient chatClient = new OpenAIClient(new ApiKeyCredential(key), clientOptions)
            .GetChatClient(_options.ModelOrDeployment.Trim());

        return new SdkChatCompletionTransport(chatClient);
    }
}
