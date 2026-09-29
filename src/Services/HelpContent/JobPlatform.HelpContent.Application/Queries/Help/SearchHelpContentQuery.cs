using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.HelpContent.Application.Queries.Help;

/// <summary>US-3.7.2-01/03: "no results" is a normal outcome (AC-02), never an error.</summary>
public sealed record SearchHelpContentQuery(string Keyword, HelpRole? Role, int Page = 1, int PageSize = 20) : IQuery<PagedResult<HelpSearchResultView>>;
