namespace GameServer.Domain.Passives;

/// <summary>
/// One <c>PassiveCharged</c> report — the domain-side description of a Passive
/// whose progress increased (<c>GAME_EVENTS.md</c> §2, <c>PASSIVE_RULES.md</c>
/// §2, §7).
///
/// <code>
/// PassiveId     which Passive charged
/// Progress      the progress the increment produced  (GAME_EVENTS.md §2)
/// Threshold     the Passive's threshold              (GAME_EVENTS.md §2)
/// Source        "pet" or "boss" — which entity's Passive  (GAME_EVENTS.md §2)
/// SourceId      the owning entity's identity
/// </code>
///
/// <b>This is a Domain value, not a wire type.</b> It introduces no protocol
/// message and defines no serialization (<c>GAME_EVENTS.md</c> §3 item 1,
/// <c>SIGNALR_PROTOCOL.md</c> §8 item 1), exactly as the Match-3 event payloads
/// do not (<c>GemMatchedEvent</c>).
///
/// <b>The progress reported is the value after this increment</b>, and it is
/// the value at the moment the Match was counted. Because
/// <c>PASSIVE_RULES.md</c> §2 item 3 evaluates the Threshold once, after the whole
/// Cascade's batch, a charge is <b>informational</b>: it never causes a threshold
/// evaluation and never causes a reset, and the value it reports may sit at or
/// above the Threshold on a Cascade that triggered exactly once (the batch's
/// overshoot, §4 item 2, §5). A charge is emitted for every Match (§2 item 1:
/// +1 per Match), so the charged values of a Cascade run <c>P+1 … P+N</c>
/// regardless of where the single evaluation lands.
///
/// <b>The event is shared by the Pet and Boss Passive systems</b>
/// (<c>PASSIVE_RULES.md</c> §7, <c>BOSS_RULES.md</c> §3, §7): <c>source</c>
/// distinguishes them and <c>sourceId</c> names the owning entity.
/// <c>BOSS_RULES.md</c> §7 is explicit that "no Boss-specific passive event name
/// is needed", so there is no <c>BossPassiveCharged</c>.
/// </summary>
/// <param name="PassiveId">
/// The identity of the Passive that charged — the active Pet's
/// <c>PetState.PassiveId</c> (<c>GAME_STATE.md</c> §2.3) or the Boss's
/// <c>BossState.PassiveId</c> (§2.4).
/// </param>
/// <param name="Progress">
/// The progress value this increment produced (<c>GAME_EVENTS.md</c> §2:
/// "new progress value").
/// </param>
/// <param name="Threshold">
/// The Passive's Threshold, reported alongside so the client renders
/// <c>Progress / Threshold</c> without recomputing it (<c>GAME_EVENTS.md</c>
/// §2 item 2, <c>PASSIVE_RULES.md</c> §6 item 1).
/// </param>
/// <param name="Source">
/// Which entity's Passive charged — <c>"pet"</c> or <c>"boss"</c>
/// (<c>GAME_EVENTS.md</c> §2, <c>SIGNALR_PROTOCOL.md</c> §3.2.16 item 1). The
/// default is <c>"pet"</c>, which is the Pet Passive stage's value and keeps
/// every existing Pet call site unchanged; the Boss Passive stage passes
/// <c>"boss"</c> explicitly.
/// </param>
/// <param name="SourceId">
/// The identity of the owning entity — <c>PetState.PetId</c> for a Pet, or
/// <c>BossState.BossId</c> for a Boss (<c>SIGNALR_PROTOCOL.md</c> §3.2.16
/// item 2). For a Boss it is the display-name BossId (e.g. <c>"Hỏa Long"</c>),
/// never a slug (<c>BOSS_RULES.md</c> §6.4). <c>null</c> means the caller did not
/// name the owner: <c>PetState</c> carries no <c>PetId</c> in this stage
/// (<c>GAME_STATE.md</c> §2.3 — it belongs to the Pet identity stage), so the
/// Pet Passive stage has no value to report yet and omits it rather than
/// inventing one (<c>AGENTS.md</c> §7). The Boss Passive stage always supplies
/// its BossId.
/// </param>
public readonly record struct PassiveChargedEvent(
    PassiveId PassiveId,
    int Progress,
    int Threshold,
    string Source = PassiveEventSource.Pet,
    string? SourceId = null)
{
    /// <summary>
    /// "PassiveCharged (boss:boss-hoa-long-rage 3 / 5)" — for test diagnostics
    /// only.
    /// </summary>
    public override string ToString() =>
        $"PassiveCharged ({Source}:{PassiveId} {Progress} / {Threshold})";
}

