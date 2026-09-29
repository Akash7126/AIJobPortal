using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.Performance;

/// <summary>Scheduled (leader-locked): evaluates every enabled rule over its window and raises alerts. Returns the number raised.</summary>
public sealed record EvaluatePerformanceAlertsCommand : ICommand<int>;
