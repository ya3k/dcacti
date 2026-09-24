namespace GameServer.Application.Identity;

/// <summary>
/// A verified Discord identity — the outcome of the authorization-code
/// exchange defined by <c>API_CONTRACTS.md</c> §2.2–§2.4 (<c>ADR-013</c>).
///
/// <see cref="DiscordUserId"/> is the contract's <b>sole output</b> to Player
/// persistence: the User object's <c>id</c>, a snowflake serialized by Discord
/// as a string (§2.3 item 3, §2.4). It is held as an opaque string and is
/// never parsed into a numeric type.
///
/// It is not the authorization code, the Discord access token, or the
/// application session (<c>API_CONTRACTS.md</c> §2.4), and this type carries
/// none of those — the exchange's credentials do not cross this boundary.
/// </summary>
/// <param name="DiscordUserId">
/// The verified <c>DiscordUserId</c> (<c>API_CONTRACTS.md</c> §2.4). Non-empty
/// by construction: §2.6 treats a missing or empty <c>id</c> as
/// <c>DISCORD_BAD_RESPONSE</c> rather than coercing it into an empty identity,
/// so a failure never produces this type.
/// </param>
public readonly record struct DiscordIdentity(string DiscordUserId);

/// <summary>
/// The result of resolving a Discord authorization code to a verified
/// identity (<c>API_CONTRACTS.md</c> §2.2–§2.4), or the defensive failure the
/// boundary maps to the <c>API_CONTRACTS.md</c> §6 error envelope.
///
/// A failed result carries <b>no identity</b>: this is what makes
/// "no Player is created or matched on any failure path"
/// (<c>API_CONTRACTS.md</c> §2.6 rule 5, §2.7 item 6) enforceable at the
/// caller rather than merely asserted. There is no way to obtain a
/// <see cref="DiscordIdentity"/> from a failure, so an unverified identity
/// cannot reach Player persistence.
///
/// The failure members carry only this contract's own error code and message.
/// Upstream Discord detail is never placed here (§2.7 item 5).
/// </summary>
public sealed record DiscordIdentityResolution
{
    private DiscordIdentityResolution(
        bool succeeded,
        DiscordIdentity identity,
        int statusCode,
        string? error,
        string? message)
    {
        Succeeded = succeeded;
        Identity = identity;
        StatusCode = statusCode;
        Error = error;
        Message = message;
    }

    /// <summary>Whether the identity was verified.</summary>
    public bool Succeeded { get; }

    /// <summary>
    /// The verified identity. Meaningful only when <see cref="Succeeded"/> is
    /// true.
    /// </summary>
    public DiscordIdentity Identity { get; }

    /// <summary>
    /// The HTTP status for the failure, per <c>API_CONTRACTS.md</c> §2.6.
    /// <c>200</c> when <see cref="Succeeded"/>.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// The machine-readable error code for the failure, per
    /// <c>API_CONTRACTS.md</c> §2.6. <see langword="null"/> when
    /// <see cref="Succeeded"/>.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// The human-readable failure detail, per the §6 envelope.
    /// <see langword="null"/> when <see cref="Succeeded"/>.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// The successful result for a verified <paramref name="identity"/>.
    /// </summary>
    public static DiscordIdentityResolution Success(DiscordIdentity identity) =>
        new(true, identity, 200, null, null);

    /// <summary>
    /// A failure using the <c>API_CONTRACTS.md</c> §2.6 status and error code.
    /// It carries no identity, so Player persistence is unreachable from it.
    /// </summary>
    public static DiscordIdentityResolution Failure(int statusCode, string error, string message) =>
        new(false, default, statusCode, error, message);
}

/// <summary>
/// Resolves a Discord authorization code to a verified Discord identity
/// (<c>API_CONTRACTS.md</c> §2.2–§2.4).
///
/// <b>The implementation is Infrastructure</b> — <c>ADR-013</c> item 13 places
/// the Discord exchange client in a module under
/// <c>GameServer.Infrastructure</c>, sibling to <c>Postgres/</c>, <c>Redis/</c>,
/// and <c>SignalR/</c>, so it never leaks into Domain or into the controller.
///
/// <b>TASK-023 does not define or implement the exchange.</b> The contract's
/// endpoint, grant type, request encoding, credential mechanism, identity
/// endpoint, extracted field, and failure mapping are fixed by
/// <c>API_CONTRACTS.md</c> §2 and owned by TASK-035; this boundary exists so
/// the Player ownership layer consumes the resulting <c>DiscordUserId</c>
/// without reimplementing or re-deciding any of it (<c>AGENTS.md</c> §7, §18).
/// </summary>
public interface IDiscordIdentityResolver
{
    /// <summary>
    /// Exchanges <paramref name="code"/> for the verified Discord identity.
    ///
    /// Returns a failure — never an identity — for every condition
    /// <c>API_CONTRACTS.md</c> §2.6 defines: a missing/invalid/expired/reused
    /// code, a transport or upstream failure, or an unusable identity
    /// response. A missing or empty <c>id</c> is such a failure and is never
    /// coerced into an empty <c>DiscordUserId</c> (§2.6 rule 3).
    /// </summary>
    /// <param name="code">
    /// The Discord authorization code from the request. An authorization code
    /// is not an identity and is never used as the Player matching key
    /// (<c>API_CONTRACTS.md</c> §2.4).
    /// </param>
    /// <param name="cancellationToken">Cancels the exchange with the request.</param>
    Task<DiscordIdentityResolution> ResolveAsync(
        string code,
        CancellationToken cancellationToken = default);
}
