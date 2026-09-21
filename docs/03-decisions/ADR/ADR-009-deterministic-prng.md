# ADR-009: Deterministic PRNG for Server-Authoritative Gameplay Randomness

**Status:** Proposed
**Date:** 2026-09-19

> **Status note.** This decision was not previously established by any
> document: `MATCH3_RULES.md` §7 and `TDD.md` §6 required a *server-seeded*
> RNG without naming one. Per `docs/03-decisions/README.md` §5, `Accepted`
> is reserved for decisions already established in `docs/00-overview/`,
> `docs/01-game-design/`, or `docs/02-technical/` — so this ADR is
> `Proposed` until the algorithm is owned by a technical document. The
> normative state contract already lives in `GAME_STATE.md` §2.6; the
> algorithm selection below becomes `Accepted` once that ownership is
> confirmed by a human decision.

## Context

`MATCH3_RULES.md` §1.2 requires the initial board to be produced by a
deterministic row-major constrained random fill, and §7 requires that Gem
values spawned into empty cells use a **server-seeded RNG**. `TDD.md` §6 states
the RNG seed is part of Active Battle State so a recovered/reconnected session
produces identical results, and `GAME_STATE.md` §2 already reserves
`RngSeed` / `RngState` as `BattleState` fields.

No document specifies **which** algorithm that RNG is. "Server-seeded RNG" is
a property, not an implementation: it does not say how a value is produced,
how the stream advances, or how a value is mapped onto the four Gem types.
Without that, `MATCH3_RULES.md` §7 item 1's determinism requirement ("given the
same board state and the same Swap input … must be fully deterministic") is not
satisfiable, and the first implementation task would have to invent an
algorithm — exactly the unnamed architectural decision `AGENTS.md` §18
forbids introducing silently inside a code change.

## Decision

Gameplay randomness uses **PCG-XSH-RR 64/32** (PCG32) as the single
randomization mechanism for server-authoritative gameplay.

```text
Algorithm:  PCG-XSH-RR 64/32 (PCG32) — O'Neill, "PCG: A Family of Simple
            Fast Space-Efficient Statistically Good Algorithms"
State:      64-bit LCG state + 64-bit stream selector (the increment)
Output:     32-bit value
Sequence:   the canonical PCG32 reference sequence
```

1. There is exactly one gameplay PRNG. It is never used for anything other
   than server-authoritative gameplay randomness (`AGENTS.md` §11: no second
   randomization mechanism alongside the documented one).
2. `Random.Shared`, `System.Random`, `Guid`-derived values, timestamps,
   thread-local generators, and any client-side source are **not** gameplay
   RNG and must never produce a gameplay-relevant value (`GAME_RULES.md` §18,
   `TDD.md` §6 item 3).
3. Actual Gem values are always derived from the PRNG's output by the
   documented conversion (`MATCH3_RULES.md` §1.1–§1.2), never by re-seeding.
   The board-generation algorithm is a **consumer** of this PRNG, not a
   replacement for it: the constrained fill of `MATCH3_RULES.md` §1.2.1
   changes which values are drawn and in what order, and changes nothing
   about the generator, its state, or its advancement.

**Where this decision's detail lives.** This ADR records *why* PCG32 was
chosen. The normative contract a future implementer must satisfy is owned by
the technical documents, not restated here:

```text
Seed representation and source    GAME_STATE.md §2.6.1
State representation, pair shape  GAME_STATE.md §2.6.2
Value → Gem type conversion       MATCH3_RULES.md §1.1–§1.2
Board fill algorithm and its
  per-cell RNG consumption        MATCH3_RULES.md §1.2.1, §1.2.1.4
Which operations consume the RNG  MATCH3_RULES.md §1.2.1.4, §4, §7
Determinism requirement           MATCH3_RULES.md §7, TDD.md §6
```

Algorithm identity, state/output width, and advancement follow PCG32's
published reference definition; the state row above matches the two-word
`RngState` contract in `GAME_STATE.md` §2.6.2.

## Why PCG32

Required properties, in the order the surrounding documentation imposes them:

