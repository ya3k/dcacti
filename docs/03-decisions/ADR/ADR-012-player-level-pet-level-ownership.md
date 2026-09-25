# ADR-012: Player Level, Pet Level Formula, Ownership/Equipment Boundaries, and MVP Scope Closure

**Status:** Accepted
**Date:** 2026-09-24

## Context

ADR-011 established the Player = account/owner / Pet = combat character /
`PetState` = battle runtime model, but deliberately left three OPEN
conflicts reported in `PET_RULES.md` §5.1 and one scope gap:

1. **Player Level was undefined.** The formula
   `Pet Level = Player Level × Pet Level Multiplier` (now in
   `GAME_RULES.md` §9.3 and ADR-011) has no owning definition for its
   Player Level input — range, persistence, or how it increases.
2. **The 1–50 level-cap interaction was unresolved.** The historical
   `Range: 1–50` on Pet Level predates the formula; it was unclear whether
   it clamps the formula result, constrains Player Level, or is superseded.
3. **Tier/Star independence was unstated.** Making Level account-derived
   raised whether Tier and Star also derive from Player Level.
4. **`MVP_SCOPE.md` did not list Player Level.** Per `MVP_SCOPE.md` §4,
   anything absent from §1 and §2 defaults to FUTURE — yet the formula
   requires Player Level for MVP Pet Level progression, which §1 lists as
   IN ("Tier, Star, Level progression").

Separately, ownership/equipment for Relics and Cards had residual
ambiguity (`DATABASE.md` Card ownership was an ASSUMPTION; Relic equip
had no explicit battle-start snapshot wording), and the task scope
included Level/Evolution interaction — **no Evolution system exists in
any document**.

Wire labels `playerState`, `finalPlayerHp`, and `target="player"` were
already fixed protocol labels under ADR-011 and are not renamed here.

## Decision

Twelve numbered decisions, all documentation-level (no code, no
migrations in this change):

```text
1.  Player Level is an MVP persistent account attribute, range 1–50,
    stored on Player (DATABASE.md §1), listed IN in MVP_SCOPE.md §1.
    It carries NO combat stats (ADR-011 single combat-stat home:
    PetState).

2.  Player Level increases through Meta Progression battle Rewards
    (GDD §14). Exact XP amounts/curve are balance/config concerns and
    are not owned by this ADR or by PET_RULES.md §5.

3.  Pet Level formula (resolved form; operational semantics completed in
    `PET_RULES.md` §5):
        Pet.Level = clamp(
            floor(Player.Level × PetDefinition.PetLevelMultiplier),
            1,
            50
        )
    Pet Level Multiplier remains a per-Pet configuration value on
    PetDefinition (DATABASE.md §1), never hard-coded. Type `decimal`,
    range `> 0`, `floor` rounding, floor before clamp — owned solely by
    `PET_RULES.md` §5.

4.  The historical 1–50 range clamps the FORMULA RESULT (after `floor`).
    Both Player Level and Pet Level independently respect [1, 50].
    Neither range supersedes the other.

5.  Tier and Star remain INDEPENDENT progression axes. They are not
    derived from Player Level. PET_RULES.md §3–§4 are unchanged in
    substance; only Level is account-derived.

6.  There is no Pet XP system and no Evolution system. Evolution is
    out of scope (MVP_SCOPE.md §4 default FUTURE; no rules exist).
    There is no Level/Evolution interaction to specify.

7.  Relic ownership vs. equipment:
    - Ownership (persistent): Player owns Relic instances
      (DATABASE.md §1–§2).
    - Equipment (battle-scoped): 3–5 owned Relics selected at
      POST /api/battle/start for the one active Pet; locked for the
      battle; no persistent equip table (RELIC_RULES.md §2).

8.  Relic equipment runtime representation: the selected loadout is
    snapshotted into PetState.EquippedRelics[] at battle start with
    fixed slot order (GAME_STATE.md §2.3, RELIC_RULES.md §4). That
    array travels inside BattleState in Redis (REDIS_STATE.md §2);
    it does not write back to PostgreSQL ownership rows.

9.  Card ownership model (resolves DATABASE.md §2 ASSUMPTION):
    PlayerUnlockedCard(PlayerId, CardDefinitionId) unlock-flag join
    table. MVP Cards have no Tier/Star/Level, so an instance table is
    unnecessary. No Pet.CardInventory exists or may be introduced.

10. Card loadout is battle-scoped for the active Pet: exactly 3 Basic +
    1 Pet Skill Card selected at battle start, snapshotted into
    PetState.EquippedCards[] (CARD_RULES.md §1, GAME_STATE.md §2.3).

11. Wire labels playerState, finalPlayerHp, and target="player" remain
    fixed protocol labels (reaffirming ADR-011 item 6). Their doc
    descriptions already correct them to Combo/MatchCount, active-Pet
    HP, and player's-side semantics respectively. Renaming is a future
    protocol-breaking task, not this ADR.

12. MVP scope closure: Player Level is added to MVP_SCOPE.md §1; Pet
    Level progression remains IN and is now fully specified; no Player
    combat pool is introduced; BattleState shape is unchanged (no
    PlayerState node — ADR-011).
```

