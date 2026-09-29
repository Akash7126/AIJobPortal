namespace JobPlatform.Reporting.Application.DTOs.Common;

// Read models and API shapes. Every figure that can be too thin carries an explicit insufficientData flag instead of a misleading number.

/// <summary>A single figure, or an explicit "no figure" with the reason (insufficient data, no data source).</summary>
public sealed record FigureDto(bool InsufficientData, decimal? Value, long SampleSize, string? Reason = null);
