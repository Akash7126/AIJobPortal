using JobPlatform.Reporting.Application.Commands.Performance;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Performance;

internal sealed class EvaluatePerformanceAlertsHandler : ICommandHandler<EvaluatePerformanceAlertsCommand, int>
{
    private readonly AlertEvaluator _evaluator;

    public EvaluatePerformanceAlertsHandler(AlertEvaluator evaluator) => _evaluator = evaluator;

    public async Task<Result<int>> Handle(EvaluatePerformanceAlertsCommand request, CancellationToken ct) => await _evaluator.EvaluateAsync(ct);
}
