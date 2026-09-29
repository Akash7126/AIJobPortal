using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.Performance;

/// <summary>Scheduled: samples the telemetry source into FactSystemMetric. Returns the number of samples stored (0 = no data, degraded).</summary>
public sealed record SampleSystemMetricsCommand : ICommand<int>;
