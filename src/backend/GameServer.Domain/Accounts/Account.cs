namespace GameServer.Domain.Accounts;

/// <summary>
/// The user account entity (ADR-020, DATABASE.md §1).
/// Owns the authentication credentials (username, password hash)
/// and links 1-to-1 with a Player profile.
/// </summary>
public class Account
{
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 32;
    public const int MinPasswordLength = 6;

    /// <summary>Unique identifier for the account.</summary>
    public required Guid AccountId { get; init; }

    /// <summary>Unique username (alphanumeric + underscore, case-insensitive).</summary>
    public required string Username { get; set; }

    /// <summary>Secure password hash (PBKDF2 with salt).</summary>
    public required string PasswordHash { get; set; }

    /// <summary>UTC timestamp of account creation.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