/// <summary>
/// One <c>PassiveTriggered</c> report — the domain-side description of a
/// Passive that reached its Threshold and activated
/// (<c>GAME_EVENTS.md</c> §2, <c>PASSIVE_RULES.md</c> §2 item 3, §7).
///
/// <code>
/// PassiveId     which Passive triggered
/// Progress      the progress at the moment the threshold was crossed
/// Threshold     the Passive's threshold
/// Source        "pet" or "boss" — which entity's Passive
/// SourceId      the owning entity's identity
/// </code>
///
/// <b>The <c>effect summary</c> member is deliberately absent.</b>
/// <c>PASSIVE_RULES.md</c> §7 defines this event as emitted "when the Passive
/// activates and its Effect resolves", and <c>GAME_EVENTS.md</c> §2 item 3
/// records that member as <b>deferred</b> to the Combat stage: what the effect
/// does — Burn, Shield, Crit, Defense — is owned by <c>COMBAT_RULES.md</c>, not
/// by the Passive tracker. This value is therefore the trigger's own report, and
/// the effect summary is populated by the Combat/Pet stage's task rather than
/// inferred here. Nothing in this event states what the effect did, and a
/// consumer must not read its absence as "no effect occurred"
/// (<c>GAME_EVENTS.md</c> §2 item 3).
///
/// <b>The progress reported is pre-reset.</b> <c>PASSIVE_RULES.md</c> §2 item 3
/// evaluates the Threshold once, after all of the Cascade's Matches have been
/// counted, and §4 applies the Reset Behavior <b>after</b> the trigger, so this
/// value is the accumulated batch total the trigger was reached at — not the
/// progress left afterwards. That is what makes §5's examples readable on the
/// event: at Threshold 5 a 7-Match Cascade reports <c>7</c> and leaves <c>0</c>
/// (Default) or <c>2</c> (Partial); at Threshold 3 it reports <c>7</c> and leaves
/// <c>4</c> under Partial Reset (§4 item 2).
/// </summary>
/// <param name="PassiveId">
/// The identity of the Passive that triggered — the active Pet's
/// <c>PetState.PassiveId</c> (<c>GAME_STATE.md</c> §2.3,
/// <c>GAME_EVENTS.md</c> §2 item 1) or the Boss's <c>BossState.PassiveId</c>
/// (§2.4).
/// </param>
/// <param name="Progress">
/// The progress at the moment the Threshold was crossed, before this trigger's
/// reset (<c>GAME_EVENTS.md</c> §2 item 2).
/// </param>
/// <param name="Threshold">
/// The Passive's Threshold (<c>GAME_EVENTS.md</c> §2 item 2).
/// </param>
/// <param name="Source">
/// Which entity's Passive triggered — <c>"pet"</c> or <c>"boss"</c>
/// (<c>GAME_EVENTS.md</c> §2, <c>SIGNALR_PROTOCOL.md</c> §3.2.17). The default is
/// <c>"pet"</c>, the Pet Passive stage's value; the Boss Passive stage passes
/// <c>"boss"</c> explicitly.
/// </param>
/// <param name="SourceId">
/// The identity of the owning entity — <c>PetState.PetId</c> or
/// <c>BossState.BossId</c> (<c>SIGNALR_PROTOCOL.md</c> §3.2.17). For a Boss it is
/// the display-name BossId (<c>BOSS_RULES.md</c> §6.4). <c>null</c> means the
/// caller did not name the owner; see
/// <see cref="PassiveChargedEvent"/>'s <c>SourceId</c> for why the Pet Passive
/// stage has no such value in this stage.
/// </param>
public readonly record struct PassiveTriggeredEvent(
    PassiveId PassiveId,
    int Progress,
    int Threshold,
    string Source = PassiveEventSource.Pet,
    string? SourceId = null)
{
    /// <summary>
    /// "PassiveTriggered (boss:boss-hoa-long-rage at 5 / 5)" — for test
    /// diagnostics only.
    /// </summary>
    public override string ToString() =>
        $"PassiveTriggered ({Source}:{PassiveId} at {Progress} / {Threshold})";
}

