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
