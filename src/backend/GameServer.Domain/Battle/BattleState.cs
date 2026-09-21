using GameServer.Domain.Match3;
using GameServer.Domain.Passives;

namespace GameServer.Domain.Battle;

/// <summary>
/// Active Battle State through the Pet / Passive stage — the authoritative battle
/// state at the Board Foundation stage (<c>GAME_STATE.md</c> §2.0.5) plus the
/// <c>PlayerState</c> fields of §2.2 and the <c>PetState</c> fields of §2.3.
///
/// It is the Battle State Foundation (§2.0) <b>plus</b> the fields §2 already
/// defines for the Match-3 board, the randomness that generates it, the commit
/// record the staleness check reads, the player's Match/Combo progression, and the
/// active Pet's Passive — nothing else is added, and nothing already in §2.0
/// changes (§0 item 5):
///
/// <code>
/// BattleState
/// ├── BattleId
/// ├── Turn          = 0
/// ├── Sequence      = 0
/// ├── RngSeed                        (§2.6)
/// ├── RngState                       (§2.6)
/// ├── BoardState                     (§2.1)
/// │   └── Cells[64]
/// ├── PlayerState                    (§2.2)
/// │   ├── HP         = 1000
/// │   ├── MaxHP      = 1000
/// │   ├── ATK        = 50
/// │   ├── DEF        = 25
/// │   ├── Power      = 0
/// │   ├── Crit       = 5
/// │   ├── MatchCount = 0
/// │   └── Combo      = 0
/// ├── PetState                       (§2.3)
/// │   ├── PassiveId
/// │   ├── PassiveProgress           (Threshold, Current = 0)
/// │   └── PassiveResetOverride?     (absent when Default)
/// ── LastCommittedSwapPair         (§2.1.10; absent until the first
///                                    committed Swap)
/// </code>
///
/// Each field has the same name, meaning, and rules as its §2 counterpart; each
/// stage is where those §2 fields first come into existence (§2.0.5). The
/// remaining §2 fields — the rest of <c>PlayerState</c> (§2.2:
/// <c>StatusEffects</c>, <c>EquippedRelics</c>, <c>EquippedCards</c>), the rest
/// of <c>PetState</c> (§2.3: <c>PetId</c>/Identity, <c>Element</c>,
/// <c>Tier</c>/<c>Star</c>/<c>Level</c>), and <c>BossState</c> (§2.4) — are still
/// absent and still owned by later stages (§2.0.5.3). Their absence is a staging
/// position, not a scope reduction of §2: a field absent from a stage is <b>not
/// yet implemented</b>, not <b>not required</b> (§0 item 4).
///
/// <see cref="PlayerState"/> and <see cref="PetState"/> are the documented owners
/// of the progression values they carry (§2.2, §2.3), which is why they appear
/// here as nested fields rather than as flat members of this record: §0 item 5
/// forbids a stage from introducing a parallel representation of a concept
/// another stage owns, and a flat <c>BattleState.MatchCount</c> or
/// <c>BattleState.PassiveProgress</c> would be a second owner.
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
/// <param name="PlayerState">
/// The player's battle progression and combat state (<c>GAME_STATE.md</c> §2.2) —
/// the documented owner of <c>MatchCount</c>, <c>Combo</c>, and the combat stats
/// <c>HP</c>/<c>MaxHP</c>, <c>ATK</c>/<c>DEF</c>/<c>Crit</c>, and <c>Power</c>. It
/// exists from battle creation with every value at its documented starting point
/// (<see cref="PlayerState.Initial"/>): the progression values at <c>0</c> and the
/// combat stats at their <c>COMBAT_RULES.md</c> §1.1 MVP defaults, at full health
/// with no Power yet generated. It is never <c>null</c> and is never created
/// lazily on the first Swap.
///
/// It is written in the same single post-resolution write-back as
/// <see cref="Turn"/> and <see cref="Sequence"/> (§5.1), for a <b>committed</b>
/// Swap only: a rejected Swap writes nothing, so both values keep what they held
/// (<c>MATCH3_RULES.md</c> §2.1.5 item 5). No intermediate value of either field
/// is ever written or published (§5.1 item 2): the state only ever carries a
/// finished resolution's result.
/// </param>
/// <param name="PetState">
/// The active Pet's state (<c>GAME_STATE.md</c> §2.3) — the documented owner of
/// the Passive identity, its progress, and its declared Reset Behavior
/// (<c>PASSIVE_RULES.md</c> §1, §2, §4).
///
/// It is <b>not optional, not nullable, and never lazily initialized</b>: §2.3
/// item 3 makes <c>PassiveId</c> present from battle creation ("a battle always
/// has its one active Pet and therefore its one Passive"), so there is no "no
/// Passive yet" state for an absent value to spell — unlike
/// <see cref="LastCommittedSwapPair"/>, whose absence is documented (§2.1.10
/// item 3). It is therefore a required field of this record, and a caller must
/// supply the active Pet's Passive identity and Threshold rather than letting one
/// be defaulted with an invented value.
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
/// UI-facing value (<c>SIGNALR_PROTOCOL.md</c> §4 item 13, §4.3). It adds no
/// message, method, or subscription.
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
    int Turn,
    int Sequence,
    ulong RngSeed,
    RngState RngState,
    BoardState BoardState,
    PlayerState PlayerState,
    PetState PetState,
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
    /// The <c>PetState</c> a battle begins with when the caller supplies only the
    /// two values the Passive's rule needs — its identity and its Threshold — and
    /// declares no non-default Reset Behavior.
    ///
    /// <b>It is not a default for <see cref="PetState"/>.</b> The identity and the
    /// Threshold are supplied by the caller, exactly as
    /// <see cref="PetState.AtBattleCreation"/> requires, and the Reset Behavior is
    /// <see cref="PassiveResetBehavior.Default"/> — which is not an invented
    /// value but the documented reading of an absent override: §2.3 makes the
    /// field "only present if this Pet's Passive uses non-default reset behavior",
    /// and <c>PASSIVE_RULES.md</c> §4 item 1 defines the default as "progress
    /// resets to <c>0</c> immediately after the Passive triggers". It is also the
    /// behavior of all five MVP Pet Passives (§8). A Passive that declares a
    /// non-default behavior is never silent about it: §4 item 3 requires it to be
    /// documented on the Passive's definition, and a caller declaring one supplies
    /// a <see cref="PetState"/> with the override set.
    /// </summary>
    /// <param name="passiveId">
    /// The active Pet's Passive identity (<c>GAME_STATE.md</c> §2.3) — the same
    /// value <c>GAME_EVENTS.md</c> §2's <c>PassiveCharged</c>/<c>PassiveTriggered</c>
    /// report.
    /// </param>
    /// <param name="passiveThreshold">
    /// The Passive's Threshold — "e.g. 'every 5 Matches'"
    /// (<c>PASSIVE_RULES.md</c> §1). It is the Passive definition's own value, and
    /// no Threshold is invented here.
    /// </param>
    public static PetState DefaultPassive(PassiveId passiveId, int passiveThreshold) =>
        PetState.AtBattleCreation(passiveId, passiveThreshold);

    /// <summary>
    /// Creates the authoritative state for a newly created battle session: the
    /// documented initial values, including the player's progression state and the
    /// active Pet's Passive state, plus the generated board and the retained RNG
    /// state (<c>GAME_STATE.md</c> §2.0.5, §2.2, §2.3, §2.7.1).
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
    /// (§2.3 item 3): the battle's one active Pet carries its one Passive from
    /// creation. Its progress starts at the Passive's own Threshold with
    /// <c>Current = 0</c>, and its Reset Behavior is the declared one or the
    /// default (§4 item 1) — generation does not charge it, reset it, or evaluate
    /// its Threshold, because nothing has been resolved
    /// (<c>PASSIVE_RULES.md</c> §2 item 1 counts Matches).
    ///
    /// Authoritative state is server-produced (<c>GAME_RULES.md</c> §18,
    /// <c>ADR-001</c>); this factory is the only place a battle begins.
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="rngSeed">
    /// The server-chosen seed (<c>GAME_STATE.md</c> §2.6.1). It must originate
    /// server-side; this type neither generates nor influences it.
    /// </param>
    /// <param name="petState">
    /// The active Pet's state (<c>GAME_STATE.md</c> §2.3) — required, because
    /// <c>PassiveId</c> is present from battle creation (§2.3 item 3) and no
    /// value may be invented for it. Pet selection and progression are not
    /// implemented, so the caller supplies the battle's Passive configuration;
    /// see <see cref="PetState.AtBattleCreation"/> for the documented initial
    /// progress, or <see cref="Create(string, ulong, PassiveId, int, PassiveResetBehavior?)"/>
    /// for the identity/threshold form.
    /// </param>
    /// <exception cref="BoardGenerationFailedException">
    /// No candidate board satisfied the documented initial-board constraints
    /// within the documented 64-attempt bound
    /// (<c>MATCH3_RULES.md</c> §1.5 item 3): the battle creation is rejected as an
    /// error.
    /// </exception>
    public static BattleState Create(string battleId, ulong rngSeed, PetState petState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        // Generation continues the stream from the seed and returns the board
        // together with the state after all consumption the accepted attempt
        // used (MATCH3_RULES.md §1.2.1, GAME_STATE.md §2.7.1).
        var generation = BoardGenerator.Generate(rngSeed);

        return new BattleState(
            battleId,
            InitialTurn,
            InitialSequence,
            rngSeed,
            generation.RngState,
            generation.Board!,
            // §2.2: PlayerState exists from creation with MatchCount = 0 and
            // Combo = 0 — no null, no sentinel, and no lazy creation on the
            // first Swap — and with the combat stats at their COMBAT_RULES.md
            // §1.1 MVP defaults at full health.
            PlayerState.Initial,
            // §2.3 item 3: PetState likewise exists from creation — "it is present
            // from battle creation" — with progress at the start of its first
            // charge. It is not optional, not defaulted, and not created lazily
            // on the first Swap.
            petState);
    }

    /// <summary>
    /// Creates a battle whose active Pet carries an arbitrary Passive whose
    /// identity and Threshold are test fixtures rather than product values.
    ///
    /// <b>Test-only.</b> It exists because a battle cannot be created without a
    /// <c>PetState</c> (<c>GAME_STATE.md</c> §2.3 item 3: "it is present from
    /// battle creation") and the suites for the earlier stages — board, Special
    /// Gems, Swap execution, Match/Combo accounting — need a battle to exercise
    /// their own contract. The Passive they carry is irrelevant to what those
    /// suites assert, so this helper supplies one fixture value in one place
    /// instead of repeating it at every creation site.
    ///
    /// It reaches production assemblies because the test project has
    /// <c>InternalsVisibleTo</c> and not the reverse;
    /// <see cref="BattleStateService.CreateBattle(string, BattleStateService.PassiveConfiguration)"/>
    /// remains the real creation path and takes the battle's actual Passive
    /// configuration. It is not a product default and no production caller uses
    /// it — it exists only so the earlier stages' tests stay readable.
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="rngSeed">The seed, so a test's board is deterministic.</param>
    public static BattleState CreateWith(string battleId, ulong rngSeed) =>
        Create(battleId, rngSeed, DefaultPassive(new PassiveId("fixture-passive"), 5));

    /// <summary>
    /// Creates the authoritative state for a newly created battle session from the
    /// battle's Passive configuration — the required identity and Threshold, plus
    /// the optional non-default Reset Behavior
    /// (<c>GAME_STATE.md</c> §2.3, <c>PASSIVE_RULES.md</c> §1, §4).
    ///
    /// This is the same creation as <see cref="Create(string, ulong, PetState)"/>
    /// with the documented starting progress applied: the Threshold is the
    /// Passive's own value and <c>Current</c> begins at <c>0</c>
    /// (§2.3 item 3, <c>SIGNALR_PROTOCOL.md</c> §4.3 item 4). PetState is never
    /// absent, never defaulted, and never created lazily.
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="rngSeed">The server-chosen seed (<c>GAME_STATE.md</c> §2.6.1).</param>
    /// <param name="passiveId">
    /// The active Pet's Passive identity (<c>GAME_STATE.md</c> §2.3) — set at
    /// battle creation and never changed (§2.3 item 2).
    /// </param>
    /// <param name="passiveThreshold">
    /// The Passive's Threshold — "e.g. 'every 5 Matches'" (<c>PASSIVE_RULES.md</c>
    /// §1), the value the progress pair is measured against.
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
        PassiveId passiveId,
        int passiveThreshold,
        PassiveResetBehavior? passiveResetOverride = null) =>
        Create(
            battleId,
            rngSeed,
            PetState.AtBattleCreation(passiveId, passiveThreshold, passiveResetOverride));
}