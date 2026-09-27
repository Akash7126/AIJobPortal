using FluentValidation;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application;

// ---------------------------------------------------------------------- job data mapping (US-3.1.3-04, US-3.4.1-03)

public sealed record MappingRuleInput(string SourceField, string TargetField, string Transform);

public sealed record ConfigureJobDataMappingCommand(IReadOnlyList<MappingRuleInput> Rules, string StandardSchemaVersion) : PartnerCommand<JobDataMappingView>;

public sealed class ConfigureJobDataMappingValidator : AbstractValidator<ConfigureJobDataMappingCommand>
{
    private static readonly string[] AllowedTransforms = Enum.GetNames<MappingTransform>();

    public ConfigureJobDataMappingValidator()
    {
        RuleFor(c => c.Rules).NotEmpty().WithErrorCode("VAL.Rules.Required");
        RuleForEach(c => c.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.SourceField).NotEmpty().WithErrorCode("VAL.SourceField.Required");
            rule.RuleFor(r => r.TargetField).NotEmpty().WithErrorCode("VAL.TargetField.Required");
            rule.RuleFor(r => r.Transform).Must(t => AllowedTransforms.Contains(t, StringComparer.OrdinalIgnoreCase))
                .WithErrorCode("VAL.Transform.Invalid");
        });
        RuleFor(c => c.Rules).Must(rules => rules.Select(r => r.TargetField.ToLowerInvariant()).Distinct().Count() == rules.Count)
            .WithErrorCode("VAL.Rules.DuplicateTargetField");
        RuleFor(c => c.StandardSchemaVersion).NotEmpty().WithErrorCode("VAL.StandardSchemaVersion.Required");
    }
}

internal sealed class ConfigureJobDataMappingHandler : ICommandHandler<ConfigureJobDataMappingCommand, JobDataMappingView>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly IJobDataMappingRepository _mappings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ConfigureJobDataMappingHandler(IExternalJobSiteIntegrationRepository integrations, IJobDataMappingRepository mappings, ICurrentUser user,
        TimeProvider clock)
    {
        _integrations = integrations;
        _mappings = mappings;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<JobDataMappingView>> Handle(ConfigureJobDataMappingCommand request, CancellationToken ct)
    {
        var integration = await _integrations.GetByPartnerAccountAsync(_user.UserId!.Value, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        var rules = request.Rules.Select(r => new MappingRule(r.SourceField, r.TargetField, Enum.Parse<MappingTransform>(r.Transform, true))).ToArray();
        var now = _clock.GetUtcNow().UtcDateTime;
        var actorId = ActorFactory.From(_user).Id;
        var mapping = await _mappings.GetByIntegrationAsync(integration.Id, ct);
        if (mapping is null)
        {
            mapping = JobDataMapping.Create(Guid.NewGuid(), integration.Id, rules, request.StandardSchemaVersion, actorId, now);
            _mappings.Add(mapping);
            integration.AssignMapping(mapping.Id);
        }
        else
        {
            mapping.Configure(rules, actorId, now);
        }

        return ToView(mapping);
    }

    internal static JobDataMappingView ToView(JobDataMapping m) => new(m.Id, m.IntegrationId, m.MappingVersion, m.StandardSchemaVersion,
        m.Rules.Select(r => new MappingRuleView(r.SourceField, r.TargetField, r.Transform.ToString())).ToArray());
}

public sealed record GetJobDataMappingQuery : PartnerQuery<JobDataMappingView>;

internal sealed class GetJobDataMappingHandler : IQueryHandler<GetJobDataMappingQuery, JobDataMappingView>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly IJobDataMappingRepository _mappings;
    private readonly ICurrentUser _user;

    public GetJobDataMappingHandler(IExternalJobSiteIntegrationRepository integrations, IJobDataMappingRepository mappings, ICurrentUser user)
    {
        _integrations = integrations;
        _mappings = mappings;
        _user = user;
    }

    public async Task<Result<JobDataMappingView>> Handle(GetJobDataMappingQuery request, CancellationToken ct)
    {
        var integration = await _integrations.GetByPartnerAccountAsync(_user.UserId!.Value, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        var mapping = await _mappings.GetByIntegrationAsync(integration.Id, ct);
        return mapping is null
            ? Error.NotFound(ErrorCodes.NotFound, "No mapping has been configured for this integration yet.")
            : ConfigureJobDataMappingHandler.ToView(mapping);
    }
}

// ---------------------------------------------------------------------- standard schema (static reference data, US-3.1.3-04)

public sealed record GetStandardSchemaQuery : PartnerQuery<StandardSchemaView>;

internal sealed class GetStandardSchemaHandler : IQueryHandler<GetStandardSchemaQuery, StandardSchemaView>
{
    public static readonly StandardSchemaView Schema = new("v1", new[]
    {
        new StandardSchemaFieldView("title", true, "Job title."),
        new StandardSchemaFieldView("summary", true, "Job description/summary."),
        new StandardSchemaFieldView("skills", true, "Comma-separated list of required skills."),
        new StandardSchemaFieldView("contractType", false, "FullTime, PartTime, Contract, Temporary or Internship."),
        new StandardSchemaFieldView("workFormat", false, "Physical, Remote or Hybrid."),
        new StandardSchemaFieldView("applicationDeadline", false, "ISO-8601 date/time."),
        new StandardSchemaFieldView("location", false, "Free-text location."),
        new StandardSchemaFieldView("sourceUrl", false, "Absolute HTTPS URL to the original posting on the partner site.")
    });

    public Task<Result<StandardSchemaView>> Handle(GetStandardSchemaQuery request, CancellationToken ct) =>
        Task.FromResult(Result.Success(Schema));
}
