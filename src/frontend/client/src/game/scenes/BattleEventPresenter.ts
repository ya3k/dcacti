/**
 * BattleEventPresenter — Scene-local interpretation and formatting of in-battle server events.
 *
 * Implements interpretation for the fourteen non-outcome event types of the closed wire discriminator
 * (SIGNALR_PROTOCOL.md §3.2.2, §3.2.6–§3.2.24, GAME_EVENTS.md §2):
 *
 *   1. MatchCreated
 *   2. CascadeCreated
 *   3. ComboChanged
 *   4. GemMatched
 *   5. DamageCalculated
 *   6. DamageDealt
 *   7. DamageTaken
 *   8. PassiveCharged
 *   9. PassiveTriggered
 *  10. BossSkillCast
 *  11. CardCast
 *  12. PetSkillCast
 *  13. RelicTriggered
 *  14. PowerChanged
 *
 * Server-authoritative boundary (GAME_RULES.md §18, AGENTS.md §10):
 * - Presents only delivered members verbatim.
 * - Performs zero gameplay calculation (no HP accumulation, no damage/combo derivation,
 *   no match detection, no passive threshold evaluation).
 * - Unknown event types are safely ignored with no fabricated presentation.
 */

export interface PresentedMatchCreated {
  readonly type: 'MatchCreated';
  readonly shape: string;
  readonly cells: readonly number[];
  readonly gemType: string;
  readonly cascadeDepth: number;
  readonly createdSpecialGems?: ReadonlyArray<{
    readonly cellIndex: number;
    readonly specialGem: {
      readonly type: string;
      readonly orientation?: string;
    };
  }>;
}

export interface PresentedCascadeCreated {
  readonly type: 'CascadeCreated';
  readonly cascadeDepth: number;
}

export interface PresentedComboChanged {
  readonly type: 'ComboChanged';
  readonly combo: number;
}

export interface PresentedGemMatched {
  readonly type: 'GemMatched';
  readonly cellIndex: number;
  readonly gemType: string;
  readonly specialGem?: {
    readonly type: string;
    readonly orientation?: string;
  };
}

export interface PresentedDamageCalculated {
  readonly type: 'DamageCalculated';
  readonly base: number;
  readonly comboModifier: number;
  readonly elementModifier: number;
  readonly otherModifiers: number;
  readonly defense: number;
  readonly finalDamage: number;
}

export interface PresentedDamageDealt {
  readonly type: 'DamageDealt';
  readonly source: string;
  readonly target: string;
  readonly amount: number;
}

export interface PresentedDamageTaken {
  readonly type: 'DamageTaken';
  readonly source: string;
  readonly target: string;
  readonly amount: number;
}

export interface PresentedPassiveCharged {
  readonly type: 'PassiveCharged';
  readonly passiveId: string;
  readonly source: string;
  readonly sourceId: string;
  readonly progress: number;
  readonly threshold: number;
}

export interface PresentedPassiveTriggered {
  readonly type: 'PassiveTriggered';
  readonly passiveId: string;
  readonly source: string;
  readonly sourceId: string;
  readonly progress: number;
  readonly threshold: number;
}

export interface PresentedBossSkillCast {
  readonly type: 'BossSkillCast';
  readonly skillId: string;
  readonly sourceId: string;
}

export interface PresentedCardCast {
  readonly type: 'CardCast';
  readonly cardId: string;
}

export interface PresentedPetSkillCast {
  readonly type: 'PetSkillCast';
  readonly cardId: string;
}

/**
 * A triggered Relic (SIGNALR_PROTOCOL.md §3.2.23) — the Relic's owned instance
 * identity, and nothing else. No effect summary exists on this event (§3.2.23
 * item 2, §3.2.25): the resulting state reaches the client through the
 * `BattleState` projection (§4), not here.
 */
export interface PresentedRelicTriggered {
  readonly type: 'RelicTriggered';
  readonly relicId: string;
}

