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
 * When `currentlyEnraged` is true, the state remains latched for the rest of that battle,
 * persisting through healing and defeat (hp <= 0).
 * Otherwise, the initial transition occurs when hp > 0 and hp < threshold.
 * For unknown boss IDs, safely falls back to 30% of maxHp (BaseEnrageThreshold).
 */
export function isBossEnraged(
  bossId: string,
  hp: number,
  maxHp: number,
  currentlyEnraged: boolean = false
): boolean {
  if (currentlyEnraged) {
    return true;
  }
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
 * The two MVP damage party identifiers (`SIGNALR_PROTOCOL.md` §3.2.14 item 3).
 *
 * They are the delivered `source` / `target` values, read as sent. This module
 * owns the pair so the scene has one spelling of them: a party is never
 * invented, and an unrecognized one is treated as "not this phase" rather than
 * guessed at.
 */
export const DAMAGE_PARTY_PLAYER = 'player';
export const DAMAGE_PARTY_BOSS = 'boss';

/**
 * The ordered presentation phase one delivered damage instance belongs to.
 *
 * ```text
 * damage        source="player", target="boss"   the player's hit on the Boss
 *               (GAME_RULES.md §17 steps 15–17, SIGNALR_PROTOCOL.md §3.2.14)
 * retaliation   source="boss",   target="player" the Boss's response
 *               (GAME_RULES.md §17 step 18c, SIGNALR_PROTOCOL.md §3.2.14 item 3)
 * ```
 *
 * It is a **presentation classification and nothing else**: it reads the two
 * delivered party strings to decide *which phase of the timeline* a hit is
 * sequenced into. It applies no damage, derives no HP from `amount`, evaluates
 * no combat step, and decides nothing about whether a hit lands.
 *
 * `null` means the delivered parties are not one of the two MVP directions — an
 * unrecognized direction is never assigned to a phase, exactly as
 * `BattleScene.damageAnchor` declines to guess which panel was hurt.
 */
export type DamagePresentationPhase = 'damage' | 'retaliation';

export function resolveDamagePresentationPhase(
  source: string,
  target: string
): DamagePresentationPhase | null {
  if (source === DAMAGE_PARTY_PLAYER && target === DAMAGE_PARTY_BOSS) {
    return 'damage';
  }

  if (source === DAMAGE_PARTY_BOSS && target === DAMAGE_PARTY_PLAYER) {
    return 'retaliation';
  }

  return null;
}

// ---------------------------------------------------------------------------
// The presentation actor/action model (TASK-252)
// ---------------------------------------------------------------------------

/**
 * The actor one presented action is attributed to.
 *
 * Every value is decision-free and is read from a **delivered member**; none is
 * inferred from animation timing, from a client-held turn, or from a stage name
 * used as a side. The mapping is exactly:
 *
 * ```text
 * swap     the committed Swap — the resolution's board cycle. A Match, a matched
 *          Gem, a Cascade pass and the Combo are the board's own delivered
 *          arithmetic performed *by* that Swap (`GAME_EVENTS.md` §1.1), and none
 *          of them carries a party member, so the Swap is the only actor the
 *          delivered members name.
 * player   CardCast, PetSkillCast and RelicTriggered — the player's own action
 *          (`SIGNALR_PROTOCOL.md` §3.2.20, §3.2.21, §3.2.23), and a
 *          DamageDealt / DamageTaken whose delivered `source` is `"player"`
 *          (§3.2.14 item 1).
 * pet      a PassiveCharged / PassiveTriggered whose delivered `source` is
 *          `"pet"` — the active Pet's own Passive (§3.2.16 item 1).
 * boss     a PassiveCharged / PassiveTriggered whose `source` is `"boss"`, a
 *          BossSkillCast (§3.2.18), and a damage report whose `source` is
 *          `"boss"` — the Boss acting.
 * power    PowerChanged — the Pet Power **mutation stage** the delivered
 *          `source` names (`"match"` / `"card"` / `"relic"` / `"boss"`). It is a
 *          stage, never a side: those four values answer "which stage owns the
 *          mutation", not "which party" (§3.2.24 item 3), so `"boss"` here is
 *          never read as the Boss party and `"card"` never as the player party.
 * battle   BattleWon / BattleLost — the battle's own terminal outcome
 *          (§3.2.19).
 * ```
 *
 * `null` is the honest answer when the delivered members name no actor this
 * contract defines — a damage report whose `source`/`target` pair is neither of
 * the two documented directions. No side is guessed for it (`AGENTS.md` §10).
 */
export type PresentationActor = 'swap' | 'player' | 'pet' | 'boss' | 'power' | 'battle';

/**
 * The action one presented event reports, in the delivered event's own terms.
 *
 * It is finer-grained than the phase kind: a `MatchCreated` and a `GemMatched`
 * are both the board cycle's `match`, a `CardCast` and a `PetSkillCast` are two
 * different casts that share one `cast` phase, and a `PassiveCharged` and a
 * `PassiveTriggered` are two different Passive actions.
 */
export type PresentationAction =
  | 'swap'
  | 'match'
  | 'combo'
  | 'cascade'
  | 'settle'
  | 'card-cast'
  | 'pet-skill-cast'
  | 'relic'
  | 'passive-charge'
  | 'passive-trigger'
  | 'boss-skill'
  | 'damage'
  | 'retaliation'
  | 'power'
  | 'outcome';

/**
 * The ordered presentation step one delivered event belongs to.
 *
 * These are presentation groupings only: they decide *which step of the local
 * timeline* a delivered event is shown in, and are never a second event
 * vocabulary, a wire member, or a server phase. No server-side phase exists to
 * mirror — the server resolves an action completely, writes the state back, and
 * only then sends the batch (`SIGNALR_PROTOCOL.md` §3.1 items 1–2) — so the
 * names describe the client's own presentation and nothing else.
 */
export type PresentationPhaseKind =
  | 'swap'
  | 'match'
  | 'cascade'
  | 'cast'
  | 'relic'
  | 'boss-skill'
  | 'settle'
  | 'damage'
  | 'retaliation'
  | 'feedback'
  | 'outcome';

/** One delivered event's presentation step: who acted, what, and in which phase. */
export interface PresentedActionStep {
  /** The actor the delivered members name, or `null` when they name none. */
  readonly actor: PresentationActor | null;
  /** The delivered action this event reports. */
  readonly action: PresentationAction;
  /** The presentation phase this step is grouped into. */
  readonly kind: PresentationPhaseKind;
  /** The developer-diagnostic line the phase records when it plays. */
  readonly detail: string;
}

/**
 * The two entity-owner values a `PassiveCharged` / `PassiveTriggered` `source`
 * carries (`SIGNALR_PROTOCOL.md` §3.2.16 item 1).
 *
 * They are the delivered values, read as sent, and they are the vocabulary that
 * decides *whose* passive readout a phase presents: the Pet panel carries the
 * active Pet's pair (`PetState.PassiveId` / `PassiveProgress`), so a Boss-owned
 * Passive charge presents nothing there. The module owns the pair so the scene
 * has one spelling of them.
 */
export const PASSIVE_OWNER_PET = 'pet';
export const PASSIVE_OWNER_BOSS = 'boss';

/**
 * The actor a `PassiveCharged` / `PassiveTriggered` `source` names, or `null`.
 *
 * The value set is per-event (`SIGNALR_PROTOCOL.md` §3.2.16 item 1: `"pet"` or
 * `"boss"`); a value outside it names no actor this contract defines, so none is
 * claimed.
 */
export function passiveOwnerActor(source: string): PresentationActor | null {
  if (source === PASSIVE_OWNER_BOSS) {
    return 'boss';
  }

  if (source === PASSIVE_OWNER_PET) {
    return 'pet';
  }

  return null;
}

/**
 * Whether one delivered Passive event is the active Pet's own.
 *
 * It answers exactly that — the panel's pair is `PetState`'s, so only the Pet's
 * own charge/trigger presents it — and nothing about any rule: no charge,
 * threshold, overflow or reset is evaluated (`PASSIVE_RULES.md` §2–§5).
 */
export function isPetPassiveEvent(event: {
  readonly source: string;
}): boolean {
  return event.source === PASSIVE_OWNER_PET;
}

/**
 * The one adapter from a delivered event to its presentation step.
 *
 * It is the **single** place a delivered event is interpreted into the actor/
 * action vocabulary: a Swap, a Boss Skill, a Boss Passive, a Card cast and a Pet
 * Skill cast all reach their phases through this one call and through no branch,
 * pipeline, registry, interface or event bus (`AGENTS.md` §9). It reads only
 * delivered members, performs no gameplay derivation, and returns `null` for an
 * event that presents nothing of its own:
 *
 * ```text
 * DamageCalculated   the six pipeline factors are developer detail; the number
 *                    reaches the player as the damage floater and the HP readout.
 * ```
 *
 * A step whose `kind` is `feedback` is a **readout / floater step**: it is
 * presented by the phase already showing, rather than opening a step of its own,
 * so a delivered Combo, Power or Passive change neither reorders the resolution
 * nor lengthens it (TASK-246 §5's bounded pacing). The readouts a `feedback`
 * phase carries are still presented by the timeline — by the phase holding the
 * event that owns them — and never by a raw push (operator decision D-2).
 *
 * `PowerChanged` is attributed to the **stage** the delivered `source` names and
 * never to a side (§3.2.24 item 3), and a damage report whose parties are not
 * one of the two documented directions keeps `actor: null` and joins the
 * floater-only `feedback` phase rather than being guessed onto a side.
 */
export function adaptPresentedEvent(event: InBattleServerEvent): PresentedActionStep | null {
  switch (event.type) {
    case 'MatchCreated':
      return { actor: 'swap', action: 'match', kind: 'match', detail: `match depth=${event.cascadeDepth}` };

    case 'GemMatched':
      // The Gem's feedback belongs to the Match/board cycle that consumed it
      // (`GAME_EVENTS.md` §1.1 item 3) and carries no actor member of its own.
      return { actor: 'swap', action: 'match', kind: 'match', detail: 'match' };

    case 'CascadeCreated':
      return {
        actor: 'swap',
        action: 'cascade',
        kind: 'cascade',
        detail: `cascade depth=${event.cascadeDepth}`,
      };

    case 'ComboChanged':
      // The Combo is the board cycle's own accounting (`GAME_EVENTS.md` §1.1 item
      // 5), emitted between the Matches of one pass, so its step is presented by
      // the board phase it belongs to rather than splitting it.
      return { actor: 'swap', action: 'combo', kind: 'feedback', detail: `combo ${event.combo}` };

    case 'PowerChanged':
      // The delivered `source` names the stage that owns the mutation
      // (`"match"` / `"card"` / `"relic"` / `"boss"`), so the actor is the Power
      // stage. Reading it as a side would be the §3.2.24 item 3 defect.
      return { actor: 'power', action: 'power', kind: 'feedback', detail: `power ${event.delta}` };

    case 'PassiveCharged':
      return {
        actor: passiveOwnerActor(event.source),
        action: 'passive-charge',
        kind: 'feedback',
        detail: `passive ${event.passiveId}`,
      };

    case 'PassiveTriggered':
      return {
        actor: passiveOwnerActor(event.source),
        action: 'passive-trigger',
        kind: 'feedback',
        detail: `passive ${event.passiveId}`,
      };

    case 'BossSkillCast':
      return {
        actor: 'boss',
        action: 'boss-skill',
        kind: 'boss-skill',
        detail: `boss-skill ${event.skillId}`,
      };

    case 'CardCast':
      return { actor: 'player', action: 'card-cast', kind: 'cast', detail: `cast ${event.cardId}` };

    case 'PetSkillCast':
      return {
        actor: 'player',
        action: 'pet-skill-cast',
        kind: 'cast',
        detail: `cast ${event.cardId}`,
      };

    case 'RelicTriggered':
      // The player's own build paying off (RELIC_RULES.md §7): the delivered
      // members name the player's side and nothing else.
      return { actor: 'player', action: 'relic', kind: 'relic', detail: `relic ${event.relicId}` };

    case 'DamageDealt':
    case 'DamageTaken': {
      const phase = resolveDamagePresentationPhase(event.source, event.target);

      if (phase === null) {
        // Neither documented direction: no side is guessed for it, so the step
        // carries no actor and joins the floater-only feedback phase.
        return { actor: null, action: 'damage', kind: 'feedback', detail: 'feedback' };
      }

      return {
        actor: event.source === DAMAGE_PARTY_BOSS ? 'boss' : 'player',
        action: phase,
        kind: phase,
        detail: `${phase} ${event.source}->${event.target}`,
      };
    }

    case 'DamageCalculated':
      // The six pipeline factors are developer detail; the resulting number
      // reaches the player as the damage floater and as the HP readout.
      return null;
  }
}

// ---------------------------------------------------------------------------
// The developer Move Log's row vocabulary (TASK-253, operator decision D-1)
// ---------------------------------------------------------------------------

/**
 * The panel's caption — presentation text, not a delivered value.
 *
 * It is deliberately the only string in the panel that names no wire member: it
 * labels the developer surface and nothing else.
 */
export const MOVE_LOG_TITLE = 'MOVE LOG';

/**
 * The lifecycle verbs one row records (TASK-253).
 *
 * ```text
 * queued      the client built a resolution's ordered timeline from a delivered
 *             batch, before any of it played
 * animating   the presentation machine entered / advanced to this step
 * completed   the step's own presentation finished and the machine moved on
 * aborted     the in-flight timeline was abandoned because a newer delivered
 *             value superseded it — not a failure of the resolution
 * failed      the in-flight timeline was abandoned because the delivered batch
 *             could not be presented at all
 * ```
 *
 * They are the **client's own** presentation lifecycle, sourced from the
 * orchestration machine's transitions and from the batch the client accepted —
 * never from animation timing, and never from a server phase (there is none:
 * `SIGNALR_PROTOCOL.md` §3.1 items 1–2).
 */
export type MoveLogLifecycleState =
  | 'queued'
  | 'animating'
  | 'completed'
  | 'aborted'
  | 'failed';

/**
 * Why an in-flight presentation timeline was abandoned.
 *
 * Each value names a delivered/client fact the scene already acted on, so the
 * row reports the actual cause rather than a generic "aborted".
 */
export type MoveLogAbortReason =
  | 'superseded-state'
  | 'superseded-batch'
  | 'malformed-batch';

/** Whether a row's lifecycle verb describes the whole timeline or one phase of it. */
export type MoveLogLifecycleScope = 'timeline' | 'phase';

/**
 * Everything one Move Log row can be built from.
 *
 * It is a discriminated union of the three row kinds the panel draws, and every
 * member of every kind is either a delivered wire value, the client's own
 * orchestration fact, or a counter the panel owns. Nothing here is derived from a
 * gameplay rule, and no row can be constructed for an event the server did not
 * send (`AGENTS.md` §10).
 */
export type MoveLogRowInput =
  | {
      readonly kind: 'event';
      readonly ordinal: number;
      /** `ReceiveEvents.serverSequence` (`SIGNALR_PROTOCOL.md` §3). */
      readonly sequence: number;
      /** `BattleStateUpdated.turn` (`SIGNALR_PROTOCOL.md` §4), or `null` when none was delivered. */
      readonly turn: number | null;
      /** The adapter's step, or `null` when the delivered event names no step. */
      readonly step: PresentedActionStep | null;
      readonly event: InBattleServerEvent;
    }
  | {
      readonly kind: 'lifecycle';
      readonly ordinal: number;
      readonly scope: MoveLogLifecycleScope;
      readonly state: MoveLogLifecycleState;
      /** The resolution's `serverSequence`, or `null` when none belongs to the row. */
      readonly sequence: number | null;
      /** The client's own detail: the phase's position and its delivered members. */
      readonly detail: string;
    }
  | {
      readonly kind: 'outcome';
      readonly ordinal: number;
      readonly outcome: string;
      readonly finalBossHp: number;
      readonly finalPlayerHp: number;
    };

/**
 * Formats exactly one Move Log row.
 *
 * It is the **single** row formatter, and it is a formatter and nothing else:
 *
 * - an event row reuses `formatInBattleEvent`, so the delivered members are spelled
 *   once in the codebase and the panel is not a second event vocabulary;
 * - the `type=` field names the delivered `type` discriminator verbatim, so the
 *   closed sixteen-name set of `SIGNALR_PROTOCOL.md` §3.2.2 stays the only event
 *   vocabulary the row can name;
 * - `actor`/`action` come from the one adapter (`adaptPresentedEvent`), and are
 *   `-` when the delivered members name neither — never guessed (`AGENTS.md` §10);
 * - `turn` is `-` when no `BattleStateUpdated` has been delivered.
 *
 * It performs no gameplay calculation, evaluates no rule, and infers no value the
 * server did not deliver.
 */
export function formatMoveLogRow(row: MoveLogRowInput): string {
  switch (row.kind) {
    case 'event': {
      const actor = row.step?.actor ?? '-';
      const action = row.step?.action ?? '-';
      const turn = row.turn === null ? '-' : `${row.turn}`;
      return `#${row.ordinal} seq=${row.sequence} turn=${turn} type=${row.event.type} ${actor}/${action} | ${formatInBattleEvent(row.event)}`;
    }

    case 'lifecycle': {
      const detail = row.detail.length > 0 ? ` ${row.detail}` : '';
      const sequence = row.sequence === null ? '' : ` seq=${row.sequence}`;
      return `#${row.ordinal} ${row.scope} ${row.state}${detail}${sequence}`;
    }

    case 'outcome':
      return `#${row.ordinal} outcome ${row.outcome} bossHp=${row.finalBossHp} playerHp=${row.finalPlayerHp}`;
  }
}

// ---------------------------------------------------------------------------
// The client's own interaction indicator (TASK-246 §4)
// ---------------------------------------------------------------------------

/**
 * One statement of the client's **own** interaction state, ready to be drawn.
 *
 * It is deliberately not an event, a phase, or a battle value. The wire carries
 * no turn ownership, no phase, and no status field — `GAME_STATE.md` §2.0.3
 * ("§2.0 contains no `Status` field, and this document introduces none") and
 * `SIGNALR_PROTOCOL.md` §3.2.2's closed `type` discriminator set, which admits
 * no turn or phase event — so the strongest honest statement a client can make
 * is about what the client itself is doing. `label` is the line a player reads;
 * `color` is a display choice and carries no meaning.
 */
export interface InteractionStatement {
  readonly label: string;
  readonly color: string;
}

/**
 * The client currently permits player input: no action is in flight, and no
 * presentation timeline is playing.
 *
 * **It is not a server-asserted turn.** It states what this client's own input
 * guard permits (`BattleScene.isInputLocked`), and nothing on the wire says
 * whose turn it is (`TASK-246` F-3). Describing it as the server's own turn
 * claim would assert a value the server never sent (`AGENTS.md` §10).
 */
export const INTERACTION_YOUR_TURN: InteractionStatement = Object.freeze({
  label: 'YOUR TURN',
  color: '#4ade80',
});

/**
 * The local presentation timeline is playing, so input is held until it ends.
 *
 * A presentation status, **not** a server-defined phase. A resolution is
 * resolved and its state written back *before* its batch is sent, and no
 * per-step delivery exists (`SIGNALR_PROTOCOL.md` §3.1 items 1–2), so there is
 * no remote phase to observe at all — the honest meaning is exactly this local
 * timeline playing.
 */
export const INTERACTION_RESOLVING: InteractionStatement = Object.freeze({
  label: 'RESOLVING',
  color: '#94a3b8',
});

/**
 * The Boss's response is being presented.
 *
 * It is shown only while the local timeline plays a phase that a **delivered**
 * damage instance opened with `source="boss"` and `target="player"`
 * (`SIGNALR_PROTOCOL.md` §3.2.14 item 3; `GAME_RULES.md` §17 step 18c) — the
 * same classification `resolveDamagePresentationPhase` performs, so the party
 * vocabulary keeps one spelling. No Boss *intent* and no Boss-turn boundary is
 * inferred: neither exists on the wire, so neither can be shown before the
 * delivered batch arrives.
 */
export const INTERACTION_BOSS_ATTACK: InteractionStatement = Object.freeze({
  label: 'BOSS ATTACK',
  color: CALLOUT_BOSS_COLOR,
});

/**
 * The terminal statement for one delivered battle outcome.
 *
 * `outcome` is the delivered `BattleWon` / `BattleLost` member, fixed by
 * `SIGNALR_PROTOCOL.md` §3.2.19 item 1 as `"victory"` or `"defeat"`: the
 * battle's end is the server's own fact and this only names it for the player.
 * A value outside that pair is presented **as itself** rather than mapped onto
 * one of the two, because replacing a delivered value with a guessed one is what
 * `AGENTS.md` §7 forbids.
 */
export function resolveOutcomeStatement(outcome: string): InteractionStatement {
  switch (outcome.toLowerCase()) {
    case 'victory':
      return { label: 'VICTORY', color: '#34d399' };
    case 'defeat':
      return { label: 'DEFEAT', color: '#f87171' };
    default:
      return { label: outcome.toUpperCase(), color: '#f8fafc' };
  }
}

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
 * The single callout a delivered event batch should present, **together with the
 * delivered event it was selected from**.
 *
 * The event member is what binds the callout to its own step of the local
 * presentation timeline: the wire carries no per-step delivery
 * (`SIGNALR_PROTOCOL.md` §3.1 items 1–2), so the phase that presents the event
 * the callout reports is the phase the callout belongs to. `BattleScene` raises
 * it there — with that phase — rather than at batch arrival, so a callout is
 * never on screen before the delivered step it names has been presented
 * (TASK-254 AF-2).
 */
export interface BatchCalloutSelection {
  /** The callout the batch presents. */
  readonly callout: EventCallout;
  /** The delivered event that callout was selected from. */
  readonly event: InBattleServerEvent;
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
 *
 * It is the callout-only projection of the one selection rule: the priority
 * ladder and the delivery-order tie-break live in `selectBatchCalloutEvent`
 * alone, so there is no second spelling of either.
 */
export function selectBatchCallout(
  events: readonly InBattleServerEvent[],
  resolveCardName: (cardId: string) => string,
  resolveRelicName: (relicId: string) => string | null,
  resolveBossSkillName: (skillId: string) => string | null = resolveBossSkillDisplayName
): EventCallout | null {
  return (
    selectBatchCalloutEvent(events, resolveCardName, resolveRelicName, resolveBossSkillName)
      ?.callout ?? null
  );
}

/**
 * The batch's single callout and the delivered event that produced it.
 *
 * The selection is verbatim `selectBatchCallout`'s: the lowest-priority-number
 * event wins, and among equals the last delivered one. Naming the winning event
 * is what lets the caller present the line with the timeline phase that owns it
 * (TASK-254 AF-2) instead of at batch arrival, without adding a second
 * importance ladder or a queue.
 */
export function selectBatchCalloutEvent(
  events: readonly InBattleServerEvent[],
  resolveCardName: (cardId: string) => string,
  resolveRelicName: (relicId: string) => string | null,
  resolveBossSkillName: (skillId: string) => string | null = resolveBossSkillDisplayName
): BatchCalloutSelection | null {
  let selected: BatchCalloutSelection | null = null;

  for (const event of events) {
    const candidate = describeEventCallout(
      event,
      resolveCardName,
      resolveRelicName,
      resolveBossSkillName
    );

    if (
      candidate !== null &&
      (selected === null || candidate.priority <= selected.callout.priority)
    ) {
      selected = { callout: candidate, event };
    }
  }

  return selected;
}

// ---------------------------------------------------------------------------
// Authoritative Special Gem presentation (MATCH3_RULES.md §5, GAME_STATE.md §2.1.4)
// ---------------------------------------------------------------------------

/**
 * Visual differentiation configuration for authoritative Special Gems
 * (MATCH3_RULES.md §5, GAME_STATE.md §2.1.4, SIGNALR_PROTOCOL.md §3.2.10).
 */
export type SpecialGemVisualVariant = 'LineClearHorizontal' | 'LineClearVertical' | 'Burst' | 'Area';

export interface SpecialGemPresentationConfig {
  readonly variant: SpecialGemVisualVariant;
  readonly type: string;
  readonly orientation?: string;
  readonly badge: string;
  readonly strokeColor: number;
  readonly strokeWidth: number;
  readonly description: string;
}

export const SPECIAL_GEM_PRESENTATIONS: Readonly<Record<SpecialGemVisualVariant, SpecialGemPresentationConfig>> = Object.freeze({
  LineClearHorizontal: Object.freeze({
    variant: 'LineClearHorizontal',
    type: 'LineClear',
    orientation: 'Horizontal',
    badge: '[H]',
    strokeColor: 0x38bdf8, // Sky blue
    strokeWidth: 3,
    description: 'Horizontal Line Clear',
  }),
  LineClearVertical: Object.freeze({
    variant: 'LineClearVertical',
    type: 'LineClear',
    orientation: 'Vertical',
    badge: '[V]',
    strokeColor: 0x38bdf8, // Sky blue
    strokeWidth: 3,
    description: 'Vertical Line Clear',
  }),
  Burst: Object.freeze({
    variant: 'Burst',
    type: 'Burst',
    badge: '[BURST]',
    strokeColor: 0xfacc15, // Golden yellow
    strokeWidth: 3,
    description: 'Burst (3x3 Square)',
  }),
  Area: Object.freeze({
    variant: 'Area',
    type: 'Area',
    badge: '[AREA]',
    strokeColor: 0xf472b6, // Vibrant pink
    strokeWidth: 3,
    description: 'Area (Cross/Plus)',
  }),
});

/**
 * Resolves the visual presentation config for an authoritative SpecialGem metadata object
 * (SIGNALR_PROTOCOL.md §3.2.10, GAME_STATE.md §2.1.4).
 * Returns null for ordinary gems or unrecognized special gem metadata.
 */
export function resolveSpecialGemPresentation(
  specialGem?: { readonly type?: string | null; readonly orientation?: string | null } | null
): SpecialGemPresentationConfig | null {
  if (!specialGem || typeof specialGem !== 'object' || !specialGem.type) {
    return null;
  }

  const type = specialGem.type;
  if (type === 'LineClear') {
    if (specialGem.orientation === 'Vertical') {
      return SPECIAL_GEM_PRESENTATIONS.LineClearVertical;
    }
    return SPECIAL_GEM_PRESENTATIONS.LineClearHorizontal;
  }

  if (type === 'Burst') {
    return SPECIAL_GEM_PRESENTATIONS.Burst;
  }

  if (type === 'Area') {
    return SPECIAL_GEM_PRESENTATIONS.Area;
  }

  return null;
}

/**
 * Formats a gem cell label, appending the special gem badge if a special gem is present.
 * For normal gems, returns baseLabel unmodified.
 */
export function formatGemCellLabel(
  baseLabel: string,
  specialGem?: { readonly type?: string | null; readonly orientation?: string | null } | null
): string {
  const spec = resolveSpecialGemPresentation(specialGem);
  if (!spec) {
    return baseLabel;
  }
  return `${baseLabel} ${spec.badge}`;
}

/**
 * Extracts the underlying base gem label (e.g. 'ATK', 'DEF', 'HP', 'PWR')
 * from a potentially decorated cell label ('ATK [H]' -> 'ATK').
 */
export function parseGemBaseLabel(renderedLabel?: string | null): string {
  if (!renderedLabel) {
    return '';
  }
  return renderedLabel.split(' ')[0] ?? renderedLabel;
}

/**
 * Extracts the special gem badge (e.g. '[H]', '[V]', '[BURST]', '[AREA]')
 * from a cell label, or null if it is an ordinary gem.
 */
export function parseSpecialGemBadge(renderedLabel?: string | null): string | null {
  if (!renderedLabel) {
    return null;
  }
  const parts = renderedLabel.split(' ');
  return parts.length > 1 ? (parts.slice(1).join(' ') || null) : null;
}

