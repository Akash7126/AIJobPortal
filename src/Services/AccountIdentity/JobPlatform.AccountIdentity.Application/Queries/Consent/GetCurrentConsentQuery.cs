using JobPlatform.AccountIdentity.Application.DTOs.Consent;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Queries.Consent;

public sealed record GetCurrentConsentQuery(Guid? GuestId) : IQuery<ConsentStatusDto>;