/**
 * A change to the active Pet's Power (SIGNALR_PROTOCOL.md §3.2.24) — the signed
 * change, the resulting value, and what changed it. `delta` is signed and is
 * presented as delivered; `power` is the server's resulting value and is never
 * recomputed from `delta` on the client.
 */
export interface PresentedPowerChanged {
  readonly type: 'PowerChanged';
  readonly delta: number;
  readonly power: number;
  readonly source: string;
}

export type InBattleServerEvent =
  | PresentedMatchCreated
  | PresentedCascadeCreated
  | PresentedComboChanged
  | PresentedGemMatched
  | PresentedDamageCalculated
  | PresentedDamageDealt
  | PresentedDamageTaken
  | PresentedPassiveCharged
  | PresentedPassiveTriggered
  | PresentedBossSkillCast
  | PresentedCardCast
  | PresentedPetSkillCast
  | PresentedRelicTriggered
  | PresentedPowerChanged;

/**
 * Validates and parses a raw event object into a typed InBattleServerEvent.
 * Returns null if the event is unknown, malformed, or an outcome event.
 */
export function parseInBattleEvent(raw: unknown): InBattleServerEvent | null {
  if (typeof raw !== 'object' || raw === null || !('type' in raw)) {
    return null;
  }

  const candidate = raw as { readonly type: unknown };
  if (typeof candidate.type !== 'string') {
    return null;
  }

  switch (candidate.type) {
    case 'MatchCreated':
      return parseMatchCreated(raw);
    case 'CascadeCreated':
      return parseCascadeCreated(raw);
    case 'ComboChanged':
      return parseComboChanged(raw);
    case 'GemMatched':
      return parseGemMatched(raw);
    case 'DamageCalculated':
      return parseDamageCalculated(raw);
    case 'DamageDealt':
      return parseDamageDealt(raw);
    case 'DamageTaken':
      return parseDamageTaken(raw);
    case 'PassiveCharged':
      return parsePassiveCharged(raw);
    case 'PassiveTriggered':
      return parsePassiveTriggered(raw);
    case 'BossSkillCast':
      return parseBossSkillCast(raw);
    case 'CardCast':
      return parseCardCast(raw);
    case 'PetSkillCast':
      return parsePetSkillCast(raw);
    case 'RelicTriggered':
      return parseRelicTriggered(raw);
    case 'PowerChanged':
      return parsePowerChanged(raw);
    default:
      // Unknown event type or outcome event — ignored safely per SIGNALR_PROTOCOL.md §3.2.2
      return null;
  }
}

function parseMatchCreated(raw: unknown): PresentedMatchCreated | null {
  const p = raw as Partial<PresentedMatchCreated>;
  if (
    typeof p.shape !== 'string' ||
    !Array.isArray(p.cells) ||
    !p.cells.every((c) => typeof c === 'number') ||
    typeof p.gemType !== 'string' ||
    typeof p.cascadeDepth !== 'number'
  ) {
    return null;
  }

  let createdSpecialGems: PresentedMatchCreated['createdSpecialGems'];
  if (Array.isArray(p.createdSpecialGems)) {
    const validGems: Array<{
      cellIndex: number;
      specialGem: { type: string; orientation?: string };
    }> = [];
    for (const entry of p.createdSpecialGems) {
      if (
        typeof entry === 'object' &&
        entry !== null &&
        typeof entry.cellIndex === 'number' &&
        typeof entry.specialGem === 'object' &&
        entry.specialGem !== null &&
        typeof entry.specialGem.type === 'string'
      ) {
        validGems.push({
          cellIndex: entry.cellIndex,
          specialGem: {
            type: entry.specialGem.type,
            ...(typeof entry.specialGem.orientation === 'string'
              ? { orientation: entry.specialGem.orientation }
              : {}),
          },
        });
      }
    }
    createdSpecialGems = validGems;
  }

  return {
    type: 'MatchCreated',
    shape: p.shape,
    cells: p.cells,
    gemType: p.gemType,
    cascadeDepth: p.cascadeDepth,
    ...(createdSpecialGems ? { createdSpecialGems } : {}),
  };
}

