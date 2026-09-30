using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Behaviors;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Diagnostics;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.BuildingBlocks.Infrastructure.UnitTests;

/// <summary>Observability baseline (foundation section 12): spans, handler/outbox/inbox/cache metrics and trace propagation through the outbox.</summary>
public sealed class TelemetryTests : IAsyncLifetime
{
    private readonly List<Activity> _activities = new();
    private readonly List<(string Instrument, double Value, Dictionary<string, object?> Tags)> _measurements = new();
    private readonly object _gate = new();
    private ActivityListener _activityListener = null!;
    private MeterListener _meterListener = null!;
    private SqliteHost _host = null!;

    public Task InitializeAsync()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BuildingBlockTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_gate)
                {
                    _activities.Add(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == BuildingBlockTelemetry.Name)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument.Name, value, tags));
        _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument.Name, value, tags));
        _meterListener.Start();

        _host = new SqliteHost();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
        await _host.DisposeAsync();
    }

    private void Record(string instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copy = tags.ToArray().ToDictionary(t => t.Key, t => t.Value);
        lock (_gate)
        {
            _measurements.Add((instrument, value, copy));
        }
    }

    private List<(string Instrument, double Value, Dictionary<string, object?> Tags)> Measurements(string instrument, string tag, object value)
    {
        lock (_gate)
        {
            return _measurements.Where(m => m.Instrument == instrument && m.Tags.TryGetValue(tag, out var v) && Equals(v, value)).ToList();
        }
    }

    public sealed record TelemetryProbeCommand : ICommand<string>;

    [Fact]
    public async Task LoggingBehavior_EmitsASpanAndAHandlerDurationMeasurement_WithTheOutcome()
    {
        var behavior = new LoggingBehavior<TelemetryProbeCommand, string>(NullLogger<LoggingBehavior<TelemetryProbeCommand, string>>.Instance,
            Substitute.For<ICorrelationContext>());

        await behavior.Handle(new TelemetryProbeCommand(), () => Task.FromResult<Result<string>>("ok"), default);
        await behavior.Handle(new TelemetryProbeCommand(), () => Task.FromResult<Result<string>>(Error.Conflict("E-X", "m")), default);
        var thrown = () => behavior.Handle(new TelemetryProbeCommand(), () => throw new InvalidOperationException("boom"), default);
        await thrown.Should().ThrowAsync<InvalidOperationException>();

        var durations = Measurements("jobplatform.handler.duration", "request", nameof(TelemetryProbeCommand));
        durations.Select(d => d.Tags["outcome"]).Should().BeEquivalentTo(new object[] { "success", "Conflict", "exception" });
        lock (_gate)
        {
            var spans = _activities.Where(a => a.OperationName == "handle " + nameof(TelemetryProbeCommand)).ToList();
            spans.Should().HaveCount(3);
            spans.Should().ContainSingle(a => a.Status == ActivityStatusCode.Error);
            spans.Should().Contain(a => (string?)a.GetTagItem("error.code") == "E-X");
        }
    }

    [Fact]
    public async Task CacheLookups_CountHitsAndMisses_ByKeyKind()
    {
        var cache = new InMemoryCacheStore(new FakeTimeProvider(), Options.Create(new CacheOptions()));
        await cache.SetJsonAsync("telemetrykind:1", "v", TimeSpan.FromMinutes(1));

        (await cache.GetJsonAsync<string>("telemetrykind:1")).Should().Be("v");
        (await cache.GetJsonAsync<string>("telemetrykind:2")).Should().BeNull();

        Measurements("jobplatform.cache.hits", "kind", "telemetrykind").Should().ContainSingle();
        Measurements("jobplatform.cache.misses", "kind", "telemetrykind").Should().ContainSingle();
    }

    [Fact]
    public async Task Outbox_CarriesTheTraceParent_AndThePublishSpanContinuesTheRequestTrace_WithLagAndCounters()
    {
        using var requestActivity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var traceId = requestActivity.TraceId;
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            db.Orders.Add(TestOrder.Place("T-1", _host.Clock.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync();
        }

        var row = await _host.WithDb(db => db.Set<OutboxMessage>().AsNoTracking().SingleAsync());
        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Headers)!;
        headers[MessagingHeaders.TraceParent].Should().Contain(traceId.ToString());

        _host.Clock.Advance(TimeSpan.FromSeconds(7));
        requestActivity.Stop();
        await _host.Services.GetRequiredService<OutboxProcessor<TestDbContext>>().ProcessBatchAsync(default);

        lock (_gate)
        {
            var publish = _activities.Single(a => a.OperationName == "publish OrderPlaced" && a.TraceId == traceId);
            publish.Kind.Should().Be(ActivityKind.Producer);
            publish.ParentSpanId.Should().Be(requestActivity.SpanId);
            publish.GetTagItem("messaging.destination.name").Should().Be(row.Exchange);
        }

        Measurements("jobplatform.outbox.lag", "type", "OrderPlaced").Where(m => m.Value >= 7).Should()
            .NotBeEmpty("the row waited 7 s of fake time before it was published");
        Measurements("jobplatform.outbox.published", "type", "OrderPlaced").Should().NotBeEmpty();
    }

    [Fact]
    public async Task Outbox_CountsFailuresAndDeadLetters()
    {
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            db.Orders.Add(TestOrder.Place("T-2", _host.Clock.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync();
        }

        _host.Bus.FailWith = new InvalidOperationException("broker down");
        var processor = _host.Services.GetRequiredService<OutboxProcessor<TestDbContext>>();
        var before = Measurements("jobplatform.outbox.dead_lettered", "type", "OrderPlaced").Count;
        for (var i = 0; i < 3; i++)
        {
            await processor.ProcessBatchAsync(default);
            _host.Clock.Advance(TimeSpan.FromMinutes(10));
        }

        Measurements("jobplatform.outbox.failed", "type", "OrderPlaced").Should().NotBeEmpty();
        Measurements("jobplatform.outbox.dead_lettered", "type", "OrderPlaced").Count.Should().BeGreaterThan(before, "MaxAttempts is 3 in the test host");
    }

    [Fact]
    public void ParentOf_ReturnsDefaultForMissingOrMalformedTraceParents()
    {
        BuildingBlockTelemetry.ParentOf(new Dictionary<string, string>()).Should().Be(default(ActivityContext));
        BuildingBlockTelemetry.ParentOf(new Dictionary<string, string> { [MessagingHeaders.TraceParent] = "garbage" }).Should().Be(default(ActivityContext));
        BuildingBlockTelemetry.ParentOf(new Dictionary<string, string>
        {
            [MessagingHeaders.TraceParent] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
        }).TraceId.ToString().Should().Be("0af7651916cd43dd8448eb211c80319c");
    }
}
