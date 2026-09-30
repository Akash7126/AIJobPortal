using JobPlatform.ExternalIntegration.Application.DTOs.Mapping;
using JobPlatform.ExternalIntegration.Application.Queries.Mapping;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Mapping;

internal sealed class GetStandardSchemaHandler : IQueryHandler<GetStandardSchemaQuery, StandardSchemaView>
{
    public static readonly StandardSchemaView Schema = new("v1", new[]
    {
        new StandardSchemaFieldView("title", true, "Job title."),
        new StandardSchemaFieldView("summary", true, "Job description/summary."),
        new StandardSchemaFieldView("skills", true, "Comma-separated list of required skills."),
        new StandardSchemaFieldView("contractType", false, "FullTime, PartTime, Contract, Temporary or Internship."),
        new StandardSchemaFieldView("workFormat", false, "Physical, Remote or Hybrid."),
        new StandardSchemaFieldView("applicationDeadline", false, "ISO-8601 date/time."),
        new StandardSchemaFieldView("location", false, "Free-text location."),
        new StandardSchemaFieldView("sourceUrl", false, "Absolute HTTPS URL to the original posting on the partner site.")
    });

    public Task<Result<StandardSchemaView>> Handle(GetStandardSchemaQuery request, CancellationToken ct) =>
        Task.FromResult(Result.Success(Schema));
}
