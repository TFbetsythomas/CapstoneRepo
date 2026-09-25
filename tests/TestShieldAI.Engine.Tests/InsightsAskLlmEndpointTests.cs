using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TestShieldAI.Api.Ai;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Endpoints;
using TestShieldAI.Api.Insights;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class InsightsAskLlmEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Ask_SuccessfulLlm_ReturnsLlmAnswer()
    {
        using var factory = new InsightsAskLlmWebApplicationFactory();
        factory.Completion.TextCompletion = "The run is blocked because GET /pets regressed.";
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await SeedLastRunAsync(factory, project.Id, nameof(RegressionDecision.Block));

        var body = await AskOkAsync(client, project.Id, "Why was this run blocked?");

        Assert.True(body.AssistantAvailable);
        Assert.Equal(AskInsightsResponse.LlmSource, body.AnswerSource);
        Assert.Equal("The run is blocked because GET /pets regressed.", body.Answer);
        Assert.Equal(nameof(RegressionDecision.Block), body.Evidence.Decision);
        Assert.Equal(1, factory.Completion.TextCalls);
        Assert.Contains("Why was this run blocked?", factory.Completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("ExplainDecision", factory.Completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("AUTHORITATIVE_DECISION:", factory.Completion.LastTextPrompt, StringComparison.Ordinal);
        Assert.Contains("TESTSHIELD_INSIGHTS_CONTEXT", factory.Completion.LastTextPrompt, StringComparison.Ordinal);
        AssertSafe(factory.Completion.LastTextPrompt);
    }

    [Fact]
    public async Task Ask_LlmFailure_Returns200WithDeterministicFallback()
    {
        using var factory = new InsightsAskLlmWebApplicationFactory();
        factory.Completion.Exception = new HttpRequestException("provider down");
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await SeedLastRunAsync(factory, project.Id, nameof(RegressionDecision.Block));

        var response = await client.PostAsJsonAsync(
            InsightsAskEndpoint.RouteFor(project.Id),
            new { question = "Why was this run blocked?" });
        var body = await response.Content.ReadFromJsonAsync<AskInsightsResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.AssistantAvailable);
        Assert.Equal(AskInsightsResponse.DeterministicSource, body.AnswerSource);
        Assert.Equal(InsightsAssistantResult.UnavailableWarning, body.Warning);
        Assert.Contains("This run is Block", body.Answer, StringComparison.Ordinal);
        Assert.Equal(nameof(RegressionDecision.Block), body.Evidence.Decision);
    }

    [Fact]
    public async Task Ask_EmptyLlmResponse_UsesDeterministicFallback()
    {
        using var factory = new InsightsAskLlmWebApplicationFactory();
        factory.Completion.TextCompletion = " ";
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await SeedLastRunAsync(factory, project.Id, nameof(RegressionDecision.Review));

        var body = await AskOkAsync(client, project.Id, "Show me the run overview");

        Assert.False(body.AssistantAvailable);
        Assert.Equal(AskInsightsResponse.DeterministicSource, body.AnswerSource);
        Assert.Contains("Decision Review", body.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_LlmSaysSafe_DoesNotChangePersistedBlockDecision()
    {
        using var factory = new InsightsAskLlmWebApplicationFactory();
        factory.Completion.TextCompletion = "This appears safe.";
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await SeedLastRunAsync(factory, project.Id, nameof(RegressionDecision.Block));

        var body = await AskOkAsync(client, project.Id, "Why was this run blocked?");

        Assert.True(body.AssistantAvailable);
        Assert.Equal("This appears safe.", body.Answer);
        Assert.Equal(nameof(RegressionDecision.Block), body.Evidence.Decision);
    }

    [Fact]
    public async Task Ask_MissingApiKey_Returns200Deterministic()
    {
        using var factory = new InsightsAskMissingKeyWebApplicationFactory();
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await SeedLastRunAsync(factory, project.Id, nameof(RegressionDecision.Block));

        var response = await client.PostAsJsonAsync(
            InsightsAskEndpoint.RouteFor(project.Id),
            new { question = "Why was this run blocked?" });
        var body = await response.Content.ReadFromJsonAsync<AskInsightsResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.AssistantAvailable);
        Assert.Equal(AskInsightsResponse.DeterministicSource, body.AnswerSource);
        Assert.Equal(nameof(RegressionDecision.Block), body.Evidence.Decision);
    }

    [Fact]
    public async Task Ask_WithLlm_DoesNotModifyLastRunOrBaseline()
    {
        using var factory = new InsightsAskLlmWebApplicationFactory();
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await SeedLastRunAsync(factory, project.Id, nameof(RegressionDecision.Block));
        await SeedBaselineAsync(factory, project.Id);
        var runBefore = await FindLastRunAsync(factory, project.Id);
        var baselineBefore = await FindBaselineAsync(factory, project.Id);
        Assert.NotNull(runBefore);
        Assert.NotNull(baselineBefore);

        (await client.PostAsJsonAsync(
            InsightsAskEndpoint.RouteFor(project.Id),
            new { question = "Why was this run blocked?" })).EnsureSuccessStatusCode();

        var runAfter = await FindLastRunAsync(factory, project.Id);
        var baselineAfter = await FindBaselineAsync(factory, project.Id);
        Assert.NotNull(runAfter);
        Assert.NotNull(baselineAfter);
        Assert.Equal(runBefore.Decision, runAfter.Decision);
        Assert.Equal(runBefore.ResultsJson, runAfter.ResultsJson);
        Assert.Equal(runBefore.CompletedAt, runAfter.CompletedAt);
        Assert.Equal(baselineBefore.SnapshotJson, baselineAfter.SnapshotJson);
        Assert.Equal(baselineBefore.EstablishedAt, baselineAfter.EstablishedAt);
    }

    private static void AssertSafe(string? prompt)
    {
        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.DoesNotContain("https://pets.example.com", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("baseUrl", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization:", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Authorization\"", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestBody", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("responseBody", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openapi", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("swagger", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source=", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("POWER_AUTOMATE", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sig=", prompt, StringComparison.Ordinal);
    }

    private static async Task<AskInsightsResponse> AskOkAsync(HttpClient client, Guid projectId, string question)
    {
        var response = await client.PostAsJsonAsync(
            InsightsAskEndpoint.RouteFor(projectId),
            new { question });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AskInsightsResponse>(JsonOptions);
        Assert.NotNull(body);
        return body;
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            ProjectsEndpoint.Route,
            new { name = "Pets API", baseUrl = "https://pets.example.com" });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        return project;
    }

    private static async Task SeedLastRunAsync(WebApplicationFactory<Program> factory, Guid projectId, string decision)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IProjectTestRunStore>().SaveAsync(
            projectId,
            new ProjectTestRunSave
            {
                Decision = decision,
                PassedCount = 0,
                FailedCount = 1,
                ErrorCount = 0,
                FindingCount = 1,
                Results = new PersistedTestRunResults
                {
                    Tests =
                    [
                        new PersistedTestRunCase
                        {
                            Kind = nameof(GeneratedApiTestKind.HappyPath),
                            SourceMethod = "GET",
                            SourcePath = "/pets",
                            SpecKey = "Pets",
                            ExpectedStatus = 200,
                            ActualStatus = 500,
                            Outcome = nameof(ContractValidationOutcome.Failed),
                            ScenarioKey = null,
                            Failures = []
                        }
                    ],
                    Findings =
                    [
                        new PersistedRegressionFinding
                        {
                            Category = nameof(RegressionFindingCategory.Regression),
                            Code = "VALIDATION_REGRESSED",
                            Severity = nameof(RegressionFindingSeverity.High),
                            Kind = nameof(GeneratedApiTestKind.HappyPath),
                            Method = "GET",
                            Path = "/pets",
                            Expected = "Passed",
                            Current = "Failed",
                            JsonPath = null,
                            Message = "Validation regressed."
                        }
                    ]
                }
            });
    }

    private static async Task SeedBaselineAsync(WebApplicationFactory<Program> factory, Guid projectId)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IProjectBaselineStore>().SaveAsync(
            projectId,
            [
                new RegressionTestSnapshot(
                    GeneratedApiTestKind.HappyPath,
                    "GET",
                    "/pets",
                    "GET",
                    "/pets",
                    200,
                    expectedResponseSchema: null,
                    ContractValidationOutcome.Passed,
                    scenarioKey: null,
                    specKey: "Pets")
            ]);
    }

    private static async Task<ProjectTestRunRecord?> FindLastRunAsync(
        WebApplicationFactory<Program> factory,
        Guid projectId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProjectTestRunStore>()
            .FindByProjectIdAsync(projectId);
    }

    private static async Task<ProjectBaselineRecord?> FindBaselineAsync(
        WebApplicationFactory<Program> factory,
        Guid projectId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProjectBaselineStore>()
            .FindByProjectIdAsync(projectId);
    }
}

