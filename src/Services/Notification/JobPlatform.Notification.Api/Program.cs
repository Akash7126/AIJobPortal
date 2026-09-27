using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.Notification.Api;
using JobPlatform.Notification.Application;
using JobPlatform.Notification.Infrastructure;
using JobPlatform.Notification.Infrastructure.Persistence;
using JobPlatform.Notification.Infrastructure.Realtime;

var builder = WebApplication.CreateBuilder(args);

var info = new ServiceInfo("JobPlatform.Notification", "JobPlatform - Notification API",
    "BC-13 Notification: e-mail, in-app (SignalR /hubs/notifications) and SMS delivery, preferences and consent, templates, delivery status, compliance controls "
    + "and the job-confirmation callback. Errors are application/problem+json with a stable `code`.",
    typeof(Program).Assembly);

builder.AddJobPlatformService(info, NotificationErrorMessages.Catalog);
builder.Services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
builder.Services.AddNotificationApplication();
builder.Services.AddNotificationInfrastructure(builder.Configuration);
builder.Services.AddBuildingBlockHealthChecks<NotificationDbContext>(builder.Configuration);

var app = builder.Build();
app.UseJobPlatformService(info);
app.MapHub<NotificationHub>(NotificationHub.Route);
app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
