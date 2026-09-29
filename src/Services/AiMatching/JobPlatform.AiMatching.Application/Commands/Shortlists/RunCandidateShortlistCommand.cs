using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Commands.Shortlists;

/// <summary>Worker side: computes and completes a queued shortlist.</summary>
public sealed record RunCandidateShortlistCommand(Guid ShortlistId) : ICommand;
