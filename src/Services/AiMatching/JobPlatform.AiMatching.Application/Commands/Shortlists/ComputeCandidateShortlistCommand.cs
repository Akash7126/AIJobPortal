using JobPlatform.AiMatching.Application.DTOs.Shortlists;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Commands.Shortlists;

/// <summary>US-3.3.1-02: request the top-N shortlist of an owned posting. Batch work: answered 202, computed by the worker, polled with GetCandidateShortlistQuery.</summary>
public sealed record ComputeCandidateShortlistCommand(Guid JobPostingId, int? Size = null) : EmployerRequest, ICommand<ShortlistDto>;
