import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * TASK-227 / TASK-228 — the Phase 6, Phase 6b and Phase 6d cast-feedback waits.
 *
 * `standalone-web-smoke.mjs` clicks a cast control and then polls the battle
 * scene's `castText` line until the server's acknowledgement shows up. The line
 * carries **three** different kinds of text (`BattleScene.renderCastStatus`,
 * `SIGNALR_PROTOCOL.md` §2, §5):
 *
 * ```text
 * CardCast <cardId> in flight…                      request on the wire
 * PetSkillCast in flight…                           request on the wire
 * CardCast <cardId> not sent: <error>               request never left the client
 * CardCast <cardId>: accepted. Awaiting the server's state push.   settled
 * PetSkillCast: rejected (<reason>).                              settled
 * ```
 *
 * Waiting for "a caption that changed" therefore waits for the *transient*
 * `… in flight…` line as well, which is the race this task removes: only
 * `accepted` / `rejected` is the server's own answer.
 *
 * The guard is a **source guard** in the repository's established style (see
 * `SceneLifecycle.test.ts`): the smoke script is a `.mjs` entry point that drives
 * Chrome over CDP, so it can neither be imported by this TypeScript suite nor
 * executed here. Instead the script's own predicates are read out of its source,
 * comment-stripped, and — for the two waits and their shared predicate — compiled
 * and run against the real caption vocabulary. Nothing in this file starts a
 * browser, and no line number is referenced anywhere.
 */

const SMOKE_SCRIPT = resolve(__dirname, '../scripts/standalone-web-smoke.mjs');
const BATTLE_SCENE = resolve(__dirname, '../src/game/scenes/BattleScene.ts');

/**
 * The repository's comment-stripping convention: prose that *names* a construct
 * is not the construct. Applied before every inspection below, so an explanatory
 * comment can never satisfy — or trip — an assertion.
 */
function stripComments(source: string): string {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:])\/\/.*$/gm, '$1');
}

const SOURCE = stripComments(readFileSync(SMOKE_SCRIPT, 'utf8'));

// ---------------------------------------------------------------------------
// The scene's own captions, verbatim (`BattleScene.renderCastStatus`). Asserted
// against that source below, so these fixtures are the product's strings rather
// than this test's invention.
// ---------------------------------------------------------------------------
const CARD_IN_FLIGHT = 'CardCast card-inferno in flight…';
const SKILL_IN_FLIGHT = 'PetSkillCast in flight…';
const NOT_SENT = 'PetSkillCast not sent: Network request failed';
const UNAVAILABLE = 'CardCast unavailable: no runtime is connected.';
/** Phase 6's first cast: the settled caption Phase 6b's stale marker holds. */
const CARD_ACCEPTED = "CardCast card-heal: accepted. Awaiting the server's state push.";
/** Phase 6b's second control — the `in flight…` caption above is its own. */
const SECOND_CARD_ACCEPTED = "CardCast card-inferno: accepted. Awaiting the server's state push.";
const SECOND_CARD_REJECTED =
  'CardCast card-inferno: rejected (CARD_CAST_ALREADY_USED_THIS_TURN).';
const SKILL_ACCEPTED = "PetSkillCast: accepted. Awaiting the server's state push.";
const SKILL_REJECTED = 'PetSkillCast: rejected (PET_SKILL_NOT_OWNED).';

