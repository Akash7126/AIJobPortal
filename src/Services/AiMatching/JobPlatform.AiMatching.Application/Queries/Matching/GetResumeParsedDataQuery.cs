using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Queries.Matching;

/// <summary>Internal (BC-04): the parse result of one resume.</summary>
public sealed record GetResumeParsedDataQuery(Guid ResumeParsedDataId) : ServiceRequest, IQuery<ResumeParsedDataDto>;
