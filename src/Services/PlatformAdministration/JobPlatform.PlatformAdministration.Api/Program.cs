using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.PlatformAdministration.Api;
using JobPlatform.PlatformAdministration.Application;
using JobPlatform.PlatformAdministration.Infrastructure;
using JobPlatform.PlatformAdministration.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.PlatformAdministration", "JobPlatform - Platform Administration API",
    "BC-08 Platform Administration: administrator control over platform entities, system settings, reference files, the platform taxonomy and job-offering "
    + "moderation; the reference-data authority for the other services. Errors are application/problem+json with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, AdminErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddPlatformAdministrationApplication();
builder.Services.AddPlatformAdministrationInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<AdminDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
