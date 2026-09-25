using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Insights;
using TestShieldAI.Api.Persistence;

namespace TestShieldAI.Api.Endpoints;

public static class InsightsAskEndpoint
{
    public const string Route = "/api/projects/{projectId}/insights/ask";

    public const int MaxQuestionLength = 500;

    public static string RouteFor(Guid projectId) => $"/api/projects/{projectId}/insights/ask";

    public static IEndpointRouteBuilder MapInsightsAsk(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Route, Ask)
            .WithName("AskInsights")
            .WithOpenApi();

        return endpoints;
    }

    private static async Task<IResult> Ask(
        Guid projectId,
        AskInsightsRequest? request,
        IProjectStore projects,
        IInsightsIntentRouter router,
        IInsightsEvidenceRetriever retriever,
        IInsightsContextBuilder contextBuilder,
        IInsightsAssistant assistant,
        IDeterministicInsightsAnswerer answerer,
        CancellationToken cancellationToken)
    {
        var project = await projects.FindByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return Results.Problem(
                title: "Project not found",
                detail: $"No project exists with id '{projectId}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var question = request?.Question?.Trim() ?? "";
        if (question.Length == 0)
        {
            return Results.Problem(
                title: "Invalid question",
                detail: "Question is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (question.Length > MaxQuestionLength)
        {
            return Results.Problem(
                title: "Invalid question",
                detail: $"Question must be {MaxQuestionLength} characters or fewer.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var intent = router.Route(question);
        var evidence = await retriever.RetrieveAsync(project, intent, cancellationToken);
        var context = contextBuilder.Build(intent, evidence);
        var assistantResult = await assistant.AnswerAsync(question, intent, context, cancellationToken);

        var usedLlm = assistantResult.Succeeded && !string.IsNullOrWhiteSpace(assistantResult.Answer);
        var answer = usedLlm
            ? assistantResult.Answer!
            : answerer.Answer(intent, evidence);

        return Results.Ok(new AskInsightsResponse
        {
            Question = question,
            Intent = intent.ToString(),
            Answer = answer,
            AssistantAvailable = usedLlm,
            AnswerSource = usedLlm
                ? AskInsightsResponse.LlmSource
                : AskInsightsResponse.DeterministicSource,
            Warning = usedLlm ? assistantResult.Warning : assistantResult.Warning ?? InsightsAssistantResult.UnavailableWarning,
            Evidence = new InsightsEvidenceDto
            {
                Decision = evidence.Decision,
                LastRunCompletedAt = evidence.LastRunCompletedAt,
                Retrieved = evidence.RetrievedEvidenceIds
            }
        });
    }
}
