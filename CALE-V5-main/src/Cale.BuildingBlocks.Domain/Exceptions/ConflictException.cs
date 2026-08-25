namespace Cale.BuildingBlocks.Domain.Exceptions;

public sealed class ConflictException : DomainException
{
    public ConflictException(string message, string errorCode = "conflict")
        : base(message, 409, errorCode)
    {
    }
}
