using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.Activity;

public sealed record SetActivityRetentionPolicyCommand(int Months) : ActivityCommandRequest, ICommand<RetentionPolicyDto>;
