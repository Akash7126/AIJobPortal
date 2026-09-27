using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.GovernmentIntegration.Api;
using JobPlatform.GovernmentIntegration.Application;
using JobPlatform.GovernmentIntegration.Infrastructure;
using JobPlatform.GovernmentIntegration.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.GovernmentIntegration", "JobPlatform - Government Integration API",
    "BC-01 Government Integration: employer verification against MoL, government/education/identity data verification, MoL/PEF connection "
    + "management and legacy data migration. An anti-corruption layer towards 7 external government systems. Errors are application/problem+json "
    + "with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, GovernmentIntegrationErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddGovernmentIntegrationApplication();
builder.Services.AddGovernmentIntegrationInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<GovernmentIntegrationDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
