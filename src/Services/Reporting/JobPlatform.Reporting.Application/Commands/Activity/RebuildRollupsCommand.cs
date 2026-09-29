using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.Activity;

/// <summary>Ops command: rebuilds every daily rollup from the fact table. Not exposed over HTTP.</summary>
public sealed record RebuildRollupsCommand : ICommand<int>;
