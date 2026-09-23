using GameServer.Application.Battle;

namespace GameServer.Application.Tests;

/// <summary>
/// A deterministic <see cref="IRngSeedSource"/> for tests
/// (<c>GAME_STATE.md</c> §2.6.1).
///
/// <b>Why this exists.</b> <see cref="BattleStateService"/>'s default constructor
/// draws its seed from the server's real entropy
/// (<see cref="SystemEntropyRngSeedSource"/>), so every battle a test creates has a
/// <b>different generated board</b>. That is correct for production — §2.6.1 item 2
/// assigns seed generation to server entropy — but it makes a test whose
/// expectation depends on the board's shape non-reproducible.
///
/// A scenario that asserts a documented rule must vary only what the rule is about.
/// When a test asserts "one Swap's damage does not kill the Boss", the board's Match
/// count is incidental; drawing it randomly makes the test pass or fail by luck.
/// This source pins the seed, so the board — and therefore the Match count, the
/// generated resources, and the damage — is the same on every run.
///
/// <b>It does not weaken the contract it tests.</b> §2.6.1 requires a
/// <i>server-side</i> seed, not an unpredictable one: the seed's origin is the
/// server process, and the interface exists precisely so the entropy source is a
/// replaceable dependency. Supplying a fixed one is the same substitution a test
/// makes when it passes a fixed <c>ComboModifiers</c> table instead of
/// <c>ComboModifiers.Default</c> — the documented rules are unchanged, and the
/// determinism <c>MATCH3_RULES.md</c> §7 requires is what makes the assertion
/// meaningful.
///
/// <b>The seed is not a gameplay value.</b> It selects which board is generated; no
/// Gem value, damage number, or rule outcome is derived from it directly
/// (<c>ADR-009</c>). Two runs with the same seed therefore produce the same battle,
/// which is exactly the reproducibility <c>GAME_STATE.md</c> §2.6.2 item 4 requires
/// of a recovered session.
/// </summary>
internal sealed class FixedRngSeedSource : IRngSeedSource
{
    /// <summary>
    /// The seed every battle in these tests is created with. It is an arbitrary
    /// constant — its <i>value</i> carries no meaning; what matters is that it is the
    /// same one every run, so the board is reproducible.
    /// </summary>
    public const ulong Seed = 20260722UL;

    /// <inheritdoc />
    public ulong CreateSeed() => Seed;
}
