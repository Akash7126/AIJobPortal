using JobPlatform.BuildingBlocks.Infrastructure.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace JobPlatform.AccountIdentity.Api;

/// <summary>
/// OpenTelemetry traces and metrics (foundation section 12): ASP.NET Core and HttpClient instrumentation plus the shared building-block
/// source/meter (handler duration, outbox/inbox lag, cache hit ratio). Nothing leaves the process unless <c>Telemetry:Otlp:Endpoint</c> is set,
/// so local runs and tests stay self-contained. Trace context also travels with outbox messages (<c>traceparent</c> header).
/// </summary>
public static class TelemetrySetup
{
    public const string ServiceName = "JobPlatform.AccountIdentity";

    public static IServiceCollection AddAccountIdentityTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue("Telemetry:Enabled", true))
        {
            return services;
        }

        var endpoint = configuration["Telemetry:Otlp:Endpoint"];
        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName));

        telemetry.WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource(BuildingBlockTelemetry.Name);
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                tracing.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
            }
        });

        telemetry.WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(BuildingBlockTelemetry.Name);
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                metrics.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
            }
        });

        return services;
    }
}
