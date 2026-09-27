using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.PlatformAdministration.Application;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence;

/// <summary>
/// Implements "later save wins" (US-3.1.4-06/07/08 AC-03) for settings, reference files and taxonomies. Registered outside the unit-of-work
/// behavior: when a concurrent save won the optimistic race (E-CONCURRENCY-CONFLICT) the request is repeated on fresh state instead of being rejected,
/// so the last request to run is the one that is kept. Every attempt still appends its own history / version.
/// </summary>
public sealed class LaterSaveWinsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public const int MaxAttempts = 5;
    private const string ConflictCode = "E-CONCURRENCY-CONFLICT";

    private readonly AdminDbContext _db;
    private readonly DomainEventBuffer _events;
    private readonly ILogger<LaterSaveWinsBehavior<TRequest, TResponse>> _logger;

    public LaterSaveWinsBehavior(AdminDbContext db, DomainEventBuffer events, ILogger<LaterSaveWinsBehavior<TRequest, TResponse>> logger)
    {
        _db = db;
        _events = events;
        _logger = logger;
    }

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ILaterSaveWinsCommand)
        {
            return await next();
        }

        for (var attempt = 1; ; attempt++)
        {
            var result = await next();
            if (result.IsSuccess || result.Error?.Code != ConflictCode || attempt >= MaxAttempts)
            {
                return result;
            }

            _logger.LogInformation("{Request} lost an optimistic-concurrency race (attempt {Attempt}); repeating on fresh state (later save wins)", typeof(TRequest).Name, attempt);
            _db.ChangeTracker.Clear();
            _events.Drain();
        }
    }
}
