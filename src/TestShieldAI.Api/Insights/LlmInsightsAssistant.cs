using Microsoft.Extensions.Options;
using TestShieldAI.Api.Ai;
using TestShieldAI.Engine;

namespace TestShieldAI.Api.Insights;

public sealed class LlmInsightsAssistant : IInsightsAssistant
{
    private readonly IAiCompletionClient _completion;
    private readonly AiOptions _options;
    private readonly InsightsContextFormatter _formatter;

    public LlmInsightsAssistant(
        IAiCompletionClient completion,
        IOptions<AiOptions> options,
        InsightsContextFormatter formatter)
    {
        _completion = completion ?? throw new ArgumentNullException(nameof(completion));
        _options = options?.Value ?? new AiOptions();
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    public async Task<InsightsAssistantResult> AnswerAsync(
        string question,
        InsightsIntent intent,
        InsightsContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (AiCompletionClientFactory.UseNoOp(_options))
        {
            return InsightsAssistantResult.Failure();
        }

        try
        {
            var prompt = InsightsAssistantPrompt.Build(
                question,
                intent,
                _formatter.Format(context));
            var completion = await _completion.CompleteTextAsync(prompt, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(completion))
            {
                return InsightsAssistantResult.Failure();
            }

            return InsightsAssistantResult.Success(completion.Trim());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return InsightsAssistantResult.Failure();
        }
    }
}
