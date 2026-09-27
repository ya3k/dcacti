# ADR-016: Independent Player XP and Pet XP Progression Tracks

**Status:** Accepted
**Date:** 2026-09-27

## Context

ADR-012 defined Pet Level as a value derived from the Player's account
Level — `Pet.Level = clamp(floor(Player.Level × PetDefinition.PetLevelMultiplier),
1, 50)` — and ADR-011 item 7 closed the question of Pet progression by
stating that no Pet XP system exists. `PET_RULES.md` §5, `GAME_RULES.md`
§9.3, `MVP_SCOPE.md` §1, and `ADR-012` item 6 all restated that single-track
model.

That model has two consequences the product owner has now rejected:

1. **A Pet could not progress on its own.** A Pet's Level was a function of
   the account's Level times a per-definition multiplier, so two Pets owned
   by the same account were locked to the same input value, and a newly
   acquired Pet inherited the account's progression rather than starting its
   own. Pet identity is defined by Element + Passive + Signature Skill +
   statistics (`PET_RULES.md` §7), but the Pet's own growth had no
   independent input.
2. **A Pet could never earn anything from a battle it fought.** Combat
   progression and account progression were the same quantity, so the
   combat character's performance was not itself a progression input.

Separately, the Player's progression mechanism was stated only at the
mechanism level: ADR-012 item 2 recorded that Player Level "increases
through Meta Progression battle Rewards" while explicitly leaving the
amount and curve to a future balance task, and `PET_RULES.md` §5 item 4
deferred the same values. No document owned a Player XP formula, and
TASK-033 was blocked on exactly that gap.

The product owner has now supplied explicit decisions resolving the Player
half of that gap and confirming the structure of the Pet half. At the time
of this decision the Pet half's **balance values remained undecided**, and
this ADR deliberately did not supply them; those values were subsequently
finalized (see the Amendment below).

## Decision

```text
1.  Player owns persistent XP and Level for account/meta/content
    progression.

2.  Player Level range is [1, 50].

3.  Player Level has NO combat stats. Battle-time HP/ATK/DEF/Crit/Power
    remain the active Pet's PetState (ADR-011).

4.  BattleWon grants Player XP +100.

5.  BattleLost grants Player XP +0.

6.  The Player XP reward amount is a configuration value, not a formula
    constant. Its MVP initial value is 100.

7.  The Player XP progression curve constant is 100, and the Player Level
    formula is Player.Level = min(floor(Player.XP / 100) + 1, 50).
    The reward amount and the curve constant are two independent values
    that happen to be equal; neither may be derived from the other.

8.  Player XP is uncapped and continues to accumulate after Level 50.
    Player Level alone is capped at 50. No post-50 progression system
    (Prestige, Paragon, Season XP, or an additional XP currency) is
    introduced.

9.  A Pet owns its own XP and its own Level, as combat-character
    progression.

10. Pet Level is NOT derived from Player Level. The account's Level is
    never an input to any Pet attribute.

11. Pet XP belongs to the Pet INSTANCE, not to PetDefinition. A Pet
    definition is static content; progression is per owned Pet.

12. Player account progression and Pet combat progression are separate
    tracks that neither read nor modify each other.

13. The old Player.Level × PetLevelMultiplier derivation is RETIRED.
    PetLevelMultiplier has no role in the Pet XP model and is not
    reinterpreted as an XP curve multiplier, an XP reward multiplier, or
    a Pet Level input. Removing it from the persistent contract and from
    the implementation is follow-up work.

14. The Pet XP balance values and reward semantics were, at the time of this
    decision, UNRESOLVED human gameplay decisions. The complete list was
    owned by `PET_RULES.md` §5.2, which was the single enumeration of those
    open items and must not be duplicated:
```

**This ADR does not authorize implementation of unresolved Pet XP balance
or reward-targeting semantics.**

### Amendment — Pet XP balance finalized (TASK-062)

The twelve Pet XP balance/reward decisions deferred by item 14 above have
since been made by the product owner and are now the authoritative contract,
owned by `PET_RULES.md` §5.1–§5.5:

```text
Pet.XP persists with the Pet instance; initial 0; Pet.Level initial 1.
Pet Level range [1, 50].
BattleWon → active combat Pet +100 Pet XP; BattleLost → +0.
Inactive owned Pets → +0; no passive/shared Pet XP.
Pet.Level = min(floor(Pet.XP / 100) + 1, 50).
Pet XP is HARD-CAPPED at 4900 (stops at Level 50; no overflow) — a
  deliberate divergence from the uncapped Player track.
Pet and Player use the same formula shape but remain independent pools
  with independently configurable reward amounts.
```

Consequences of this amendment:

1. **Item 14's deferral is discharged.** The item is preserved above as the
   historical record; the values it deferred are no longer unresolved, and
   `PET_RULES.md` §5.2 is no longer an enumeration of open items.
2. **The authorization bar above is discharged for these semantics.** It
   prohibited implementing *unresolved* Pet XP balance or reward-targeting
   semantics. Those semantics are now resolved (item 1 above), so the bar no
   longer blocks their implementation. The bar continues to apply to any
   Pet XP semantics that remain undecided.
3. **No architectural change is introduced by this amendment.** Items 1–13
   of this ADR's Decision — the two-track ownership model, the Player
   contract, the Pet-instance ownership, and the `PetLevelMultiplier`
   retirement — are unchanged and remain Accepted. The finalized values are
   balance/configuration, which is not an architectural decision
   (`.ai/workflow/architecture/adr-change.md` §1; a balance number is not
   architectural).

