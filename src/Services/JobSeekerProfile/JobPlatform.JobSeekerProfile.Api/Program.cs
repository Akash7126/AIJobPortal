using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.JobSeekerProfile.Api;
using JobPlatform.JobSeekerProfile.Application;
using JobPlatform.JobSeekerProfile.Infrastructure;
using JobPlatform.JobSeekerProfile.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.JobSeekerProfile", "JobPlatform - Job Seeker Profile API",
    "BC-04 Job Seeker Profile: staged profile (Level 1-3), completion tracking, job preferences, privacy, resume and supplementary documents, "
    + "profile share link. Errors are application/problem+json with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, JobSeekerProfileErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddJobSeekerProfileApplication();
builder.Services.AddJobSeekerProfileInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<JobSeekerProfileDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
