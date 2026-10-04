using Microsoft.Extensions.Configuration;

namespace GameServer.Infrastructure.Discord;

/// <summary>
/// The Discord credential's configuration contract — the two canonical keys
/// (<c>ADR-013</c> item 11), the channels that may supply them (<c>ADR-019</c>
/// D1), and the environment-sensitive presence rule the host applies during
/// composition (<c>ADR-019</c> D3/D4).
///
/// <code>
/// Discord:ClientId       a PUBLIC identifier — it is not a secret and may also
///                        reach the frontend as a client id
/// Discord:ClientSecret   a CREDENTIAL — backend only: never committed, logged,
///                        returned in a response, or bundled (ADR-007 item 3)
/// </code>
///
/// <b>It reads presence, never value.</b> Nothing here inspects, echoes, stores,
/// hashes, compares, or logs a credential. <see cref="ValidateForEnvironment"/>
/// decides only whether a value exists, so its failure can never carry the value
/// it rejected — which is what makes a startup failure safe to print to a
/// console, a log, or a terminal.
///
/// <b>The keys are names, not values.</b> They are supplied through the
/// configuration providers the host already composes (<c>ADR-019</c> D1): the
/// <c>GameServer.Api</c> project's <c>dotnet user-secrets</c> store in local
/// development, a host environment variable
/// (<c>Discord__ClientId</c> / <c>Discord__ClientSecret</c>) in deployment. No
/// <c>.env</c> provider is composed, and no credential is ever part of a tracked
/// file — the tracked <c>appsettings.json</c> carries an empty placeholder by
/// design (D3).
///
/// <b>No absence behaviour is invented.</b> In Development the host starts
/// without the credential and the unchanged <c>503 DISCORD_UNAVAILABLE</c> stands
/// (<c>API_CONTRACTS.md</c> §2.6); outside Development the host refuses to start
/// rather than serving a permanently unusable identity path (D4). There is no
/// anonymous fallback, no generated credential, and no default value.
/// </summary>
public static class DiscordCredentialOptions
{
    /// <summary>
    /// The configuration section the Discord credential is bound from
    /// (<c>ADR-013</c> item 11).
    /// </summary>
    public const string SectionName = "Discord";

    /// <summary>
    /// The configuration key naming the Discord application's client id. It is a
    /// <b>public identifier</b>, not a secret (<c>ADR-007</c> item 3), so it is
    /// the one Discord value that may also appear in a tracked file or reach the
    /// frontend.
    /// </summary>
    public const string ClientIdPath = $"{SectionName}:ClientId";

    /// <summary>
    /// The configuration key naming the Discord application's client secret. It
    /// is a <b>credential</b>: it reaches the backend through an approved
    /// server-side channel only, and never a tracked file, a build output, a git
    /// history, a frontend bundle, a response body, or a log line
    /// (<c>ADR-007</c> item 3; <c>ADR-019</c> security invariants).
    /// </summary>
    public const string ClientSecretPath = $"{SectionName}:ClientSecret";

    /// <summary>
    /// Whether the credential at <paramref name="configurationPath"/> is
    /// configured — <c>false</c> for an absent key, an empty value, and a
    /// whitespace-only value alike (<c>ADR-019</c> D3: "absent" means null, empty,
    /// or whitespace-only).
    /// </summary>
    /// <remarks>
    /// This is the same absence test <c>ApplicationSessionSigningKeys</c> applies
    /// to the session signing secret, and it is deliberately the whole test: the
    /// application only needs to establish that a credential exists. It does not
    /// validate the credential's format, length, or shape, because no document
    /// fixes any of those and a guess here would reject a valid operator-supplied
    /// value (<c>AGENTS.md</c> §7).
    /// </remarks>
    public static bool IsConfigured(IConfiguration configuration, string configurationPath) =>
        !string.IsNullOrWhiteSpace(configuration[configurationPath]);

    /// <summary>
    /// Applies <c>ADR-019</c> D4: outside Development, a host with an absent
    /// <see cref="ClientIdPath"/> or <see cref="ClientSecretPath"/> refuses to
    /// start; in Development the call is a no-op.
    /// </summary>
    /// <param name="configuration">The application configuration (D1).</param>
    /// <param name="isDevelopmentEnvironment">
    /// Whether the host is running in the Development environment. It is supplied
    /// by the caller because it is a host fact, read from <c>IHostEnvironment</c>
    /// rather than from an <c>appsettings</c> file, so no tracked configuration can
    /// decide for itself whether the credential is required (the same rule
    /// <c>DevelopmentAuthenticationOptions</c> follows).
    /// </param>
    /// <exception cref="ConfigurationException">
    /// A required Discord credential is absent outside Development. Discord is
    /// production's only identity path, so a host that cannot perform the exchange
    /// must not start and then serve requests as if it could (D4).
    /// </exception>
    public static void ValidateForEnvironment(
        IConfiguration configuration,
        bool isDevelopmentEnvironment)
    {
        if (isDevelopmentEnvironment)
        {
            // D4: Development deliberately stays permissive. The exchange client
            // is not implemented yet, and TASK-181's local browser path is a
            // deliberate development route; failing startup here would break it
            // for no security gain, because Development imposes no production
            // exposure. The endpoint keeps the unchanged 503 instead.
            return;
        }

        var missing = MissingCredentialPaths(configuration);

        if (missing.Count == 0)
        {
            return;
        }

        // The message names the absent configuration keys — never a value, and
        // never a partially-supplied one. It states the approved channels so an
        // operator can fix the deployment without consulting source, and it states
        // that nothing is substituted, so the failure cannot be mistaken for a
        // recoverable one (D1, D4).
        throw new ConfigurationException(
            "The Discord credential is not configured. Set "
            + string.Join(" and ", missing.Select(path => $"'{path}'"))
            + " through an approved server-side channel (ADR-019 D1): a host environment variable in "
            + "deployment (Discord__ClientId / Discord__ClientSecret), or the GameServer.Api "
            + "dotnet user-secrets store in local development. Discord is this environment's identity "
            + "path, so a host that cannot perform the exchange refuses to start (ADR-019 D4). No "
            + "generated, default, anonymous, or development credential is substituted, and no "
            + "credential value is echoed here.");
    }

    /// <summary>
    /// The credential keys that are absent (null, empty, or whitespace-only),
    /// in the order they are declared.
    /// </summary>
    private static List<string> MissingCredentialPaths(IConfiguration configuration)
    {
        var missing = new List<string>(capacity: 2);

        foreach (var path in new[] { ClientIdPath, ClientSecretPath })
        {
            if (!IsConfigured(configuration, path))
            {
                missing.Add(path);
            }
        }

        return missing;
    }

    /// <summary>
    /// The Discord credential is absent in an environment that requires it
    /// (<c>ADR-019</c> D4).
    /// </summary>
    /// <remarks>
    /// The failure carries the absent keys only. It exists so the composition root
    /// stops during host construction, where the misconfiguration is fixable,
    /// rather than at the first login attempt, where it is not.
    /// </remarks>
    public sealed class ConfigurationException(string message) : Exception(message);
}
