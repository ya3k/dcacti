namespace GameServer.Domain.Match3;

/// <summary>
/// The PRNG state pair owned by <c>BattleState</c> (<c>GAME_STATE.md</c> §2.6.2).
///
/// <c>RngState</c> is a pair, not a single word: the generator selected by
/// <c>ADR-009</c> keeps a 64-bit internal state <b>plus</b> a 64-bit stream
/// selector, so a single 64-bit integer cannot represent it. The two components
/// are one logical field and are never split across separate
/// <c>BattleState</c> fields.
/// </summary>
/// <param name="State">
/// The PRNG's current internal state — the 64-bit LCG state of PCG-XSH-RR
/// 64/32 (<c>GAME_STATE.md</c> §2.6.2, <c>ADR-009</c>).
/// </param>
/// <param name="Increment">
/// The PRNG's stream selector (<c>GAME_STATE.md</c> §2.6.2). PCG-XSH-RR 64/32
/// requires this to be odd; <see cref="Pcg32"/> enforces that on seeding.
/// </param>
public readonly record struct RngState(ulong State, ulong Increment);

/// <summary>
/// PCG-XSH-RR 64/32 (PCG32) — the single gameplay PRNG (<c>ADR-009</c>).
///
/// The algorithm, its state/output widths, and its advancement step follow the
/// canonical PCG32 reference sequence exactly — not an approximation
/// (<c>ADR-009</c>: "the reference sequence must be implemented exactly (not
/// 'approximately') or determinism breaks"):
///
/// <code>
/// state       = oldstate * 6364136223846793005 + inc
/// xorshifted  = ((oldstate >> 18) ^ oldstate) >> 27
/// rot         = oldstate >> 59
/// output      = (xorshifted >> rot) | (xorshifted &lt;&lt; ((-rot) &amp; 31))
/// </code>
///
/// It is the only randomization mechanism for server-authoritative gameplay
/// (<c>ADR-009</c> §1, <c>AGENTS.md</c> §11): <c>System.Random</c>,
/// <c>Random.Shared</c>, cryptographic RNGs, <c>Guid</c>-derived values,
/// timestamps, and client-side sources must never produce a gameplay-relevant
/// value (<c>GAME_RULES.md</c> §18, <c>TDD.md</c> §6 item 3).
///
/// This type is deliberately mutable and stateful — it is the generator itself,
/// holding the state that <c>BattleState.RngState</c> stores. It is
/// framework-independent (<c>ARCHITECTURE.md</c> §2.1) and uses only
/// integer arithmetic with explicit 64-bit wrapping, so the stream is
/// bit-for-bit reproducible across machines and runtimes.
/// </summary>
public sealed class Pcg32
{
    /// <summary>PCG32's LCG multiplier (the canonical reference constant).</summary>
    public const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private ulong _increment;

    /// <summary>
    /// Creates a generator from an explicit <c>RngState</c> pair — the
    /// documented resume point (<c>GAME_STATE.md</c> §2.6.2: "where the stream
    /// resumes").
    /// </summary>
    /// <param name="state">The 64-bit internal state to resume from.</param>
    /// <param name="increment">
    /// The 64-bit stream selector. Must be odd, as PCG-XSH-RR 64/32 requires
    /// (<c>ADR-009</c> state row).
    /// </param>
    public Pcg32(ulong state, ulong increment)
    {
        if ((increment & 1UL) == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(increment),
                increment,
                "The PCG32 stream selector (increment) must be odd.");
        }

        _state = state;
        _increment = increment;
    }

    private Pcg32()
    {
    }

    /// <summary>
    /// Initializes a generator from the battle's server-chosen seed
    /// (<c>GAME_STATE.md</c> §2.6.1) using the canonical
    /// <c>pcg32_srandom_r(initstate, initseq)</c> seeding sequence.
    /// </summary>
    /// <param name="seed">
    /// The battle's <c>RngSeed</c> — an unsigned 64-bit value chosen by the
    /// server at battle creation (<c>GAME_STATE.md</c> §2.6.1). It is never
    /// supplied or influenced by the client.
    /// </param>
    /// <param name="stream">
    /// The stream/sequence selection constant. The documented contract requires
    /// only a seed (<c>GAME_STATE.md</c> §2.6.1) and defines no second
    /// client-visible value, so this defaults to the canonical stream <c>0</c>.
    /// </param>
    public static Pcg32 FromSeed(ulong seed, ulong stream = 0UL)
    {
        var rng = new Pcg32
        {
            _state = 0UL,
            // The stream selector must always be odd (ADR-009 state row).
            _increment = (stream << 1) | 1UL,
        };

        rng.NextUInt32();
        rng._state += seed;
        rng.NextUInt32();

        return rng;
    }

    /// <summary>
    /// The generator's current state, as the <c>RngState</c> pair stored in
    /// <c>BattleState</c> (<c>GAME_STATE.md</c> §2.6.2).
    /// </summary>
    public RngState CurrentState => new(_state, _increment);

    /// <summary>
    /// Advances the stream by one step and returns a uniformly distributed
    /// 32-bit value — the canonical <c>pcg32_random_r</c> step.
    ///
    /// This is the only place the state advances: <c>RngState</c> changes when a
    /// value is drawn and not otherwise (<c>GAME_STATE.md</c> §2.6.2 item 3).
    /// </summary>
    public uint NextUInt32()
    {
        var oldState = _state;

        _state = unchecked((oldState * Multiplier) + _increment);

        // XSH-RR output permutation.
        var xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        var rot = (uint)(oldState >> 59);

        return (xorshifted >> (int)rot) | (xorshifted << (int)((0u - rot) & 31u));
    }

    /// <summary>
    /// Advances the stream and returns a uniformly distributed value in
    /// <c>[0, bound)</c>, using the canonical <c>pcg32_boundedrand_r</c>
    /// rejection method so the result is unbiased.
    /// </summary>
    /// <param name="bound">The exclusive upper bound. Must be greater than zero.</param>
    public uint NextBounded(uint bound)
    {
        if (bound == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bound), bound, "Bound must be greater than zero.");
        }

        // threshold = (2^32 - bound) % bound, computed as -bound % bound to stay
        // in 32-bit arithmetic (canonical reference).
        var threshold = (0u - bound) % bound;

        while (true)
        {
            var value = NextUInt32();
            if (value >= threshold)
            {
                return value % bound;
            }
        }
    }
}