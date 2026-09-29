namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record PostingCountsDto(long Total, long Active, long Closed, long External);
