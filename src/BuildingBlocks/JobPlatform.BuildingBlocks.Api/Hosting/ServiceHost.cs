using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Diagnostics;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace JobPlatform.BuildingBlocks.Api.Hosting;

/// <param name="ServiceName">Telemetry service name, e.g. JobPlatform.AuditLogging.</param>
/// <param name="Title">OpenAPI title.</param>
/// <param name="ControllersAssembly">Assembly holding the controllers (the service's Api assembly).</param>
public sealed record ServiceInfo(string ServiceName, string Title, string Description, Assembly ControllersAssembly);

/// <summary>Composition-root helpers shared by every BC Api: Serilog, building blocks, JWT authentication, controllers, ProblemDetails, rate limiting, OpenAPI, telemetry.</summary>
public static class ServiceHost
{
    public const string BearerScheme = "Bearer";

    // Dev-only CORS so the Angular dev server (ng serve, http://localhost:4200) can call any BC's Api cross-origin.
    // Production traffic goes through the gateway/same origin, so no policy is registered outside Development.
    public const string DevFrontendCorsPolicy = "DevFrontend";

    public static WebApplicationBuilder AddJobPlatformService(this WebApplicationBuilder builder, ServiceInfo info,
        params IReadOnlyDictionary<string, (string En, string Ar)>[] errorMessages)
    {
        // Serilog is the single logging pipeline. Sinks registered in DI (ILogEventSink) are picked up by ReadFrom.Services.
        builder.Host.UseSerilog((context, services, logger) => logger
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddHttpContextAccessor();
        services.AddBuildingBlocks(configuration);
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<IErrorMessageLocalizer>(_ => new CatalogErrorMessageLocalizer(errorMessages));
        services.AddJobPlatformAuthentication(configuration);

        services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .AddApplicationPart(info.ControllersAssembly)
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

        services.AddJobPlatformOpenApi(info);
        services.AddJobPlatformTelemetry(configuration, info.ServiceName);

        if (builder.Environment.IsDevelopment())
        {
            services.AddCors(options => options.AddPolicy(DevFrontendCorsPolicy, policy => policy
                .WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod()));
        }

        // Rate limiting (THR-055): global per source; a stricter policy for anonymous/public surfaces.
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, config) =>
        {
            var globalLimit = config.GetValue("RateLimiting:Global:PermitLimit", 600);
            var publicLimit = config.GetValue("RateLimiting:Public:PermitLimit", 120);
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(globalLimit)));
            options.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(publicLimit)));
            options.OnRejected = async (context, ct) =>
            {
                var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? after : TimeSpan.FromSeconds(60);
                await context.HttpContext.WriteProblemAsync(Error.TooManyRequests("E-RATE-LIMITED", "Too many requests.", retry), ct);
            };

            static string PartitionKey(HttpContext c) =>
                $"{c.Connection.RemoteIpAddress}|{c.Request.Headers[HttpCurrentUser.DeviceHeader].FirstOrDefault()}";

            static FixedWindowRateLimiterOptions Window(int limit) => new() { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 };
        });

        return builder;
    }

    public static WebApplication UseJobPlatformService(this WebApplication app, ServiceInfo info)
    {
        // Use the host's own logger rather than the static Log.Logger, so several hosts in one process (tests) never share request logging.
        app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());
        app.UseForwardedHeaders();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler(_ => { });
        if (app.Environment.IsDevelopment())
        {
            app.UseCors(DevFrontendCorsPolicy);
        }

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health/live", new() { Predicate = check => check.Tags.Contains("live") });
        app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

        if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
        {
            app.MapOpenApi().AllowAnonymous();
            app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", info.Title + " v1"));
        }

        return app;
    }

    public static IServiceCollection AddJobPlatformOpenApi(this IServiceCollection services, ServiceInfo info)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo { Title = info.Title, Version = "v1", Description = info.Description };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token issued by BC-03 Account Identity (POST /api/v1/auth/login or POST /oauth/token)."
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var requiresAuth = metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any()
                                   && !metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any();
                operation.Responses ??= new OpenApiResponses();
                if (requiresAuth)
                {
                    operation.Security ??= new List<OpenApiSecurityRequirement>();
                    operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = new List<string>() });
                    AddProblem(operation, "401", "Missing, expired or invalid access token (E-AAFR-UNAUTHORIZED).");
                    AddProblem(operation, "403", "The caller may not perform this action (E-AAFR-FORBIDDEN).");
                }

                if (context.Description.HttpMethod is "POST" or "PUT" or "PATCH" or "DELETE")
                {
                    AddProblem(operation, "400", "Malformed request: `errors` lists the field codes (VAL.*).");
                    AddProblem(operation, "409", "The request conflicts with the current state (duplicates, concurrency).");
                    AddProblem(operation, "422", "A business rule refused the request.");
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }

    private static void AddProblem(OpenApiOperation operation, string status, string description)
    {
        operation.Responses ??= new OpenApiResponses();
        if (operation.Responses.ContainsKey(status))
        {
            return;
        }

        operation.Responses[status] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() }
        };
    }

    /// <summary>OpenTelemetry traces and metrics (foundation section 12); nothing leaves the process unless Telemetry:Otlp:Endpoint is set.</summary>
    public static IServiceCollection AddJobPlatformTelemetry(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        if (!configuration.GetValue("Telemetry:Enabled", true))
        {
            return services;
        }

        var endpoint = configuration["Telemetry:Otlp:Endpoint"];
        var telemetry = services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService(serviceName));
        telemetry.WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource(BuildingBlockTelemetry.Name);
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                tracing.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
            }
        });
        telemetry.WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddMeter(BuildingBlockTelemetry.Name);
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                metrics.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
            }
        });
        return services;
    }
}

/// <summary>Base of every BC controller: thin, dispatches one request and maps the Result to HTTP (problem+json on failure).</summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ISender Sender
    {
        get
        {
            return HttpContext.RequestServices.GetRequiredService<ISender>();
        }
    }

    protected string? IdempotencyKey
    {
        get
        {
            return Request.Headers["Idempotency-Key"].FirstOrDefault();
        }
    }

    protected string? IfMatch
    {
        get
        {
            return Request.Headers.IfMatch.FirstOrDefault();
        }
    }

    protected async Task<IActionResult> Send<T>(IRequest<T> request, Func<T, IActionResult> onSuccess, CancellationToken ct)
    {
        return (await Sender.Send(request, ct)).ToActionResult(HttpContext, onSuccess);
    }

    protected Task<IActionResult> Send<T>(IRequest<T> request, CancellationToken ct)
    {
        return Send(request, value => Ok(value), ct);
    }

    protected Task<IActionResult> SendNoContent(IRequest<Unit> request, CancellationToken ct)
    {
        return Send(request, _ => NoContent(), ct);
    }

    protected Task<IActionResult> SendCreated<T>(IRequest<T> request, Func<T, string> location, CancellationToken ct)
    {
        return Send(request, value => Created(location(value), value), ct);
    }

    /// <summary>Adds the RowVersion-derived ETag header (foundation section 11) to the response.</summary>
    protected void SetETag(byte[]? rowVersion)
    {
        if (rowVersion is { Length: > 0 })
        {
            Response.Headers.ETag = ETag.From(rowVersion);
        }
    }
}
