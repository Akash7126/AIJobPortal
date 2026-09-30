namespace JobPlatform.AiMatching.Infrastructure.Interfaces.Persistence;

/// <summary>Application-level encryption of parsed resume content (handover section 8: PII encrypted).</summary>
public interface IPiiProtector
{
    string Protect(string plain);

    string Unprotect(string stored);
}