function parseCascadeCreated(raw: unknown): PresentedCascadeCreated | null {
  const p = raw as Partial<PresentedCascadeCreated>;
  if (typeof p.cascadeDepth !== 'number') {
    return null;
  }
  return {
    type: 'CascadeCreated',
    cascadeDepth: p.cascadeDepth,
  };
}

function parseComboChanged(raw: unknown): PresentedComboChanged | null {
  const p = raw as Partial<PresentedComboChanged>;
  if (typeof p.combo !== 'number') {
    return null;
  }
  return {
    type: 'ComboChanged',
    combo: p.combo,
  };
}

function parseGemMatched(raw: unknown): PresentedGemMatched | null {
  const p = raw as Partial<PresentedGemMatched>;
  if (typeof p.cellIndex !== 'number' || typeof p.gemType !== 'string') {
    return null;
  }

  let specialGem: PresentedGemMatched['specialGem'];
  if (
    typeof p.specialGem === 'object' &&
    p.specialGem !== null &&
    typeof p.specialGem.type === 'string'
  ) {
    specialGem = {
      type: p.specialGem.type,
      ...(typeof p.specialGem.orientation === 'string'
        ? { orientation: p.specialGem.orientation }
        : {}),
    };
  }

  return {
    type: 'GemMatched',
    cellIndex: p.cellIndex,
    gemType: p.gemType,
    ...(specialGem ? { specialGem } : {}),
  };
}

function parseDamageCalculated(raw: unknown): PresentedDamageCalculated | null {
  const p = raw as Partial<PresentedDamageCalculated>;
  if (
    typeof p.base !== 'number' ||
    typeof p.comboModifier !== 'number' ||
    typeof p.elementModifier !== 'number' ||
    typeof p.otherModifiers !== 'number' ||
    typeof p.defense !== 'number' ||
    typeof p.finalDamage !== 'number'
  ) {
    return null;
  }
  return {
    type: 'DamageCalculated',
    base: p.base,
    comboModifier: p.comboModifier,
    elementModifier: p.elementModifier,
    otherModifiers: p.otherModifiers,
    defense: p.defense,
    finalDamage: p.finalDamage,
  };
}

function parseDamageDealt(raw: unknown): PresentedDamageDealt | null {
  const p = raw as Partial<PresentedDamageDealt>;
  if (
    typeof p.source !== 'string' ||
    typeof p.target !== 'string' ||
    typeof p.amount !== 'number'
  ) {
    return null;
  }
  return {
    type: 'DamageDealt',
    source: p.source,
    target: p.target,
    amount: p.amount,
  };
}

function parseDamageTaken(raw: unknown): PresentedDamageTaken | null {
  const p = raw as Partial<PresentedDamageTaken>;
  if (
    typeof p.source !== 'string' ||
    typeof p.target !== 'string' ||
    typeof p.amount !== 'number'
  ) {
    return null;
  }
  return {
    type: 'DamageTaken',
    source: p.source,
    target: p.target,
    amount: p.amount,
  };
}

function parsePassiveCharged(raw: unknown): PresentedPassiveCharged | null {
  const p = raw as Partial<PresentedPassiveCharged>;
  if (
    typeof p.passiveId !== 'string' ||
    typeof p.source !== 'string' ||
    typeof p.sourceId !== 'string' ||
    typeof p.progress !== 'number' ||
    typeof p.threshold !== 'number'
  ) {
    return null;
  }
  return {
    type: 'PassiveCharged',
    passiveId: p.passiveId,
    source: p.source,
    sourceId: p.sourceId,
    progress: p.progress,
    threshold: p.threshold,
  };
}

