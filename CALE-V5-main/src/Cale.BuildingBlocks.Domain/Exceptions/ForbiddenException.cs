namespace Cale.BuildingBlocks.Domain.Exceptions;

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message, string errorCode = "forbidden")
        : base(message, 403, errorCode)
    {
    }
}
