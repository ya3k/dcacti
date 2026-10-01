# Card Rules

**Version:** 1.5 (§4.1 — the three MVP Pet Skill Card effect magnitudes are
authored per TASK-110 Product Owner decisions D-1…D-6: Inferno 100 flat Fire
damage + Burn 50/tick for 2 Turns; Tidal Barrier Heal 20% Max HP + Shield
20% Max HP (refresh-not-stack); Iron Fang 120 flat damage + Crit +10
percentage points for the next attack only, independent of Bạch Hổ's Passive
config value. Prior 1.4: §1 item 5 — MVP `LoadoutCopyLimit` values recorded per
TASK-082 decisions C / R1-5 / R2-6: the value is **1** for every
CardDefinition defined by this document (§2 Basic Cards and §4.1 Pet
Skill Cards); prior 1.3: §1 loadout copy limit — each CardDefinition's explicit
`LoadoutCopyLimit` governs how many times it may appear in the submitted
3-card Basic loadout; explicit value required, no default; concrete
values are content/balance configuration; prior 1.2: §1 ownership
clarified — Player owns the Card collection; loadout is battle-scoped
for the active Pet; no `Pet.CardInventory`; prior 1.1: Basic Card
effects and Power cost clarify target = active Pet / PetState)
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

Expands GAME_RULES.md §11 (Card Rules). Conflicts resolve in favor of
GAME_RULES.md.

---

# 1. Card Categories

```text
Basic Card       shared across all Pets
Pet Skill Card    tied to the active Pet's Signature Skill (exactly one per Pet)
```

A battle loadout always contains exactly 3 Basic Cards + 1 Pet Skill Card
(GDD §3), for 4 total Cards available to cast.

**Ownership vs. loadout.** The Player owns the Card *collection*
(unlock rows in `DATABASE.md` §2 — `PlayerUnlockedCard`, ADR-012). The
battle loadout is selected at `POST /api/battle/start` for the one active
Pet and is snapshotted into `PetState.EquippedCards[]`
(`GAME_STATE.md` §2.3) for that battle only. There is **no**
`Pet.CardInventory` and no persistent per-Pet Card ownership: Cards are
owned by the Player, equipped per battle for the active Pet.

**Loadout copy limit (per CardDefinition).** The number of times one
Basic `CardDefinition` may appear in the submitted 3-card loadout is
governed by that CardDefinition's **loadout copy limit**
(`LoadoutCopyLimit`, `DATABASE.md` §1):

```text
For each CardDefinitionId in cardLoadout:
    occurrence count ≤  that CardDefinition's LoadoutCopyLimit
```

1. Duplicates are permitted up to the limit; a limit of 1 makes a
   Basic Card loadout-unique. `cardLoadout = [A, A, B]` is valid only
   if `LoadoutCopyLimit(A) ≥ 2`, and each entry independently satisfies
   this section's count (exactly 3), category (all `Category = Basic`),
   and ownership (unlocked `PlayerUnlockedCard` row) rules.
2. Every CardDefinition must define its limit explicitly; a missing
   value is invalid definition data. There is no default.
3. The limit counts occurrences in **one submitted battle loadout**.
   It is not inventory quantity, ownership quantity, or a collection
   limit: ownership remains one unlock row per (`Player`,
   `CardDefinition`) regardless of allowed copies (`PlayerUnlockedCard`,
   ADR-012 item 9), and no Card instance exists at any time.
4. The limit is read only for the submitted Basic loadout. The derived
   Signature Skill Card (`PetDefinition.SignatureSkillCardId`) is not
   submitted, never counted, and keeps its own composition slot (the
   loadout remains 3 Basic + 1 Pet Skill); a `PetSkill` CardDefinition
   can never satisfy this section's Basic-Card composition rule.
5. Concrete MVP limit values are recorded here (TASK-082 decisions C,
   R1-5, R2-6): **`LoadoutCopyLimit` = 1 for every CardDefinition
   defined by this document** — the §2 Basic Cards and the §4.1 Pet
   Skill Cards. For a `PetSkill` row the value is persisted only
   because the database column is required and non-nullable; it is
   never read for the submitted Basic loadout (item 4), so it
   introduces no additional gameplay behavior.

---

# 2. Basic Cards (MVP)

```text
Heal
  Cost:   20 Power
  Effect: Restore the active Pet's HP by 20% of its Max HP

Shield
  Cost:   20 Power
  Effect: Active Pet gains Shield equal to 20% of its Max HP

Power Charge
  Cost:   0 Power
  Effect: Active Pet gains 25 Power
```

Basic Card HP/Shield/Power effects target the **active Pet** (the combat
character, `PetState`) — not a separate Player entity.

Rules:

