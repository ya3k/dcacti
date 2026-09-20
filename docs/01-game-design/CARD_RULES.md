# Card Rules

**Version:** 1.0
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

---

# 2. Basic Cards (MVP)

```text
Heal
  Cost:   20 Power
  Effect: Restore 20% Max HP

Shield
  Cost:   20 Power
  Effect: Gain Shield equal to 20% Max HP

Power Charge
  Cost:   0 Power
  Effect: Gain 25 Power
```

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
   * The Card is in the player's current loadout for this battle.
   * The player has sufficient Power (`current Power ≥ Card Cost`).
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
  Effect: Deal high Fire (Hỏa) damage; apply Burn

Huyền Quy — Tidal Barrier
  Cost:   80 Power
  Effect: Heal; Gain Shield

Bạch Hổ — Iron Fang
  Cost:   100 Power
  Effect: High damage; increased Crit chance
```

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