function parsePassiveTriggered(raw: unknown): PresentedPassiveTriggered | null {
  const p = raw as Partial<PresentedPassiveTriggered>;
  if (
    typeof p.passiveId !== 'string' ||
    typeof p.source !== 'string' ||
    typeof p.sourceId !== 'string' ||
    typeof p.progress !== 'number' ||
    typeof p.threshold !== 'number'
  ) {
    return null;
  }
  return {
    type: 'PassiveTriggered',
    passiveId: p.passiveId,
    source: p.source,
    sourceId: p.sourceId,
    progress: p.progress,
    threshold: p.threshold,
  };
}

function parseBossSkillCast(raw: unknown): PresentedBossSkillCast | null {
  const p = raw as Partial<PresentedBossSkillCast>;
  if (typeof p.skillId !== 'string' || typeof p.sourceId !== 'string') {
    return null;
  }
  return {
    type: 'BossSkillCast',
    skillId: p.skillId,
    sourceId: p.sourceId,
  };
}

function parseCardCast(raw: unknown): PresentedCardCast | null {
  const p = raw as Partial<PresentedCardCast>;
  if (typeof p.cardId !== 'string' || p.cardId.length === 0) {
    return null;
  }
  return {
    type: 'CardCast',
    cardId: p.cardId,
  };
}

function parsePetSkillCast(raw: unknown): PresentedPetSkillCast | null {
  const p = raw as Partial<PresentedPetSkillCast>;
  if (typeof p.cardId !== 'string' || p.cardId.length === 0) {
    return null;
  }
  return {
    type: 'PetSkillCast',
    cardId: p.cardId,
  };
}

/**
 * `RelicTriggered` (SIGNALR_PROTOCOL.md §3.2.23) — exactly `type` and `relicId`.
 *
 * `relicId` is the Relic's owned instance identity (RELIC_RULES.md §2.2 item 3),
 * not a `RelicDefinitionId` and not a display name. It is read as delivered; no
 * client-side definition lookup or label mapping is performed. Any other member
 * on the payload (e.g. a hypothetical `effectSummary`) is deliberately not read:
 * §3.2.23 item 2 omits it under §3.2.25.
 */
function parseRelicTriggered(raw: unknown): PresentedRelicTriggered | null {
  const p = raw as Partial<PresentedRelicTriggered>;
  if (typeof p.relicId !== 'string' || p.relicId.length === 0) {
    return null;
  }
  return {
    type: 'RelicTriggered',
    relicId: p.relicId,
  };
}

/**
 * `PowerChanged` (SIGNALR_PROTOCOL.md §3.2.24) — `type`, `delta`, `power`, `source`.
 *
 * `delta` is the signed change and `power` is `PetState.Power` after the change;
 * both are presented exactly as delivered, and `power` is never re-derived from
 * `delta` (that would be client-side gameplay calculation). `source` is the
 * delivered string (`"match"` / `"card"` / `"relic"`); it is not validated
 * against a client-side enum and an unrecognized value is not rejected — the
 * client presents what the server projected rather than deciding what is valid.
 */
function parsePowerChanged(raw: unknown): PresentedPowerChanged | null {
  const p = raw as Partial<PresentedPowerChanged>;
  if (
    typeof p.delta !== 'number' ||
    typeof p.power !== 'number' ||
    typeof p.source !== 'string'
  ) {
    return null;
  }
  return {
    type: 'PowerChanged',
    delta: p.delta,
    power: p.power,
    source: p.source,
  };
}

/**
 * Formats an in-battle event for presentation using only delivered members verbatim.
 * Contains zero client-side calculation or derivation.
 */
