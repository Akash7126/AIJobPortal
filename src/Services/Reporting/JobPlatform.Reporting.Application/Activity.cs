namespace JobPlatform.Reporting.Application;

/// <summary>Scoped accessor so handlers read the current options without depending on the options infrastructure.</summary>
public sealed class ReportingOptionsAccessor
{
    private readonly Microsoft.Extensions.Options.IOptions<ReportingOptions> _options;

    public ReportingOptionsAccessor(Microsoft.Extensions.Options.IOptions<ReportingOptions> options) => _options = options;

    public ReportingOptions Value => _options.Value;
}