public class InsightsAskLlmWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"testshield-insights-llm-{Guid.NewGuid():N}.db");

    public RecordingInsightsCompletionClient Completion { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TestShield", $"Data Source={_dbPath}");
        builder.UseSetting("Ai:Enabled", "true");
        builder.UseSetting("Ai:Provider", "OpenAI");
        builder.UseSetting("Ai:ModelOrDeployment", "gpt-4o-mini");
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IAiCompletionClient)).ToList())
            {
                services.Remove(descriptor);
            }

            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IApiTestRunner)).ToList())
            {
                services.Remove(descriptor);
            }

            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IRegressionDetector)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton(Completion);
            services.AddSingleton<IAiCompletionClient>(provider => provider.GetRequiredService<RecordingInsightsCompletionClient>());
            services.AddSingleton<IApiTestRunner, InsightsForbiddenApiTestRunner>();
            services.AddSingleton<IRegressionDetector, InsightsForbiddenRegressionDetector>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}

public class InsightsAskMissingKeyWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"testshield-insights-nokey-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TestShield", $"Data Source={_dbPath}");
        builder.UseSetting("Ai:Enabled", "true");
        builder.UseSetting("Ai:Provider", "AzureOpenAI");
        builder.UseSetting("Ai:Endpoint", "https://example.openai.azure.com/");
        builder.UseSetting("Ai:ModelOrDeployment", "gpt-4o-mini");
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IAiApiKeyAccessor)).ToList())
            {
                services.Remove(descriptor);
            }

            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IApiTestRunner)).ToList())
            {
                services.Remove(descriptor);
            }

            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IRegressionDetector)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IAiApiKeyAccessor, EmptyAiApiKeyAccessor>();
            services.AddSingleton<IApiTestRunner, InsightsForbiddenApiTestRunner>();
            services.AddSingleton<IRegressionDetector, InsightsForbiddenRegressionDetector>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}

public sealed class EmptyAiApiKeyAccessor : IAiApiKeyAccessor
{
    public string? GetApiKey(AiProvider provider) => null;
}
