using JobPlatform.CandidateSourcing.Application.DTOs.Threshold;

namespace JobPlatform.CandidateSourcing.Application.Commands.Threshold;

/// <summary>US-3.3.3-03: set the per-posting qualification cutoff. INV-06 (snapshot-at-run-start) is honoured by the read handlers, which capture
/// <see cref="ThresholdView.Percent"/> once at the start of their computation.</summary>
public sealed record SetQualificationThresholdCommand(Guid JobPostingId, int Percent) : EmployerCommand<ThresholdView>;
