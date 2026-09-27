using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.JobPosting.Api;
using JobPlatform.JobPosting.Application;
using JobPlatform.JobPosting.Infrastructure;
using JobPlatform.JobPosting.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.JobPosting", "JobPlatform - Job Posting API",
    "BC-09 Job Posting: employer job-posting creation, publishing and lifecycle management; guest/job-seeker search, favourites, saved "
    + "searches and the interested list. Errors are application/problem+json with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, JobPostingErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddJobPostingApplication();
builder.Services.AddJobPostingInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<JobPostingDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