export function formatInBattleEvent(event: InBattleServerEvent): string {
  switch (event.type) {
    case 'MatchCreated': {
      const specialGemsDesc =
        event.createdSpecialGems && event.createdSpecialGems.length > 0
          ? ` (created: ${event.createdSpecialGems
              .map(
                (g) =>
                  `${g.specialGem.type}${g.specialGem.orientation ? `[${g.specialGem.orientation}]` : ''}@${g.cellIndex}`
              )
              .join(', ')})`
          : '';
      return `Match: ${event.shape} ${event.gemType} [${event.cells.join(', ')}] depth: ${event.cascadeDepth}${specialGemsDesc}`;
    }
    case 'GemMatched': {
      const specialDesc = event.specialGem
        ? ` [Special: ${event.specialGem.type}${event.specialGem.orientation ? ` ${event.specialGem.orientation}` : ''}]`
        : '';
      return `GemMatched: cell ${event.cellIndex} (${event.gemType})${specialDesc}`;
    }
    case 'CascadeCreated':
      return `Cascade: depth ${event.cascadeDepth}`;
    case 'ComboChanged':
      return `Combo: ${event.combo}`;
    case 'DamageCalculated':
      return `DamageCalculated: base ${event.base}, combo ${event.comboModifier}x, elem ${event.elementModifier}x, other ${event.otherModifiers}x, def ${event.defense}, final ${event.finalDamage}`;
    case 'DamageDealt':
      return `DamageDealt: ${event.amount} (${event.source} -> ${event.target})`;
    case 'DamageTaken':
      return `DamageTaken: ${event.amount} on ${event.target} (from ${event.source})`;
    case 'PassiveCharged':
      return `PassiveCharged: ${event.passiveId} (${event.source}:${event.sourceId}) ${event.progress}/${event.threshold}`;
    case 'PassiveTriggered':
      return `PassiveTriggered: ${event.passiveId} (${event.source}:${event.sourceId}) ${event.progress}/${event.threshold}`;
    case 'BossSkillCast':
      return `BossSkillCast: ${event.skillId} by ${event.sourceId}`;
    case 'CardCast':
      return `CardCast: ${event.cardId}`;
    case 'PetSkillCast':
      return `PetSkillCast: ${event.cardId}`;
    case 'RelicTriggered':
      return `RelicTriggered: ${event.relicId}`;
    case 'PowerChanged':
      return `PowerChanged: ${event.delta >= 0 ? '+' : ''}${event.delta} -> ${event.power} (${event.source})`;
  }
}

/**
 * One player-facing statement of what just happened, ready to be drawn.
 *
 * It is a *presentation* of an already-delivered event: the message names no
 * value the event did not carry, and the colour is a display choice.
 */
export interface EventCallout {
  /** The line a player reads, e.g. `COMBO ×4`, `BOSS SKILL`, `CARD: Heal`. */
  readonly message: string;
  /** The display colour for the callout. */
  readonly color: string;
  /**
   * How newsworthy the callout is: **lower is more important**. A batch presents
   * its lowest-numbered callout, so the most significant thing that happened is
   * what the player is told (TASK-210 §7).
   */
  readonly priority: number;
}

/** The callout palette. Display only; no colour carries gameplay meaning. */
const CALLOUT_MATCH_COLOR = '#e2e8f0';
const CALLOUT_COMBO_COLOR = '#fbbf24';
const CALLOUT_CAST_COLOR = '#a5b4fc';
/** The Relic callout's colour: the build's own colour, beside the cast's. */
const CALLOUT_RELIC_COLOR = '#7dd3fc';
export const CALLOUT_BOSS_COLOR = '#f87171';
const CALLOUT_PASSIVE_COLOR = '#c4b5fd';

/**
 * Canonical MVP Boss Skill display names mapped from BOSS_RULES.md §6.3 / §6.4.
 *
 * Each of the five MVP Bosses carries one content-defined Skill with its canonical
 * display name:
 *   - flame-burst    -> Flame Burst (Hỏa Long)
 *   - drain-power    -> Drain Power (Thủy Ma)
 *   - root           -> Root (Mộc Yêu)
 *   - earthquake     -> Earthquake (Sơn Thạch Vệ)
 *   - thunder-strike -> Thunder Strike (Kim Lôi Vương)
 */
export const BOSS_SKILL_NAMES: Readonly<Record<string, string>> = Object.freeze({
  'flame-burst': 'Flame Burst',
  'drain-power': 'Drain Power',
  root: 'Root',
  earthquake: 'Earthquake',
  'thunder-strike': 'Thunder Strike',
});