```text
Reproducible bit-for-bit across runs, instances, and machines
    → needs a fully specified integer algorithm, not a library default
    → PCG32 is defined by an exact reference sequence, so two
      implementations agree without sharing a library
Cheap to serialize inside BattleState (GAME_STATE.md §2, §2.6)
    → needs tiny, integer-only state: 16 bytes (state + increment)
    → excludes generator objects with large internal tables
Deterministic tests (AGENTS.md §15)
    → needs a stream that can be advanced from a known state and compared
      against fixed expected values
Server-authoritative and non-reversible from the client
    → needs state that is not derivable from observed outputs
    → a 64-bit-state / 32-bit-output design satisfies this; a seed-only
      generator (e.g. hashing a counter) would not
Cross-runtime compatibility
    → integer-only arithmetic with explicit 64-bit wrapping; no floating
      point in the stream, so no platform-dependent rounding
```

`System.Random` was rejected because its algorithm is not contractually fixed
across .NET versions, which breaks the "recovered session replays identically"
requirement in `TDD.md` §6 item 2. Mersenne Twister was rejected as oversized for
the need (a large state array inside `BattleState` for no gameplay benefit).
Neither is a *correctness* problem alone, but both would make the determinism
requirement depend on a runtime implementation detail rather than on a
documented contract.

## Alternatives Considered

### Option A — `System.Random` with a Fixed Seed
Seed `System.Random` from `RngSeed` and use `Next(0, 4)` for Gem selection.
Rejected: the generated sequence is a runtime implementation detail, not a
documented contract, so determinism would not survive a .NET upgrade, and
`TDD.md` §6 item 2's replay-identical requirement could not be guaranteed.

### Option B — Cryptographic RNG (`RandomNumberGenerator`)
Use a CSPRNG and record its seed. Rejected: cryptographic strength is not a
requirement (MVP is single-player-vs-Boss, `TDD.md` §7), and a CSPRNG's
internal state is not designed to be snapshotted into `BattleState` and
resumed, which `TDD.md` §6 item 2 requires.

### Option C — Named Hash Stream (`hash(seed, counter)`)
Derive each value by hashing the seed plus a monotonically increasing counter.
Rejected: cheap and reproducible, but outputs are derivable from the seed
alone and it provides a weaker state model; it also invites re-deriving values
from a counter rather than advancing documented state, which complicates
snapshot/recovery semantics.

### Option D — PCG32 (Chosen)
Small serializable integer state, an exact reference sequence, integer-only
arithmetic, and a well-defined jump-ahead for testing.

## Consequences

### Positive
- Determinism is a documented contract rather than a runtime property, so
  board generation is reproducible across instances, machines, and .NET
  versions.
- Snapshot/recovery (`ADR-008`) works naturally: `RngState` is small enough to
  live inside `BattleState` and resume exactly.
- Tests can assert fixed expected sequences, so a future implementation cannot
  silently drift.

### Negative
- The project owns the generator's correctness rather than delegating it to a
  framework type; the reference sequence must be implemented exactly (not
  "approximately") or determinism breaks.
- Two integers must be threaded through battle state and any future
  resolution step that consumes randomness.

### Trade-offs
- Choosing an explicitly specified small generator means slightly more
  up-front implementation care than calling `Random.Shared`, in exchange for
  the determinism guarantees `MATCH3_RULES.md` §7 and `TDD.md` §6 already
  require. Statistical quality beyond "adequate for a 4-way Gem draw" was not
  pursued, because nothing in the documentation asks for it.

## Related Documents

- `docs/01-game-design/MATCH3_RULES.md` (§1.1–§1.2, §1.2.1, §7)
- `docs/01-game-design/GAME_RULES.md` (§18)
- `docs/02-technical/TDD.md` (§6)
- `docs/02-technical/GAME_STATE.md` (§2, §2.6, §2.7)
- `docs/02-technical/SIGNALR_PROTOCOL.md` (§4)
- `docs/02-technical/REDIS_STATE.md` (§7)
- `docs/02-technical/ARCHITECTURE.md` (§2.2.1)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md`
- `docs/AGENTS.md` (§11, §18)
