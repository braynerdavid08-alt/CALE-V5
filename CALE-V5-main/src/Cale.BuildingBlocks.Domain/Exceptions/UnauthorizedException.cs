namespace Cale.BuildingBlocks.Domain.Exceptions;

public sealed class UnauthorizedException : DomainException
{
    public UnauthorizedException(
        string message,
        string errorCode = "unauthorized")
        : base(message, 401, errorCode)
    {
    }
}