**Related Documents** below is updated to point at the finalized sections;
the historical rationale of this ADR is otherwise preserved unmodified.

## Alternatives Considered

### Option A — Keep the single derived Pet Level track
Rejected: it is the model this decision replaces. Keeping it would leave
the Pet with no progression input of its own and would keep the account's
Level as the authority over a combat character's growth, contradicting the
Player-as-owner / Pet-as-combat-character split established by ADR-011.

### Option B — Independent Pet XP track that reuses the Player's numbers
Rejected: adopting the Player's reward amount, curve constant, level range,
or initial values for the Pet would decide Pet balance by analogy rather
than by a gameplay decision. The two tracks have different owners and
different progression roles; sharing numbers is a balance decision that has
not been made and must not be inferred.

### Option C — Retain `PetLevelMultiplier` as the Pet's XP curve multiplier
Rejected: this reinterprets an existing configuration field as a new kind
of value with no gameplay decision behind it. It would also leave the
field's purpose undocumented while silently giving it a meaning the rules
never assigned. A configuration field is not evidence of a rule.

### Option D — Decide the Pet XP balance values here as well
Rejected at the time: those values were human gameplay decisions that had
not been made. Recording them as unresolved was the correct outcome;
inventing them would have been exactly the failure mode `AGENTS.md` §7
exists to prevent. They were decided later by the product owner and are
recorded in `PET_RULES.md` §5.1–§5.5 (see the Amendment) — not in this ADR,
which continues to record only the architectural decision.

## Why

The two tracks answer two different questions, so they need two different
owners. Player XP answers "how far has this account progressed?" — a
meta/content question that survives every battle and every Pet the account
owns. Pet XP answers "how strong is this combat character?" — a question
that belongs to the individual Pet that actually fought, and that must be
able to differ between two Pets of the same account.

Deriving one from the other collapses those questions into one. The old
model made account progress and combat-character progress the same number,
which is why a Pet could not start its own progression and could not earn
anything from a battle. Separating them lets each track have its own
reward, curve, and balance without either becoming a hidden input to the
other.

Retiring `PetLevelMultiplier` follows from the same reasoning: with Pet
Level no longer derived from Player Level, the field has no remaining
documented purpose, and keeping a field whose meaning is undefined is how
undocumented rules appear in a codebase.

## Consequences

### Positive
- Pet progression has an owner, a stored input (`Pet.XP`), and a defined
  direction (`Pet.XP → Pet.Level`) instead of being a projection of the
  account's Level.
- Account progression has an explicit, deterministic contract: a persisted
  `Player.XP`, a reward per battle outcome, and a Level formula, replacing
  the previously deferred curve.
- The two tracks can be balanced independently; retuning the Player reward
  no longer perturbs Pet progression, and vice versa.
- The Pet balance was, at the time of this decision, recorded as a blocking,
  explicitly open question rather than being silently defaulted. It has
  since been finalized (see the Amendment).

### Negative
- **Documentation vs. implementation divergence is expected until a
  follow-up implementation task lands.** The current source code and its
  tests still implement the retired derivation and still declare that no XP
  column exists. Aligning them with this decision is a separate
  implementation task and is not part of this decision — the specific
  artifacts are recorded in TASK-059's Completion Evidence, not here
  (`.ai/workflow/architecture/adr-change.md` §1: an ADR records the *why*).
- Pet progression was not playable when this decision was made: `Pet.XP`
  persisted but no Pet reward, curve, or level-up rule was defined. The
  reward, curve, and cap rules have since been finalized (`PET_RULES.md`
  §5.3–§5.5); implementation remains a separate task.
- `RewardSummary`'s Pet-track **member list** remains deferred to the
  implementation task. The reward *semantics* it would project are now
  decided (`PET_RULES.md` §5.3), so this is a representation decision, not
  an open gameplay decision (`DATABASE.md` §1).
- The Player's post-Level-50 behavior is "XP keeps accumulating with no
  further effect," which is a deliberately thin answer that a later
  decision may revisit.

### Trade-offs
- At the time of this decision, recording the Pet XP balance as unresolved
  rather than selecting provisional defaults kept the documentation honest
  at the cost of an incomplete Pet track. The alternative — placeholder
  values — would have created a second, undocumented source of truth for
  gameplay balance. The balance was subsequently decided explicitly by the
  product owner, which is the outcome this trade-off was preserving room for.

## Related Documents

- `docs/01-game-design/COMBAT_RULES.md` (§7 — canonical Player XP / Level
  contract: reward, curve, uncapped-XP / capped-Level, no combat stats)
- `docs/01-game-design/PET_RULES.md` (§5.1 — Pet XP / Level ownership;
  §5.2 — initial values; §5.3 — reward targeting; §5.4 — XP → Level formula;
  §5.5 — XP cap and post-cap behavior; §5.6 — retired terms)
- `docs/01-game-design/GAME_RULES.md` (§9.3, §20)
- `docs/01-game-design/COMBAT_RULES.md` §1.1 (`PetState` combat stats)
- `docs/00-overview/MVP_SCOPE.md` (§1 Player, §1 Pets, §4)
- `docs/00-overview/GDD.md` (§6, §14)
- `docs/02-technical/DATABASE.md` (§1, §3 — `Player.XP`, `Pet.XP`,
  `RewardSummary` ownership and Pet-track dependency)
- `docs/02-technical/API_CONTRACTS.md` (§4 notes 1, 3)
- `docs/02-technical/GAME_EVENTS.md` (§2 `BattleWon` / `BattleLost`)
- `docs/02-technical/GAME_STATE.md` (§2.3)
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md`
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md`
