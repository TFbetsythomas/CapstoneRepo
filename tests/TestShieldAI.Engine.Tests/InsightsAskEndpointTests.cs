using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TestShieldAI.Api.Contracts;
using TestShieldAI.Api.Endpoints;
using TestShieldAI.Api.Insights;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class InsightsAskEndpointTests : IClassFixture<InsightsAskWebApplicationFactory>
{
    private const string Spec = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets": {
              "post": {
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "required": ["name"],
                        "properties": {
                          "name": { "type": "string" }
                        }
                      }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "description": "Created",
                    "content": {
                      "application/json": {
                        "schema": { "type": "object" }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly InsightsAskWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InsightsAskEndpointTests(InsightsAskWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ask_UnknownProject_Returns404()
    {
        var response = await AskAsync(Guid.NewGuid(), "Why was this run blocked?");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Ask_BlankQuestion_Returns400()
    {
        var project = await CreateProjectAsync();

        var response = await AskAsync(project.Id, "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ask_QuestionLongerThan500_Returns400()
    {
        var project = await CreateProjectAsync();

        var response = await AskAsync(project.Id, new string('a', InsightsAskEndpoint.MaxQuestionLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Why was this run blocked?", nameof(InsightsIntent.ExplainDecision))]
    [InlineData("Why is coverage 75%?", nameof(InsightsIntent.ExplainCoverage))]
    [InlineData("What changed from the previous baseline?", nameof(InsightsIntent.BaselineDiff))]
    [InlineData("Which operations have contract drift?", nameof(InsightsIntent.ContractDrift))]
    [InlineData("What tests failed?", nameof(InsightsIntent.FailedTests))]
    [InlineData("What negative scenarios exist?", nameof(InsightsIntent.NegativeScenarios))]
    [InlineData("Show me the run overview", nameof(InsightsIntent.RunOverview))]
    [InlineData("hello there", nameof(InsightsIntent.RunOverview))]
    public async Task Ask_RoutesQuestionToIntent(string question, string expectedIntent)
    {
        var project = await CreateProjectAsync();
        await SeedLastRunAsync(project.Id, nameof(RegressionDecision.Block), findingCount: 1);

        var body = await AskOkAsync(project.Id, question);

        Assert.Equal(expectedIntent, body.Intent);
        Assert.False(body.AssistantAvailable);
        Assert.Equal(AskInsightsResponse.DeterministicSource, body.AnswerSource);
    }

    [Fact]
    public async Task Ask_NoLastRun_ReturnsDeterministicNoRunAnswer()
    {
        var project = await CreateProjectAsync();

        var body = await AskOkAsync(project.Id, "Why was this run blocked?");

        Assert.Equal(nameof(InsightsIntent.ExplainDecision), body.Intent);
        Assert.Equal(DeterministicInsightsAnswerer.NoRunAnswer, body.Answer);
        Assert.Null(body.Evidence.Decision);
        Assert.Empty(body.Evidence.Retrieved);
    }

    [Fact]
    public async Task Ask_BlockedPersistedRun_UsesStoredDecision()
    {
        var project = await CreateProjectAsync();
        await SeedLastRunAsync(project.Id, nameof(RegressionDecision.Block), findingCount: 2);

        var body = await AskOkAsync(project.Id, "Why was this run blocked?");
        var json = JsonSerializer.Serialize(body);

        Assert.Equal(nameof(InsightsIntent.ExplainDecision), body.Intent);
        Assert.Equal(nameof(RegressionDecision.Block), body.Evidence.Decision);
        Assert.Contains("This run is Block", body.Answer, StringComparison.Ordinal);
        Assert.Contains("2 finding(s)", body.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain(project.BaseUrl, json, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openapi", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("POWER_AUTOMATE", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sig=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("baseUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestBody", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"headers\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ask_CoverageQuestion_RecomputesCoverageWithoutRunningTests()
    {
        var project = await CreateProjectAsync();
        await ImportAsync(project.Id, Spec);
        await SeedLastRunAsync(project.Id, nameof(RegressionDecision.Safe), findingCount: 0);

        var body = await AskOkAsync(project.Id, "Why is coverage 75%?");

        Assert.Equal(nameof(InsightsIntent.ExplainCoverage), body.Intent);
        Assert.Contains("Coverage is", body.Answer, StringComparison.Ordinal);
        Assert.Contains("operations are covered", body.Answer, StringComparison.Ordinal);
        Assert.Contains("coverage", body.Evidence.Retrieved, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Ask_NegativeScenarios_UsesGeneratedTestsWithoutExecution()
    {
        var project = await CreateProjectAsync();
        await ImportAsync(project.Id, Spec);
        await SeedLastRunAsync(project.Id, nameof(RegressionDecision.Safe), findingCount: 0);

        var body = await AskOkAsync(project.Id, "What negative scenarios exist?");

        Assert.Equal(nameof(InsightsIntent.NegativeScenarios), body.Intent);
        Assert.Contains("negative scenario", body.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("generated.negativeScenarios", body.Evidence.Retrieved, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Ask_DoesNotModifyProjectTestRuns()
    {
        var project = await CreateProjectAsync();
        await SeedLastRunAsync(project.Id, nameof(RegressionDecision.Review), findingCount: 0);
        var before = await FindLastRunAsync(project.Id);
        Assert.NotNull(before);

        (await AskAsync(project.Id, "Show me the run overview")).EnsureSuccessStatusCode();

        var after = await FindLastRunAsync(project.Id);
        Assert.NotNull(after);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Decision, after.Decision);
        Assert.Equal(before.CompletedAt, after.CompletedAt);
        Assert.Equal(before.PassedCount, after.PassedCount);
        Assert.Equal(before.FailedCount, after.FailedCount);
        Assert.Equal(before.ErrorCount, after.ErrorCount);
        Assert.Equal(before.FindingCount, after.FindingCount);
        Assert.Equal(before.ResultsJson, after.ResultsJson);
    }

    [Fact]
    public async Task Ask_DoesNotModifyProjectBaselines()
    {
        var project = await CreateProjectAsync();
        await SeedLastRunAsync(project.Id, nameof(RegressionDecision.Safe), findingCount: 0);
        await SeedBaselineAsync(project.Id);
        var before = await FindBaselineAsync(project.Id);
        Assert.NotNull(before);

        (await AskAsync(project.Id, "What changed from the previous baseline?")).EnsureSuccessStatusCode();

        var after = await FindBaselineAsync(project.Id);
        Assert.NotNull(after);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.EstablishedAt, after.EstablishedAt);
        Assert.Equal(before.SnapshotJson, after.SnapshotJson);
        Assert.Equal(before.SpecificationId, after.SpecificationId);
    }

    private async Task<AskInsightsResponse> AskOkAsync(Guid projectId, string question)
    {
        var response = await AskAsync(projectId, question);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AskInsightsResponse>(JsonOptions);
        Assert.NotNull(body);
        return body;
    }

    private Task<HttpResponseMessage> AskAsync(Guid projectId, string question) =>
        _client.PostAsJsonAsync(InsightsAskEndpoint.RouteFor(projectId), new { question });

    private async Task<ProjectResponse> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync(
            ProjectsEndpoint.Route,
            new { name = "Pets API", baseUrl = "https://pets.example.com" });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions);
        Assert.NotNull(project);
        return project;
    }

    private async Task ImportAsync(Guid projectId, string specification)
    {
        var json = JsonSerializer.Serialize(new { specification });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(SpecsImportEndpoint.RouteFor(projectId), content);
        response.EnsureSuccessStatusCode();
    }

    private async Task SeedLastRunAsync(Guid projectId, string decision, int findingCount)
    {
        var findings = Enumerable.Range(0, findingCount)
            .Select(index => new PersistedRegressionFinding
            {
                Category = index == 0
                    ? nameof(RegressionFindingCategory.Regression)
                    : nameof(RegressionFindingCategory.ContractDrift),
                Code = "VALIDATION_REGRESSED",
                Severity = nameof(RegressionFindingSeverity.High),
                Kind = nameof(GeneratedApiTestKind.HappyPath),
                Method = "GET",
                Path = "/pets",
                Expected = "Passed",
                Current = "Failed",
                JsonPath = null,
                Message = "Validation regressed."
            })
            .ToList();

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IProjectTestRunStore>().SaveAsync(
            projectId,
            new ProjectTestRunSave
            {
                Decision = decision,
                PassedCount = 0,
                FailedCount = 1,
                ErrorCount = 0,
                FindingCount = findingCount,
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
                    Findings = findings
                }
            });
    }

    private async Task SeedBaselineAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
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

    private async Task<ProjectTestRunRecord?> FindLastRunAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProjectTestRunStore>()
            .FindByProjectIdAsync(projectId);
    }

    private async Task<ProjectBaselineRecord?> FindBaselineAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProjectBaselineStore>()
            .FindByProjectIdAsync(projectId);
    }
}

public class InsightsAskWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"testshield-insights-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TestShield", $"Data Source={_dbPath}");
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IApiTestRunner)).ToList())
            {
                services.Remove(descriptor);
            }

            foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IRegressionDetector)).ToList())
            {
                services.Remove(descriptor);
            }

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

public sealed class InsightsForbiddenApiTestRunner : IApiTestRunner
{
    public Task<IReadOnlyList<ApiTestExecutionResult>> RunAsync(
        string baseUrl,
        IReadOnlyList<GeneratedApiTestCase> tests,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Insights must not execute tests.");
}

public sealed class InsightsForbiddenRegressionDetector : IRegressionDetector
{
    public RegressionDetectionResult Detect(
        IReadOnlyList<RegressionTestSnapshot> baseline,
        IReadOnlyList<RegressionTestSnapshot> current) =>
        throw new InvalidOperationException("Insights must not detect regressions.");
}
