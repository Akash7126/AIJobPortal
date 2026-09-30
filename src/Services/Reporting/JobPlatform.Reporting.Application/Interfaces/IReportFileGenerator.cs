using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Interfaces;

/// <summary>Renders a tabular report to a file (CSV, Excel, PDF).</summary>
public interface IReportFileGenerator
{
    GeneratedFile Generate(string title, ReportTable table, ReportFormat format);
}
