using JobPlatform.AiMatching.Application.DTOs.Parsing;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Queries.Parsing;

public sealed record GetParsedProfileDataQuery : SeekerRequest, IQuery<ParsedProfileDataDto>;
