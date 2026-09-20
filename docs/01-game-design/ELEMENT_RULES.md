# Element Rules

**Version:** 1.0
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

Expands GAME_RULES.md §8 (Element Rules). Conflicts resolve in favor of
GAME_RULES.md (see §20 there).

---

# 1. The Five Elements

```text
Mộc  (Wood)
Hỏa  (Fire)
Thổ  (Earth)
Kim  (Metal)
Thủy (Water)
```

## 1.1 Element Ownership

Only the following entities carry an Element:

```text
Pet
Boss
Skill (Pet Signature Skill / Boss Skill)
Effect (e.g. Burn = Hỏa, a Shield effect could be elementless, etc.)
```

Gems, Cards (Basic), and Relics do **not** carry an Element by default.
A Card or Relic may reference or interact with an Element (e.g. "Burning Curse"
increases Burn damage) without itself belonging to an Element.

## 1.2 One Element Per Entity

Each Pet and each Boss has exactly one Element. MVP does not support dual/multi
element entities.

---

# 2. Tương Khắc (Overcoming Cycle)

MVP implements only the Tương Khắc relationship. Tương Sinh (generating cycle) is
explicitly out of scope (GAME_RULES.md §8.7).

```text
Mộc  →  Thổ
Thổ  →  Thủy
Thủy →  Hỏa
Hỏa  →  Kim
Kim  →  Mộc
```

Reading: the element on the left **counters** the element on the right.

## 2.1 Matchup Resolution

For an attack with element `A` against a defending entity with element `D`:

```text
IF A counters D (A → D in the cycle above)         → Advantage
ELSE IF D counters A (D → A in the cycle above)     → Disadvantage
ELSE (A == D, or no defending element)              → Neutral
```

There is no partial/graduated advantage. A matchup is exactly one of
Advantage / Neutral / Disadvantage.

## 2.2 Default Modifiers

```text
Advantage     = 1.50×
Neutral       = 1.00×
Disadvantage  = 0.75×
```

These are configuration values (see COMBAT_RULES.md for where they apply in the
damage pipeline), not hardcoded constants. Changing them is a balance change and
does not require a rule-conflict process unless it changes the *relationship*
structure itself (e.g. adding Tương Sinh).

---

# 3. Elementless Attacks

Some damage sources may have no Element (e.g. a Basic Card with no elemental
tag, or a pure "true damage" effect). Rule:

1. An elementless attack against any defending Element resolves as **Neutral**
   (1.00×).
2. An attack against an elementless target (if any such target exists) also
   resolves as **Neutral**.

---

# 4. Element Does Not Restrict Builds

Per GAME_RULES.md §8.2–8.6:

1. Element does not determine which Cards a Pet may equip/use.
2. Element does not determine which Relics a Pet may equip.
3. Element does not gate Pet Passive design — Passive design is free-form.
4. A Pet may apply, benefit from, or be built around an Effect belonging to a
   different Element than itself (e.g. a Thủy Pet using a Burn/Hỏa-flavored
   Relic such as "Burning Curse").
5. Element is purely a **matchup system** layered on top of damage instances; it
   is never a hard gameplay gate.

---

# 5. Where Element Applies

Element Modifier (§2.2) applies to any discrete damage instance that has both:

* an attacking Element (from the Pet, Boss, Skill, or Effect dealing the damage), and
* a defending Element (from the Pet or Boss receiving the damage).

This includes, non-exhaustively:

```text
Pet Signature Skill damage
Boss Skill damage
Status Effect damage-over-time ticks (e.g. Burn), using the Effect's Element
Basic Card damage, if the Basic Card is elementally tagged (none are, in MVP)
```

It does **not** apply to non-damage effects (Heal, Shield amount, Power gain,
stat buffs) even if those effects are thematically tied to an Element.

Exact placement of the Element Modifier within the full damage formula is
defined in COMBAT_RULES.md §3 (Damage Pipeline).

---

# 6. MVP Element Assignments

```text
Pet         Element
---------   -------
Thanh Xà    Mộc
Xích Lang   Hỏa
Sơn Hùng    Thổ
Bạch Hổ     Kim
Huyền Quy   Thủy
```

Boss element assignments are defined in BOSS_RULES.md.

---

# 7. Future Expansion (Not MVP)

Explicitly deferred, listed here only for awareness — do not implement without
going through the Rule Change Policy (GAME_RULES.md §20):

* Tương Sinh (generating cycle) as a secondary, weaker bonus.
* Dual-element Pets/Bosses.
* Element-based resistances independent of Tương Khắc.
* Environment/terrain elemental modifiers.
