namespace GameServer.Api.Authentication;

/// <summary>
/// The application session's wire/identity contract constants
/// (<c>API_CONTRACTS.md</c> §2.8, <c>ADR-015</c> D3/D9/D11).
///
/// These are the fixed strings the session contract itself names — the claim the
/// token carries, the one audience value both consumers share, and the key id
/// header. They are declared once here and read by both issuance and validation,
/// so the two halves of the contract cannot drift apart.
/// </summary>
public static class ApplicationSessionClaims
{
    /// <summary>
    /// The identity claim (<c>API_CONTRACTS.md</c> §2.8 "Identity";
    /// <c>ADR-015</c> D3).
    ///
    /// <code>
    /// claim: player_id
    /// value: PlayerId
    /// </code>
    ///
    /// It carries the <c>PlayerId</c> the Discord identity exchange already
    /// resolved and mapped — never the Discord user identity, and never a
    /// client-supplied value. <c>DiscordUserId</c> is not carried as an
    /// authoritative ownership claim at all (D3).
    /// </summary>
    public const string PlayerId = "player_id";

    /// <summary>
    /// The single audience value (<c>ADR-015</c> D9).
    ///
    /// <code>
    /// claim: aud
    /// value: dcacti-backend
    /// </code>
    ///
    /// One value covers both consumers: REST and the <c>BattleHub</c> access
    /// token are the same token under one scheme (D4/D6). Audience validation is
    /// required with an exact match.
    /// </summary>
    public const string Audience = "dcacti-backend";

    /// <summary>
    /// The header that names the signing key (<c>ADR-015</c> D11). Every token
    /// carries it, and validation accepts exactly two keys: the current signing
    /// key and the previous one.
    /// </summary>
    public const string KeyIdHeader = "kid";
}
