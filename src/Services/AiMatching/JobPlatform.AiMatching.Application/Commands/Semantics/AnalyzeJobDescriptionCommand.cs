using JobPlatform.AiMatching.Application.DTOs.Semantics;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Commands.Semantics;

/// <param name="Seed">What the event carried; BC-09's internal API is asked for the full posting and the seed is used when it is unavailable.</param>
public sealed record AnalyzeJobDescriptionCommand(PostingSource Seed) : ICommand;
