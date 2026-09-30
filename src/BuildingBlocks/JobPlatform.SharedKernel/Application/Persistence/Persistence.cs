namespace JobPlatform.SharedKernel.Application.Persistence;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Exception inner) : base("The record was modified by another request.", inner)
    {
    }
}

public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(Exception inner) : base("A unique constraint was violated.", inner)
    {
    }
}
