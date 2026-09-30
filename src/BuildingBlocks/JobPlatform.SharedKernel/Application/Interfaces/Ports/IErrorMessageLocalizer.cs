using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.SharedKernel.Application.Interfaces.Ports;

/// <summary>Localises user-facing error messages by code (Accept-Language). Implemented by each Api from resource files.</summary>
public interface IErrorMessageLocalizer
{
    string Localize(string code, string fallback, Language language);
}
