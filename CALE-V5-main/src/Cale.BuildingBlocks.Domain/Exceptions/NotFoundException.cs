namespace Cale.BuildingBlocks.Domain.Exceptions;

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message, string errorCode = "not_found")
        : base(message, 404, errorCode)
    {
    }
}
