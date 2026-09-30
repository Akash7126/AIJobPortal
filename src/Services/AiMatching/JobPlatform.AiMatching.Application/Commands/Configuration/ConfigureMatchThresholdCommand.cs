using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Commands.Configuration;

public sealed record ConfigureMatchThresholdCommand(decimal ThresholdPercent, string? IfMatch = null) : AdminRequest, ICommand;
