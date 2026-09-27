using JobPlatform.AuditLogging.Api;
using JobPlatform.AuditLogging.Application;
using JobPlatform.AuditLogging.Infrastructure;
using JobPlatform.AuditLogging.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.AuditLogging", "JobPlatform - Audit Logging API",
    "BC-07 Audit Logging: read-model context that records what other BCs did (immutable audit entries, sync/usage/status projections) and lets partners, "
    + "employers, users and administrators see their logs and dashboards. Errors are application/problem+json with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, AuditErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddAuditLoggingApplication();
builder.Services.AddAuditLoggingInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<AuditDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
