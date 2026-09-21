using GameServer.Domain.Battle;

namespace GameServer.Application.Battle;

/// <summary>
/// The server-side source of a battle's <c>RngSeed</c>
/// (<c>GAME_STATE.md</c> §2.6.1).
///
/// The seed "comes from the server's own entropy at battle creation"
/// (§2.6.1 item 2). Nothing in the documentation requires a specific entropy
/// source, so the source itself is an implementation detail — but it must be a
/// <b>server-side</b> source and it must produce a <c>System.UInt64</c>
/// (§2.6.1 item 2, <c>ADR-009</c>).
///
/// It is never supplied, influenced, or derived from client input — not from
/// client <c>Math.random()</c>, client timestamps, client-generated values, or
/// any client-provided field (§2.6.1 item 1, <c>TDD.md</c> §6 item 3).
///
/// This interface exists so the entropy source is an explicit, replaceable
/// dependency of battle creation rather than a static call buried in
/// orchestration — it is the smallest shape that keeps the one documented
/// requirement (server-side) verifiable.
/// </summary>
public interface IRngSeedSource
{
    /// <summary>Produces the server-chosen seed for a new battle.</summary>
    ulong CreateSeed();
}

/// <summary>
/// The default server-side seed source: a fresh <c>System.UInt64</c> drawn from
/// the server process's entropy pool.
///
/// This is the one place in the project where a non-PCG generator is
/// legitimately used, and it is not a violation of <c>ADR-009</c> §1–§2: the
/// documentation explicitly assigns seed <i>generation</i> to server entropy
/// (<c>GAME_STATE.md</c> §2.6.1 item 2) while forbidding non-PCG sources only for
/// <b>gameplay randomness</b> and gameplay-relevant values. This value seeds the
/// PCG32 stream; it is not itself a gameplay draw, and no Gem value is ever
/// derived from it directly.
///
/// <c>RandomNumberGenerator</c> is selected because it is the server-side entropy
/// primitive available without introducing a dependency, and because the seed is
/// a secret origin point that is never sent to or derived by the client
/// (<c>GAME_STATE.md</c> §2.6.1 item 3; <c>ADR-009</c> "Server-authoritative and
/// non-reversible from the client"). It is not used for any gameplay-relevant
/// value, and it is never used as a second randomization mechanism alongside
/// PCG32 (<c>AGENTS.md</c> §11, <c>ADR-009</c> §1).
/// </summary>
public sealed class SystemEntropyRngSeedSource : IRngSeedSource
{
    /// <inheritdoc />
    public ulong CreateSeed()
    {
        // 8 bytes of server entropy, assembled little-endian into the documented
        // System.UInt64 seed type (GAME_STATE.md §2.6.1).
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

        return BitConverter.ToUInt64(bytes);
    }
}