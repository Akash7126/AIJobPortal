using JobPlatform.AccountIdentity.Application.DTOs.Consent;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AccountIdentity.Application.Queries.Consent;

public sealed record GetCurrentConsentQuery(Guid? GuestId) : IQuery<ConsentStatusDto>;
