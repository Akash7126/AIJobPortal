using JobPlatform.Reporting.Application.Commands.Performance;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Performance;

internal sealed class SampleSystemMetricsHandler : ICommandHandler<SampleSystemMetricsCommand, int>
{
    private readonly MetricsSampler _sampler;

    public SampleSystemMetricsHandler(MetricsSampler sampler) => _sampler = sampler;

    public async Task<Result<int>> Handle(SampleSystemMetricsCommand request, CancellationToken ct) => await _sampler.SampleAsync(ct);
}
