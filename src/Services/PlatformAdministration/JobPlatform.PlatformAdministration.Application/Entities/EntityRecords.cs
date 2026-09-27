using FluentValidation;
using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.PlatformAdministration.Application.Entities;

/// <summary>US-3.1.4-02: an administrator creates a job seeker, employer or job offering record. Accepts Idempotency-Key.</summary>
public sealed record CreatePlatformEntityRecordCommand(PlatformEntityType EntityType, Dictionary<string, string>? Core, string? IdempotencyKey = null)
    : AdminCommand<EntityRecordView>, IIdempotentCommand, IConflictAwareCommand
{
    public string UniqueViolationErrorCode => ErrorCodes.Duplicate;
}

public sealed class CreatePlatformEntityRecordValidator : AbstractValidator<CreatePlatformEntityRecordCommand>
{
    private const int MaxFields = 20;
    private const int MaxValueLength = 200;

    public CreatePlatformEntityRecordValidator()
    {
        RuleFor(c => c.EntityType).IsInEnum().WithErrorCode("VAL.EntityType.Invalid");
        RuleFor(c => c.Core).NotNull().WithErrorCode("VAL.Core.Required");
        RuleFor(c => c.Core!).Must(core => core.Count <= MaxFields).WithErrorCode("VAL.Core.TooManyFields").When(c => c.Core is not null);
        RuleFor(c => c.Core!).Must(core => core.Values.All(v => v is null || v.Length <= MaxValueLength))
            .WithErrorCode("VAL.Core.ValueTooLong").When(c => c.Core is not null);

        // Required fields mirror the self-service counterpart (INV-01, shared specification in the domain).
        RuleFor(c => c).Custom((command, context) =>
        {
            if (command.Core is null || !Enum.IsDefined(command.EntityType))
            {
                return;
            }

            var core = EntityCore.From(command.Core);
            foreach (var field in EntityCoreSpecification.MissingFields(command.EntityType, core))
            {
                context.AddFailure(new FluentValidation.Results.ValidationFailure($"Core.{field}", "required") { ErrorCode = $"VAL.{Capitalise(field)}.Required" });
            }

            if (core.Get(EntityCoreSpecification.Mobile) is { } mobile && !MobileNumber.TryCreate(mobile, out _))
            {
                Fail(context, EntityCoreSpecification.Mobile, "VAL.MobileNumber.Invalid");
            }

            if (core.Get(EntityCoreSpecification.Email) is { } email && !Email.TryCreate(email, out _))
            {
                Fail(context, EntityCoreSpecification.Email, "VAL.Email.Invalid");
            }
        });
    }

    private static void Fail(FluentValidation.ValidationContext<CreatePlatformEntityRecordCommand> context, string field, string code)
    {
        context.AddFailure(new FluentValidation.Results.ValidationFailure($"Core.{field}", "invalid") { ErrorCode = code });
    }

    private static string Capitalise(string field) => char.ToUpperInvariant(field[0]) + field[1..];
}

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

public sealed record GetEntityRecordQuery(Guid Id) : AdminQuery<EntityRecordView>;

internal sealed class GetEntityRecordHandler : IQueryHandler<GetEntityRecordQuery, EntityRecordView>
{
    private readonly IAdminReadStore _store;

    public GetEntityRecordHandler(IAdminReadStore store) => _store = store;

    public async Task<Result<EntityRecordView>> Handle(GetEntityRecordQuery request, CancellationToken ct) =>
        await _store.GetEntityRecordAsync(request.Id, ct) is { } view
            ? view
            : Error.NotFound(ErrorCodes.NotFound, "The entity record was not found.");
}
