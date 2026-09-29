using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Commands.Parsing;

public sealed record CorrectParsedProfileDataCommand(string Field, string Value) : SeekerRequest, ICommand;
