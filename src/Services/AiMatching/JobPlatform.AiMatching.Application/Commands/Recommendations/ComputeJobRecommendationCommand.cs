using JobPlatform.AiMatching.Application.DTOs.Recommendations;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Commands.Recommendations;

/// <summary>Computes and stores a recommendation list (scheduled weekly + on demand). Internal: no caller identity, the actor id is carried.</summary>
public sealed record ComputeJobRecommendationCommand(Guid ProfileId, Guid ActorId) : ICommand<JobRecommendationDto>;
