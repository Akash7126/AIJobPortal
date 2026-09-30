using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.JobPosting.Application.Commands.Postings;

/// <summary>Scheduled (US-3.2.1-01 AC-02): moves due postings with auto-close enabled to Expired. Not exposed over HTTP.</summary>
public sealed record ExpireDuePostingsCommand(int BatchSize = 200) : ICommand<int>;