1. Basic Cards are identical for every Pet; no Pet-specific variation in MVP.
2. Basic Cards have no Element (ELEMENT_RULES.md §1.1) and resolve at Neutral
   modifier if they ever deal damage (none currently do).
3. "Power Charge" costs 0 Power by design — it exists as a resource-recovery
   option and must never be blocked by insufficient Power.

---

# 3. Casting Rules

1. A Card cast is an explicit player action, distinct from a Swap
   (GAME_RULES.md §11.1, §11.4).
2. On cast request, the server validates:
   * The Card is in the active Pet's battle loadout for this battle.
   * The active Pet has sufficient Power (`current Power ≥ Card Cost`).
   * Any additional Card-specific preconditions (none in MVP Basic Cards).
3. If validation fails, the cast is rejected and no state changes occur (no
   Power spent, no Effect applied, no Event emitted beyond a rejection
   response to the client).
4. If validation succeeds:
   ```text
   Deduct Cost from Power
        ↓
   Apply Effect
        ↓
   Emit CardCast event (and PetSkillCast, if applicable)
        ↓
   Allow Effect to trigger downstream Relics (RELIC_RULES.md §3, OnCardCast)
   ```
5. Casting a Card does NOT consume a Turn and does NOT interact with Combo —
   it is independent of the Match-3 Turn/Combo system (GAME_RULES.md §2, §5)
   unless a specific Card/Relic explicitly says otherwise.
6. Card casts are never authoritative from the client; the client sends a
   cast *request*, and the server computes and emits the actual result
   (GAME_RULES.md §18).

---

# 4. Pet Skill Card (Signature Skill)

1. Each Pet has exactly one Signature Skill, expressed as one Pet Skill Card
   (GAME_RULES.md §9.5).
2. The Pet Skill Card is only available while that Pet is the active Pet for
   the battle (PET_RULES.md §2.3).
3. Pet Skill Cards generally cost more Power than Basic Cards (see examples
   below), reflecting the "spend now vs. save for Skill" tension (GDD §10).
4. Pet Skill Cards may carry an Element (inherited from the Pet, or
   explicitly stated) if they deal damage, and go through the full Damage
   Pipeline including Element Modifier (COMBAT_RULES.md §3, ELEMENT_RULES.md §5).

## 4.1 MVP Pet Skill Examples

```text
Xích Lang — Inferno
  Cost:   100 Power
  Effect: Deal 100 Fire (Hỏa) damage (flat base value, entering the Damage
          Pipeline as the Card/Skill base value — COMBAT_RULES.md §3 step 1);
          apply Burn
  Burn:   50 damage per tick for 2 Turns (fixed; ticks once per resolved Turn
          at End Turn — GAME_RULES.md §17 step 19a, COMBAT_RULES.md §5.1/§5.2)

Huyền Quy — Tidal Barrier
  Cost:   80 Power
  Effect: Heal the active Pet for 20% of its Max HP; the active Pet gains
          Shield equal to 20% of its Max HP (refresh-not-stack —
          COMBAT_RULES.md §4)

Bạch Hổ — Iron Fang
  Cost:   100 Power
  Effect: Deal 120 damage (flat base value, entering the Damage Pipeline as
          the Card/Skill base value — COMBAT_RULES.md §3 step 1); increase
          Crit chance by 10 percentage points for the next attack only
```

Effect magnitudes above are the Product Owner's decided values and are owned
by this section. Burn ticks and Shield application follow the frozen contracts
cited inline; neither is restated here.

**Iron Fang's Crit increase is the Card's own value.** It is **independent**
of Bạch Hổ's Pet Passive configuration value (`PASSIVE_RULES.md` §7/§8) — the
two are separate sources that both modify Crit chance
(`COMBAT_RULES.md` §3.3 item 3) and neither is derived from, nor shared with,
the other.

Thanh Xà and Sơn Hùng Signature Skills are not yet content-defined; when
authored they must follow this same structure (Cost + Effect, consistent with
§4).

---

# 5. Cards vs. Relics

Per GAME_RULES.md §13.2 and GDD §9: Cards are **active** — the player chooses
when to cast them. Relics are **passive** — they react automatically to
events and are never directly cast by the player. A system must never blur
this line (e.g. a "Relic" that requires manual activation should instead be
modeled as a Card).

---

# 6. Events

```text
CardCast        emitted for every successful Basic Card or Pet Skill Card cast
PetSkillCast    emitted specifically when the cast Card is the active Pet's
                 Signature Skill (in addition to CardCast)
```

Both are part of the Battle Event Model (GAME_RULES.md §16) and fire at the
point shown in the Event Resolution Rules (GAME_RULES.md §17, step 14
"Resolve Player Effects").
