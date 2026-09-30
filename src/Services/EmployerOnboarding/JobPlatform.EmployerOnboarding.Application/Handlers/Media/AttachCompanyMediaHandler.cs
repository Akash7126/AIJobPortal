using JobPlatform.EmployerOnboarding.Application.Commands.Media;
using JobPlatform.EmployerOnboarding.Application.DTOs.Media;
using JobPlatform.EmployerOnboarding.Application.Interfaces;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Handlers.Media;

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
