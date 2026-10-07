import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import {
  describeCardEffect,
  describeRelicCondition,
  describeRelicEffect,
  formatCardEffects,
  formatCardSummary,
  formatRelicEffects,
  formatRelicSummary,
  isUndeterminedValueType,
} from '../src/game/presentation/ContentEffectFormat';
import type {
  CardEffectResponse,
  CardResponse,
  RelicConditionResponse,
  RelicEffectResponse,
  RelicResponse,
} from '../src/services/api/CollectionModels';

/**
 * TASK-212A-1 — the shared Card/Relic content formatter.
 *
 * ```text
 * CardResponse.effectDefinition[]   §5.3  → formatCardSummary
 * RelicResponse.trigger/condition/
 *   effectDefinition                §5.4  → formatRelicSummary
 * ```
 *
 * Every expectation below is a value `API_CONTRACTS.md` §5.3/§5.4 carries, and
 * the assertions pin the formatter's two rules: **print what the payload says**
 * (token for token, number for number) and **fail closed** (raw token or
 * omitted line — never a guessed name, a fabricated magnitude, a defaulted
 * member, or a `NaN`/`undefined` leaking into the text).
 */

/** A §5.3 Card carrying the given effect rule. */
function card(effectDefinition: CardResponse['effectDefinition']): CardResponse {
  return { cardId: 'card-x', name: 'Card', category: 'Basic', effectDefinition };
}

/** A §5.4 Relic carrying the given content members. */
function relic(
  content: Partial<Pick<RelicResponse, 'trigger' | 'condition' | 'effectDefinition'>>
): RelicResponse {
  return {
    relicId: 'relic-x',
    name: 'Relic',
    trigger: content.trigger ?? 'OnMatchCount',
    condition: content.condition ?? null,
    effectDefinition: content.effectDefinition ?? [],
  };
}

describe('ContentEffectFormat — Card effects (API_CONTRACTS.md §5.3)', () => {
  it('renders the identity, the magnitude, and the value interpretation', () => {
    // CARD_RULES.md §2's three Basic Cards, transcribed from the response.
    expect(
      formatCardSummary(card([{ effectType: 'Heal', valueType: 'PercentMaxHp', value: 20 }]))
    ).toBe('Heal 20% Max HP');

    expect(
      formatCardSummary(card([{ effectType: 'Shield', valueType: 'PercentMaxHp', value: 20 }]))
    ).toBe('Shield 20% Max HP');

    expect(
      formatCardSummary(card([{ effectType: 'Power', valueType: 'Flat', value: 25 }]))
    ).toBe('Power 25');
  });

  it('renders a percentage-point magnitude with its own unit', () => {
    // CARD_RULES.md §4.1 states the Crit increase in percentage points, and
    // DATABASE.md §3 makes `scope` present iff effectType = Crit.
    expect(
      formatCardSummary(
        card([{ effectType: 'Crit', valueType: 'PercentagePoints', value: 10, scope: 'NextAttack' }])
      )
    ).toBe('Crit 10 percentage points (NextAttack)');
  });

  it('renders every effect of a multi-effect Card separately, in order', () => {
    // TASK-111 D-1b: one element per effect, in stored order, never merged.
    expect(
      formatCardSummary(
        card([
          { effectType: 'Damage', valueType: 'Flat', value: 100 },
          { effectType: 'Burn', valueType: 'Flat', value: 50, duration: 2 },
        ])
      )
    ).toBe('Damage 100, Burn 50 (2 turns)');
  });

  it('preserves the Burn duration and the Crit scope exactly', () => {
    expect(
      describeCardEffect({ effectType: 'Burn', valueType: 'Flat', value: 50, duration: 2 })
    ).toBe('Burn 50 (2 turns)');

    // A duration the payload states is never rounded, defaulted, or dropped.
    expect(
      describeCardEffect({ effectType: 'Burn', valueType: 'Flat', value: 7, duration: 11 })
    ).toBe('Burn 7 (11 turns)');
  });

  it('states an Undetermined element without inventing a magnitude', () => {
    // DATABASE.md §1 item 9 / §3: the effect is stated, the magnitude is not.
    // `Undetermined` is a real token, and no number is substituted for it.
    expect(describeCardEffect({ effectType: 'Heal', valueType: 'Undetermined' })).toBe(
      'Heal (Undetermined)'
    );
  });

  it('renders an unknown effect identity verbatim', () => {
    expect(
      describeCardEffect({ effectType: 'FutureEffect', valueType: 'Flat', value: 3 })
    ).toBe('FutureEffect 3');
  });

  it('renders an unknown value interpretation verbatim beside its magnitude', () => {
    // The number is never given a unit this presentation does not know.
    expect(
      describeCardEffect({ effectType: 'FutureEffect', valueType: 'FutureUnit', value: 7 })
    ).toBe('FutureEffect 7 (FutureUnit)');

    // ...and an unknown interpretation with no magnitude still travels raw.
    expect(describeCardEffect({ effectType: 'FutureEffect', valueType: 'FutureUnit' })).toBe(
      'FutureEffect (FutureUnit)'
    );
  });

  it('omits an element that states no effect identity at all', () => {
    // There is no name to print, so the element is dropped rather than shown as
    // an unnamed or guessed effect (AGENTS.md §7).
    expect(describeCardEffect({} as CardEffectResponse)).toBe('');
    expect(
      formatCardEffects([{ valueType: 'Flat', value: 3 }, { effectType: 'Heal', valueType: 'Flat', value: 1 }])
    ).toBe('Heal 1');
  });

  it('renders empty text for an absent, non-array, or unusable member', () => {
    expect(formatCardEffects(undefined)).toBe('');
    expect(formatCardEffects(null)).toBe('');
    expect(formatCardEffects('Heal 20% Max HP')).toBe('');
    expect(formatCardEffects([])).toBe('');
    expect(formatCardEffects([null, 7, 'x'])).toBe('');
    expect(formatCardSummary({} as CardResponse)).toBe('');
  });

  it('never lets NaN, Infinity, undefined, or null reach the text', () => {
    const malformed = [
      { effectType: 'Heal', valueType: 'PercentMaxHp', value: Number.NaN },
      { effectType: 'Heal', valueType: 'PercentMaxHp', value: Number.POSITIVE_INFINITY },
      { effectType: 'Heal', valueType: 'PercentMaxHp', value: null },
      { effectType: 'Heal', valueType: 'PercentMaxHp', value: undefined },
      { effectType: 'Heal', valueType: 'Flat', value: '20' },
      { effectType: 'Burn', valueType: 'Flat', value: 50, duration: Number.NaN },
    ];

    for (const element of malformed) {
      const text = describeCardEffect(element as CardEffectResponse);

      expect(text).not.toMatch(/NaN|Infinity|undefined|null/);
      // A non-numeric magnitude is treated as absent, so the element states its
      // interpretation token instead of a fabricated number.
      expect(text.startsWith('Heal') || text.startsWith('Burn')).toBe(true);
    }

    expect(describeCardEffect({ effectType: 'Heal', valueType: 'PercentMaxHp', value: Number.NaN })).toBe(
      'Heal (PercentMaxHp)'
    );
    expect(
      describeCardEffect({ effectType: 'Burn', valueType: 'Flat', value: 50, duration: Number.NaN })
    ).toBe('Burn 50');
  });
});

