using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.Reporting.Api;
using JobPlatform.Reporting.Application;
using JobPlatform.Reporting.Infrastructure;
using JobPlatform.Reporting.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.Reporting", "JobPlatform - Reporting API",
    "BC-12 Reporting: read-model context that turns the events of every other BC into employment statistics, system performance, user activity and custom, scheduled and "
    + "exported reports for administrators. Errors are application/problem+json with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, ReportingErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddReportingApplication();
builder.Services.AddReportingInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<ReportingDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
