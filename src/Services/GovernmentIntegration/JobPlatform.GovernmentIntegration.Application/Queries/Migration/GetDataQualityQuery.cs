using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

namespace JobPlatform.GovernmentIntegration.Application.Queries.Migration;

public sealed record GetDataQualityQuery(Guid BatchId) : AdminQuery<DataQualityView>;
