namespace Receipts.Core;

public interface ITokenService
{
    string CreateToken(User user);
}