describe('ContentEffectFormat — Relic content (API_CONTRACTS.md §5.4)', () => {
  it('renders the trigger, the condition, and the effect of a provisioned Relic', () => {
    // RELIC_RULES.md §6/§8.5's Berserker Core, transcribed from the response.
    expect(
      formatRelicSummary(
        relic({
          trigger: 'OnMatchCount',
          condition: { conditionType: 'MatchCountAtLeast', threshold: 3 },
          effectDefinition: [
            {
              effectType: 'ATK',
              valueType: 'Percentage',
              value: 5,
              target: 'Pet',
              lifetime: 'Battle',
            },
          ],
        })
      )
    ).toBe('OnMatchCount MatchCountAtLeast 3 | ATK 5% (Pet, Battle)');
  });

  it('omits the condition segment when the Relic declares none', () => {
    // RELIC_RULES.md §8.1 item 4: the Trigger alone is the complete condition.
    // Nothing is guessed to fill the gap.
    expect(
      formatRelicSummary(
        relic({
          trigger: 'OnBattleStart',
          condition: null,
          effectDefinition: [
            {
              effectType: 'BurnDamage',
              valueType: 'Percentage',
              value: 30,
              target: 'Pet',
              lifetime: 'Battle',
            },
          ],
        })
      )
    ).toBe('OnBattleStart | BurnDamage 30% (Pet, Battle)');
  });

  it('renders each documented condition form and its own threshold', () => {
    expect(describeRelicCondition({ conditionType: 'MatchCountAtLeast', threshold: 4 })).toBe(
      'MatchCountAtLeast 4'
    );
    expect(describeRelicCondition({ conditionType: 'ComboAtLeast', threshold: 3 })).toBe(
      'ComboAtLeast 3'
    );
    expect(describeRelicCondition({ conditionType: 'HpPercentageBelow', threshold: 30 })).toBe(
      'HpPercentageBelow 30'
    );

    // The nullable case renders as nothing at all — never as a defaulted form.
    expect(describeRelicCondition(null)).toBe('');
    expect(describeRelicCondition(undefined as unknown as RelicConditionResponse | null)).toBe('');
  });

  it('renders an unknown condition form verbatim and never invents a threshold', () => {
    expect(describeRelicCondition({ conditionType: 'FutureForm', threshold: 9 })).toBe(
      'FutureForm 9'
    );
    expect(
      describeRelicCondition({ conditionType: 'FutureForm' } as RelicConditionResponse)
    ).toBe('FutureForm');
    expect(describeRelicCondition({ threshold: 9 } as RelicConditionResponse)).toBe('9');
    expect(describeRelicCondition({} as RelicConditionResponse)).toBe('');
  });

  it('renders the effect contract’s target and lifetime members', () => {
    // RELIC_RULES.md §8.3: `target` and `lifetime` are required members of every
    // element, and the lifetime is a property of the element, not of the label.
    expect(
      describeRelicEffect({
        effectType: 'Power',
        valueType: 'Flat',
        value: 10,
        target: 'Pet',
        lifetime: 'Immediate',
      })
    ).toBe('Power 10 (Pet, Immediate)');

    expect(
      describeRelicEffect({
        effectType: 'Crit',
        valueType: 'PercentagePoints',
        value: 20,
        target: 'Pet',
        lifetime: 'NextAttack',
      })
    ).toBe('Crit 20 percentage points (Pet, NextAttack)');

    expect(
      describeRelicEffect({
        effectType: 'CardCost',
        valueType: 'Percentage',
        value: 50,
        target: 'Pet',
        lifetime: 'Battle',
      })
    ).toBe('CardCost 50% (Pet, Battle)');
  });

  it('states an Undetermined Relic effect without inventing a magnitude', () => {
    // RELIC_RULES.md §8.2 item 3: no `value` member at all, never 0.
    expect(
      describeRelicEffect({
        effectType: 'Power',
        valueType: 'Undetermined',
        target: 'Pet',
        lifetime: 'Immediate',
      })
    ).toBe('Power (Undetermined) (Pet, Immediate)');
  });

  it('renders unknown Relic tokens verbatim', () => {
    expect(
      describeRelicEffect({
        effectType: 'FutureEffect',
        valueType: 'FutureUnit',
        value: 7,
        target: 'FutureTarget',
        lifetime: 'FutureLifetime',
      })
    ).toBe('FutureEffect 7 (FutureUnit) (FutureTarget, FutureLifetime)');
  });

  it('omits an element that states no effect identity at all', () => {
    expect(describeRelicEffect({} as RelicEffectResponse)).toBe('');
    expect(formatRelicEffects([{ target: 'Pet' }])).toBe('');
  });

  it('renders empty text for an absent, non-array, or unusable member', () => {
    expect(formatRelicEffects(undefined)).toBe('');
    expect(formatRelicEffects(null)).toBe('');
    expect(formatRelicEffects({})).toBe('');
    expect(formatRelicEffects([null, 'x', 3])).toBe('');
    expect(formatRelicSummary({} as RelicResponse)).toBe('');
  });

  it('never lets NaN, undefined, or null reach the text', () => {
    const malformed = [
      { effectType: 'ATK', valueType: 'Percentage', value: Number.NaN, target: 'Pet', lifetime: 'Battle' },
      { effectType: 'ATK', valueType: 'Percentage', value: null, target: 'Pet', lifetime: 'Battle' },
      { effectType: 'ATK', valueType: 'Percentage', value: undefined, target: 'Pet', lifetime: 'Battle' },
    ];

    for (const element of malformed) {
      expect(describeRelicEffect(element as RelicEffectResponse)).not.toMatch(
        /NaN|Infinity|undefined|null/
      );
    }

    // ...and a missing target/lifetime never renders an "undefined" pair.
    expect(
      describeRelicEffect({ effectType: 'ATK', valueType: 'Percentage', value: 5 } as RelicEffectResponse)
    ).toBe('ATK 5%');
  });

  it('reports the contract’s own "no magnitude authored" token', () => {
    expect(isUndeterminedValueType('Undetermined')).toBe(true);
    expect(isUndeterminedValueType('Flat')).toBe(false);
    expect(isUndeterminedValueType(undefined)).toBe(false);
  });
});

describe('ContentEffectFormat — scope (AGENTS.md §7/§9/§10)', () => {
  it('holds no per-id content table and no hardcoded magnitude', () => {
    const source = readFileSync(
      resolve(__dirname, '../src/game/presentation/ContentEffectFormat.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    // A content catalog — a per-Card, per-Relic, per-definition, or per-id
    // lookup — would be a second source of truth (RELIC_RULES.md §8.2 item 1,
    // GAME_STATE.md §0 item 5). The module is a token translation plus a
    // printer, and it names no content row.
    for (const forbidden of [
      'card-heal',
      'card-shield',
      'card-power-charge',
      'relic-berserker-core',
      'CardDefinitionId',
      'RelicDefinitionId',
      'relicId',
      'cardId',
    ]) {
      expect(source, `the formatter must not reference "${forbidden}"`).not.toContain(forbidden);
    }

    // And it computes no cost, affordability, or legality value.
    for (const forbidden of [
      'powerCost',
      'effectiveCost',
      'affordable',
      'canCast',
      'legality',
      'Math.',
    ]) {
      expect(source, `the formatter must not reference "${forbidden}"`).not.toContain(forbidden);
    }
  });
});