## Consequences

### Positive
- `PET_RULES.md` §5.1 carries no open items; the Pet Level formula is
  fully specified and deterministic.
- MVP scope and the formula agree — no FUTURE-default conflict.
- Relic/Card ownership vs. equipment is unambiguous at both the DB and
  the battle-state layers.
- Evolution is explicitly closed, preventing a phantom Level/Evolution
  design thread.

### Negative
- Player Level progression mechanism (Reward XP) is stated at the
  mechanism level only; exact XP numbers remain a future balance task.
- `Pet.Level` in PostgreSQL is a denormalized snapshot of a derived
  value; if Player Level or Multiplier changes outside battle, the
  stored Pet.Level must be recomputed (implementation concern, not
  specified further here).
- Wire labels remain semantically misleading until a deliberate protocol
  version bump (carried forward from ADR-011).

### Trade-offs
- Option A (minimal Player Level in MVP) was chosen over Option B
  (defer Player Level / temporary MVP Pet Level rule) because Option B
  would have superseded the formula already published in
  `GAME_RULES.md` §9.3 and ADR-011, reintroducing a second authority
  for the same value.

## Alternatives Considered

### Option B — Defer Player Level; temporary MVP Pet Level rule
Rejected: would require either reverting to an independent Pet Level
axis (contradicting ADR-011 / GAME_RULES §9.3) or inventing a second
MVP-only formula — two authorities for one value.

### Option A-variant — Player Level in MVP but leave increase mechanism OPEN
Rejected: leaves a missing rule (how Player Level increases) that
battle Rewards implementation would need; mechanism-level statement
(Rewards/XP) is the smallest complete rule, with numbers deferred to
balance config as elsewhere in these docs.

### Evolution stub in MVP_SCOPE §3 FUTURE
Rejected per product decision: no Evolution text exists anywhere;
adding a FUTURE stub would acknowledge a system no design document
describes. FUTURE-by-default (§4) already covers any later proposal.

## Related Documents

- `docs/00-overview/MVP_SCOPE.md` (§1 Player, §1 Pets, §4)
- `docs/00-overview/GDD.md` (§6, §14)
- `docs/01-game-design/GAME_RULES.md` (§9.3)
- `docs/01-game-design/PET_RULES.md` (§5, §5.1, §6)
- `docs/01-game-design/RELIC_RULES.md` (§2, §4)
- `docs/01-game-design/CARD_RULES.md` (§1)
- `docs/02-technical/DATABASE.md` (§1, §2, §3, §4)
- `docs/02-technical/GAME_STATE.md` (§2, §2.3)
- `docs/02-technical/REDIS_STATE.md` (§2)
- `docs/02-technical/API_CONTRACTS.md` (§3, §5)
- `docs/02-technical/SIGNALR_PROTOCOL.md` (§4.2, §4.3)
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md`
