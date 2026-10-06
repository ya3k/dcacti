/**
 * The client's one rendering of a delivered `RewardSummary`
 * (`API_CONTRACTS.md` §4 note 1, `DATABASE.md` §1).
 *
 * It exists so the two surfaces that present account progression render the
 * eight delivered members the same way: `ResultScene` (the completed battle's
 * own result, `GET /api/battle/{battleId}/result`) and `BattleHistoryScene`
 * (the latest delivered history element, `GET /api/battle/history`). The
 * alternative — a second, local formatting of the same members — would be a
 * second representation of one contract, which is what `docs/AGENTS.md` §2
 * forbids.
 *
 * **It formats; it never computes.** Every member is printed as delivered:
 * no XP is summed, no Level is derived from XP, no `leveledUp` flag is
 * re-derived by comparing Level values, and a `null` member — the contract's
 * "this track had no row" value (§1 item 4) — is rendered as unavailable rather
 * than converted to `0`. A `0` grant on a defeat is printed as `0` because it is
 * a real delivered value (§1 item 5).
 *
 * It holds no state, is not a store, and is deliberately not a class or a
 * service: it is the presentation-layer function the scenes call
 * (`AGENTS.md` §9).
 */

import type { RewardSummaryResponse } from '../../services/api/BattleModels';

/**
 * Formats the delivered `RewardSummary` members as the labelled block the
 * scenes draw.
 *
 * The four Player-track members and the four Pet-track members are grouped
 * under their own heading so the account progression and the Pet progression
 * stay distinguishable.
 */
export function formatRewards(rewards: RewardSummaryResponse): string {
  return [
    'REWARDS',
    `Player: +${rewards.playerXpGained} XP  ·  XP ${formatDeliveredValue(rewards.newPlayerXp)}  ·  Level ${formatDeliveredValue(rewards.newPlayerLevel)}${formatLeveledUp(rewards.playerLeveledUp)}`,
    `Pet: +${rewards.petXpGained} XP  ·  XP ${formatDeliveredValue(rewards.newPetXp)}  ·  Level ${formatDeliveredValue(rewards.newPetLevel)}${formatLeveledUp(rewards.petLeveledUp)}`,
  ].join('\n');
}

/**
 * Renders one delivered "resulting value" member.
 *
 * A `null` member is the contract's "no row" value, not an absent field and not
 * a zero (`DATABASE.md` §1 item 4), so it is shown as unavailable rather than
 * converted.
 */
export function formatDeliveredValue(value: number | null): string {
  return value === null ? '—' : `${value}`;
}

/**
 * Renders a delivered `leveledUp` flag.
 *
 * The flag is printed from the delivered boolean and is never re-derived by
 * comparing XP or Level values (`DATABASE.md` §1 item 1).
 */
export function formatLeveledUp(leveledUp: boolean | null): string {
  if (leveledUp === null) {
    return '  ·  —';
  }

  return leveledUp ? '  ·  LEVEL UP' : '';
}
