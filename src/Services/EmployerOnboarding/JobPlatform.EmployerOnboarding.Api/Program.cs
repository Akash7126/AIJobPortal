using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.EmployerOnboarding.Api;
using JobPlatform.EmployerOnboarding.Application;
using JobPlatform.EmployerOnboarding.Infrastructure;
using JobPlatform.EmployerOnboarding.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.EmployerOnboarding", "JobPlatform - Employer Onboarding API",
    "BC-05 Employer Onboarding: employer admission (registration approval), company media/documents and the Verified Employer standing that BC-09 "
    + "and BC-11 rely on. Errors are application/problem+json with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, EmployerOnboardingErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddEmployerOnboardingApplication();
builder.Services.AddEmployerOnboardingInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<EmployerOnboardingDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
