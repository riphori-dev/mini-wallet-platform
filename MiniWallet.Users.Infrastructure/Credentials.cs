using System.Security.Cryptography;
using System.Text;
using MiniWallet.Users.Application;

namespace MiniWallet.Users.Infrastructure;

public sealed class PasswordService : IPasswordService
{
    private const int Iterations = 600_000;
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256"
            || !int.TryParse(parts[1], out var iterations) || iterations is < 600_000 or > 2_000_000) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            return CryptographicOperations.FixedTimeEquals(expected,
                Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32));
        }
        catch (FormatException) { return false; }
    }
}
public sealed class VerificationSecrets : IVerificationSecrets
{
    public VerificationSecret Create()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return new(token, Hash(token));
    }
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.ToUpperInvariant())));
}

