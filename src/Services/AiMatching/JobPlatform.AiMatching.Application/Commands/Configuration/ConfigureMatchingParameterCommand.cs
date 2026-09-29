using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Commands.Configuration;

public sealed record ConfigureMatchingParameterCommand(
    decimal SkillOverlap, decimal Education, decimal Training, decimal Location, decimal Experience, decimal Salary, string? IfMatch = null) : AdminRequest, ICommand;
