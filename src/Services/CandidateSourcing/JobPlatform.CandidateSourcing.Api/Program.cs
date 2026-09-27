using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.CandidateSourcing.Api;
using JobPlatform.CandidateSourcing.Application;
using JobPlatform.CandidateSourcing.Infrastructure;
using JobPlatform.CandidateSourcing.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.CandidateSourcing", "JobPlatform - Candidate Sourcing API",
    "BC-11 Candidate Sourcing: employer-facing candidate recommendation, ranking, qualification thresholds, the candidate database, candidate "
    + "insight and the talent pool - all privacy-filtered. Errors are application/problem+json with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, CandidateSourcingErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddCandidateSourcingApplication();
builder.Services.AddCandidateSourcingInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<CandidateSourcingDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
