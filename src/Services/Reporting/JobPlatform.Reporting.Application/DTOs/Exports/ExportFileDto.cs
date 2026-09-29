namespace JobPlatform.Reporting.Application.DTOs.Exports;

public sealed record ExportFileDto(string FileName, string ContentType, byte[] Content);
