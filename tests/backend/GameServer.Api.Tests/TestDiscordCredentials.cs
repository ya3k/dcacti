using GameServer.Infrastructure.Discord;

namespace GameServer.Api.Tests;

/// <summary>
/// Test fixtures for the Discord credential's <b>presence</b> — everything the
/// host requires of <c>Discord:ClientId</c> / <c>Discord:ClientSecret</c>
/// (<c>ADR-019</c> D3/D4).
///
/// <b>They are not credentials.</b> No test performs a Discord exchange, makes a
/// Discord API call, or validates a value's format: the contract under test is
/// that the credential is <i>present</i> outside Development and that
/// Development needs no credential at all. The values are therefore deliberately
/// unusable — prefixed so no reader could mistake one for a real credential, and
/// never printed by a test, an assertion, or a failure message.
///
/// The client id here is a stand-in for a public identifier, exactly as
/// <c>appsettings.json</c> carries one; the secret stands in for the operator's
/// user-secrets value, which is never in this repository
/// (<c>ADR-019</c> D1).
/// </summary>
internal static class TestDiscordCredentials
{
    /// <summary>A stand-in for the public client id.</summary>
    internal const string ClientId = "test-public-discord-client-id";

    /// <summary>
    /// A stand-in for the operator-supplied credential. It is supplied through
    /// configuration in tests only, and it authenticates nothing.
    /// </summary>
    internal const string ClientSecret = "test-only-dummy-discord-client-secret";

    /// <summary>
    /// The configuration values that satisfy the requirement for a
    /// non-Development host (<c>ADR-019</c> D4). A host applies these with
    /// <c>UseSetting</c> or an in-memory collection.
    /// </summary>
    internal static readonly (string Key, string Value)[] Configuration =
    [
        (DiscordCredentialOptions.ClientIdPath, ClientId),
        (DiscordCredentialOptions.ClientSecretPath, ClientSecret),
    ];
}
