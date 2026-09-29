using JobPlatform.EmployerOnboarding.Application.DTOs.Registration;
using JobPlatform.EmployerOnboarding.Domain;

namespace JobPlatform.EmployerOnboarding.Application.Commands.Registration;

public sealed record SubmitEmployerLevel2Command(
    string CompanyName, string CompanyId, string RegistrationNumber,
    string Website, string Industry, CompanySize Size, string Governorate, string City, string? Street, string Description)
    : EmployerCommand<EmployerRegistrationView>;
