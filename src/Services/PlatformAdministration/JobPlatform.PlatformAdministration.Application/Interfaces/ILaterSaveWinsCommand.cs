namespace JobPlatform.PlatformAdministration.Application.Interfaces;

/// <summary>Command whose concurrent saves follow "later save wins" (US-3.1.4-06/07/08 AC-03): a lost optimistic race is retried on fresh state.</summary>
public interface ILaterSaveWinsCommand
{
}
