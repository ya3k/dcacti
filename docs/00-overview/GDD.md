# Game Design Document (GDD)

**Project:** Match-3 RPG Discord Activity
**Version:** 2.2 (Player Level defined for MVP — persistent account
attribute, 1–50, source of Pet Level; ADR-012; prior 2.1: Player =
account/owner / Pet = combat character clarified)
**Status:** MVP Design
**Platform:** Discord Activity
**Genre:** Match-3 RPG / Boss Battler
**Players:** Single-player battle experience
**Core Technology:** React + TypeScript + Vite + Phaser 4

> This document answers: **"What is this game?"**
> It does not define exact rules, formulas, or numbers. For those, see
> `GAME_RULES.md` and the domain rule documents in `docs/01-game-design/`.

---

# 1. Game Overview

## 1.1 Concept

A Match-3 RPG where players collect multiple elemental Pets, build them with
Cards and Relics, and exploit the Five Elements to defeat challenging Bosses.

The game combines Match-3 puzzle gameplay, RPG combat, collectible Pets, a
Five Elements counter system, Cards, Relics, and Boss mechanics.

The player makes tactical decisions through board manipulation and resource
management rather than relying only on raw character statistics.

## 1.2 Core Identity

> **Pet quyết định bạn là ai.
> Ngũ hành quyết định bạn khắc ai.
> Card quyết định bạn làm gì.
> Relic quyết định build của bạn.
> Boss quyết định bạn phải thích nghi thế nào.**

## 1.3 Design Pillars

1. **Match-3 First** — the board is the primary gameplay interaction; players
   continuously make meaningful swap decisions.
2. **Build Diversity** — Pets, Cards and Relics allow different approaches to
   the same battle.
3. **Elemental Counterplay** — the Five Elements provide a clear matchup
   system without overcomplicating it.
4. **Reactive Bosses** — Bosses create pressure through mechanics, not just HP.
5. **Simple Core, Expandable Systems** — the MVP establishes a strong core
   loop while leaving room for future systems (Map, Environment, PvP, etc.).

---

# 2. Core Gameplay Loop

```text
Enter Battle → Choose Pet → Equip Cards → Equip Relics → Start Battle
  → Swap Gems → Match → Cascade → Combo → Generate Resources
  → Charge Passive → Trigger Relics → Use Cards / Pet Skill
  → Calculate Damage → Apply Element Modifier → Boss Responds
  → Continue Battle → Boss Defeated → Rewards
```

Exact step-by-step resolution order is defined in `GAME_RULES.md`
("Event Resolution Rules"). The battle ends when either the Boss or the
active Pet reaches 0 HP.

---

# 3. Battle Structure

A battle consists of 1 Player (the account/owner), 1 selected Pet (the
combat character), 1 Boss, 1 Match-3 board, 3 Basic Cards, 1 Pet Skill
Card, and 3–5 Relics equipped on the active Pet.

The player may own many Pets but uses exactly one Pet per battle. The
Player owns the collections (Pets, Cards, Relics) and issues actions; the
active Pet is what fights and what can reach 0 HP.

Exact counts and invariants: see `GAME_RULES.md` §1 (Core Battle Rules).

---

# 4. Match-3 System

The board is an 8×8 grid of functional Gems (ATK, DEF, HP, POWER). Gems carry
no elemental identity — Element belongs to Pets, Bosses, Skills and Effects.

Players Swap two adjacent Gems to form a Match of 3 or more. Matches can
cascade (removed Gems trigger gravity and new spawns, which may create
further Matches). A Combo counts consecutive Matches/Cascades from a single
Swap and increases damage.

Exact board size, swap validation, match detection, cascade loop, Special
Gem behavior (Match 4 / Match 5 / L-T), and Combo multipliers: see
`GAME_RULES.md` §2–§7 and `MATCH3_RULES.md`.

---

# 5. Five Elements

The game uses the Asian Five Elements (Mộc, Hỏa, Thổ, Kim, Thủy) with only
the Tương Khắc (overcoming) relationship in MVP — Tương Sinh is out of scope.

An attack's Element against a defending Element resolves as Advantage,
Neutral, or Disadvantage, modifying damage accordingly. Element never
restricts which Cards, Relics, or Passives a Pet can use.

Exact Tương Khắc cycle and damage modifier values: see `GAME_RULES.md` §8
and `ELEMENT_RULES.md`.

---

# 6. Pet System

