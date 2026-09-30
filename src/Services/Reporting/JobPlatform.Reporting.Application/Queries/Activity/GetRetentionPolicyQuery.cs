using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Activity;

public sealed record GetRetentionPolicyQuery : ActivityRequest, IQuery<RetentionPolicyDto>;