/// <summary>
/// The <c>source</c> member's two documented values — which entity's Passive a
/// shared <c>PassiveCharged</c>/<c>PassiveTriggered</c> report belongs to
/// (<c>GAME_EVENTS.md</c> §2, <c>SIGNALR_PROTOCOL.md</c> §3.2.16 item 1,
/// <c>BOSS_RULES.md</c> §7).
///
/// <code>
/// "pet"    the active Pet's Passive    (PASSIVE_RULES.md §7)
/// "boss"   the Boss's Passive          (BOSS_RULES.md §3, §7)
/// </code>
///
/// <b>They are constants, not an enum, because the wire member is a string.</b>
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.16 item 1 fixes the value as the string
/// <c>"pet"</c> or <c>"boss"</c>, and the convention matches
/// <c>DamageDealt</c>/<c>DamageTaken</c>'s party identifiers (§3.2.14 item 1).
/// Naming the spellings once here keeps the two emitters — the Pet Passive stage
/// and the Boss Passive stage — from each writing their own literal. This type
/// defines no third value: <c>BOSS_RULES.md</c> §7 states the set is exactly
/// these two.
/// </summary>
public static class PassiveEventSource
{
    /// <summary>The active Pet's Passive (<c>PASSIVE_RULES.md</c> §7).</summary>
    public const string Pet = "pet";

    /// <summary>The Boss's Passive (<c>BOSS_RULES.md</c> §3, §7).</summary>
    public const string Boss = "boss";
}

/// <summary>
/// The outcome of charging a Passive over a run of Matches: the progress the run
/// settled at, and the <c>PassiveCharged</c>/<c>PassiveTriggered</c> reports it
/// produced, in Match order (<c>PASSIVE_RULES.md</c> §2, §5, §7;
/// <c>GAME_EVENTS.md</c> §1, §2).
///
/// <code>
/// PassiveChargeResult
/// ├── Progress            the settled PassiveProgress  (GAME_STATE.md §2.3)
/// ├── Charges[]           one per Match, in Match order
/// └── Triggers[]          zero or one per Cascade, for every Reset Behavior
/// </code>
///
/// <b>The events and the settled progress are two views of one resolution.</b>
/// <c>GAME_EVENTS.md</c> §3 item 6 states that events are not state: the charge
/// reports describe what happened, and <see cref="Progress"/> is the value the
/// caller writes back to <c>PetState.PassiveProgress</c>. Both come from the
/// same walk of the Matches, so neither is a re-derivation of the other.
///
/// <b>The two lists are separately ordered and are not interleaved by index.</b>
/// This tracker is Domain-pure and does not own event-stream assembly, so it does
/// not merge its reports into one sequence: <c>GAME_EVENTS.md</c> §1.1 places
/// <c>PassiveCharged</c> per Match and <c>PassiveTriggered</c> only when the
/// threshold is crossed, and the Application-layer stage that assembles the
/// ordered event list is a separate task. Each list preserves the order the
/// Cascade produced it in, and because the trigger is evaluated once after the
/// batch, it is the Cascade's last Passive event.
/// </summary>
/// <param name="Progress">
/// The Passive's progress after the whole Cascade, including the reset the
/// Cascade may have performed (<c>PASSIVE_RULES.md</c> §4, §5). This is the
/// value to write back to <c>PetState.PassiveProgress</c>.
/// </param>
/// <param name="Charges">
/// One <c>PassiveCharged</c> per Match processed, in the order the Matches were
/// supplied (<c>PASSIVE_RULES.md</c> §2 item 1, §5). These are informational
/// progress reports (§6 item 1); they carry no trigger of their own.
/// </param>
/// <param name="Triggers">
/// <b>Zero or one</b> <c>PassiveTriggered</c> — §2 item 3 and §5 make the
/// Threshold evaluation a single act after the Cascade's whole Match batch, and
/// the Passive triggers "at most once per Cascade" whatever its Reset Behavior
/// (§2 item 4).
/// </param>
public readonly record struct PassiveChargeResult(
    PassiveProgress Progress,
    IReadOnlyList<PassiveChargedEvent> Charges,
    IReadOnlyList<PassiveTriggeredEvent> Triggers);
