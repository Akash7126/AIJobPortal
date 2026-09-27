using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Api.Security;
using JobPlatform.AccountIdentity.Application;
using JobPlatform.AccountIdentity.Application.Accounts;
using JobPlatform.AccountIdentity.Application.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Authentication;
using JobPlatform.AccountIdentity.Application.Consent;
using JobPlatform.AccountIdentity.Infrastructure.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPlatform.AccountIdentity.Api.Controllers;

[Route("api/v1/accounts")]
[EnableRateLimiting("auth")]
public sealed class AccountsController : ApiControllerBase
{
    [HttpPost("job-seekers")]
    [AllowAnonymous]
    [ProducesResponseType<RegisteredAccountDto>(StatusCodes.Status201Created)]
    public Task<IActionResult> RegisterJobSeeker(
    RegisterJobSeekerRequest body,
    CancellationToken ct)
    {
        var command = new RegisterJobSeekerAccountCommand(
            body.FullName,
            body.Mobile,
            body.Email,
            body.Password,
            body.PreferredLanguage,
            IdempotencyKey
        );

        return Send(command, CreateResponse, ct);

        IActionResult CreateResponse(RegisteredAccountDto dto)
        {
            return Created(
                $"/api/v1/admin/accounts/{dto.AccountId}",
                dto
            );
        }
    }

    [HttpPost("employers")]
    [AllowAnonymous]
    [ProducesResponseType<RegisteredAccountDto>(StatusCodes.Status201Created)]
    public Task<IActionResult> RegisterEmployer(RegisterEmployerRequest body, CancellationToken ct)
    {
        return Send(new RegisterEmployerAccountCommand(body.CompanyName, body.Email, body.Mobile, body.CompanyId, body.RegistrationNumber, body.Password,
                body.Level, IdempotencyKey),
            dto => Created($"/api/v1/admin/accounts/{dto.AccountId}", dto), ct);
    }

    [HttpPost("external-job-sites")]
    [AllowAnonymous]
    [ProducesResponseType<RegisteredAccountDto>(StatusCodes.Status201Created)]
    public Task<IActionResult> RegisterPartner(RegisterPartnerRequest body, CancellationToken ct)
    {
        return Send(new RegisterPartnerAccountCommand(body.OrganisationName, body.ContactEmail, body.Mobile, body.Identity, body.Password, IdempotencyKey),
            dto => Created($"/api/v1/admin/accounts/{dto.AccountId}", dto), ct);
    }

    [HttpPost("{id:guid}/activation-code")]
    [AllowAnonymous]
    public Task<IActionResult> ResendActivationCode(Guid id, CancellationToken ct)
    {
        return Send(new ResendActivationCodeCommand(id), _ => Accepted(), ct);
    }

    [HttpPost("{id:guid}/activate")]
    [AllowAnonymous]
    public Task<IActionResult> Activate(Guid id, ActivateAccountRequest body, CancellationToken ct)
    {
        return SendNoContent(new ActivateAccountCommand(id, body.Code), ct);
    }

    [HttpPost("{id:guid}/approve-partner")]
    [Authorize(Policy = Policies.Administrator)]
    [ForbiddenCode("E-AUM-FORBIDDEN")]
    public Task<IActionResult> ApprovePartner(Guid id, CancellationToken ct)
    {
        return SendNoContent(new ApprovePartnerAccountCommand(id), ct);
    }
}
