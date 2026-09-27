namespace GameServer.Domain.Battle;

/// <summary>
/// A battle's server-chosen PRNG seed (<c>GAME_STATE.md</c> §2.6.1) as it is
/// passed to the battle-creation boundary.
///
/// <code>
/// BattleState.RngSeed  ←  BattleSeed.Value
/// </code>
///
/// <b>The wrapper exists so an unset seed is not a value.</b> A bare
/// <c>ulong</c> parameter would make <c>0</c> ambiguous: it is a legal seed
/// value, but it is also what an omitted argument looks like, and the two must
/// not be the same thing. <c>GAME_STATE.md</c> §2.6.1 makes the seed a real
/// unsigned 64-bit value produced by the server's entropy, so "no seed supplied"
/// belongs to the call shape, not to the number — which is exactly what an
/// optional <see cref="BattleSeed"/> expresses and an optional <c>ulong</c>
/// could not.
///
/// <b>It carries no rule.</b> How a seed is produced is the server's
/// (<c>GAME_STATE.md</c> §2.6.1 items 1–2, <c>TDD.md</c> §6 item 3): this type
/// does not generate one, default one, or validate which values are acceptable,
/// and it is never populated from client input.
/// </summary>
/// <param name="Value">
/// The seed's unsigned 64-bit value, recorded as the battle's
/// <c>RngSeed</c> at creation (<c>GAME_STATE.md</c> §2.6.1).
/// </param>
public readonly record struct BattleSeed(ulong Value);
