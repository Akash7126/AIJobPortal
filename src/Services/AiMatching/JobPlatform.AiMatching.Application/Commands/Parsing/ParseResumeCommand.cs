using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Commands.Parsing;

public sealed record ParseResumeCommand(Guid ResumeId, Guid ProfileId, string Format, long SizeBytes, string Sha256) : ICommand;
