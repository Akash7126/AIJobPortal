using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.JobDataFlows;

namespace JobPlatform.ExternalIntegration.Application.Validators.JobDataFlows;

public sealed class PushJobDataValidator : AbstractValidator<PushJobDataCommand>
{
    public PushJobDataValidator()
    {
        RuleFor(c => c.SourceJobId).NotEmpty().MaximumLength(100).WithErrorCode("VAL.SourceJobId.Required");
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Title.Required");
        RuleFor(c => c.Summary).NotEmpty().MaximumLength(5000).WithErrorCode("VAL.Summary.Required");
        RuleFor(c => c.Skills).NotEmpty().WithErrorCode("VAL.Skills.Required");
        RuleFor(c => c.ContractType).NotEmpty().WithErrorCode("VAL.ContractType.Required");
        RuleFor(c => c.WorkFormat).NotEmpty().WithErrorCode("VAL.WorkFormat.Required");
        RuleFor(c => c.Location).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Location.Required");
        RuleFor(c => c.SourceUrl).Must(BeAnAbsoluteHttpsUrl).When(c => c.SourceUrl is not null).WithErrorCode("VAL.SourceUrl.Invalid");
    }

    private static bool BeAnAbsoluteHttpsUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
