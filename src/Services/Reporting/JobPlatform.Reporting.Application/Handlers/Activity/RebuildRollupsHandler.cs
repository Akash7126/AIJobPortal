using JobPlatform.Reporting.Application.Commands.Activity;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Activity;

internal sealed class RebuildRollupsHandler : ICommandHandler<RebuildRollupsCommand, int>
{
    private readonly IFactStore _facts;

    public RebuildRollupsHandler(IFactStore facts) => _facts = facts;

    public async Task<Result<int>> Handle(RebuildRollupsCommand request, CancellationToken ct) => await _facts.RebuildRollupsAsync(ct);
}
