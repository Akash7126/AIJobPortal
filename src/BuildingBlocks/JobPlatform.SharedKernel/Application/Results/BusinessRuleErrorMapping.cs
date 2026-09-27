using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.SharedKernel.Application.Results;

public static class BusinessRuleErrorMapping
{
    /// <summary>Translates a broken domain rule into the published error (external code when the rule has one, else the rule code).</summary>
    public static Error ToError(this BusinessRuleViolationException ex)
    {
        var type = ex.Kind switch
        {
            BusinessRuleKind.Conflict => ErrorType.Conflict,
            BusinessRuleKind.Forbidden => ErrorType.Forbidden,
            BusinessRuleKind.Unauthorized => ErrorType.Unauthorized,
            BusinessRuleKind.RateLimited => ErrorType.TooManyRequests,
            BusinessRuleKind.InvalidInput => ErrorType.Validation,
            _ => ErrorType.BusinessRule
        };

        var error = new Error(ex.ExternalCode ?? ex.Code, ex.Message, type) { RuleCode = ex.Code };
        if (type == ErrorType.Validation && ex.Args.TryGetValue("violations", out var violations) && violations is string[] codes)
        {
            var field = ex.Args.TryGetValue("field", out var f) && f is string name ? name : "value";
            error = error with { ValidationErrors = new Dictionary<string, string[]> { [field] = codes } };
        }

        if (type == ErrorType.TooManyRequests)
        {
            error = error with { RetryAfter = TimeSpan.FromMinutes(15) };
        }

        return error;
    }
}
