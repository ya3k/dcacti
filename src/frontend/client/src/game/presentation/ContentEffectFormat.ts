/**
 * The client's one rendering of the structured Card and Relic content
 * `API_CONTRACTS.md` §5.3/§5.4 deliver.
 *
 * ```text
 * CardResponse.effectDefinition[]        §5.3  → describeCardEffect
 * RelicResponse.trigger/condition        §5.4  → describeRelicCondition
 * RelicResponse.effectDefinition[]       §5.4  → describeRelicEffect
 *         ↓
 * LobbyScene's loadout rows  ·  CollectionViewerScene's detail panel
 * ```
 *
 * It exists so the two surfaces that present the same content render it the
 * same way (`RewardSummaryFormat.ts`'s precedent): a second, local formatting
 * of the same members would be a second representation of one contract, which
 * is what `docs/AGENTS.md` §2 forbids. It is deliberately a plain module of
 * functions, not a class, service, registry, or store (`AGENTS.md` §9).
 *
 * **It reads; it never infers, defaults, or computes.** Every token and every
 * number is printed from the payload: no magnitude is invented for an element
 * that carries none, no absent optional member is interpolated, no
 * `PercentMaxHp` is resolved against a Pet's Max HP, no `Damage` element is
 * turned into a dealt amount, and no lifetime, threshold, or duration is
 * derived (`AGENTS.md` §10, `SIGNALR_PROTOCOL.md` §4 item 15,
 * `API_CONTRACTS.md` §5.3/§5.4's boundary notes).
 *
 * **It fails closed.** An unrecognized token is rendered **verbatim** rather
 * than translated or guessed; an element with no effect identity at all is
 * omitted rather than shown as an unnamed effect; a non-array, a non-object
 * element, or an absent response member renders as empty text and the caller
 * draws no line. Nothing is ever fabricated to fill a gap (`AGENTS.md` §7;
 * `TASK-208` §D's "never a guessed name" rule).
 *
 * **It is not a content catalog.** There is no per-Card, per-Relic,
 * per-definition, or per-id table and no hardcoded magnitude: the only tables
 * below are the token→unit translations the contract's own closed vocabularies
 * require in order to be readable, and an unmapped token simply passes through
 * (`RELIC_RULES.md` §8.2 item 1, `AGENTS.md` §7/§9).
 */

import type {
  CardEffectResponse,
  CardResponse,
  RelicConditionResponse,
  RelicEffectResponse,
  RelicResponse,
} from '../../services/api/CollectionModels';

/**
 * How a known Card `valueType` reads after its magnitude
 * (`DATABASE.md` §1 item 1 / §3's interpreting members; the magnitudes they
 * interpret are `CARD_RULES.md` §2/§4.1's).
 *
 * `Flat` carries no unit text — the magnitude is a plain amount — and the two
 * proportional members keep their own unit so the number is never read as the
 * wrong quantity. `Undetermined` is deliberately absent: it interprets no
 * magnitude, so it is rendered as its own token rather than as a unit.
 */
const CARD_VALUE_TYPE_UNITS: Readonly<Record<string, string>> = {
  Flat: '',
  PercentMaxHp: '% Max HP',
  PercentagePoints: 'percentage points',
};

/**
 * How a known Relic `valueType` reads after its magnitude
 * (`RELIC_RULES.md` §8.2 item 2 / §8.3's table). Same convention as
 * {@link CARD_VALUE_TYPE_UNITS}: `Percentage` is a proportion and
 * `PercentagePoints` a percentage-point quantity, and the two are never
 * collapsed into one another.
 */
const RELIC_VALUE_TYPE_UNITS: Readonly<Record<string, string>> = {
  Flat: '',
  Percentage: '%',
  PercentagePoints: 'percentage points',
};

/**
 * The one spelling of "no magnitude was authored" the structured contracts
 * define — `DATABASE.md` §1 item 9 for Cards, `RELIC_RULES.md` §8.2 item 3 for
 * Relics. It is a real token, not an error state.
 */
