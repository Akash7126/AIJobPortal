using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.ExternalIntegration.Api;
using JobPlatform.ExternalIntegration.Application;
using JobPlatform.ExternalIntegration.Infrastructure;
using JobPlatform.ExternalIntegration.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.ExternalIntegration", "JobPlatform - External Integration API",
    "BC-02 External Integration: partner (external job site) onboarding, job-data exchange (push/pull), source attribution and the "
    + "platform's API framework/documentation. Errors are application/problem+json with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, ExternalIntegrationErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddExternalIntegrationApplication();
builder.Services.AddExternalIntegrationInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<ExternalIntegrationDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
