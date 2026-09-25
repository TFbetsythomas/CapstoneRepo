using Microsoft.Extensions.Options;
using TestShieldAI.Api.Ai;
using TestShieldAI.Api.Insights;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class LlmInsightsAssistantTests
{
    [Fact]
    public async Task AnswerAsync_AiDisabled_DoesNotCallCompletion()
    {
        var completion = new RecordingInsightsCompletionClient();
        var assistant = Create(completion, enabled: false);

        var result = await assistant.AnswerAsync(
            "Why was this run blocked?",
            InsightsIntent.ExplainDecision,
            DecisionContext("Block"));

        Assert.False(result.Succeeded);
        Assert.Equal(InsightsAssistantResult.UnavailableWarning, result.Warning);
        Assert.Equal(0, completion.TextCalls);
    }

    [Fact]
    public async Task AnswerAsync_ProviderNone_DoesNotCallCompletion()
    {
        var completion = new RecordingInsightsCompletionClient();
        var assistant = Create(completion, enabled: true, AiProvider.None);

        var result = await assistant.AnswerAsync(
            "Why was this run blocked?",
            InsightsIntent.ExplainDecision,
            DecisionContext("Block"));

        Assert.False(result.Succeeded);
        Assert.Equal(0, completion.TextCalls);
    }

    [Fact]
    public async Task AnswerAsync_Success_ReturnsTrimmedText()
    {
        var completion = new RecordingInsightsCompletionClient
        {
            TextCompletion = "  This run is blocked by a high-severity finding.  "
        };
        var assistant = Create(completion, enabled: true);

        var result = await assistant.AnswerAsync(
            "Why was this run blocked?",
            InsightsIntent.ExplainDecision,
            DecisionContext("Block"));

        Assert.True(result.Succeeded);
        Assert.Equal("This run is blocked by a high-severity finding.", result.Answer);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task AnswerAsync_PromptContainsQuestionIntentAndContext()
    {
        var completion = new RecordingInsightsCompletionClient();
        var assistant = Create(completion, enabled: true);
        var context = DecisionContext("Block");

        await assistant.AnswerAsync(
            "Why was this run blocked?",
            InsightsIntent.ExplainDecision,
            context);

        Assert.Equal(1, completion.TextCalls);
        Assert.Contains("Why was this run blocked?", completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("ExplainDecision", completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("AUTHORITATIVE_DECISION:", completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("Block", completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("TESTSHIELD_INSIGHTS_CONTEXT", completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("SYSTEM INSTRUCTIONS:", completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("USER_QUESTION:", completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("INTENT:", completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("TESTSHIELD_EVIDENCE:", completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("https://pets.example.com", completion.LastTextPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization:", completion.LastTextPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Authorization\"", completion.LastTextPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", completion.LastTextPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestBody", completion.LastTextPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openapi", completion.LastTextPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source=", completion.LastTextPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("POWER_AUTOMATE", completion.LastTextPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sig=", completion.LastTextPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnswerAsync_EmptyCompletion_Fails()
    {
        var completion = new RecordingInsightsCompletionClient { TextCompletion = "   " };
        var assistant = Create(completion, enabled: true);

        var result = await assistant.AnswerAsync(
            "Why was this run blocked?",
            InsightsIntent.ExplainDecision,
            DecisionContext("Block"));

        Assert.False(result.Succeeded);
        Assert.Equal(InsightsAssistantResult.UnavailableWarning, result.Warning);
    }

    [Fact]
    public async Task AnswerAsync_ProviderException_FailsWithoutLeakingSecret()
    {
        var completion = new RecordingInsightsCompletionClient
        {
            Exception = new AiCompletionException("Azure OpenAI API key is not configured. sk-secret")
        };
        var assistant = Create(completion, enabled: true);

        var result = await assistant.AnswerAsync(
            "Why was this run blocked?",
            InsightsIntent.ExplainDecision,
            DecisionContext("Block"));

        Assert.False(result.Succeeded);
        Assert.Equal(InsightsAssistantResult.UnavailableWarning, result.Warning);
        Assert.DoesNotContain("sk-secret", result.Warning, StringComparison.Ordinal);
        Assert.DoesNotContain("API key", result.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnswerAsync_DoesNotTreatLlmTextAsDecision()
    {
        var completion = new RecordingInsightsCompletionClient
        {
            TextCompletion = "This appears safe."
        };
        var assistant = Create(completion, enabled: true);

        var result = await assistant.AnswerAsync(
            "Why was this run blocked?",
            InsightsIntent.ExplainDecision,
            DecisionContext("Block"));

        Assert.True(result.Succeeded);
        Assert.Equal("This appears safe.", result.Answer);
        Assert.Equal("Block", DecisionContext("Block").AuthoritativeDecision);
    }

    private static LlmInsightsAssistant Create(
        IAiCompletionClient completion,
        bool enabled,
        AiProvider provider = AiProvider.OpenAI) =>
        new(
            completion,
            Options.Create(new AiOptions
            {
                Enabled = enabled,
                Provider = provider,
                ModelOrDeployment = "gpt-4o-mini"
            }),
            new InsightsContextFormatter());

    private static InsightsContext DecisionContext(string decision) =>
        new InsightsContextBuilder().Build(
            InsightsIntent.ExplainDecision,
            new InsightsEvidence
            {
                ProjectId = Guid.NewGuid(),
                ProjectName = "Pets API",
                HasLastRun = true,
                Decision = decision,
                FindingCount = 1,
                FailedCount = 1,
                Findings =
                [
                    new InsightsFindingEvidence
                    {
                        Category = "Regression",
                        Severity = "High",
                        Code = "VALIDATION_REGRESSED",
                        Method = "GET",
                        Path = "/pets",
                        Message = "Validation regressed."
                    }
                ]
            });
}

public sealed class RecordingInsightsCompletionClient : IAiCompletionClient
{
    public int TextCalls { get; private set; }

    public string? LastTextPrompt { get; private set; }

    public string TextCompletion { get; set; } = "The latest run is blocked by a regression finding.";

    public Exception? Exception { get; set; }

    public Task<string> CompleteJsonAsync(string prompt, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Insights must use text completion, not JSON completion.");

    public Task<string> CompleteTextAsync(string prompt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TextCalls++;
        LastTextPrompt = prompt;
        if (Exception is not null)
        {
            throw Exception;
        }

        return Task.FromResult(TextCompletion);
    }
}
