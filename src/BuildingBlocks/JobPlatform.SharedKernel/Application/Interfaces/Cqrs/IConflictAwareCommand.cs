namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

/// <summary>Command that can hit a unique-index violation; supplies the error code for that case.</summary>
public interface IConflictAwareCommand
{
    string UniqueViolationErrorCode { get; }
}
