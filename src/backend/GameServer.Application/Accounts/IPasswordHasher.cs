namespace GameServer.Application.Accounts;

/// <summary>
/// Password hashing and verification service (ADR-020).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Generates a cryptographically salted hash of the password.</summary>
    string HashPassword(string password);

    /// <summary>Verifies that a plaintext password matches the stored hash.</summary>
    bool VerifyPassword(string password, string passwordHash);
}
