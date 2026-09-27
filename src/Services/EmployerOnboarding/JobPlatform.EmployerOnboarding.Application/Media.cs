using FluentValidation;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application;

public sealed record AttachCompanyMediaCommand(MediaKind Kind, string FileName, string ContentType, long SizeBytes, byte[] Content) : EmployerCommand<CompanyMediaView>;

public sealed class AttachCompanyMediaValidator : AbstractValidator<AttachCompanyMediaCommand>
{
    public AttachCompanyMediaValidator()
    {
        RuleFor(c => c.Kind).IsInEnum().WithErrorCode("VAL.Kind.Invalid");
        RuleFor(c => c.FileName).NotEmpty().MaximumLength(260).WithErrorCode("VAL.FileName.Required");
        RuleFor(c => c.ContentType).NotEmpty().WithErrorCode("VAL.ContentType.Required");
        RuleFor(c => c.SizeBytes).GreaterThan(0).LessThanOrEqualTo(CompanyMediaAndDocument.MaxSizeBytes).WithErrorCode("VAL.SizeBytes.TooLarge");
    }
}

internal sealed class AttachCompanyMediaHandler : ICommandHandler<AttachCompanyMediaCommand, CompanyMediaView>
{
    private readonly ICompanyMediaRepository _media;
    private readonly IFileStorage _storage;
    private readonly IMalwareScanner _scanner;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public AttachCompanyMediaHandler(ICompanyMediaRepository media, IFileStorage storage, IMalwareScanner scanner, ICurrentUser user, TimeProvider clock)
    {
        _media = media;
        _storage = storage;
        _scanner = scanner;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<CompanyMediaView>> Handle(AttachCompanyMediaCommand request, CancellationToken ct)
    {
        var employerAccountId = _user.UserId!.Value;
        var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(request.Content));
        if (await _media.GetByHashAsync(employerAccountId, sha256, ct) is { } existing)
        {
            return ToView(existing);
        }

        await using var stream = new MemoryStream(request.Content);
        var scan = await _scanner.ScanAsync(stream, request.ContentType, ct);
        if (!scan.Clean)
        {
            return Error.BusinessRule(Domain.Common.ErrorCodes.MediaUnsupportedFormat, scan.Reason ?? "The file failed a malware scan.");
        }

        stream.Position = 0;
        var storageKey = await _storage.SaveAsync(stream, request.FileName, request.ContentType, ct);
        var file = new FileReference(storageKey, request.FileName, request.SizeBytes, request.ContentType, sha256);
        var media = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerAccountId, request.Kind, file, ActorFactory.From(_user),
            _clock.GetUtcNow().UtcDateTime);
        _media.Add(media);
        return ToView(media);
    }

    internal static CompanyMediaView ToView(CompanyMediaAndDocument m) =>
        new(m.Id, m.Kind.ToString(), m.File.FileName, m.File.ContentType, m.File.SizeBytes, m.IsPrimaryLogo, m.UploadedAtUtc);
}

public sealed record RemoveCompanyMediaCommand(Guid CompanyMediaId) : EmployerCommand<Unit>;

internal sealed class RemoveCompanyMediaHandler : ICommandHandler<RemoveCompanyMediaCommand, Unit>
{
    private readonly ICompanyMediaRepository _media;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RemoveCompanyMediaHandler(ICompanyMediaRepository media, ICurrentUser user, TimeProvider clock)
    {
        _media = media;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(RemoveCompanyMediaCommand request, CancellationToken ct)
    {
        var media = await _media.GetByIdAsync(request.CompanyMediaId, ct);
        if (media is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The media item was not found.");
        }

        media.Remove(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

public sealed record SetPrimaryLogoCommand(Guid CompanyMediaId) : EmployerCommand<Unit>;

internal sealed class SetPrimaryLogoHandler : ICommandHandler<SetPrimaryLogoCommand, Unit>
{
    private readonly ICompanyMediaRepository _media;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SetPrimaryLogoHandler(ICompanyMediaRepository media, ICurrentUser user, TimeProvider clock)
    {
        _media = media;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(SetPrimaryLogoCommand request, CancellationToken ct)
    {
        var target = await _media.GetByIdAsync(request.CompanyMediaId, ct);
        if (target is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The media item was not found.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var actor = ActorFactory.From(_user);
        foreach (var other in await _media.ListByEmployerAsync(target.EmployerAccountId, ct))
        {
            if (other.IsPrimaryLogo && other.Id != target.Id)
            {
                other.UnsetPrimaryLogo();
            }
        }

        target.SetAsPrimaryLogo(actor, now);
        return Result.Success();
    }
}

public sealed record ListCompanyMediaQuery : EmployerQuery<IReadOnlyList<CompanyMediaView>>;

internal sealed class ListCompanyMediaHandler : IQueryHandler<ListCompanyMediaQuery, IReadOnlyList<CompanyMediaView>>
{
    private readonly IEmployerReadStore _store;
    private readonly ICurrentUser _user;

    public ListCompanyMediaHandler(IEmployerReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<CompanyMediaView>>> Handle(ListCompanyMediaQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListMediaAsync(_user.UserId!.Value, ct));
}