/**
 * Resolves a Boss Skill's canonical display name from BOSS_RULES.md §6.3 / §6.4,
 * or returns null when the technical skillId is unrecognized.
 */
export function resolveBossSkillDisplayName(skillId: string): string | null {
  return BOSS_SKILL_NAMES[skillId] ?? null;
}

/**
 * Canonical Boss Enrage thresholds from BOSS_RULES.md §6.1.
 *
 * Enrage is a permanent state transition triggered when BossHP < EnrageThreshold
 * (BOSS_RULES.md §5 item 4). Strict inequality applies.
 */
export const BOSS_ENRAGE_THRESHOLDS: Readonly<Record<string, number>> = Object.freeze({
  'boss-hoa-long': 1500,
  'boss-thuy-ma': 1500,
  'boss-moc-yeu': 1500,
  'boss-son-thach-ve': 1500,
  'boss-kim-loi-vuong': 2100,
});

/**
 * Evaluates whether a Boss is in Enraged state according to BOSS_RULES.md §5 item 4 & §6.1.
 * A defeated Boss (hp <= 0) is not considered Enraged.
 * For unknown boss IDs, safely falls back to 30% of maxHp (BaseEnrageThreshold).
 */
export function isBossEnraged(bossId: string, hp: number, maxHp: number): boolean {
  if (hp <= 0) {
    return false;
  }
  const threshold = BOSS_ENRAGE_THRESHOLDS[bossId] ?? Math.floor(maxHp * 0.3);
  return hp < threshold;
}

/**
 * Canonical Swap rejection codes mapped to player-friendly messages
 * (MATCH3_RULES.md §2.1.2, §2.1.4; SIGNALR_PROTOCOL.md §5 item 3).
 */
export const SWAP_REJECTION_MESSAGES: Readonly<Record<string, string>> = Object.freeze({
  NO_MATCH_FROM_SWAP: 'Swap does not create a match.',
  INVALID_SWAP: 'Invalid swap. Gems must be adjacent.',
  INVALID_CELL_INDEX: 'Invalid board position.',
  STALE_ACTION: 'Board has changed. Please try again.',
  BATTLE_NOT_FOUND: 'Battle session not found.',
});

/**
 * Formats a Swap rejection reason into a player-friendly presentation message,
 * providing a safe fallback for unknown codes.
 */
export function formatSwapRejection(reason?: string | null): string {
  if (!reason) {
    return 'Swap rejected.';
  }
  return SWAP_REJECTION_MESSAGES[reason] ?? `Swap rejected (${reason}).`;
}

/**
 * Canonical CardCast and PetSkillCast rejection codes mapped to player-friendly messages
 * (CARD_RULES.md §3; SIGNALR_PROTOCOL.md §5 item 3).
 */
export const CARD_CAST_REJECTION_MESSAGES: Readonly<Record<string, string>> = Object.freeze({
  INSUFFICIENT_POWER: 'Not enough Power.',
  CARD_CAST_ALREADY_USED_THIS_TURN: 'Only one card can be cast per turn.',
  CARD_NOT_IN_LOADOUT: 'Card is not in your current loadout.',
  INVALID_CARD: 'Invalid card.',
  PET_SKILL_NOT_OWNED: 'Pet skill is not available.',
  BATTLE_NOT_FOUND: 'Battle session not found.',
});

/**
 * Formats a CardCast or PetSkillCast rejection reason into a player-friendly presentation message,
 * providing a safe fallback for unknown codes.
 */
export function formatCardCastRejection(reason?: string | null): string {
  if (!reason) {
    return 'Cast rejected.';
  }
  return CARD_CAST_REJECTION_MESSAGES[reason] ?? `Cast rejected (${reason}).`;
}

