using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;

namespace GameServer.Domain.Battle;

/// <summary>
/// Active Battle State through the Boss stage — the authoritative battle state
/// at the Board Foundation stage (<c>GAME_STATE.md</c> §2.0.5) plus the
/// Battle Identity member of §2.8, the
/// Match/Combo accounting fields of §2.2 and the <c>PetState</c> fields of §2.3,
/// and the <c>BossState</c> fields of §2.4.
///
/// It is the Battle State Foundation (§2.0) <b>plus</b> the fields §2 already
/// defines for the battle's owner identity, the Match-3 board, the randomness
/// that generates it, the commit
/// record the staleness check reads, the battle's Match/Combo accounting, the
/// active Pet's combat stats, Element, and Passive, and the battle's one Boss —
/// nothing else is added, and nothing already in §2.0 changes (§0 item 5):
///
/// <code>
/// BattleState
/// ├── BattleId
/// ├── PlayerId                       (§2.8 — owner identity only; not a wire
/// │                                  member)
/// ├── Turn          = 0
/// ├── Sequence      = 0
/// ├── RngSeed                        (§2.6)
/// ├── RngState                       (§2.6)
/// ├── BoardState                     (§2.1)
/// │   └── Cells[64]
/// ├── Combo          = 0             (§2.2 — Match/Combo accounting at the
/// ├── MatchCount     = 0             │   root; there is no PlayerState node)
/// ├── PetState                       (§2.3)
/// │   ├── PetId                     (the owned Pet instance —
/// │   │                              Pet.PetInstanceId)
/// │   ├── HP         = 1000
/// │   ├── MaxHP      = 1000
/// │   ├── ATK        = 50
/// │   ├── DEF        = 25
/// │   ├── Crit       = 5
/// │   ├── Power      = 0
/// │   ├── Element
/// │   ├── PassiveId
/// │   ├── PassiveProgress           (Threshold, Current = 0)
/// │   └── PassiveResetOverride?     (absent when Default)
/// ├── BossState                      (§2.4)
/// │   ├── BossId
/// │   ├── Element
/// │   ├── HP / MaxHP / ATK / DEF
/// │   └── State                     (Idle / Charging / Enraged / Stunned)
/// └── LastCommittedSwapPair?         (§2.1.10; absent until the first
///                                    committed Swap)
/// </code>
///
/// Each field has the same name, meaning, and rules as its §2 counterpart; each
/// stage is where those §2 fields first come into existence (§2.0.5). The
/// remaining §2 fields — <c>PetState</c>'s <c>StatusEffects</c> and its
/// <c>Tier</c>/<c>Star</c>/<c>Level</c>, and
/// <c>BossState</c>'s <c>StatusEffects[]</c> — are still
/// absent and still owned by later stages (§2.0.5.3). Their absence is a staging
/// position, not a scope reduction of §2: a field absent from a stage is <b>not
/// yet implemented</b>, not <b>not required</b> (§0 item 4).
///
/// <b><c>PlayerId</c> is the battle's owner identity only.</b> It is the
/// identity of the Player who created the battle (<c>Player.PlayerId</c>,
/// <c>DATABASE.md</c> §1), recorded from the authenticated battle-start request
/// (<c>API_CONTRACTS.md</c> §1, §3) at battle creation and carried unchanged for
/// the battle's lifetime (§2.8 items 1–2, <c>ADR-014</c> decision 1). It carries
/// no stats, no resource pool, and no gameplay value of any kind, and it is not a
/// lifecycle field — §2.0.3's no-<c>Status</c> rule is unaffected. It exists so
/// the battle-end persistence path can source <c>BattleResult.PlayerId</c> from
/// authoritative state without re-deriving identity from a session or from client
/// input (§2.8 item 4). It is <b>not a wire member</b>: §2.8 item 3 excludes it
/// from every <c>BattleStateUpdated</c> stage projection, every Battle Event, and
/// the <c>GetBattleState</c> snapshot (<c>SIGNALR_PROTOCOL.md</c> §4 item 4,
/// §7.1) — state added is not wire exposure added.
///
/// <b>There is no <c>PlayerState</c> member.</b> §2 says so explicitly: the
/// Player is the account/owner and has no authoritative battle-time combat pool.
/// Match/Combo accounting lives at this root (§2.2 — a flat
/// <c>BattleState.MatchCount</c> is the documented owner, not a second
/// representation of a nested one), and the combat stats and the battle loadout
/// live under <see cref="PetState"/> (§2.3, <c>ADR-011</c>).
///
/// The wire member name <c>playerState</c> used by <c>SIGNALR_PROTOCOL.md</c>
/// §4.2 is a <b>fixed protocol label</b> for the Combo/MatchCount projection and
/// does not reintroduce a state path of that name (§2.2, §2.2.1, <c>ADR-011</c>
/// item 6). This type carries no such label either; the projection site supplies
/// it (<c>GameServer.Api.Hubs.BattleHub</c>).
///
/// <see cref="PetState"/> and <see cref="BossState"/>
/// are the documented owners of the values they carry (§2.3, §2.4), which
/// is why they appear
/// here as nested fields rather than as flat members of this record: §0 item 5
/// forbids a stage from introducing a parallel representation of a concept
/// another stage owns, and a flat <c>BattleState.PassiveProgress</c> or
/// <c>BattleState.BossHP</c> would be a second owner. <c>Combo</c> and
/// <c>MatchCount</c> are flat for the opposite reason: §2.2 makes this root
/// their only owner, so there is no nested node for them to duplicate.
///
/// <c>BoardState</c> carries Special Gem state inside its <c>Cells[64]</c>
/// entries (<c>GAME_STATE.md</c> §2.1.1, §2.1.4). No field was added to this
/// type for it: the former <c>BoardState.PendingSpecialGems[]</c> placeholder
/// does not exist in any form (§2.1.2 item 1), and Special Gem state travels
/// with the board because it travels with the cells.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR,
/// EF Core, Redis, HTTP, Phaser, or Discord concern.
///
/// It contains no <c>Status</c> field and no lifecycle enum
/// (<c>GAME_STATE.md</c> §2.0.3, §2.0.5): a battle has no lifecycle state
/// machine, and battle outcome is expressed as the
/// <c>BattleWon</c>/<c>BattleLost</c> events (<c>GAME_EVENTS.md</c> §2), not as
/// state. Extending the state to the board stage adds no lifecycle value either,
/// and neither does this stage.
/// </summary>
/// <param name="BattleId">
/// Identity of the battle session (<c>GAME_STATE.md</c> §2.0.1, §2.0.5.1). It
/// scopes the client's SignalR group membership (<c>SIGNALR_PROTOCOL.md</c>
/// §1.2) and carries no gameplay content — it selects no Pet, Boss, or loadout.
/// </param>
/// <param name="PlayerId">
/// The identity of the Player who created this battle (<c>GAME_STATE.md</c> §2.8)
/// — the account/owner identity (<c>Player.PlayerId</c>, <c>DATABASE.md</c> §1),
/// recorded from the authenticated battle-start request
/// (<c>API_CONTRACTS.md</c> §1, §3) at battle creation and carried unchanged in
/// this record for the battle's lifetime (<c>ADR-014</c> decision 1).
///
/// <b>Identity only.</b> It carries no stats, no resource pool, and no gameplay
/// value (§2.8 item 1): the Player is the account/owner with no battle-time
/// combat pool (<c>ADR-011</c>), and this member exists so the battle-end
/// persistence path can source <c>BattleResult.PlayerId</c> (<c>DATABASE.md</c>
/// §1) from authoritative state rather than re-deriving it from a session or
/// from client input (§2.8 item 4, <c>GAME_RULES.md</c> §18, <c>AGENTS.md</c>
/// §10).
///
/// It is <b>not</b> a lifecycle value: a battle has no lifecycle state machine
/// and no <c>Status</c> field (<c>GAME_STATE.md</c> §2.0.3), and this member
/// changes nothing about that.
///
/// It is <b>not a wire member</b> (§2.8 item 3): it is delivered by no
/// <c>BattleStateUpdated</c> stage projection, no Battle Event, and no
/// <c>GetBattleState</c> snapshot — the client never receives it.
///
/// It is <b>not optional, not nullable, and never lazily initialized</b>: it is
/// recorded at creation, so a caller supplies the authenticated requesting
/// Player's identity rather than letting one be defaulted with an invented
/// value.
/// </param>
/// <param name="Turn">
/// Current Turn number (<c>GAME_STATE.md</c> §2.0.1, §2.0.5.1, §2,
/// <c>GAME_RULES.md</c> §2). Still <c>0</c> at this stage: board generation is
/// not a player Swap/Action and starts no Turn (§2.0.5.2 item 1). This type
/// defines no Turn increment rule — §2.0.2 leaves that to the task that
/// implements action resolution.
/// </param>
/// <param name="Sequence">
/// Monotonic resolution counter (<c>GAME_STATE.md</c> §2.0.1, §2.0.5.1, §2, §5).
/// Still <c>0</c> at this stage: board generation is not an action resolution and
/// increments no counter (§2.0.5.2 item 1). <c>0</c> is the only valid value
/// until the first resolution succeeds. Not to be confused with
/// <c>GameServer.Application.Runtime.RuntimeStatus.Sequence</c>, which is a
/// technical connection counter and not battle state.
/// </param>
/// <param name="RngSeed">
/// The battle's server-chosen PRNG seed (<c>GAME_STATE.md</c> §2.6.1). It records
/// the battle's origin point and is never rewritten after creation
/// (§2.6.1 item 3). It is an unsigned 64-bit value produced by the server's own
/// entropy at battle creation and is never supplied, influenced, or derived from
/// client input (§2.6.1 items 1–2, <c>TDD.md</c> §6 item 3).
/// </param>
/// <param name="RngState">
/// The PRNG state after generating the initial board (<c>GAME_STATE.md</c>
/// §2.0.5.1, §2.6.2, §2.7.1 step 6). It is the state <b>pair</b> — internal state
/// plus stream selector — not a single word (§2.6.2 item 1). It is retained, not
/// reset and not re-derived from the final board, so recovering a battle from a
/// snapshot restores the stream exactly (§2.6.2 item 4).
/// </param>
/// <param name="BoardState">
/// The authoritative board (<c>GAME_STATE.md</c> §2.0.5.1, §2.1). At this stage
/// its <c>Cells[64]</c> hold the initial board generated per §2.7. It is
/// server-authored and the client never generates, fills, or repairs it
/// (§2.1.1 item 4).
/// </param>
/// <param name="Combo">
/// The Combo of the most recently <b>committed</b> Swap
/// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §5, <c>MATCH3_RULES.md</c>
/// §6) — a <b>root</b> member of this record, not a field of a nested container
/// (§2.2, <c>ADR-011</c> item 2).
///
/// Its rule-level value starts at <see cref="InitialCombo"/> and reads <c>0</c>
/// only before the battle's first committed Swap
/// (<c>MATCH3_RULES.md</c> §6.5 item 4). A committed Swap resets it to <c>0</c>
/// before its first Match is counted and then increments it by exactly <b>1 per
/// Match</b>, in the detection order of <c>MATCH3_RULES.md</c> §3.2 and §4.2,
/// across every pass of the cascade loop (§6.2, §6.3). It is therefore equal to
/// the number of Matches that Swap produced, and never <c>0</c> for a committed
/// Swap (§6.5 item 3).
///
/// It is <b>not</b> turn-cumulative: it does not carry across a Turn boundary,
/// and it does not accumulate over a battle (§6.1 item 4). Its next reset is the
/// next committed Swap — nothing else resets it, and the terminating pass that
/// detects no Match is not a Match and does not reset it (§6.4). A rejected Swap
/// neither resets nor changes it (§6.1 item 3, §6.5 item 2), Special Gem
/// activation, chaining, and creation never change it (§6.3.1), and no cleared
/// cell and no cascade depth is ever converted into it (§6.2 item 2).
///
/// It is written in the same single post-resolution write-back as
/// <see cref="Turn"/> and <see cref="Sequence"/> (§5.1), for a <b>committed</b>
/// Swap only: a rejected Swap writes nothing, so the value keeps what it held
/// (<c>MATCH3_RULES.md</c> §2.1.5 item 5). No intermediate value is ever written
/// or published (§5.1 item 2): the state only ever carries a finished
/// resolution's result.
///
/// It is <b>delivered</b> to the client under the fixed wire label
/// <c>playerState</c> (<c>SIGNALR_PROTOCOL.md</c> §4.2) — the label is a
/// protocol member name, not an ownership path (§2.2 item 1, §2.2.1).
/// </param>
/// <param name="MatchCount">
/// The cumulative number of Matches this battle has produced
/// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §3) — a <b>root</b> member of
/// this record, on the same terms as <see cref="Combo"/>.
///
/// Its value starts at <see cref="InitialMatchCount"/> and is
/// <b>battle-cumulative</b>: it increments by exactly <b>1 per Match</b> and
/// never resets — not per Turn, not per Swap, and not per Cascade. Matches
/// produced by cascades count, and several Matches produced by one Swap each
/// count separately, because each distinct detected shape is one Match however
/// many cells it spans (<c>MATCH3_RULES.md</c> §3 item 5, §6.2 item 3).
///
/// It counts <b>Matches only</b>: a Special Gem activation, chain, or the N
/// cells one clears are not Matches and never increment it
/// (<c>MATCH3_RULES.md</c> §5.5.5 item 8, §6.3.1), and it is not a cleared-cell
/// total. It is independent of <c>Turn</c> and of <c>Sequence</c>: one committed
/// Swap advances <c>Turn</c> and <c>Sequence</c> by exactly 1 each whatever its
/// Match count is (<c>MATCH3_RULES.md</c> §8.1, §8.2). A rejected Swap does not
/// change it (§2.1.5 item 5).
///
/// Neither member is nullable and neither is omitted when it is <c>0</c>:
/// <c>Combo = 0</c> and <c>MatchCount = 0</c> are real publishable values at
/// battle creation, not an absence convention (§2.2.1 item 1).
/// </param>
/// <param name="PetState">
/// The active Pet's state (<c>GAME_STATE.md</c> §2.3) — the documented owner of
/// the battle's <b>Pet identity</b> (<c>PetId</c>, the owned Pet instance —
/// <c>Pet.PetInstanceId</c>, <c>DATABASE.md</c> §1), the <b>combat stats</b>
/// (<c>HP</c>/<c>MaxHP</c>,
/// <c>ATK</c>/<c>DEF</c>/<c>Crit</c>, <c>Power</c>), the Element, and the Passive
/// identity, its progress, and its declared Reset Behavior
/// (<c>COMBAT_RULES.md</c> §1.1, <c>PET_RULES.md</c> §1,
/// <c>PASSIVE_RULES.md</c> §1, §2, §4; <c>ADR-011</c> item 3).
///
/// It is <b>not optional, not nullable, and never lazily initialized</b>: §2.3
/// item 3 makes <c>PassiveId</c> present from battle creation ("a battle always
/// has its one active Pet and therefore its one Passive"), so there is no "no
/// Passive yet" state for an absent value to spell — unlike
/// <see cref="LastCommittedSwapPair"/>, whose absence is documented (§2.1.10
/// item 3). It is therefore a required field of this record, and a caller must
/// supply the active Pet's combat stats, Element, Passive identity, and Threshold
/// rather than letting one be defaulted with an invented value.
///
/// Its <c>PassiveProgress</c> starts at the Passive's own Threshold with
/// <c>Current = 0</c> (§2.3 item 3, <c>SIGNALR_PROTOCOL.md</c> §4.3 item 4) and is
/// written after each committed Swap's resolution, in the same single
/// post-resolution write-back as <see cref="Turn"/> and
/// <see cref="Sequence"/> (§5.1). A compromised rejection writes nothing, so a
/// rejected Swap leaves the progress exactly where it was
/// (<c>MATCH3_RULES.md</c> §2.1.5 item 5) — in particular it charges no Passive
/// (<c>PASSIVE_RULES.md</c> §2 item 1 counts Matches, and a rejected Swap produces
/// none).
///
/// It is <b>delivered</b> to the client through the existing
/// <c>BattleStateUpdated</c> push as the §4.3 <c>petState</c> object — unlike
/// <see cref="LastCommittedSwapPair"/>, and because
/// <c>PASSIVE_RULES.md</c> §6 item 1 requires the progress to be exposed as a
/// UI-facing value (<c>SIGNALR_PROTOCOL.md</c> §4 item 13, §4.3). That payload
/// carries the Passive trio only; the combat stats are state and are not wire
/// members (§4.3 item 2). It adds no message, method, or subscription.
/// </param>
/// <param name="BossState">
/// The battle's one Boss (<c>GAME_STATE.md</c> §2.4,
/// <c>GAME_RULES.md</c> §1.1) — the documented owner of the Boss's identity,
/// Element, <c>HP</c>/<c>MaxHP</c>, <c>ATK</c>, <c>DEF</c>, and <c>State</c>
/// (<c>BOSS_RULES.md</c> §1, §6.1).
///
/// It is <b>not optional, not nullable, and never lazily initialized</b>: a
/// battle has exactly one Boss (<c>GAME_RULES.md</c> §1.1), and
/// <c>GAME_EVENTS.md</c> §2 makes <c>BattleStarted</c> "a gameplay event and
/// requires a created battle with a Pet and a Boss". There is therefore no
/// "no Boss yet" state for an absent value to spell — unlike
/// <see cref="LastCommittedSwapPair"/>, whose absence is documented (§2.1.10
/// item 3). It is a required field of this record, and a caller must supply the
/// Boss's real definition (<see cref="Bosses.BossDefinition.ToInitialState"/>)
/// rather than letting one be defaulted with an invented Element or
/// <c>MaxHP</c>.
/// </param>
/// <param name="LastCommittedSwapPair">
/// The unordered pair most recently committed to the board
/// (<c>GAME_STATE.md</c> §2.1.10) — the authoritative record
/// <c>MATCH3_RULES.md</c> §2.1.4's already-applied check reads, stored
/// canonically as <c>(min, max)</c> so a request's argument order cannot make
/// two spellings of one pair compare unequal (§2.1.10 item 2).
///
/// It is <c>null</c> until the first Swap is committed: absence is the
/// documented representation of "no Swap has been committed", and no default
/// or sentinel pair stands in for it (§2.1.10 item 3). A newly created battle
/// therefore has none — board generation commits no Swap and is not an action
/// resolution (§2.0.5.2 item 1) — which is also why a request can never be
/// rejected as already applied against a battle that has not played a Swap.
///
/// It is written only when a Swap is committed, in the same single
/// post-resolution write-back as <see cref="Turn"/> and
/// <see cref="Sequence"/> (§2.1.10 item 5, §5.1 item 7); a rejected action —
/// including <c>STALE_ACTION</c> — leaves it unchanged and never clears it
/// (§2.1.10 items 6–7, <c>MATCH3_RULES.md</c> §2.1.5).
///
/// It is <b>not</b> delivered to the client and is never carried by a Swap
/// request: it is server-side bookkeeping inside authoritative state
/// (<c>SIGNALR_PROTOCOL.md</c> §4 item 12, <c>MATCH3_RULES.md</c> §2.1.1
/// item 3, §2.1.10 item 8). It is not a version and is never used for
/// concurrency control (§5 item 3, §2.1.10 item 11).
/// </param>
public sealed record BattleState(
    string BattleId,
    PlayerId PlayerId,
    int Turn,
    int Sequence,
    ulong RngSeed,
    RngState RngState,
    BoardState BoardState,
    int Combo,
    int MatchCount,
    PetState PetState,
    BossState BossState,
    CommittedSwapPair? LastCommittedSwapPair = null)
{
    /// <summary>
    /// Initial <c>Turn</c> for a battle with no resolved action
    /// (<c>GAME_STATE.md</c> §2.0.2, §2.0.5.2).
    /// </summary>
    public const int InitialTurn = 0;

    /// <summary>
    /// Initial <c>Sequence</c> for a battle with no resolved action
    /// (<c>GAME_STATE.md</c> §2.0.2, §2.0.5.2, §5).
    /// </summary>
    public const int InitialSequence = 0;

    /// <summary>
    /// The initial <c>MatchCount</c> for a battle that has produced no Match
    /// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §3).
    /// </summary>
    public const int InitialMatchCount = 0;

    /// <summary>
    /// The initial <c>Combo</c> for a battle whose first Swap has not been
    /// committed (<c>GAME_STATE.md</c> §2.2, <c>MATCH3_RULES.md</c> §6.1 item 1).
    ///
    /// <c>0</c> is a real, publishable value here — it is the value the state
    /// reads before the first committed Swap — not an "absent" convention and
    /// not an omitted field (<c>MATCH3_RULES.md</c> §6.5 item 4,
    /// <c>GAME_STATE.md</c> §2.1.7 item 5).
    /// </summary>
    public const int InitialCombo = 0;

    /// <summary>
    /// Creates the authoritative state for a newly created battle session: the
    /// documented initial values, including the battle's Match/Combo accounting,
    /// the active Pet's state, and the battle's one Boss, plus the generated board
    /// and the retained RNG state (<c>GAME_STATE.md</c> §2.0.5, §2.2, §2.3, §2.4,
    /// §2.7.1).
    ///
    /// Board generation is not a resolution, so <c>Turn</c> and <c>Sequence</c>
    /// remain <c>0</c> (§2.0.5.2 item 1), and <c>MatchCount</c> and <c>Combo</c>
    /// stay at the documented <c>0</c>: generation produces no Match, commits no
    /// Swap, and is not a Swap/Action (<c>MATCH3_RULES.md</c> §8.1 item 4).
    /// No Battle Event is emitted by board generation (§2.0.5.2 item 2) and
    /// nothing within this type emits one.
    ///
    /// <c>LastCommittedSwapPair</c> is likewise absent: generation commits no
    /// Swap (§2.1.10 item 3, §2.0.5.2 item 1), so the returned state carries
    /// none and no pair is invented to stand for the absence.
    ///
    /// <c>PetState</c>, by contrast, <b>is</b> created here and is never absent
    /// (§2.3 item 3): the battle's one active Pet carries its combat stats at the
    /// <c>COMBAT_RULES.md</c> §1.1 MVP defaults, at full health with no Power yet
    /// generated, together with its one Element and its
    /// one Passive from creation. Its progress starts at the Passive's own
    /// Threshold with
    /// <c>Current = 0</c>, and its Reset Behavior is the declared one or the
    /// default (§4 item 1) — generation does not charge it, reset it, or evaluate
    /// its Threshold, because nothing has been resolved
    /// (<c>PASSIVE_RULES.md</c> §2 item 1 counts Matches).
    ///
    /// <c>BossState</c> is likewise <b>created here</b> and is never absent
    /// (§2.4): a battle has exactly one Boss (<c>GAME_RULES.md</c> §1.1) and
    /// <c>BattleStarted</c> requires one (<c>GAME_EVENTS.md</c> §2). It begins at
    /// the Boss definition's stats at full health and in the documented Initial
    /// State, and creation changes none of them: generation applies no damage,
    /// fires no Boss Passive or Skill, and transitions no State
    /// (<c>BOSS_RULES.md</c> §3–§5).
    ///
    /// Authoritative state is server-produced (<c>GAME_RULES.md</c> §18,
    /// <c>ADR-001</c>); this factory is the only place a battle begins.
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="rngSeed">
    /// The server-chosen seed (<c>GAME_STATE.md</c> §2.6.1). It must originate
    /// server-side; this type neither generates nor influences it.
    /// </param>
    /// <param name="playerId">
    /// The identity of the Player who created this battle
    /// (<c>GAME_STATE.md</c> §2.8) — required, because a battle's owner identity
    /// is recorded at creation and is never re-derived afterward (§2.8 items 2
    /// and 4). The value comes from the authenticated battle-start context
    /// (<c>API_CONTRACTS.md</c> §1, §3), never from client-supplied state
    /// (<c>GAME_RULES.md</c> §18, <c>AGENTS.md</c> §10). It is the Pet's owner,
    /// already established by the ownership check at battle start
    /// (<c>API_CONTRACTS.md</c> §3).
    /// </param>
    /// <param name="petState">
    /// The active Pet's state (<c>GAME_STATE.md</c> §2.3) — required, because its
    /// <c>PetId</c> (the owned Pet instance), combat stats, <c>Element</c>, and
    /// <c>PassiveId</c> are present from battle
    /// creation (§2.3 item 3) and no
    /// value may be invented for them. Pet selection and progression are not
    /// implemented, so the caller supplies the battle's Pet configuration;
    /// see <see cref="PetState.AtBattleCreation"/> for the documented initial
    /// progress and combat stats, or
    /// <see cref="Create(string, ulong, PlayerId, PetId, Element, PassiveId, int, BossDefinition, PassiveResetBehavior?)"/>
    /// for the Element/identity/threshold form.
    /// </param>
    /// <param name="bossState">
    /// The battle's one Boss (<c>GAME_STATE.md</c> §2.4) — required, because a
    /// battle has exactly one Boss (<c>GAME_RULES.md</c> §1.1) and no value may be
    /// invented for its Element, stats, or identity. Boss selection is not
    /// implemented, so the caller supplies the Boss's configuration; see
    /// <see cref="BossDefinition.ToInitialState"/> for the documented initial
    /// state of an MVP Boss.
    /// </param>
    /// <exception cref="BoardGenerationFailedException">
    /// No candidate board satisfied the documented initial-board constraints
    /// within the documented 64-attempt bound
    /// (<c>MATCH3_RULES.md</c> §1.5 item 3): the battle creation is rejected as an
    /// error.
    /// </exception>
    public static BattleState Create(
        string battleId,
        ulong rngSeed,
        PlayerId playerId,
        PetState petState,
        BossState bossState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        // Generation continues the stream from the seed and returns the board
        // together with the state after all consumption the accepted attempt
        // used (MATCH3_RULES.md §1.2.1, GAME_STATE.md §2.7.1).
        var generation = BoardGenerator.Generate(rngSeed);

        return new BattleState(
            battleId,
            // §2.8 item 2: the owner identity is recorded AT creation, from the
            // caller's own authenticated context. It is never re-derived later and
            // never taken from client input (§2.8 item 4, GAME_RULES.md §18).
            playerId,
            InitialTurn,
            InitialSequence,
            rngSeed,
            generation.RngState,
            generation.Board!,
            // §2.2: MatchCount = 0 and Combo = 0 at creation — no null, no
            // sentinel, and no lazy creation on the first Swap.
            InitialCombo,
            InitialMatchCount,
            // §2.3 item 3: PetState likewise exists from creation — "it is present
            // from battle creation" — with progress at the start of its first
            // charge and its combat stats at their COMBAT_RULES.md §1.1 MVP
            // defaults at full health. It is not optional, not defaulted, and not
            // created lazily on the first Swap.
            petState,
            // §2.4: BossState exists from creation too: a battle has exactly one
            // Boss (GAME_RULES.md §1.1), which BattleStarted requires
            // (GAME_EVENTS.md §2). It is likewise not optional, not defaulted, and
            // not created lazily on the first Swap. Nothing here damages the Boss
            // or changes its State.
            bossState);
    }

    /// <summary>
    /// Creates a battle whose active Pet carries an arbitrary Passive whose
    /// identity and Threshold are test fixtures rather than product values.
    ///
    /// <b>Test-only.</b> It exists because a battle cannot be created without a
    /// <c>PetState</c> (<c>GAME_STATE.md</c> §2.3 item 3: "it is present from
    /// battle creation") and a <c>BossState</c> (§2.4, <c>GAME_RULES.md</c>
    /// §1.1) and the suites for the earlier stages — board, Special
    /// Gems, Swap execution, Match/Combo accounting — need a battle to exercise
    /// their own contract. The Pet and the Boss they carry are irrelevant to what
    /// those suites assert, so this helper supplies one fixture value each in one
    /// place instead of repeating them at every creation site.
    ///
    /// It reaches production assemblies because the test project has
    /// <c>InternalsVisibleTo</c> and not the reverse;
    /// <c>BattleStateService.CreateBattleAsync(string, BattleStateService.PetConfiguration, BossDefinition, CancellationToken)</c>
    /// remains the real creation path and takes the battle's actual Player, Pet,
    /// and Boss
    /// configuration. It is not a product default and no production caller uses
    /// it — it exists only so the earlier stages' tests stay readable.
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="rngSeed">The seed, so a test's board is deterministic.</param>
    public static BattleState CreateWith(string battleId, ulong rngSeed) =>
        Create(
            battleId,
            rngSeed,
            // GAME_STATE.md §2.8: the owner identity is a fixture here, exactly as
            // the Pet and Boss below are. A test that asserts something else needs a
            // battle to exist, and the owner identity is irrelevant to what those
            // suites assert — so it is supplied once, here, rather than repeated at
            // every creation site.
            new PlayerId("fixture-player"),
            PetState.AtBattleCreation(
                // GAME_STATE.md §2.3: PetState carries the owned Pet instance
                // identity, so the fixture Pet needs one too — like the Element and
                // Passive below, it is a value no suite at this level asserts.
                new PetId("fixture-pet"),
                Element.Hoa,
                new PassiveId("fixture-passive"),
                5),
            // GAME_STATE.md §2.4.1–§2.4.3: the fixture Boss carries a Passive identity
            // and a Threshold too, because BossState's own field set requires them.
            // They are fixture values for tests that assert something else, exactly
            // as the fixture Passive above is.
            BossState.Initial(
                new BossId("fixture-boss"),
                Element.Kim,
                maxHp: 5000,
                atk: 100,
                def: 50,
                passiveId: new PassiveId("fixture-boss-passive"),
                passiveThreshold: 5));

    /// <summary>
    /// Creates the authoritative state for a newly created battle session from the
    /// battle's owner and Pet configuration — the owning Player's identity, the
    /// selected owned Pet instance, the required Element, Passive identity, and
    /// Threshold, plus
    /// the optional non-default Reset Behavior
    /// (<c>GAME_STATE.md</c> §2.3, §2.8, <c>PASSIVE_RULES.md</c> §1, §4) — against the
    /// Boss definition the battle is fought against (<c>GAME_STATE.md</c> §2.4,
    /// <c>BOSS_RULES.md</c> §6.1).
    ///
    /// This is the same creation as
    /// <see cref="Create(string, ulong, PlayerId, PetState, BossState)"/>
    /// with the documented starting progress and combat stats applied: the
    /// Threshold is the
    /// Passive's own value and <c>Current</c> begins at <c>0</c>
    /// (§2.3 item 3, <c>SIGNALR_PROTOCOL.md</c> §4.3 item 4), and the Boss begins
    /// at its definition's stats at full health in the documented Initial State
    /// (<see cref="BossDefinition.ToInitialState"/>). Neither <c>PetState</c> nor
    /// <c>BossState</c> is
    /// absent, defaulted, or created lazily.
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="rngSeed">The server-chosen seed (<c>GAME_STATE.md</c> §2.6.1).</param>
    /// <param name="playerId">
    /// The identity of the Player who created this battle
    /// (<c>GAME_STATE.md</c> §2.8) — the authenticated requesting Player, never a
    /// client-supplied value (<c>AGENTS.md</c> §10).
    /// </param>
    /// <param name="petId">
    /// The identity of the owned Pet instance the battle selected
    /// (<c>GAME_STATE.md</c> §2.3) — the resolved
    /// <c>Pet.PetInstanceId</c>, never a definition id.
    /// </param>
    /// <param name="element">
    /// The active Pet's one Element (<c>GAME_STATE.md</c> §2.3,
    /// <c>ELEMENT_RULES.md</c> §6) — set at battle creation and never changed
    /// (<c>PET_RULES.md</c> §2 item 3).
    /// </param>
    /// <param name="passiveId">
    /// The active Pet's Passive identity (<c>GAME_STATE.md</c> §2.3) — set at
    /// battle creation and never changed (§2.3 item 2).
    /// </param>
    /// <param name="passiveThreshold">
    /// The Passive's Threshold — "e.g. 'every 5 Matches'" (<c>PASSIVE_RULES.md</c>
    /// §1), the value the progress pair is measured against.
    /// </param>
    /// <param name="bossDefinition">
    /// The definition of the Boss this battle is fought against
    /// (<c>BOSS_RULES.md</c> §6, <see cref="BossDefinitions"/> for the MVP set).
    /// Its identity, Element, and stats are the Boss's own values and none is
    /// invented here.
    /// </param>
    /// <param name="passiveResetOverride">
    /// The Passive's declared non-default Reset Behavior, or <c>null</c> for the
    /// default (§4 items 1–3). A non-default behavior must be declared on the
    /// Pet's Passive definition; it is never assumed (§4 item 3).
    /// </param>
    /// <exception cref="BoardGenerationFailedException">
    /// No candidate board satisfied the documented initial-board constraints
    /// within the documented 64-attempt bound (<c>MATCH3_RULES.md</c> §1.5
    /// item 3).
    /// </exception>
    public static BattleState Create(
        string battleId,
        ulong rngSeed,
        PlayerId playerId,
        PetId petId,
        Element element,
        PassiveId passiveId,
        int passiveThreshold,
        BossDefinition bossDefinition,
        PassiveResetBehavior? passiveResetOverride = null) =>
        Create(
            battleId,
            rngSeed,
            playerId,
            PetState.AtBattleCreation(
                petId,
                element,
                passiveId,
                passiveThreshold,
                passiveResetOverride),
            // §2.4 / BOSS_RULES.md §6.1: the Boss begins at its definition's stats
            // at full health, in the documented Initial State. The definition owns
            // every value; this factory chooses none of them.
            bossDefinition.ToInitialState());
}
