using JobPlatform.EmployerOnboarding.Application.DTOs.Media;
using JobPlatform.EmployerOnboarding.Domain;

namespace JobPlatform.EmployerOnboarding.Application.Commands.Media;

public sealed record AttachCompanyMediaCommand(MediaKind Kind, string FileName, string ContentType, long SizeBytes, byte[] Content) : EmployerCommand<CompanyMediaView>;