/**
 * The priority ladder. A resolution can produce a dozen events; the player is
 * told the most important *one* of them, so the ladder is ordered by what the
 * player is actually waiting to learn:
 *
 * ```text
 * 1  a meaningful Combo   the cascade the player's own Swap produced
 * 2  a Card / Pet Skill   the player's own cast
 * 2  a Relic trigger      the player's own build paying off
 * 3  a Boss Skill         the Boss acting
 * 4  a Passive triggering the effect the player was building toward
 * 5  a Passive charging   progress toward that effect
 * 6  a Match              an ordinary single-Match Swap's result
 * ```
 *
 * A Relic trigger shares the cast's slot because it is the same kind of news: an
 * outcome of the player's own build taking effect rather than the board's own
 * arithmetic. Sharing a number rather than taking a new one changes no existing
 * event's standing — the ladder is still read by the same rule — and the
 * among-equals tie is broken by delivery order, which is already how two casts in
 * one batch are resolved.
 */
const CALLOUT_PRIORITY_COMBO = 1;
const CALLOUT_PRIORITY_CAST = 2;
const CALLOUT_PRIORITY_RELIC = 2;
const CALLOUT_PRIORITY_BOSS = 3;
const CALLOUT_PRIORITY_PASSIVE_TRIGGERED = 4;
const CALLOUT_PRIORITY_PASSIVE_CHARGED = 5;
const CALLOUT_PRIORITY_MATCH = 6;

/**
 * The player-facing callout for one delivered in-battle event, or `null` when
 * that event has none.
 *
 * Everything shown here is a delivered member, read as sent:
 *
 * ```text
 * ComboChanged      `combo`            — only from 2 upward: a published combo of
 *                                        1 is an ordinary single-Match Swap
 *                                        (MATCH3_RULES.md §6.2 item 1)
 * CardCast          `cardId`           — resolved to a display name through the
 *                                        caller's lookup; the identity itself is
 *                                        shown when no definition is loaded
 * PetSkillCast      `cardId`           — as above
 * RelicTriggered    `relicId`          — resolved to the Relic's own display name
 *                                        through the caller's lookup (below)
 * BossSkillCast     `skillId`          — resolved to canonical display name from
 *                                        BOSS_RULES.md §6.3/§6.4; falls back to
 *                                        'BOSS SKILL' when unrecognized
 * PassiveTriggered  `source`           — `"boss"` / `"pet"` selects the wording
 * PassiveCharged    `source`, `progress`, `threshold`
 * MatchCreated      (no member)        — the event's own existence is the fact
 * ```
 *
 * **`RelicTriggered` and the identity it must not show.** `relicId` is the Relic's
 * owned *instance* identity (`RELIC_RULES.md` §2.2 item 3) — technical, and never a
 * player-facing label. The name is not on the event (§3.2.23 item 5 fixes
 * `{ type, relicId }` as final), so it is resolved through the caller's
 * `resolveRelicName`, which reads the already-documented `GET /api/relics` name
 * (`API_CONTRACTS.md` §5.4). When the definition cannot be resolved the caller
 * returns `null` and this event produces **no callout at all**: the raw instance
 * identity is not shown, and no name is derived from `trigger`, `condition`, or
 * `effectDefinition` (`RELIC_RULES.md` §8.2 item 1 forbids exactly that, and
 * `API_CONTRACTS.md` §5.4 `:1061-1063` bounds it to display text of the payload's
 * own tokens). This is the one event whose resolver is allowed to fail closed —
 * for a Card the identity is at least meaningful, while a Relic instance id is
 * an opaque internal handle.
 *
 * Events with **no** callout, and why:
 *
 * ```text
 * GemMatched / CascadeCreated   already shown as the per-cell board highlights
 *                               and, for a Cascade, as the Combo the pass produced
 * DamageCalculated              the six pipeline factors are developer detail;
 *                               the resulting number reaches the player as the
 *                               damage floater and as the HP readout
 * DamageDealt / DamageTaken     drawn as one floating number over the panel of the
 *                               party that took the damage (BattleScene)
 * PowerChanged                  drawn as a signed floater over the Power gauge
 * ```
 */