Players can collect multiple Pets; one Pet is active per battle. A Pet has
an Identity, an Element, a Tier (Common → Mythic), a Star (1–5), a Level
(1–50, derived from the account's Player Level — `PET_RULES.md` §5), Stats,
a Passive, and one Signature Skill.

Higher Tier must not be a simple stat multiplier — it should add strategic
distinctiveness through Passive, Skill, or identity, not just bigger numbers.

MVP Pets:

```text
Thanh Xà  → Mộc
Xích Lang → Hỏa
Sơn Hùng  → Thổ
Bạch Hổ   → Kim
Huyền Quy → Thủy
```

Exact Tier/Star/Level rules and Passive definitions: see `PET_RULES.md` and
`PASSIVE_RULES.md`.

---

# 7. Pet Passive System

Passives are triggered primarily by Matches: progress accumulates each
Match, and reaching a threshold triggers the Passive's effect, then resets.
Progress should be visible to the player (e.g. "7 / 10 Matches").

Exact per-Pet thresholds and effects: see `PASSIVE_RULES.md`.

---

# 8. Card System

Cards represent direct player actions and are cast by spending Power. MVP has
two categories:

* **Basic Cards** — shared across all Pets (Heal, Shield, Power Charge).
* **Pet Skill Card** — each Pet's unique Signature Skill (e.g. Xích Lang's
  *Inferno*, Huyền Quy's *Tidal Barrier*, Bạch Hổ's *Iron Fang*).

Cards are active player choices; Relics are passive build modifiers that
react automatically — the two must never be confused.

Exact Power costs and effects: see `CARD_RULES.md`.

---

# 9. Power System

Power (range 0–100) is the primary resource for casting Cards and Pet
Skills; during battle it is the active Pet's resource. It is generated by
matching POWER Gems, Special Matches, and Relics.
The player continuously decides between spending Power for survival now or
saving it for a stronger Pet Skill later.

Exact generation rates and Power rules: see `GAME_RULES.md` §12 and
`COMBAT_RULES.md`.

---

# 10. Relic System

Relics passively modify the build; MVP allows 3–5 Relics equipped on the
active Pet per battle (per-Pet loadout, selected by the Player).
Relics are not restricted by Pet Element and can trigger from events such as
Matches, Combo, Damage, HP thresholds, or Card casts.

Exact MVP Relic list, trigger rules, and stacking/determinism rules: see
`RELIC_RULES.md`.

---

# 11. Combat System

Active Pet and Boss combat stats include HP, Max HP, ATK, DEF, Power,
Crit, and Status Effects — there is no separate Player combat-stat pool.
Damage conceptually flows through Base Damage → Combo Modifier → Element
Modifier → Other Modifiers → Defense → Final Damage.

Exact formulas: see `COMBAT_RULES.md`.

---

# 12. Boss System

Bosses should create difficulty through mechanics, not just high HP. Every
Boss has an Element, HP/ATK/DEF, a Passive that reacts to player/battle
state, and a Skill it initiates on its own timing. Not every Boss should use
the same trigger pattern.

Exact MVP Boss list and mechanics: see `BOSS_RULES.md`.

---

# 13. Battle Events & Server Authority

The battle engine produces events (e.g. `MatchCreated`, `DamageDealt`,
`BattleWon`) so systems can react without tight coupling. The server is
authoritative for all gameplay results (damage, HP, Power, Match/Combo
results, Passive progress, rewards); the client only sends actions and
renders resulting events.

Full event list, resolution order, and server-authority rules: see
`GAME_RULES.md` §16–§18.

---

# 14. Meta Progression

MVP persistent systems: Player (account/owner), Player Level (1–50 —
drives Pet Level via `PET_RULES.md` §5; increases through battle
Rewards), Pet Collection, Pet progression, Card Collection, Relic
Collection, Battle Results, Rewards. The Player owns the collections;
battle-time combat state belongs to the active Pet (`PetState`), not to a
Player combat pool. Player Level itself carries no combat stats.

---

# 15. MVP Scope

Full, authoritative IN / OUT / FUTURE classification: see `MVP_SCOPE.md`.

---

# 16. Future Expansion

Build phases and post-MVP direction: see `ROADMAP.md`. Future systems must
not compromise the clarity of the Match-3 + Combat core.

---

# 17. Design Philosophy

The player should always understand: what Gems do, why a Match is valuable,
what their Pet Passive does and when it triggers, how Elements affect
damage, what each Card/Relic changes, why the Boss is dangerous, and what
strategic choice comes next.

Complexity should come from meaningful interaction between systems, not from
unclear rules.