const UNDETERMINED_VALUE_TYPE = 'Undetermined';

/** The Turn unit `GAME_RULES.md` §17 step 19a / `COMBAT_RULES.md` §5.2 own. */
const TURN_UNIT = 'turns';

/** Reads a wire member as a non-empty token, or `null` when it is not one. */
function readToken(value: unknown): string | null {
  return typeof value === 'string' && value !== '' ? value : null;
}

/** Reads a wire member as a finite number, or `null` when it is not one. */
function readNumber(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

/**
 * Renders `<effectType> <magnitude><unit>` for one effect element, with the
 * effect-specific extra members the caller already rendered appended in
 * parentheses.
 *
 * The magnitude and its unit are rendered only when **both** are present and
 * the `valueType` is a known interpreting token: a magnitude whose
 * interpretation this presentation does not know is printed with its own raw
 * `valueType` token beside it, so the number is never silently given a unit the
 * payload does not state.
 *
 * @returns the rendered element, or `null` when the element states no effect
 * identity at all — the one case the caller omits rather than guesses.
 */
function describeEffect(
  effect: { readonly effectType?: unknown; readonly valueType?: unknown; readonly value?: unknown },
  units: Readonly<Record<string, string>>,
  extras: readonly string[]
): string | null {
  const effectType = readToken(effect.effectType);

  if (effectType === null) {
    return null;
  }

  const valueType = readToken(effect.valueType);
  const value = readNumber(effect.value);
  const unit = valueType === null ? undefined : units[valueType];

  let text: string;

  if (value === null) {
    // No magnitude. The interpreting valueType is still carried, so it is
    // printed as its own token — `Undetermined` is a real contract value, not
    // an error (DATABASE.md §1 item 9, RELIC_RULES.md §8.2 item 3).
    text = valueType === null ? effectType : `${effectType} (${valueType})`;
  } else if (unit !== undefined && unit !== '') {
    // A unit that is its own symbol attaches to the number (`20% Max HP`); a
    // unit that is a word is separated from it (`10 percentage points`).
    const separator = unit.startsWith('%') ? '' : ' ';

    text = `${effectType} ${value}${separator}${unit}`;
  } else if (unit !== undefined) {
    text = `${effectType} ${value}`;
  } else {
    // A magnitude whose interpretation is not one this presentation knows: the
    // raw token travels with the number instead of an assumed unit.
    text =
      valueType === null ? `${effectType} ${value}` : `${effectType} ${value} (${valueType})`;
  }

  return extras.length === 0 ? text : `${text} (${extras.join(', ')})`;
}

/** Renders one Card effect element (`API_CONTRACTS.md` §5.3). */
export function describeCardEffect(effect: CardEffectResponse): string {
  // DATABASE.md §3's present-iff extras: `duration` exists only on a Burn
  // element and `scope` only on a Crit element, so at most one is present and
  // neither is ever defaulted.
  const extras: string[] = [];

  const duration = readNumber(effect?.duration);
  const scope = readToken(effect?.scope);

  if (duration !== null) {
    extras.push(`${duration} ${TURN_UNIT}`);
  }

  if (scope !== null) {
    extras.push(scope);
  }

  return describeEffect(effect, CARD_VALUE_TYPE_UNITS, extras) ?? '';
}

/** Renders one Relic effect element (`API_CONTRACTS.md` §5.4). */
export function describeRelicEffect(effect: RelicEffectResponse): string {
  // RELIC_RULES.md §8.3's required extras, rendered together as the element's
  // own `(target, lifetime)` pair. The Card-only `duration` and `scope` members
  // have no Relic counterpart (§8.3 item 5), so neither is read here.
  const pair = [readToken(effect?.target), readToken(effect?.lifetime)]
    .filter((member): member is string => member !== null)
    .join(', ');

  return describeEffect(effect, RELIC_VALUE_TYPE_UNITS, pair === '' ? [] : [pair]) ?? '';
}

/**
 * Renders a `condition` object (`API_CONTRACTS.md` §5.4,
 * `RELIC_RULES.md` §8.1) as `<conditionType> <threshold>`.
 *
 * `null` is the contract's own "this Relic declares no extra condition" value
 * (§8.1 item 4) and renders as empty text — never as a guessed condition and
 * never as `0`.
 */
export function describeRelicCondition(condition: RelicConditionResponse | null): string {
  if (condition === null || typeof condition !== 'object') {
    return '';
  }

  const conditionType = readToken(condition.conditionType);
  const threshold = readNumber(condition.threshold);

  if (conditionType === null) {
    return threshold === null ? '' : `${threshold}`;
  }

  // A threshold is required whenever a form is stated (§8.1 item 1), so an
  // absent one is shown as an absent value rather than as a default.
  return threshold === null ? conditionType : `${conditionType} ${threshold}`;
}

/**
 * Renders a Card's complete structured effect rule as one line.
 *
 * ```text
 * Heal 20% Max HP
 * Damage 100, Burn 50 (2 turns)
 * ```
 *
 * An absent or non-array member, or a list whose every element is unusable,
 * renders as empty text — the caller then draws no effect line at all.
 */
export function formatCardEffects(effects: unknown): string {
  if (!Array.isArray(effects)) {
    return '';
  }

  const parts: string[] = [];

  for (const effect of effects) {
    if (effect === null || typeof effect !== 'object') {
      continue;
    }

    const text = describeCardEffect(effect as CardEffectResponse);

    if (text !== '') {
      parts.push(text);
    }
  }

  return parts.join(', ');
}

/** Renders one Card's own effect rule — the whole §5.3 content member. */
export function formatCardSummary(card: CardResponse): string {
  return formatCardEffects(card?.effectDefinition);
}

/**
 * Renders a Relic's complete structured content as one line: the Trigger, the
 * nullable Condition, then the effects.
 *
 * ```text
 * OnMatchCount MatchCountAtLeast 3 | ATK 5% (Pet, Battle)
 * OnBattleStart | BurnDamage 30% (Pet, Battle)
 * ```
 *
 * A Relic with no condition simply omits that segment, so
 * `RELIC_RULES.md` §8.1 item 4's nullable case is presented as "no extra
 * condition" rather than as an absent or empty one. Nothing is derived from the
 * Relic's name or ids — those are not read here at all.
 */
export function formatRelicSummary(relic: RelicResponse): string {
  const segments: string[] = [];

  const trigger = readToken(relic?.trigger);
  const condition = describeRelicCondition(relic?.condition ?? null);
  const head = [trigger, condition].filter((part) => part !== null && part !== '').join(' ');
  const effects = formatRelicEffects(relic?.effectDefinition);

  if (head !== '') {
    segments.push(head);
  }

  if (effects !== '') {
    segments.push(effects);
  }

  return segments.join(' | ');
}

/**
 * Renders a Relic's complete structured effect rule as one line, elements in
 * the order the server sent.
 *
 * An absent or non-array member renders as empty text, exactly as
 * {@link formatCardEffects} does.
 */
export function formatRelicEffects(effects: unknown): string {
  if (!Array.isArray(effects)) {
    return '';
  }

  const parts: string[] = [];

  for (const effect of effects) {
    if (effect === null || typeof effect !== 'object') {
      continue;
    }

    const text = describeRelicEffect(effect as RelicEffectResponse);

    if (text !== '') {
      parts.push(text);
    }
  }

  return parts.join(', ');
}

/**
 * Whether a delivered effect element is the contract's "no magnitude was
 * authored" case rather than an authored one.
 *
 * It reports the payload's own `valueType`, so a caller can distinguish a
 * stated-but-unquantified effect from a quantified one without inferring a
 * magnitude (`DATABASE.md` §1 item 9, `RELIC_RULES.md` §8.2 item 3).
 */
export function isUndeterminedValueType(valueType: unknown): boolean {
  return readToken(valueType) === UNDETERMINED_VALUE_TYPE;
}
