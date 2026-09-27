using System.Diagnostics;
using JobPlatform.BuildingBlocks.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

/// <summary>The host wires OpenTelemetry (foundation section 12): providers exist, requests and handlers are traced, and the outbox row remembers the trace.</summary>
public class TelemetryApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TelemetryApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void TheHost_RegistersTracerAndMeterProviders()
    {
        _factory.Services.GetService<TracerProvider>().Should().NotBeNull();
        _factory.Services.GetService<MeterProvider>().Should().NotBeNull();
    }

    [Fact]
    public async Task ARequest_ProducesAHandlerSpanUnderTheHttpRequestTrace_AndTheOutboxRowCarriesTheTraceParent()
    {
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BuildingBlockTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = span =>
            {
                lock (spans)
                {
                    spans.Add(span);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        var client = new ApiClient(_factory);

        var (id, _, _) = await client.RegisterJobSeekerAsync();

        Activity[] snapshot;
        lock (spans)
        {
            snapshot = spans.Where(s => s.OperationName == "handle RegisterJobSeekerAccountCommand").ToArray();
        }

        snapshot.Should().NotBeEmpty();
        var outbox = (await _factory.OutboxAsync("AccountCreated", id.ToString())).Single();
        outbox.Headers.Should().Contain("traceparent", "the publisher continues the request trace even from the background processor");
    }
}
