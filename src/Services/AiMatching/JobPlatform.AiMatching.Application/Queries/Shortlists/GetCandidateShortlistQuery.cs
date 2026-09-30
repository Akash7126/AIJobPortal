using JobPlatform.AiMatching.Application.DTOs.Shortlists;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Queries.Shortlists;

public sealed record GetCandidateShortlistQuery(Guid JobPostingId, Guid ShortlistId) : EmployerRequest, IQuery<ShortlistDto>;
