using FluentValidation;
using JobPlatform.AiMatching.Application.Commands.Parsing;
using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Application.Validators.Parsing;

public sealed class CorrectParsedProfileDataValidator : AbstractValidator<CorrectParsedProfileDataCommand>
{
    public static int MaxLength(ParsedFieldName name) => name switch
    {
        ParsedFieldName.PersonalDetails or ParsedFieldName.ContactInformation => 1000,
        ParsedFieldName.Skills => 2000,
        _ => 4000
    };

    public CorrectParsedProfileDataValidator()
    {
        RuleFor(x => x.Field).Must(f => Enum.TryParse<ParsedFieldName>(f, true, out _)).WithErrorCode("VAL.Field.NotAllowed");
        RuleFor(x => x.Value).NotEmpty().WithErrorCode("VAL.Value.Required");
        RuleFor(x => x).Must(x => !Enum.TryParse<ParsedFieldName>(x.Field, true, out var name) || (x.Value?.Length ?? 0) <= MaxLength(name))
            .OverridePropertyName("Value").WithErrorCode("VAL.Value.TooLong");
    }
}
