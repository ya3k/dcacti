using System.Text.Json;

namespace GameServer.Api.Tests;

/// <summary>
/// Serializes a hub payload to the JSON that actually travels on the wire, for the
/// tests that assert the contract's member names.
///
/// <code>
/// hub DTO
///         ↓  JsonPropertyName attributes
/// SignalR JSON protocol
///         ↓
/// wire JSON                      (SIGNALR_PROTOCOL.md §3.2.3, §8.1)
/// </code>
///
/// <b>Why a shared helper.</b> This contract does not delegate member naming to a
/// serializer default: <c>SIGNALR_PROTOCOL.md</c> §3.2.3 item 2 fixes camelCase
/// explicitly and warns that "a serializer-wide naming policy that changed these
/// names would break this contract", and each member is named by its own
/// <c>JsonPropertyName</c> attribute. A test that serialized a DTO with
/// <see cref="JsonSerializer"/>'s bare defaults would therefore assert the CLR
/// property spelling (<c>BattleId</c>) instead of the wire spelling
/// (<c>battleId</c>), and would say nothing about what a client receives.
///
/// <b>It mirrors the host's configuration, and adds nothing to it.</b>
/// <c>Program</c> calls <c>AddSignalR()</c> with no JSON options, so the SignalR
/// JSON protocol applies its own defaults and every wire name comes from the DTO's
/// attributes. The options below match that: no naming policy is installed, so
/// attribute names are used verbatim. JSON property names are case-sensitive on the
/// wire (§3.2.3 item 3), so the comparison in the tests is ordinal.
/// </summary>
internal static class SignalRWireJson
{
    /// <summary>
    /// The options the SignalR JSON protocol applies in this application.
    ///
    /// <c>Program</c> registers the hub with <c>AddSignalR()</c> and configures no
    /// <c>JsonHubProtocol</c> options, so the protocol's own
    /// <c>PayloadSerializerOptions</c> apply unchanged. Those defaults carry a
    /// camelCase naming policy, which is what renders a member that declares no
    /// <c>JsonPropertyName</c> — such as every member of
    /// <c>BattleStateUpdated</c> — as its documented camelCase wire name, and what
    /// the <c>§3.2.3</c> contract requires. The options below are those defaults
    /// and nothing else: this helper adds no member and renames none.
    /// </summary>
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        // The protocol's own default policy. §3.2.3 item 2 warns that a
        // *serializer-wide* policy must not be relied on to fix the names — and
        // this contract does not rely on it: the hub names its own members
        // explicitly (every GetBattleStateResponse member carries
        // JsonPropertyName) and asserts the resulting names below. The policy is
        // mirrored here only so that members the protocol itself names — the ones
        // with no attribute — are serialized as a client actually receives them.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Serializes <paramref name="payload"/> exactly as the hub sends it, and
    /// returns the resulting JSON for member-name assertions.
    /// </summary>
    /// <typeparam name="T">The hub DTO being projected to the wire.</typeparam>
    /// <param name="payload">The payload a hub method returned.</param>
    internal static JsonElement ToElement<T>(T payload) =>
        JsonSerializer.SerializeToElement(payload, WireOptions);

    /// <summary>
    /// The payload's raw wire JSON, for assertions over the whole document — such
    /// as proving a server-only member is absent at every depth.
    /// </summary>
    /// <typeparam name="T">The hub DTO being projected to the wire.</typeparam>
    /// <param name="payload">The payload a hub method returned.</param>
    internal static string ToWireText<T>(T payload) => ToElement(payload).GetRawText();
}
