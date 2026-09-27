using JobPlatform.AiMatching.Api;
using JobPlatform.AiMatching.Application;
using JobPlatform.AiMatching.Infrastructure;
using JobPlatform.AiMatching.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.AiMatching", "JobPlatform - AI Matching API",
    "BC-10 AI Matching: resume parsing, skill standardisation, job semantics, six-criterion match scores with configurable weights and threshold, ranking, "
    + "shortlists and recommendations. Errors are application/problem+json with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, AiMatchingErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddAiMatchingApplication();
builder.Services.AddAiMatchingInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<AiMatchingDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
