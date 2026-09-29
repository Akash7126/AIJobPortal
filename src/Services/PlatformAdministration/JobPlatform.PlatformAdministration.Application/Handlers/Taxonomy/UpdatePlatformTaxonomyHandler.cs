using JobPlatform.PlatformAdministration.Application.Commands.Taxonomy;
using JobPlatform.PlatformAdministration.Application.DTOs.Common;
using JobPlatform.PlatformAdministration.Application.Taxonomy;
using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Taxonomy;

internal sealed class UpdatePlatformTaxonomyHandler : ICommandHandler<UpdatePlatformTaxonomyCommand, VersionResult>
{
    private readonly IPlatformTaxonomyRepository _taxonomies;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public UpdatePlatformTaxonomyHandler(IPlatformTaxonomyRepository taxonomies, ICurrentUser user, TimeProvider clock)
    {
        _taxonomies = taxonomies;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<VersionResult>> Handle(UpdatePlatformTaxonomyCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var type = TaxonomyTypes.Normalise(request.Type);
        var taxonomy = await _taxonomies.GetByTypeAsync(type, ct);
        if (taxonomy is null)
        {
            taxonomy = PlatformTaxonomy.Create(type, now);
            _taxonomies.Add(taxonomy);
            _taxonomies.AddSnapshot(taxonomy.CreateSnapshot(now));
        }

        var changes = request.Changes!.Select(c =>
        {
            TaxonomyRules.TryParseOp(c.Op, out var kind);
            return new TaxonomyChange(kind, c.Code, c.Name is null ? null : new LocalizedText(c.Name.Ar.Trim(), c.Name.En.Trim()), c.ParentCode,
                c.Synonyms?.Select(s => s.Trim()).ToArray(), c.IsActive);
        }).ToList();

        taxonomy.ApplyChanges(changes, ActorFactory.From(_user), now);
        _taxonomies.AddSnapshot(taxonomy.CreateSnapshot(now));
        return new VersionResult(taxonomy.TaxonomyVersion);
    }
}
