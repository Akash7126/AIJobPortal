using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Commands.Parsing;

/// <param name="ResumeParsedDataId">One parse to (re)standardise, or null to re-standardise a batch of runs that are not on the latest taxonomy version.</param>
public sealed record StandardizeSkillsCommand(Guid? ResumeParsedDataId, int BatchSize = 100) : ICommand<int>;
