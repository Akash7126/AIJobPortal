using FluentValidation;
using JobPlatform.PlatformAdministration.Application.Commands.Taxonomy;
using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Taxonomy;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;

namespace JobPlatform.PlatformAdministration.Application.Validators.Taxonomy;

public sealed class UpdatePlatformTaxonomyValidator : AbstractValidator<UpdatePlatformTaxonomyCommand>
{
    public UpdatePlatformTaxonomyValidator()
    {
        RuleFor(c => c.Type).Must(t => TaxonomyTypes.IsValid(TaxonomyTypes.Normalise(t))).WithErrorCode("VAL.Type.Invalid");
        RuleFor(c => c.Changes).NotNull().WithErrorCode("VAL.Changes.Required");
        RuleFor(c => c.Changes!).Must(list => list.Count is >= 1 and <= TaxonomyRules.MaxChanges).WithErrorCode("VAL.Changes.OutOfRange").When(c => c.Changes is not null);
        RuleForEach(c => c.Changes!).SetValidator(new ChangeValidator()).When(c => c.Changes is not null);
        RuleFor(c => c.Changes!)
            .Must(list => list.Select(x => (x.Code ?? string.Empty).Trim().ToLowerInvariant()).Distinct().Count() == list.Count)
            .WithErrorCode("VAL.Code.Duplicate").When(c => c.Changes is not null);
    }

    private sealed class ChangeValidator : AbstractValidator<TaxonomyChangeRequest>
    {
        public ChangeValidator()
        {
            RuleFor(x => x.Op).Must(op => TaxonomyRules.TryParseOp(op, out _)).WithErrorCode("VAL.Op.Invalid");
            RuleFor(x => x.Code).NotEmpty().WithErrorCode("VAL.Code.Required");
            RuleFor(x => x.Code).MaximumLength(TaxonomyRules.MaxCodeLength).WithErrorCode("VAL.Code.TooLong")
                .Matches(TaxonomyRules.CodePattern()).WithErrorCode("VAL.Code.InvalidFormat").When(x => !string.IsNullOrEmpty(x.Code));
            When(x => TaxonomyRules.TryParseOp(x.Op, out var k) && k == TaxonomyChangeKind.Add,
                () => RuleFor(x => x.Name).NotNull().WithErrorCode("VAL.Name.Required"));
            When(x => x.Name is not null, () =>
            {
                RuleFor(x => x.Name!.Ar).NotEmpty().WithErrorCode("VAL.Name.Ar.Invalid").MaximumLength(TaxonomyRules.MaxNameLength).WithErrorCode("VAL.Name.Ar.Invalid");
                RuleFor(x => x.Name!.En).NotEmpty().WithErrorCode("VAL.Name.En.Invalid").MaximumLength(TaxonomyRules.MaxNameLength).WithErrorCode("VAL.Name.En.Invalid");
            });
            RuleFor(x => x).Must(x => !string.Equals(x.ParentCode?.Trim(), x.Code?.Trim(), StringComparison.OrdinalIgnoreCase))
                .WithErrorCode("VAL.ParentCode.Self").When(x => x.ParentCode is not null);
            RuleFor(x => x.Synonyms!).Must(s => s.Count <= TaxonomyRules.MaxSynonyms && s.All(v => !string.IsNullOrWhiteSpace(v) && v.Length <= TaxonomyRules.MaxSynonymLength))
                .WithErrorCode("VAL.Synonyms.Invalid").When(x => x.Synonyms is not null);
        }
    }
}
