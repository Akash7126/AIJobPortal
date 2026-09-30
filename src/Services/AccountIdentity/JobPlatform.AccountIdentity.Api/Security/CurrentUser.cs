using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Api.Security;

/// <summary>The caller as seen by the application layer, built from the validated JWT and the request.</summary>
internal sealed class CurrentUser : ICurrentUser
{
    public const string DeviceHeader = "X-Device-Id";

    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Http => _accessor.HttpContext;

    private ClaimsPrincipal? Principal => Http?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue("sub"), out var id) ? id : null;

    public ActorType? ActorType => Enum.TryParse<ActorType>(Principal?.FindFirstValue(AppClaimTypes.ActorType), out var type) ? type : null;

    public IReadOnlyCollection<Guid> RoleIds =>
        Principal?.FindAll(AppClaimTypes.RoleId).Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToArray()
        ?? Array.Empty<Guid>();

    public Guid? SessionId => Guid.TryParse(Principal?.FindFirstValue(AppClaimTypes.SessionId), out var id) ? id : null;

    public bool MfaVerified => Principal?.FindAll(AppClaimTypes.Amr).Any(c => c.Value == AppClaimTypes.MfaValue) == true;

    public bool MustChangePassword => Principal?.FindFirstValue(AppClaimTypes.MustChangePassword) == "true";

    public string? ClientId => Principal?.FindFirstValue(AppClaimTypes.ClientId);

    public IReadOnlyCollection<string> Scopes =>
        Principal?.FindFirstValue(AppClaimTypes.Scope)?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

    public string? TokenId => Principal?.FindFirstValue("jti");

    public string? IpAddress => Http?.Connection.RemoteIpAddress?.ToString();

    /// <summary>Q-06: rate-limit source = client IP + optional device id, hashed so raw values never become cache keys.</summary>
    public string SourceKey
    {
        get
        {
            var raw = $"{IpAddress ?? "unknown"}|{Http?.Request.Headers[DeviceHeader].FirstOrDefault()}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
        }
    }

    public Language Language
    {
        get
        {
            var header = Http?.Request.Headers.AcceptLanguage.ToString();
            if (string.IsNullOrWhiteSpace(header))
            {
                return Language.En;
            }

            foreach (var part in header.Split(','))
            {
                var tag = part.Split(';')[0].Trim();
                if (tag.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
                {
                    return Language.Ar;
                }

                if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                {
                    return Language.En;
                }
            }

            return Language.En;
        }
    }
}

/// <summary>Resolves user-facing error messages from resource files (ar / en) by error code (THR-004, foundation section 7).</summary>
internal sealed class ResourceErrorMessageLocalizer : IErrorMessageLocalizer
{
    private static readonly System.Resources.ResourceManager Resources =
        new("JobPlatform.AccountIdentity.Api.Resources.ErrorMessages", typeof(ResourceErrorMessageLocalizer).Assembly);

    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    public string Localize(string code, string fallback, Language language)
    {
        var text = Resources.GetString(code, language == Language.Ar ? Arabic : English);
        return string.IsNullOrEmpty(text) ? fallback : text;
    }
}