export function describeEventCallout(
  event: InBattleServerEvent,
  resolveCardName: (cardId: string) => string,
  resolveRelicName: (relicId: string) => string | null,
  resolveBossSkillName: (skillId: string) => string | null = resolveBossSkillDisplayName
): EventCallout | null {
  switch (event.type) {
    case 'ComboChanged':
      return event.combo >= 2
        ? {
            message: `COMBO ×${event.combo}`,
            color: CALLOUT_COMBO_COLOR,
            priority: CALLOUT_PRIORITY_COMBO,
          }
        : null;
    case 'CardCast':
      return {
        message: `CARD: ${resolveCardName(event.cardId)}`,
        color: CALLOUT_CAST_COLOR,
        priority: CALLOUT_PRIORITY_CAST,
      };
    case 'PetSkillCast':
      return {
        message: `PET SKILL: ${resolveCardName(event.cardId)}`,
        color: CALLOUT_CAST_COLOR,
        priority: CALLOUT_PRIORITY_CAST,
      };
    case 'RelicTriggered': {
      // The player's own Relic, named from the delivered definition read. An
      // unresolvable Relic produces nothing rather than a raw instance identity.
      const name = resolveRelicName(event.relicId);
      return name === null
        ? null
        : {
            message: `RELIC: ${name}`,
            color: CALLOUT_RELIC_COLOR,
            priority: CALLOUT_PRIORITY_RELIC,
          };
    }
    case 'BossSkillCast': {
      const skillName = resolveBossSkillName(event.skillId);
      return {
        message: skillName !== null ? `BOSS SKILL: ${skillName}` : 'BOSS SKILL',
        color: CALLOUT_BOSS_COLOR,
        priority: CALLOUT_PRIORITY_BOSS,
      };
    }
    case 'PassiveTriggered':
      return {
        message: event.source === 'boss' ? 'BOSS PASSIVE TRIGGERED' : 'PASSIVE TRIGGERED',
        color: CALLOUT_PASSIVE_COLOR,
        priority: CALLOUT_PRIORITY_PASSIVE_TRIGGERED,
      };
    case 'PassiveCharged':
      return {
        message: `${event.source === 'boss' ? 'BOSS PASSIVE' : 'PASSIVE'} ${event.progress}/${event.threshold}`,
        color: CALLOUT_PASSIVE_COLOR,
        priority: CALLOUT_PRIORITY_PASSIVE_CHARGED,
      };
    case 'MatchCreated':
      return {
        message: 'MATCH',
        color: CALLOUT_MATCH_COLOR,
        priority: CALLOUT_PRIORITY_MATCH,
      };
    default:
      return null;
  }
}

/**
 * The single callout a delivered event batch should present, or `null` when the
 * batch carries nothing a player needs told.
 *
 * The batch is one resolution, delivered in the server's own order
 * (`GAME_EVENTS.md` §1), so presenting the whole batch would be a second event
 * feed rather than feedback. The most important event wins; among equally
 * important events the **last** one wins, because that is the one the resolution
 * finished on.
 *
 * **Several Relic triggers in one batch still produce one callout.** The Relic
 * stage resolves every eligible Relic for one event in equip-slot order
 * (`RELIC_RULES.md` §4.2), so a 3–5 Relic loadout can emit more than one
 * `RelicTriggered` in a single Swap (`Emergency Core` re-emits on every Swap while
 * armed). This mechanism names the one the batch finished on, exactly as it does
 * for two casts — the single-callout rule is the existing design and is not
 * widened into a queue here.
 */
export function selectBatchCallout(
  events: readonly InBattleServerEvent[],
  resolveCardName: (cardId: string) => string,
  resolveRelicName: (relicId: string) => string | null,
  resolveBossSkillName: (skillId: string) => string | null = resolveBossSkillDisplayName
): EventCallout | null {
  let selected: EventCallout | null = null;

  for (const event of events) {
    const candidate = describeEventCallout(
      event,
      resolveCardName,
      resolveRelicName,
      resolveBossSkillName
    );

    if (candidate !== null && (selected === null || candidate.priority <= selected.priority)) {
      selected = candidate;
    }
  }

  return selected;
}
