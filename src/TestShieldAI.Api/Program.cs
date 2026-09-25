using TestShieldAI.Api.Ai;
using TestShieldAI.Api.Endpoints;
using TestShieldAI.Api.Insights;
using TestShieldAI.Api.Notifications;
using TestShieldAI.Api.Persistence;
using TestShieldAI.Api.TestsRun;
using TestShieldAI.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IOpenApiIngestor, OpenApiIngestor>();
builder.Services.AddSingleton<IApiTestGenerator, ApiTestGenerator>();
builder.Services.AddSingleton<IApiContractValidator, ApiContractValidator>();
builder.Services.AddSingleton<IRegressionDetector, RegressionDetector>();
builder.Services.AddSingleton<ICoverageCalculator, CoverageCalculator>();
builder.Services.AddSingleton<IRiskSummaryBuilder, RiskSummaryBuilder>();
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.AddSingleton<IAiApiKeyAccessor, ConfigurationAiApiKeyAccessor>();
builder.Services.AddSingleton<IAiCompletionClient>(services =>
    AiCompletionClientFactory.Create(
        services.GetRequiredService<IOptions<AiOptions>>().Value,
        services.GetRequiredService<IAiApiKeyAccessor>(),
        services.GetRequiredService<ILoggerFactory>()));
builder.Services.AddSingleton<IAiScenarioNormalizer, AiScenarioNormalizer>();
builder.Services.AddSingleton<IAiScenarioGenerator>(services =>
    AiScenarioGeneratorFactory.Create(
        services.GetRequiredService<IOptions<AiOptions>>().Value,
        services.GetRequiredService<IAiCompletionClient>()));
builder.Services.AddScoped<TestsRunOrchestrator>();
builder.Services.AddHttpClient<HttpApiTestRunner>(client =>
{
    client.Timeout = HttpApiTestRunner.DefaultTimeout;
});
builder.Services.AddTransient<IApiTestRunner>(services => services.GetRequiredService<HttpApiTestRunner>());
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.AddSingleton<INotificationTriggerUrlAccessor, ConfigurationNotificationTriggerUrlAccessor>();
builder.Services.AddSingleton<NotificationEventComposer>();
builder.Services.AddSingleton<NoOpRunCompletedNotifier>();
builder.Services.AddHttpClient<PowerAutomateHttpNotifier>((services, client) =>
{
    var timeoutSeconds = services.GetRequiredService<IOptions<NotificationOptions>>().Value.TimeoutSeconds;
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 10);
});
builder.Services.AddTransient<IRunCompletedNotifier>(services =>
{
    var options = services.GetRequiredService<IOptions<NotificationOptions>>().Value;
    return options.Enabled
        ? services.GetRequiredService<PowerAutomateHttpNotifier>()
        : services.GetRequiredService<NoOpRunCompletedNotifier>();
});

var connectionString = builder.Configuration.GetConnectionString("TestShield")
    ?? throw new InvalidOperationException("Connection string 'TestShield' is not configured.");
EnsureSqliteDirectory(connectionString);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IProjectStore, ProjectStore>();
builder.Services.AddScoped<IOpenApiSpecificationStore, OpenApiSpecificationStore>();
builder.Services.AddScoped<IProjectBaselineStore, ProjectBaselineStore>();
builder.Services.AddScoped<IProjectTestRunStore, ProjectTestRunStore>();
builder.Services.AddSingleton<IInsightsIntentRouter, InsightsIntentRouter>();
builder.Services.AddSingleton<IInsightsContextBuilder, InsightsContextBuilder>();
builder.Services.AddSingleton<InsightsContextFormatter>();
builder.Services.AddSingleton<IDeterministicInsightsAnswerer, DeterministicInsightsAnswerer>();
builder.Services.AddSingleton<IInsightsAssistant, LlmInsightsAssistant>();
builder.Services.AddScoped<IInsightsEvidenceRetriever, InsightsEvidenceRetriever>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.MapProjects();
app.MapSpecsImport();
app.MapTestsGenerate();
app.MapTestsRun();
app.MapInsightsAsk();

app.Run();

static void EnsureSqliteDirectory(string connectionString)
{
    const string prefix = "Data Source=";
    var start = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
    if (start < 0)
    {
        return;
    }

    var path = connectionString[(start + prefix.Length)..].Trim().Trim('"');
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }
}

public partial class Program;
