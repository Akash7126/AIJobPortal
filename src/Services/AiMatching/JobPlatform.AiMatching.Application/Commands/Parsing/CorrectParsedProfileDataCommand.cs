using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Commands.Parsing;

public sealed record CorrectParsedProfileDataCommand(string Field, string Value) : SeekerRequest, ICommand;
