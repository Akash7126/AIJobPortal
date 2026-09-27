using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using JobPlatform.AccountIdentity.Api;
using JobPlatform.AccountIdentity.Api.Security;
using JobPlatform.AccountIdentity.Application;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Infrastructure;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog is the single logging pipeline. Sinks registered in DI (ILogEventSink) are picked up by ReadFrom.Services.
builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

var services = builder.Services;
var configuration = builder.Configuration;

// --- application, building blocks, infrastructure (composition root)
services.AddHttpContextAccessor();
services.AddBuildingBlocks(configuration);
services.AddRequestHandlersFrom(ApplicationAssembly.Assembly);
services.AddAccountIdentityApplication();
services.AddAccountIdentityInfrastructure(configuration);
services.Configure<ConsentOptions>(configuration.GetSection(ConsentOptions.SectionName));
services.AddScoped<ICurrentUser, CurrentUser>();
services.AddSingleton<IErrorMessageLocalizer, ResourceErrorMessageLocalizer>();

// --- authentication / authorisation (JWT issued by this service, validated locally through the key service)
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
services.AddAuthorization(AuthorizationSetup.AddPolicies);
services.AddSingleton<IAuthorizationHandler, NoPendingPasswordChangeHandler>();
services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemAuthorizationResultHandler>();

// --- HTTP surface
services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(e => string.IsNullOrEmpty(e.Key) ? "body" : e.Key.TrimStart('$', '.'), _ => new[] { "VAL.INVALID_FORMAT" });
        return Error.Validation(errors).ToActionResult(context.HttpContext);
    });
services.AddProblemDetails();
services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

services.AddAccountIdentityOpenApi();
services.AddAccountIdentityTelemetry(configuration);

// --- rate limiting (THR-055): global per source, stricter on the auth surface
services.AddRateLimiter(_ => { });
services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, config) =>
{
    var globalLimit = config.GetValue("RateLimiting:Global:PermitLimit", 600);
    var authLimit = config.GetValue("RateLimiting:Auth:PermitLimit", 30);
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(globalLimit)));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(authLimit)));
    options.OnRejected = async (context, ct) =>
    {
        var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? after : TimeSpan.FromSeconds(60);
        await context.HttpContext.WriteProblemAsync(Error.TooManyRequests("E-AAFR-RATE-LIMITED", "Too many requests.", retry), ct);
    };

    static string PartitionKey(HttpContext c) =>
        $"{c.Connection.RemoteIpAddress}|{c.Request.Headers[CurrentUser.DeviceHeader].FirstOrDefault()}";

    static FixedWindowRateLimiterOptions Window(int limit) => new()
    {
        PermitLimit = limit,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    };
});

services.AddBuildingBlockHealthChecks<IdentityDbContext>(configuration);

// Dev-only CORS so the Angular dev server (ng serve, http://localhost:4200) can call this API cross-origin.
// Production traffic goes through the gateway/same origin, so no policy is registered outside Development.
const string DevFrontendCorsPolicy = "DevFrontend";
if (builder.Environment.IsDevelopment())
{
    services.AddCors(options => options.AddPolicy(DevFrontendCorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));
}

var app = builder.Build();

// Use the host's own logger rather than the static Log.Logger, so several hosts in one process (tests) never share request logging.
app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());
app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    app.UseCors(DevFrontendCorsPolicy);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler(_ => { });
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

if (configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Account Identity v1"));
}

app.Run();

/// <summary>Entry point marker so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
