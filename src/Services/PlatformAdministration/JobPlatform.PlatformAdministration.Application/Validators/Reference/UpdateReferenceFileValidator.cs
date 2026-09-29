using FluentValidation;
using JobPlatform.PlatformAdministration.Application.Commands.Reference;
using JobPlatform.PlatformAdministration.Application.DTOs.Reference;
using JobPlatform.PlatformAdministration.Application.Reference;
using JobPlatform.PlatformAdministration.Domain.Reference;

namespace JobPlatform.PlatformAdministration.Application.Validators.Reference;

public sealed class UpdateReferenceFileValidator : AbstractValidator<UpdateReferenceFileCommand>
{
    public UpdateReferenceFileValidator()
    {
        RuleFor(c => c.Type).Must(t => ReferenceRules.TryParseType(t, out _)).WithErrorCode("VAL.Type.Invalid");
        RuleFor(c => c.Changes).NotNull().WithErrorCode("VAL.Changes.Required");
        RuleFor(c => c.Changes!).Must(list => list.Count is >= 1 and <= ReferenceRules.MaxChanges).WithErrorCode("VAL.Changes.OutOfRange").When(c => c.Changes is not null);
        RuleForEach(c => c.Changes!).SetValidator(new ChangeValidator()).When(c => c.Changes is not null);
        RuleFor(c => c.Changes!)
            .Must(list => list.Where(x => x.Code is not null).Select(x => x.Code!.Trim().ToLowerInvariant()).Distinct().Count()
                          == list.Count(x => x.Code is not null))
            .WithErrorCode("VAL.Code.Duplicate").When(c => c.Changes is not null);
    }

    private sealed class ChangeValidator : AbstractValidator<ReferenceChangeRequest>
    {
        public ChangeValidator()
        {
            RuleFor(x => x.Op).Must(op => ReferenceRules.TryParseOp(op, out _)).WithErrorCode("VAL.Op.Invalid");
            When(x => ReferenceRules.TryParseOp(x.Op, out var k) && k == ReferenceChangeKind.Add, () =>
            {
                RuleFor(x => x.Code).NotEmpty().WithErrorCode("VAL.Code.Required");
                RuleFor(x => x.Code!).MaximumLength(ReferenceRules.MaxCodeLength).WithErrorCode("VAL.Code.TooLong")
                    .Matches(ReferenceRules.CodePattern()).WithErrorCode("VAL.Code.InvalidFormat").When(x => !string.IsNullOrEmpty(x.Code));
                RuleFor(x => x.Name).NotNull().WithErrorCode("VAL.Name.Required");
            });
            When(x => ReferenceRules.TryParseOp(x.Op, out var k) && k != ReferenceChangeKind.Add, () =>
                RuleFor(x => x.EntryId).NotNull().WithErrorCode("VAL.EntryId.Required"));
            When(x => ReferenceRules.TryParseOp(x.Op, out var k) && k == ReferenceChangeKind.Edit, () =>
                RuleFor(x => x).Must(x => x.Name is not null || x.IsActive is not null).WithErrorCode("VAL.Change.Empty"));
            When(x => x.Name is not null, () =>
            {
                RuleFor(x => x.Name!.Ar).NotEmpty().WithErrorCode("VAL.Name.Ar.Invalid").MaximumLength(ReferenceRules.MaxNameLength).WithErrorCode("VAL.Name.Ar.Invalid");
                RuleFor(x => x.Name!.En).NotEmpty().WithErrorCode("VAL.Name.En.Invalid").MaximumLength(ReferenceRules.MaxNameLength).WithErrorCode("VAL.Name.En.Invalid");
            });
        }
    }
}
