namespace Cale.BuildingBlocks.Domain.Security;

public interface ITokenService
{
    string Create(int userId, string email, string name, string role);
}
