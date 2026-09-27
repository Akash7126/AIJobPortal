using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.HelpContent.Api;
using JobPlatform.HelpContent.Application;
using JobPlatform.HelpContent.Infrastructure;
using JobPlatform.HelpContent.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.HelpContent", "JobPlatform - Help Content API",
    "BC-06 Help Content: news/announcements, the FAQ/help center (laws, regulations, contract-type regulations), organisation by topic and "
    + "role, feedback, onboarding tutorials, context-sensitive help and the employer company profile page. Errors are application/problem+json "
    + "with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, HelpContentErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddHelpContentApplication();
builder.Services.AddHelpContentInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<HelpContentDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
