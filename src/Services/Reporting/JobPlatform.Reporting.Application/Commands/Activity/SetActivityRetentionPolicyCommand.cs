using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Commands.Activity;

public sealed record SetActivityRetentionPolicyCommand(int Months) : ActivityCommandRequest, ICommand<RetentionPolicyDto>;
