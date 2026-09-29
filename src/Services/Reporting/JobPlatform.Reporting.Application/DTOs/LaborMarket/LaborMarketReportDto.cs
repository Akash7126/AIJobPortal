using System.Text.Json;

namespace JobPlatform.Reporting.Application.DTOs.LaborMarket;

public sealed record LaborMarketReportDto(Guid Id, string Period, DateTime GeneratedAtUtc, JsonElement Content, bool Existing);
