using JobPlatform.PlatformAdministration.Application.Commands.Entities;
using JobPlatform.PlatformAdministration.Application.DTOs.Entities;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Entities;

internal sealed class CreatePlatformEntityRecordHandler : ICommandHandler<CreatePlatformEntityRecordCommand, EntityRecordView>
{
    private readonly IPlatformEntityRecordRepository _records;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public CreatePlatformEntityRecordHandler(IPlatformEntityRecordRepository records, ICurrentUser user, TimeProvider clock)
    {
        _records = records;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<EntityRecordView>> Handle(CreatePlatformEntityRecordCommand request, CancellationToken ct)
    {
        var core = EntityCore.From(request.Core);
        var key = EntityCoreSpecification.IdentityKeyOf(request.EntityType, core);
        var exists = key is not null && await _records.ExistsByKeyAsync(request.EntityType, key, ct);
        var record = PlatformEntityRecord.Create(Guid.NewGuid(), request.EntityType, request.Core, ActorFactory.From(_user), exists, _clock.GetUtcNow().UtcDateTime);
        _records.Add(record);
        return new EntityRecordView(record.Id, record.EntityType.ToString(), record.IdentityKey, record.Status.ToString(), record.CreatedBy, record.CreatedAtUtc);
    }
}
