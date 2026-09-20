# Game Events

**Version:** 1.0
**Status:** Draft

> This document answers: **"What events exist during gameplay, and in what
> order can they occur?"** The canonical event *names* are listed in
> `GAME_RULES.md` §16; this document owns the detail (purpose, trigger, key
> payload fields, ordering) and is the authority when the two differ on
> anything beyond the name list.

Full handler implementation is not defined here — see `ARCHITECTURE.md`.

---

# 1. Event Ordering Within One Resolution

Events are emitted in the same order as the steps in `GAME_RULES.md` §17
(Event Resolution Rules). A single Swap may emit many events; the client
must apply them in the order received (`SIGNALR_PROTOCOL.md`).

```text
BattleStarted            (once, at battle start)
TurnStarted
SwapStarted
SwapResolved
  [per Match, possibly repeated across Cascades:]
  MatchCreated
  MatchResolved
  CascadeCreated          (only if this Match came from a Cascade)
  ComboChanged
  GemMatched               (once per Gem consumed)
PowerChanged
PassiveCharged             (once per Match, per PASSIVE_RULES.md §2)
PassiveTriggered            (only when threshold crossed)
RelicTriggered                (0..N, deterministic order, RELIC_RULES.md §4)
CardCast                       (only for a Card Cast action, not a Swap)
PetSkillCast                    (only when the cast Card is the Signature Skill)
DamageCalculated
DamageDealt
DamageTaken
BossSkillCast                    (only when the Boss's own timing fires it)
TurnEnded
BattleWon / BattleLost              (only when a HP reaches 0)
```

---

# 2. Event Reference

## BattleStarted
```text
Trigger:  Battle session created, before the first Turn
Payload:  BattleId, PetId, BossId, initial BattleState summary
```

## TurnStarted / TurnEnded
```text
Trigger:  Start/end of a Turn (GAME_RULES.md §2)
Payload:  Turn number
```

## SwapStarted / SwapResolved
```text
Trigger:  Client Swap request received / server finished validating+
          committing it (MATCH3_RULES.md §2)
Payload:  SwapStarted: from-cell, to-cell
          SwapResolved: whether the swap was committed or reverted
```

## MatchCreated / MatchResolved
```text
Trigger:  A Match is detected / its Gems are removed (MATCH3_RULES.md §3)
Payload:  Match shape (cells), Gem type, tier (3/4/5/L-T), Special Gem
          created (if any)
```

## CascadeCreated
```text
Trigger:  A Match detected after gravity+spawn, i.e. not the first match of
          the Swap (MATCH3_RULES.md §4)
Payload:  Cascade depth index within this Swap
```

## ComboChanged
```text
Trigger:  Combo value changes (increment on each Match within one Swap,
          reset to 0 on next Swap — GAME_RULES.md §5)
Payload:  New Combo value
```

## GemMatched
```text
Trigger:  Once per individual Gem consumed by a Match or a Special Gem
          detonation (MATCH3_RULES.md §5.5)
Payload:  Cell position, Gem type
```

## PowerChanged
```text
Trigger:  Power increases or decreases (GAME_RULES.md §12)
Payload:  Delta, new Power value, source (Gem match / Card cost / Relic)
```

## PassiveCharged / PassiveTriggered
```text
Trigger:  Passive progress increases / threshold reached
          (PASSIVE_RULES.md §2, §7)
Payload:  PassiveCharged: new progress value, threshold
          PassiveTriggered: effect summary
```

## RelicTriggered
```text
Trigger:  A Relic's Trigger+Condition is met (RELIC_RULES.md §3, §7)
Payload:  RelicId, effect summary, deterministic order index for this event
```

## CardCast / PetSkillCast
```text
Trigger:  A Card cast is validated and applied (CARD_RULES.md §3, §6)
Payload:  CardId, Power cost paid, effect summary
          PetSkillCast additionally confirms it was the active Pet's
          Signature Skill
```

## DamageCalculated / DamageDealt / DamageTaken
```text
Trigger:  Each stage of the Damage Pipeline (COMBAT_RULES.md §3)
Payload:  DamageCalculated: Base, Combo Modifier, Element Modifier, Other
          Modifiers, Defense, Final Damage (full breakdown, for client
          feedback per GDD Design Philosophy)
          DamageDealt / DamageTaken: source, target, Final Damage amount
```

## BossSkillCast
```text
Trigger:  Boss's own Skill timing rule fires (BOSS_RULES.md §4)
Payload:  SkillId, effect summary
```

## BattleWon / BattleLost
```text
Trigger:  Boss HP or Player HP reaches 0 (GAME_RULES.md §1.4)
Payload:  Outcome, final BattleState summary, reward summary (BattleWon
          only — exact reward data shape: DATABASE.md)
```

---

# 3. What This Document Does Not Define

1. Wire-level message envelope (method name, JSON shape sent over SignalR):
   see `SIGNALR_PROTOCOL.md`.
2. Whether/how events are persisted as a log: not persisted in MVP (see
   `ARCHITECTURE.md` §5.2 — no event sourcing).
3. Client-side handling of each event: client implementation detail, not a
   documentation concern.