/** The Phase 6 first-cast wait: `castFeedback = await waitForCondition(...)` (V-2). */
const PHASE_6_WAIT =
  /const castFeedback = await waitForCondition\(([\s\S]*?),\s*'Authoritative cast acknowledgement feedback'/;
/** The Phase 6b wait: `secondCastText = await waitForCondition(...)`. */
const PHASE_6B_WAIT =
  /secondCastText\s*=\s*await\s+waitForCondition\(([\s\S]*?),\s*'second cast acknowledgement'/;
/** The Phase 6d wait: `skillFeedback = await waitForCondition(...)`. */
const PHASE_6D_WAIT =
  /skillFeedback\s*=\s*await\s+waitForCondition\(([\s\S]*?),\s*'Signature Skill control acknowledgement'/;

/**
 * Compiles a self-contained snippet of the smoke script into a callable, so the
 * guard runs the script's **own** code rather than a paraphrase of it. Only pure
 * predicates are compiled; the script's CDP calls are supplied as stubs.
 */
function compile(body: string, parameters: string[], values: unknown[]): unknown {
  const factory = new Function(...parameters, body) as unknown as (
    ...args: unknown[]
  ) => unknown;
  return factory(...values);
}

/** The shared predicate the two waits must route through, as the script ships it. */
function settledPredicate(): (text: unknown) => boolean {
  const declaration = SOURCE.match(/function isSettledCastAcknowledgement\([\s\S]*?\n\}/);
  if (!declaration) {
    throw new Error(
      'standalone-web-smoke.mjs must declare a self-contained ' +
        '`function isSettledCastAcknowledgement(text) { ... }` for the cast-feedback waits'
    );
  }
  return compile(`return (${declaration[0]});`, [], []) as (text: unknown) => boolean;
}

/** One captured wait predicate, ready to be run against a caption. */
function capturedPredicate(pattern: RegExp, waitName: string): string {
  const match = SOURCE.match(pattern);
  if (!match) {
    throw new Error(`standalone-web-smoke.mjs no longer contains the ${waitName} wait`);
  }
  return match[1].trim();
}

/**
 * Runs one captured wait predicate exactly as `waitForCondition` does, with the
 * scene's `castText` stubbed to `caption` and the phase's own stale-caption
 * marker set. The Phase 6 first-cast wait has no stale marker (V-2), so leaving
 * both markers unset runs it with the same free variables it sees in the script.
 */
async function runWait(
  predicateSource: string,
  caption: unknown,
  markers: { castFeedback?: string; secondCastText?: string } = {}
): Promise<unknown> {
  const predicate = compile(
    `return (${predicateSource});`,
    [
      'evaluate',
      'cdp',
      'BATTLE_SNAPSHOT',
      'castFeedback',
      'secondCastText',
      'isSettledCastAcknowledgement',
    ],
    [
      async () => ({ castText: caption }),
      {},
      '',
      markers.castFeedback ?? null,
      markers.secondCastText ?? null,
      settledPredicate(),
    ]
  ) as () => Promise<unknown>;

  return predicate();
}

describe('standalone-web-smoke — cast feedback waits settle on an acknowledgement (TASK-227)', () => {
  it('rejects every transient caption the cast line can carry', () => {
    // `in flight…` is the caption that was misread as an acknowledgement: the
    // request is on the wire and the server has answered nothing yet.
    expect(settledPredicate()(SKILL_IN_FLIGHT)).toBe(false);
    expect(settledPredicate()(CARD_IN_FLIGHT)).toBe(false);

    // A request that never left the client is a client-side failure, not a
    // server acknowledgement.
    expect(settledPredicate()(NOT_SENT)).toBe(false);
    expect(settledPredicate()(UNAVAILABLE)).toBe(false);

    // No caption at all, and a non-string (`snap.castText || ''` can only yield
    // a string, but the predicate must not be satisfied by anything else).
    expect(settledPredicate()('')).toBe(false);
    expect(settledPredicate()(null)).toBe(false);
    expect(settledPredicate()(undefined)).toBe(false);
    expect(settledPredicate()(42)).toBe(false);
  });

  it('accepts both settled acknowledgements', () => {
    expect(settledPredicate()(CARD_ACCEPTED)).toBe(true);
    expect(settledPredicate()(SECOND_CARD_ACCEPTED)).toBe(true);
    expect(settledPredicate()(SKILL_ACCEPTED)).toBe(true);
    expect(settledPredicate()(SECOND_CARD_REJECTED)).toBe(true);
    expect(settledPredicate()(SKILL_REJECTED)).toBe(true);
  });

  it('waits on a settled acknowledgement at Phase 6b, not on a changed caption', async () => {
    // The changed-caption failure this task removes: the first cast is settled and
    // the second control's own `in flight…` line is a *change*, but it is not an
    // acknowledgement. This run is what the script used to accept.
    const structural = capturedPredicate(PHASE_6B_WAIT, 'Phase 6b');
    expect(structural, 'Phase 6b must route its wait through the settled predicate').toContain(
      'isSettledCastAcknowledgement('
    );
    // The caption is returned only under that predicate; a changed caption alone
    // can never resolve the wait.
    expect(structural, 'Phase 6b must gate its result on the settled predicate').toMatch(
      /isSettledCastAcknowledgement\(\s*text\s*\)\s*\?\s*text\s*:\s*false/
    );
    // The existing retry behavior is intact: a caption equal to the previous
    // phase's text is still not a new acknowledgement, so the click is retried.
    expect(structural, 'Phase 6b must keep rejecting the stale caption').toContain(
      'text !== castFeedback'
    );

    const markers = { castFeedback: CARD_ACCEPTED };

    // Transient: rejected, whichever transient caption it is.
    expect(await runWait(structural, CARD_IN_FLIGHT, markers)).toBe(false);
    expect(await runWait(structural, NOT_SENT, markers)).toBe(false);
    expect(await runWait(structural, '', markers)).toBe(false);
    expect(await runWait(structural, null, markers)).toBe(false);

    // Settled: accepted, and the rejection B-02 is about.
    expect(await runWait(structural, SECOND_CARD_ACCEPTED, markers)).toBe(SECOND_CARD_ACCEPTED);
    expect(await runWait(structural, SECOND_CARD_REJECTED, markers)).toBe(SECOND_CARD_REJECTED);

    // Settled but *stale* — the first cast's own caption, unchanged — is still
    // not a new acknowledgement, so the wait keeps polling.
    expect(await runWait(structural, CARD_ACCEPTED, markers)).toBe(false);
  });

  it('waits on a settled acknowledgement at Phase 6d, not on a changed caption', async () => {
    // Phase 6d's `secondCastText` is Phase 6b's own settled caption, so the
    // Signature Skill's `in flight…` line is a change — and was the intermittent
    // false failure. It must no longer satisfy this wait.
    const structural = capturedPredicate(PHASE_6D_WAIT, 'Phase 6d');
    expect(structural, 'Phase 6d must route its wait through the settled predicate').toContain(
      'isSettledCastAcknowledgement('
    );
    expect(structural, 'Phase 6d must gate its result on the settled predicate').toMatch(
      /isSettledCastAcknowledgement\(\s*text\s*\)\s*\?\s*text\s*:\s*false/
    );
    expect(structural, 'Phase 6d must keep rejecting the stale caption').toContain(
      'text !== secondCastText'
    );

    const markers = { secondCastText: SECOND_CARD_REJECTED };

    expect(await runWait(structural, SKILL_IN_FLIGHT, markers)).toBe(false);
    expect(await runWait(structural, CARD_IN_FLIGHT, markers)).toBe(false);
    expect(await runWait(structural, NOT_SENT, markers)).toBe(false);
    expect(await runWait(structural, '', markers)).toBe(false);
    expect(await runWait(structural, null, markers)).toBe(false);

    expect(await runWait(structural, SKILL_ACCEPTED, markers)).toBe(SKILL_ACCEPTED);
    expect(await runWait(structural, SKILL_REJECTED, markers)).toBe(SKILL_REJECTED);

    // Phase 6b's own caption, unchanged, is still not a new acknowledgement.
    expect(await runWait(structural, SECOND_CARD_REJECTED, markers)).toBe(false);
  });

  it('routes all three cast-feedback waits through the shared predicate', () => {
    // One declaration plus exactly three call sites: the Phase 6 first-cast wait
    // (V-2, TASK-228) and the Phase 6b / Phase 6d waits (V-1, TASK-227). A fourth
    // call site would mean the predicate was wired somewhere it was not audited
    // for.
    expect(SOURCE.match(/isSettledCastAcknowledgement\(/g) ?? []).toHaveLength(4);
    expect(SOURCE).toContain('phase6.castFeedbackReceived');
    expect(SOURCE).toContain('phase6b.secondCastAcknowledged');
    expect(SOURCE).toContain('phase6d.signatureSkillControlRemainsUsable');
  });

  it('waits on a settled acknowledgement at the Phase 6 first cast (V-2)', async () => {
    // V-2: this wait used to accept the first *non-empty* caption, which is the
    // transient `… in flight…` line the scene renders before the server answers.
    // There is no earlier caption to go stale here, so the gate is the settled
    // predicate alone.
    const structural = capturedPredicate(PHASE_6_WAIT, 'Phase 6 first cast');
    expect(structural, 'Phase 6 must route its wait through the settled predicate').toContain(
      'isSettledCastAcknowledgement('
    );
    expect(structural, 'Phase 6 must gate its result on the settled predicate').toMatch(
      /isSettledCastAcknowledgement\(\s*text\s*\)\s*\?\s*text\s*:\s*false/
    );

    // Transient captions: rejected, so the wait keeps polling.
    expect(await runWait(structural, CARD_IN_FLIGHT)).toBe(false);
    expect(await runWait(structural, 'CardCast card-inferno not sent: transport failure')).toBe(
      false
    );
    expect(await runWait(structural, UNAVAILABLE)).toBe(false);
    expect(await runWait(structural, '')).toBe(false);
    expect(await runWait(structural, null)).toBe(false);

    // Settled acknowledgements: resolved, and the *original* caption is returned.
    expect(await runWait(structural, CARD_ACCEPTED)).toBe(CARD_ACCEPTED);
    expect(await runWait(structural, 'CardCast card-heal: rejected (INSUFFICIENT_POWER).')).toBe(
      'CardCast card-heal: rejected (INSUFFICIENT_POWER).'
    );
  });

  it('leaves the production captions untouched', () => {
    // The captions this guard reasons about are the scene's own strings: the
    // transient line it must reject, and the settled forms it must accept. This
    // task changed no production caption.
    const battleScene = stripComments(readFileSync(BATTLE_SCENE, 'utf8'));
    expect(battleScene).toContain("'PetSkillCast in flight…'");
    expect(battleScene).toContain(": accepted. Awaiting the server's state push.");
    expect(battleScene).toContain(": rejected (${acknowledgement.reason ?? 'unknown'}).");
  });
});
