using System.Collections.Concurrent;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Combat;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;

namespace GameServer.Application.Battle;

/// <summary>
/// Application-layer boundary for the Board Foundation State lifecycle
/// (<c>GAME_STATE.md</c> §2.0.5) and for one Swap resolution
/// (<c>MATCH3_RULES.md</c> §2.1.6).
///
/// Responsibility: coordinate the documented staging flow —
///
/// <code>
/// Battle initialization
///         ↓
/// RNG initialization        (server-chosen seed, GAME_STATE.md §2.6.1)
///         ↓
/// Board generation          (MATCH3_RULES.md §1.2.1, §1.5)
///         ↓
/// Battle State creation     (GAME_STATE.md §2.0.5, §2.7.1)
/// </code>
///
/// — and the documented Swap resolution flow —
///
/// <code>
/// SwapRequest
///         ↓
/// Validation                (MATCH3_RULES.md §2.1.2, via Domain)
///         ↓
/// Exchange + resolution     (MATCH3_RULES.md §2.1.6, §4, via Domain)
///         ↓
/// Charge Passive            (GAME_RULES.md §17 step 10, PASSIVE_RULES.md §2)
///         ↓
/// Damage Pipeline           (GAME_RULES.md §17 steps 15–17, COMBAT_RULES.md §3)
///         ↓
/// Boss HP update            (COMBAT_RULES.md §3 step 6, via Domain)
///         ↓
/// One write-back            (GAME_STATE.md §5.1)
/// </code>
///
/// — and load, resolve, and store the authoritative state of a battle session
/// through the active-state store, so the realtime boundary can deliver it when
/// a client joins that battle's group (<c>SIGNALR_PROTOCOL.md</c> §1.2, §4.1) or
/// when a Swap resolves. It performs sequencing and coordination only — no game
/// rule logic (<c>ARCHITECTURE.md</c> §2.1). Board generation itself, the RNG,
/// the initial-board constraints, swap validation, the whole board-resolution
/// pipeline, the Passive charge/threshold/reset rules, and the Damage
/// Pipeline's formula and Boss HP write all live in Domain; this service only
/// orders the calls, supplies each engine the values the other produced, and
/// stores the result.
///
/// <b>The authoritative record is Redis's.</b> <c>REDIS_STATE.md</c> §2 item 2
/// makes the stored record the single source of truth for a battle's live state
/// and states that "the server process holds no long-lived in-memory copy across
/// requests (<c>ARCHITECTURE.md</c> §4 — <c>BattleResolutionService</c> loads,
/// uses, and saves it within one resolution)". This service holds none: it
/// creates the record (<c>§3</c> "Created"), loads it at the start of a
/// resolution (<c>§4</c> item 1), and saves it once at the end under the
/// <c>Sequence</c> compare-and-set (<c>§4</c> items 2, 5). Every read below is a
/// read of that record, so no value here survives a request.
///
/// It must never:
/// <list type="bullet">
/// <item>implement Match-3, Passive, combat, or any domain rule — including the
/// Damage Pipeline's formula (<c>COMBAT_RULES.md</c> §3), which it calls rather
/// than reimplements,</item>
/// <item>compute an authoritative gameplay value (<c>GAME_RULES.md</c> §18,
/// <c>ADR-001</c>) — including Passive progress, which is
/// <see cref="PassiveTracker"/>'s result and is written back unchanged,</item>
/// <item>hold the authoritative state across requests, or keep an in-process
/// copy of it as a fallback. The store is the record of truth
/// (<c>REDIS_STATE.md</c> §2 item 2, §7 item 5; <c>ADR-005</c> rejected process
/// memory), so a store failure is raised rather than absorbed, and nothing is
/// served from a local cache,</item>
/// <item>persist anything the contract does not define: it writes the one
/// documented active-state record and nothing else — no partial state
/// (<c>REDIS_STATE.md</c> §7 items 1–2), no second key, and nothing to
/// PostgreSQL (<c>GAME_STATE.md</c> §2.0.4 item 3, <c>DATABASE.md</c> §5
/// item 3),</item>
/// <item>emit or deliver Battle Events — board generation emits none
/// (<c>GAME_STATE.md</c> §2.0.5.2 item 2), and the Swap boundary hands back the
/// events Domain and the Passive stage produced without building, altering, or
/// delivering any of them. <c>ReceiveEvents</c> is the protocol's event path
/// (<c>SIGNALR_PROTOCOL.md</c> §3) and is not implemented here,</item>
/// <item>contain SignalR, Hub, client concerns, or Redis concerns
/// (<c>ARCHITECTURE.md</c> §2.1). It reaches the store through
/// <see cref="IBattleStateRepository"/>, which names no key, TTL, connection, or
/// Redis type.</item>
/// </list>
/// </summary>
public sealed class BattleStateService
{
    /// <summary>
    /// The Pet configuration a battle's active Pet carries — the identity of the
    /// owned Pet instance the battle selected (<c>GAME_STATE.md</c> §2.3), its
    /// Element
    /// (<c>GAME_STATE.md</c> §2.3, <c>ELEMENT_RULES.md</c> §6) and the Passive it
    /// carries (§2.3, <c>PASSIVE_RULES.md</c> §1).
    ///
    /// Pet selection and progression are not implemented (<c>GAME_STATE.md</c>
    /// §2.3, <c>SIGNALR_PROTOCOL.md</c> §4.3 item 2), so the battle's Pet is
    /// supplied by the caller rather than resolved from a Pet definition. It is
    /// exactly the values <see cref="PetState"/> holds — the owned Pet instance
    /// identity, the Element, the Passive
    /// identity, the Threshold, and the optional non-default Reset Behavior — and
    /// it introduces no further member: the Threshold is part of the documented
    /// progress pair (<c>PASSIVE_RULES.md</c> §6 item 1) and is data-driven
    /// configuration, never a value this layer invents (<c>ARCHITECTURE.md</c> §5
    /// item 1).
    /// </summary>
    /// <param name="PetId">
    /// The identity of the owned Pet instance the battle selected
    /// (<c>GAME_STATE.md</c> §2.3) — the <c>Pet.PetInstanceId</c> the battle-start
    /// path resolved from the Player's owned collection
    /// (<c>API_CONTRACTS.md</c> §3). It is the instance, never a
    /// <c>PetDefinitionId</c>: the Pet instance's definition supplies the Element
    /// and Passive below. It is <b>not</b> optional: <c>PetState</c> is present
    /// from battle creation (§2.3 item 3) and carries this identity from that
    /// moment, and no value may be invented for it (this is the documented
    /// <c>ADR-014</c> decision 4 member, not a second one).
    /// </param>
    /// <param name="Element">
    /// The active Pet's one Element (<c>GAME_STATE.md</c> §2.3,
    /// <c>ELEMENT_RULES.md</c> §6). It is set at battle creation and never changes
    /// (<c>PET_RULES.md</c> §2 item 3).
    /// </param>
    /// <param name="PassiveId">
    /// The active Pet's Passive identity (<c>GAME_STATE.md</c> §2.3). It is set at
    /// battle creation and never changes (§2.3 item 2).
    /// </param>
    /// <param name="PassiveThreshold">
    /// The Passive's Threshold — "e.g. 'every 5 Matches'" (<c>PASSIVE_RULES.md</c>
    /// §1). Progress begins at <c>0</c> against it
    /// (<c>SIGNALR_PROTOCOL.md</c> §4.3 item 4).
    /// </param>
    /// <param name="PassiveResetOverride">
    /// The Passive's declared non-default Reset Behavior, or <c>null</c> for the
    /// default (<c>PASSIVE_RULES.md</c> §4 items 1–3). It must be declared on the
    /// Pet's Passive definition; it is never assumed (§4 item 3).
    /// </param>
    /// <param name="EquippedRelics">
    /// The battle-scoped Relic loadout snapshot — 3–5 owned Relic instance
    /// identities in equip-slot order (<c>RELIC_RULES.md</c> §2.2, §2.3, §2.5;
    /// <c>GAME_STATE.md</c> §2.3). It is produced by the Relic loadout validator
    /// (<c>GameServer.Application.Relics.RelicLoadoutService</c>) and is carried
    /// into <c>PetState</c> unchanged, in the order given.
    ///
    /// It is <c>null</c> until the Relic loadout stage supplies it: Relic
    /// selection is not implemented end-to-end and no value may be invented for
    /// it, so the caller supplies it rather than this boundary defaulting one.
    /// A <c>null</c> snapshot is the documented staging position (§0 item 4:
    /// not yet implemented, not not-required) — it is not an empty loadout,
    /// because the rules define no zero-Relic battle
    /// (<c>RELIC_RULES.md</c> §2.1 item 1).
    /// </param>
    /// <param name="EquippedCards">
    /// The battle-scoped Card loadout snapshot — exactly 4
    /// <see cref="EquippedCardIdentity"/> values: the 3 submitted Basic Cards
    /// plus the active Pet's derived Signature Skill Card
    /// (<c>CARD_RULES.md</c> §1; <c>API_CONTRACTS.md</c> §3;
    /// <c>GAME_STATE.md</c> §2.3). It is produced by the Card loadout validator
    /// (<c>GameServer.Application.Cards.CardLoadoutService</c>), which also
    /// derives the Signature Skill, and is carried into <c>PetState</c>
    /// unchanged and in the order given.
    ///
    /// It is <c>null</c> until the Card loadout stage supplies it, on the same
    /// documented staging basis as <paramref name="EquippedRelics"/>: a
    /// <c>null</c> snapshot is "not yet implemented", never an invented empty
    /// loadout — <c>CARD_RULES.md</c> §1 defines no zero-Card battle.
    /// </param>
    public readonly record struct PetConfiguration(
        PetId PetId,
        Element Element,
        PassiveId PassiveId,
        int PassiveThreshold,
        PassiveResetBehavior? PassiveResetOverride = null,
        EquippedRelicIdentity[]? EquippedRelics = null,
        EquippedCardIdentity[]? EquippedCards = null)
    {
        /// <summary>
        /// The <c>PetState</c> this configuration initializes a battle's
        /// <c>BattleState</c> with — the selected owned Pet instance identity
        /// (<c>GAME_STATE.md</c> §2.3), progress at the Passive's start
        /// (§2.3 item 3), the Pet's Element, and both
        /// battle-scoped loadout snapshots: the Relic instances in submitted
        /// order (<c>RELIC_RULES.md</c> §2.5) and the 4 Cards the Card loadout
        /// validator produced (<c>CARD_RULES.md</c> §1).
        /// </summary>
        public PetState ToPetState() =>
            PetState.AtBattleCreation(
                PetId,
                Element,
                PassiveId,
                PassiveThreshold,
                PassiveResetOverride,
                EquippedRelics,
                EquippedCards);
    }

    /// <summary>
    /// The Boss definition a battle is fought against (<c>GAME_STATE.md</c> §2.4,
    /// <c>BOSS_RULES.md</c> §6).
    ///
    /// It is the documented <see cref="BossDefinition"/> itself — identity,
    /// Element, and base stats — rather than a second copy of it: the definition
    /// already is the configuration this layer needs, and restating its members
    /// here would be the duplicate representation <c>GAME_STATE.md</c> §0 item 5
    /// forbids. The MVP definitions are <see cref="BossDefinitions"/>.
    /// </summary>
    private readonly record struct BossConfiguration(BossDefinition Definition)
    {
        /// <summary>
        /// The <c>BossState</c> this configuration initializes a battle's
        /// <c>BattleState</c> with — the definition's stats at full health, in the
        /// documented Initial State, with its Passive identity and the start of its
        /// Passive progress, and with no Skill charge or cooldown
        /// (<c>GAME_STATE.md</c> §2.4, §2.4.1–§2.4.3; <c>BOSS_RULES.md</c> §6.1).
        /// </summary>
        public BossState ToBossState() => Definition.ToInitialState();
    }

    /// <summary>
    /// The active battle state store (<c>REDIS_STATE.md</c> §1–§4). It is the
    /// documented record of truth for a battle's live state, reached through the
    /// Application-layer contract so nothing above Infrastructure names Redis
    /// (<c>ARCHITECTURE.md</c> §2.1 item 3).
    ///
    /// This service keeps no state of its own: the authoritative record lives in
    /// the store, and every operation below reads it and writes it back within
    /// one resolution (<c>REDIS_STATE.md</c> §2 item 2, §4 items 1 and 5).
    /// </summary>
    private readonly IBattleStateRepository _repository;

    private readonly IRngSeedSource _seedSource;

    /// <summary>
    /// The Active Pet's configuration for each battle, attached at creation
    /// (<c>GAME_STATE.md</c> §2.3 item 2: <c>PassiveId</c> "is set at battle
    /// creation and never changes"). It is the battle's loadout input, not battle
    /// state: <c>PetState</c> is the authoritative record and is what the
    /// resolution reads and writes (<c>REDIS_STATE.md</c> §2 item 2), so this
    /// registry is not a second copy of any value it holds.
    ///
    /// <b>Why it is retained.</b> The record holds the state; it does not hold
    /// the battle's loadout <i>input</i>. A resolution needs the Pet's declared
    /// Reset Behavior as configuration, and <c>REDIS_STATE.md</c> §1 fixes the
    /// key set at one state key with §2 item 1 forbidding a Redis-only field for
    /// it — so this is the caller-supplied configuration the boundary is given
    /// at creation, exactly as the Boss definition below is. It is not the
    /// authoritative battle state and is never the source of one: every value
    /// the resolution acts on is read from the stored record.
    /// </summary>
    private readonly ConcurrentDictionary<string, PetConfiguration> _petConfiguration = new(StringComparer.Ordinal);

    /// <summary>
    /// The Boss definition for each battle, attached at creation
    /// (<c>BOSS_RULES.md</c> §6.4: the identities and the Skill/Passive timing are
    /// the Boss's <b>definition</b>, not its state). It is the battle's content
    /// input, not battle state: <c>BossState</c> in <see cref="BattleState"/> owns
    /// the current values, and this registry is not a second copy of them.
    ///
    /// <b>Why the resolution needs it.</b> The Boss Response steps read
    /// configuration that <c>BossState</c> deliberately does not carry — the
    /// Passive's Threshold and Reset Behavior, the Skill's identity, base damage,
    /// charge requirement, and cooldown length, and the Enrage threshold
    /// (<c>BOSS_RULES.md</c> §6.2–§6.4). Those are static content, so storing them
    /// on the mutable state would be the duplicate representation
    /// <c>GAME_STATE.md</c> §0 item 5 forbids. This is the same split, and the same
    /// pattern, as <see cref="_petConfiguration"/>.
    /// </summary>
    private readonly ConcurrentDictionary<string, BossConfiguration> _bossConfiguration = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the battle-state boundary over the active-state store and the
    /// server's seed source.
    ///
    /// <b>Both are required, and the store has no default.</b>
    /// <c>REDIS_STATE.md</c> §2 item 2 makes the stored record the single source
    /// of truth for a battle's live state and §7 item 5 states that nothing
    /// permits that state to live in process memory, so there is deliberately no
    /// parameterless construction and no in-process substitute: composing this
    /// service without a store must fail at startup rather than silently resolve
    /// battles whose state is never persisted (<c>ADR-005</c>).
    /// </summary>
    /// <param name="repository">
    /// The active battle state store (<c>REDIS_STATE.md</c> §1–§4).
    /// </param>
    /// <param name="seedSource">
    /// The server-side entropy source for <c>RngSeed</c>
    /// (<c>GAME_STATE.md</c> §2.6.1). It is never supplied or influenced by the
    /// client.
    /// </param>
    public BattleStateService(IBattleStateRepository repository, IRngSeedSource seedSource)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
    }

    /// <summary>
    /// Creates the authoritative state for a new battle session
    /// (<c>GAME_STATE.md</c> §2, §2.7.1) and persists it as the battle's
    /// active-state record (<c>REDIS_STATE.md</c> §3 "Created:
    /// on <c>POST /api/battle/start</c>").
    ///
    /// The server chooses the battle's seed from its own entropy (§2.6.1), then
    /// the board is generated deterministically from that seed
    /// (<c>MATCH3_RULES.md</c> §1.2.1), and the accepted board plus the retained
    /// resulting RNG state become the battle state (§2.7.1 steps 5–6). The
    /// battle's Match/Combo accounting, the active Pet's combat stats, Element,
    /// Passive, and loadouts, and the battle's one Boss are created
    /// with it at their documented starting values (§2.2, §2.3 item 3, §2.4).
    ///
    /// <b>Creation ends with the record written.</b> A battle the caller is told
    /// about is a battle whose state is stored — the write is not deferred to the
    /// first resolution, so there is no window in which a battle exists only in
    /// process memory (<c>REDIS_STATE.md</c> §2 item 2, §7 item 5). A store
    /// failure therefore fails the creation rather than yielding a battle whose
    /// state exists nowhere.
    ///
    /// Generation is initialization, not resolution: it consumes no Turn and no
    /// <c>Sequence</c>, both of which remain <c>0</c> (§2.0.5.2 item 1), and it
    /// emits no Battle Event (§2.0.5.2 item 2). Nothing here increments,
    /// re-rolls, or repairs anything, and nothing here charges the Passive: no
    /// Match has been produced (<c>PASSIVE_RULES.md</c> §2 item 1). The Boss
    /// likewise takes no damage and transitions no State
    /// (<c>BOSS_RULES.md</c> §3–§5).
    ///
    /// This is not a battle-creation endpoint or hub method. Battle creation
    /// remains <c>POST /api/battle/start</c> (<c>API_CONTRACTS.md</c> §3).
    /// <see cref="CreateBattleAsync"/> is the composition that endpoint calls; it
    /// is not itself a public creation contract, and the id it is given is
    /// server-authored (<c>§1</c>, <c>GAME_RULES.md</c> §18).
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="playerId">
    /// The identity of the Player who created this battle
    /// (<c>GAME_STATE.md</c> §2.8) — the authenticated requesting Player resolved
    /// from the battle-start context (<c>API_CONTRACTS.md</c> §1, §3). It is
    /// recorded into the created state's <c>BattleState.PlayerId</c> at creation
    /// and is never re-derived from a session or from client input afterward
    /// (§2.8 items 2 and 4, <c>GAME_RULES.md</c> §18, <c>AGENTS.md</c> §10). It is
    /// required: defaulting it would be an invented owner identity, and the
    /// battle-end persistence path sources <c>BattleResult.PlayerId</c> from this
    /// member (<c>DATABASE.md</c> §1).
    /// </param>
    /// <param name="petConfiguration">
    /// The active Pet's owned instance identity, Element, and Passive — its
    /// <c>Pet.PetInstanceId</c>, its Element, the Passive's identity,
    /// its Threshold, and its declared Reset
    /// Behavior. <c>PetState</c> is present from battle creation
    /// (<c>GAME_STATE.md</c> §2.3 item 3) and no value may be invented for it, so
    /// this is required: Pet selection is not implemented and the battle's Pet
    /// configuration is therefore supplied by the caller.
    /// </param>
    /// <param name="bossDefinition">
    /// The definition of the Boss this battle is fought against
    /// (<c>GAME_STATE.md</c> §2.4, <c>BOSS_RULES.md</c> §6.1). <c>BossState</c> is
    /// present from battle creation (§2.4) and no value may be invented for its
    /// identity, Element, or stats, so this is required: Boss selection is not
    /// implemented and the battle's Boss definition is therefore supplied by the
    /// caller — <see cref="BossDefinitions"/> holds the MVP set.
    /// </param>
    /// <param name="cancellationToken">Cancels the store write.</param>
    /// <exception cref="GameServer.Domain.Match3.BoardGenerationFailedException">
    /// No candidate board satisfied the documented initial-board constraints
    /// within the documented 64-attempt bound (<c>MATCH3_RULES.md</c> §1.5
    /// item 3). The battle creation is rejected as an error and no state is
    /// recorded; the seed is not changed and no fallback board is substituted.
    /// </exception>
    public async Task<BattleState> CreateBattleAsync(
        string battleId,
        PlayerId playerId,
        PetConfiguration petConfiguration,
        BossDefinition bossDefinition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        var seed = _seedSource.CreateSeed();

        // §2.3 / §2.4: the battle's PetState is built from the caller's Pet
        // configuration — carrying the selected owned Pet instance identity and
        // progress at the start of its first charge — and its
        // BossState from the Boss definition at full health in the documented
        // Initial State, carrying the Passive identity §2.4.2 sets at creation.
        // Neither is absent, neither is defaulted with an invented value, and
        // neither is created lazily on the first Swap.
        var bossConfiguration = new BossConfiguration(bossDefinition);

        // §2.8 item 2: the owner identity is recorded at creation from the
        // caller's authenticated context — the same Player the ownership check
        // already validated — and never re-derived afterward (§2.8 item 4).
        var state = BattleState.Create(
            battleId,
            seed,
            playerId,
            petConfiguration.ToPetState(),
            bossConfiguration.ToBossState());

        // REDIS_STATE.md §3 "Created: on POST /api/battle/start": the record is
        // written as part of creation, so a created battle has a stored state
        // from its first moment and never only in process memory (§7 item 5).
        //
        // The write precedes the loadout input registry below: if the store
        // refuses the write the creation fails and no battle exists — rather
        // than a battle whose state is recorded nowhere.
        await _repository.CreateAsync(state, cancellationToken).ConfigureAwait(false);

        // The battle's loadout and content inputs, attached at creation. These
        // are configuration, not state (see the field docs): the authoritative
        // record was written above and is what every later read returns.
        _petConfiguration[battleId] = petConfiguration;
        _bossConfiguration[battleId] = bossConfiguration;

        return state;
    }

    /// <summary>
    /// Returns the Pet configuration the battle's active Pet carries, or
    /// <c>null</c> when no session with that id exists.
    ///
    /// This is the battle's loadout input, attached at creation; the authoritative
    /// current progress is <c>BattleState.PetState</c>
    /// (<c>GAME_STATE.md</c> §2.3, §5.1) and is read from there.
    /// </summary>
    internal PetConfiguration? GetPetConfiguration(string battleId) =>
        _petConfiguration.TryGetValue(battleId, out var configuration) ? configuration : null;

    /// <summary>
    /// Returns the Boss definition the battle is fought against, or <c>null</c>
    /// when no session with that id exists.
    ///
    /// This is the battle's content input, attached at creation; the authoritative
    /// current Boss values are <c>BattleState.BossState</c>
    /// (<c>GAME_STATE.md</c> §2.4, §5.1) and are read from there.
    /// </summary>
    internal BossDefinition? GetBossDefinition(string battleId) =>
        _bossConfiguration.TryGetValue(battleId, out var configuration)
            ? configuration.Definition
            : null;

    /// <summary>
    /// Returns the authoritative state for a battle, or <c>null</c> when no
    /// record exists for that id.
    ///
    /// The value comes from the active-state store
    /// (<c>REDIS_STATE.md</c> §2 item 2) — there is no in-process copy to serve
    /// it from, so this reads the record the battle is persisted as. A
    /// <c>null</c> therefore means the battle is unknown <b>or</b> its record has
    /// expired from inactivity (§3), and the two are deliberately the same
    /// answer: neither is a battle this server holds state for.
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="cancellationToken">Cancels the store read.</param>
    public async Task<BattleState?> GetBattleAsync(
        string battleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        return await _repository.GetAsync(battleId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the state to deliver to a client that has just joined a battle's
    /// group, or <c>null</c> when the group names no known battle.
    ///
    /// This is the server-side half of the initial state push
    /// (<c>SIGNALR_PROTOCOL.md</c> §4, §4.1): joining the group is what triggers
    /// delivery, so the caller asks for the state rather than invoking a separate
    /// request. Board generation happens server-side before this push;
    /// <c>BattleStateUpdated</c> reports the result and never triggers generation
    /// (§4.1 item 4). No value is derived, adjusted, or recomputed here.
    ///
    /// The state is served from the store, so the push reports the battle's
    /// authoritative record rather than a process-local copy
    /// (<c>REDIS_STATE.md</c> §2 item 2, <c>ARCHITECTURE.md</c> §4 steps 1–3).
    /// A client that has not been sent anything yet receives the record at its
    /// current <c>Sequence</c> — including after earlier resolutions committed
    /// by another request.
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="cancellationToken">Cancels the store read.</param>
    public Task<BattleState?> GetInitialStateForGroupAsync(
        string battleId,
        CancellationToken cancellationToken = default) =>
        GetBattleAsync(battleId, cancellationToken);

    /// <summary>
    /// Resolves the board of a battle to stability and records the resulting
    /// authoritative state (<c>MATCH3_RULES.md</c> §4).
    ///
    /// This is the Application-layer boundary for one board resolution: it orders
    /// the documented calls and records the result, and it implements no game rule
    /// (<c>ARCHITECTURE.md</c> §2.1). The cascade loop, Match Detection, Special Gem
    /// creation and activation, Gravity, and Spawn all live in Domain
    /// (<c>ARCHITECTURE.md</c> §4.1).
    ///
    /// The whole resolution runs against the battle's retained <c>RngState</c>
    /// (<c>GAME_STATE.md</c> §2.6.2 item 2): Spawn is the only operation that advances
    /// it (<c>MATCH3_RULES.md</c> §4.5 item 4), and the resulting state is retained so
    /// a recovered battle resumes the same stream (ADR-008).
    ///
    /// <b>Scope.</b> This resolves the board only, from whatever board the state
    /// currently holds. It is not the Swap path: it validates no Swap, takes no
    /// <see cref="SwapRequest"/>, exchanges nothing, commits no pair, emits no
    /// Battle Event, and — because it is not a Swap — leaves <c>Turn</c> and
    /// <c>Sequence</c> unchanged (<c>MATCH3_RULES.md</c> §8.1 item 4, §8.2
    /// item 2). A committed Swap's exchange *and* resolution is
    /// <see cref="ExecuteSwap"/>, which is the documented
    /// <c>§2.1.6</c> sequence; this method remains the board-resolution entry
    /// point for a state that has no Swap behind it.
    /// </summary>
    /// <param name="battleId">The battle whose board is resolved.</param>
    /// <param name="swapOriginIndex">
    /// The swap origin for the first detection pass, or <c>null</c> when the board is
    /// not being resolved from a Swap (in which case every Match 4 / Match 5 is placed
    /// at its line centre — <c>MATCH3_RULES.md</c> §5.5.3 item 3).
    /// </param>
    /// <param name="cancellationToken">Cancels the store read and write.</param>
    /// <returns>
    /// The resulting authoritative state, or <c>null</c> when no record exists for
    /// that id.
    /// </returns>
    public async Task<BattleState?> ResolveBoardAsync(
        string battleId,
        int? swapOriginIndex = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        // §4 item 1: the resolution begins by reading the record. Nothing is
        // cached between resolutions, so this is the current authoritative state
        // and not a stale copy.
        if (await _repository.GetAsync(battleId, cancellationToken).ConfigureAwait(false) is not { } state)
        {
            return null;
        }

        // The battle's retained RngState is where the stream resumes
        // (GAME_STATE.md §2.6.2 item 2). Spawn advances it and by nothing else
        // (MATCH3_RULES.md §7.2 item 1).
        var rng = new Pcg32(state.RngState.State, state.RngState.Increment);

        var resolution = CascadeResolver.Resolve(state.BoardState, rng, swapOriginIndex);

        var resolved = state with
        {
            BoardState = resolution.Board,

            // §8.4: Spawn is the step that advances RngState; the resolution ends with
            // the state Spawn produced.
            RngState = resolution.RngState,
        };

        // Resolving the board is not an action resolution: it validates no Swap
        // and advances no Sequence (MATCH3_RULES.md §8.1 item 4, §8.2 item 2), so
        // the write is not a Sequence-gated commit and is not the documented
        // per-action write-back of REDIS_STATE.md §4 item 5. It stores the
        // resolved board with the state's own unchanged Sequence, through the
        // same compare-and-set the Swap path uses so a concurrent resolution
        // still cannot be overwritten by this one (§4 items 2–3).
        await TryStoreResolvedAsync(resolved, state.Sequence, cancellationToken).ConfigureAwait(false);

        return resolved;
    }

    /// <summary>
    /// Executes one requested Swap against a battle's authoritative state and
    /// records the resulting state (<c>MATCH3_RULES.md</c> §2.1.6, §8.3).
    ///
    /// This is the Application-layer boundary for one Swap resolution: it orders
    /// the documented calls and records the result, and it implements no game rule
    /// (<c>ARCHITECTURE.md</c> §2.1, §4.1). Validation, the exchange, Match
    /// Detection, Special Gem creation and activation, Gravity, Spawn, and the
    /// cascade loop all live in Domain — this method delegates to
    /// <see cref="SwapExecutor.Execute"/>, which sequences them, and stores what
    /// it returns. There is no second swap pipeline here.
    ///
    /// <b>A rejected request writes nothing.</b> The authoritative state is left
    /// exactly as it was — board, <c>Turn</c>, <c>Sequence</c>, <c>RngState</c>,
    /// <c>LastCommittedSwapPair</c>, and the player's <c>MatchCount</c> and
    /// <c>Combo</c> — and the caller receives the validator's
    /// reason (<c>MATCH3_RULES.md</c> §2.1.5). No partial swap is performed before
    /// the checks pass.
    ///
    /// <b>A committed request is one write-back.</b> The resolved state the
    /// executor returns already carries the stable board, the advanced
    /// <c>RngState</c>, the begun <c>Turn</c>, the incremented <c>Sequence</c>,
    /// the recorded committed pair, and the resolution's Match/Combo values
    /// together; this method extends that same value with the settled Passive
    /// progress and the Boss's post-damage <c>HP</c>, so the store is only ever
    /// handed a consistent post-resolution state (<c>GAME_STATE.md</c> §5.1).
    ///
    /// <b>The Damage Pipeline runs here, after the Passive charge.</b>
    /// <c>GAME_RULES.md</c> §17 places "Calculate Damage" / "Apply Element
    /// Modifier" / "Apply Final Damage" at steps 15–17, after "Charge Passive"
    /// (step 10) and before "Resolve Boss Response" (step 18, not implemented).
    /// This boundary calls <see cref="DamagePipeline.Calculate"/> with the values
    /// the earlier stages produced and writes its returned <c>BossState</c> back
    /// — the formula, the state transformation, and the HP clamp are Domain's
    /// (<c>COMBAT_RULES.md</c> §3); this boundary decides none of them.
    ///
    /// <b>Scope.</b> This resolves one Swap and nothing else, and the Match/Combo
    /// accounting it records is the Domain executor's (<c>GAME_STATE.md</c> §2.2,
    /// <c>MATCH3_RULES.md</c> §6) — this boundary computes no gameplay value. The
    /// Passive charge it records is the Domain tracker's
    /// (<c>GAME_STATE.md</c> §2.3, <c>PASSIVE_RULES.md</c> §2–§5) — this boundary
    /// computes no progress, evaluates no Threshold, and applies no reset. The
    /// damage it records is the Domain pipeline's — this boundary computes no
    /// Base Damage, modifier, mitigation, or Final Damage, performs no Crit roll,
    /// and applies no additional damage of its own. Victory/Defeat
    /// (<c>GAME_RULES.md</c> §17 step 19) and Boss Response (step 18) are
    /// deliberately absent: a Boss HP of <c>0</c> is recorded as state and is not
    /// read as an outcome here. The
    /// ordered Battle Events of the resolution travel on the returned result
    /// (<c>GAME_EVENTS.md</c> §1.1, <c>SwapExecutionResult.Events</c>), which
    /// this boundary assembles only by appending the Passive stage's and the
    /// Damage Pipeline's own reports to the executor's list in the documented
    /// order — it builds no event, and it reorders, filters, regroups, and drops
    /// none. No event is delivered from here —
    /// <c>ReceiveEvents</c> is the protocol's event path
    /// (<c>SIGNALR_PROTOCOL.md</c> §3) and remains unimplemented — and the
    /// resolved state is stored by the caller under the <c>Sequence</c>
    /// compare-and-set as the one write-back of the resolution
    /// (<c>REDIS_STATE.md</c> §4 items 2, 5). Nothing is written for a rejected
    /// action (§4 item 7).
    /// </summary>
    /// <param name="battleId">The battle the Swap applies to.</param>
    /// <param name="request">The two §1.0 cell indices the player is exchanging.</param>
    /// <param name="cancellationToken">Cancels the store reads and write.</param>
    /// <returns>
    /// The rejection or commit result, or <c>null</c> when no record exists for that
    /// id — consistent with <see cref="GetBattleAsync"/> and
    /// <see cref="ResolveBoardAsync"/>, which resolve nothing for an unknown battle
    /// rather than creating one.
    /// </returns>
    public async Task<SwapExecutionResult?> ExecuteSwapAsync(
        string battleId,
        SwapRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        // ===================================================================
        // REDIS_STATE.md §4 item 1: read the record, with its Sequence
        // ===================================================================
        // The resolution runs against the stored authoritative state and the
        // Sequence it was read at — the pre-resolution value §4 item 6 names as
        // the compare-and-set's expected token. Nothing is cached between
        // resolutions.
        if (await _repository.GetAsync(battleId, cancellationToken).ConfigureAwait(false) is not { } state)
        {
            return null;
        }

        // §4 item 6 / §2 item 3: the retry re-runs the resolution against fresh
        // state. The bound is a safety net for pathological contention, not part
        // of the contract's semantics — the documented behaviour is that a
        // mismatch aborts and retries against fresh state (§4 item 2), and each
        // attempt recomputes from the state it read.
        //
        // The resolution itself is deterministic (§4 item 6: "a retried
        // resolution re-runs the same deterministic computation and produces the
        // same result"), so a retry cannot produce a different outcome for an
        // unchanged board.
        const int MaxAttempts = 8;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var expectedSequence = state.Sequence;

            var committed = await ResolveSwapAsync(battleId, state, request, cancellationToken)
                .ConfigureAwait(false);

            // A rejected Swap writes nothing at all (§4 item 7, MATCH3_RULES.md
            // §2.1.5): no store call is made, so the record, its TTL, and its
            // Sequence are untouched, and the caller receives the validator's
            // reason.
            if (committed.IsRejected)
            {
                return committed;
            }

            // §4 items 2, 5: the one write-back of this resolution, guarded by
            // the Sequence read at the start of it.
            var written = await _repository
                .TryUpdateAsync(committed.State, expectedSequence, cancellationToken)
                .ConfigureAwait(false);

            if (written)
            {
                return committed;
            }

            // §4 items 2–3: the stored Sequence had moved on, so this resolution
            // was refused rather than allowed to overwrite a newer authoritative
            // state. The documented behaviour is to abort and retry against the
            // fresh state — re-read and resolve again.
            if (await _repository.GetAsync(battleId, cancellationToken).ConfigureAwait(false)
                is not { } fresh)
            {
                // The record disappeared between the read and the write (its TTL
                // elapsed, or the battle ended). There is no fresh state to retry
                // against, and no battle is invented for it.
                return null;
            }

            state = fresh;
        }

        // Contention did not settle within the bound. The authoritative record is
        // intact and holds the newest state (the refused attempts wrote nothing),
        // so nothing is corrupted — but this action was not committed.
        //
        // The rejection is produced by running the Domain validator against the
        // current record rather than being constructed here: MATCH3_RULES.md
        // §2.1.4's already-applied check is the rule an action that lost against
        // the committed record fails, and letting the validator decide the reason
        // keeps the rejection's ownership in Domain instead of this boundary
        // inventing one. If the validator reports the action still valid against
        // this state, the action is executed once more and stored — the write is
        // the loop's own purpose, and reporting an uncommitted resolution as
        // accepted would be a false acknowledgement (§3.1 item 1: the state is
        // committed before anything is published).
        var lastAttempt = await ResolveSwapAsync(battleId, state, request, cancellationToken)
            .ConfigureAwait(false);

        if (lastAttempt.IsRejected)
        {
            return lastAttempt;
        }

        return await _repository
            .TryUpdateAsync(lastAttempt.State, state.Sequence, cancellationToken)
            .ConfigureAwait(false)
            ? lastAttempt
            : null;
    }

    /// <summary>
    /// Runs one Swap resolution over a state the caller has already read from the
    /// store, and returns the result — <b>without writing anything</b>.
    ///
    /// <b>Why the write is not here.</b> The store write is the guarded one:
    /// <c>REDIS_STATE.md</c> §4 items 2–3 require the write to be refused when
    /// the stored <c>Sequence</c> has moved on, so the caller
    /// (<see cref="ExecuteSwapAsync"/>) owns the compare-and-set and can retry
    /// against fresh state. This method produces the finished post-resolution
    /// state and the ordered event list; it stores nothing, which is what keeps
    /// "one action is one write-back" (item 5) a property of the single call
    /// site rather than a convention this method has to remember.
    ///
    /// <b>A rejected request produces nothing to write.</b> The executor's
    /// rejection is returned as it is (<c>MATCH3_RULES.md</c> §2.1.5): the board
    /// pipeline is never reached and the Passive is not charged at all — so the
    /// progress, the event list, and every other value are the ones the state
    /// already held, and §4 item 7's "a rejected action writes nothing" holds
    /// without the write path even being entered.
    ///
    /// It orders the documented calls and records the result, and it implements
    /// no game rule (<c>ARCHITECTURE.md</c> §2.1). Validation, the exchange,
    /// Match Detection, Special Gem creation and activation, Gravity, Spawn, and
    /// the cascade loop all live in Domain — this method delegates to
    /// <see cref="SwapExecutor.Execute"/>, which sequences them. The Damage
    /// Pipeline's formula, the Passive's charge/threshold/reset rules, and the
    /// Enrage transition are likewise Domain's and are only ordered here.
    /// </summary>
    /// <param name="battleId">The battle the Swap applies to.</param>
    /// <param name="state">
    /// The authoritative state this resolution runs against — the record as
    /// read, with the <c>Sequence</c> the caller will compare-and-set on.
    /// </param>
    /// <param name="request">The two §1.0 cell indices the player is exchanging.</param>
    /// <param name="cancellationToken">Cancels the store reads this stage performs.</param>
    /// <returns>The rejection, or the committed result carrying its finished state.</returns>
    private async Task<SwapExecutionResult> ResolveSwapAsync(
        string battleId,
        BattleState state,
        SwapRequest request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var result = SwapExecutor.Execute(state, request);

        // §2.1.5: a rejected action writes nothing, so the returned result is the
        // executor's own rejection and no further stage runs. Only a commit
        // continues.
        if (result.IsRejected)
        {
            return result;
        }

        // ===================================================================
        // Step 3 (BOSS_RULES.md §6.3, MATCH3_RULES.md §8.1): SkillCooldown--
        // ===================================================================
        // §6.3: Cooldown "decrements by 1 at each Turn increment", and
        // MATCH3_RULES.md §8.1 makes one committed Swap begin exactly one Turn —
        // whose stored number SwapExecutor already advanced, above, in its own
        // write-back. The decrement therefore runs here, once per committed Swap,
        // AFTER SwapExecutor returns and BEFORE the rest of the resolution. It is
        // not a second Turn++: this boundary never writes Turn (see the class
        // docs), and the cooldown's decrement is tied to the Turn the executor
        // already began rather than to a second one.
        //
        // A rejected Swap returned above, so nothing here runs for one: a rejected
        // action is not a Turn and cools nothing down (MATCH3_RULES.md §2.1.5).
        var bossState = state.BossState;

        if (bossState.SkillCooldown > 0)
        {
            bossState = bossState with { SkillCooldown = bossState.SkillCooldown - 1 };
        }

        // §17 step 10 / PASSIVE_RULES.md §2, §4, §5: charge the active Pet's
        // Passive over the Matches this Cascade resolution produced. The tracker is
        // a pure Domain function — it reads no board and no RNG, and this call
        // decides no rule.
        //
        // The inputs are the values the two stages already own: the progress held
        // in the authoritative PetState (GAME_STATE.md §2.3, §5.1), the
        // resolution's own Match total (MATCH3_RULES.md §3 item 5, §5.5.5 item 8:
        // Matches only, so a Special Gem detonation charges nothing), the
        // Passive's identity, and its declared Reset Behavior. Nothing is
        // re-derived, and the Match count is never recomputed from the passes.
        var petState = state.PetState;

        var charged = PassiveTracker.Charge(
            petState.PassiveProgress,
            result.Resolution.TotalMatches,
            petState.PassiveId,
            petState.ResetBehavior);

        // §2.3 / GAME_EVENTS.md §2: the settled progress is written back into
        // PetState, in the same single post-resolution write-back the executor
        // already performed for the board, the counters, the committed pair, and
        // the Match/Combo values. The Passive's identity and its declared Reset
        // Behavior are carried across unchanged — §2.3 item 2 makes the identity
        // settable only at battle creation, and §4 makes the behavior a property of
        // the Passive's definition, not of a resolution.
        var resolved = result.State with
        {
            PetState = petState with { PassiveProgress = charged.Progress },
        };

        // GAME_EVENTS.md §1, §1.1 / GAME_RULES.md §17 step 10: the events of this
        // resolution, in the order the pipeline produced them. The executor's list
        // is the Match-3 part, which §1 places from MatchCreated through
        // ComboChanged; the tracker's reports follow it, one PassiveCharged per
        // Match in Match order and then, when the Threshold was crossed, the single
        // PassiveTriggered. Nothing is sorted, filtered, or rebuilt — the assembled
        // list is the stages' own output concatenated, and WithEvents carries every
        // other member of the result across unchanged.
        var events = new List<BattleEvent>(
            result.Events.Count
            + charged.Charges.Count
            + charged.Triggers.Count
            + 3   // Player→Boss damage
            + 8   // Boss Passive charges + trigger (bounded by the Match total) + Boss response
            + 1); // outcome

        events.AddRange(result.Events);
        events.AddRange(charged.Charges.Select(BattleEvent.ForPassiveCharged));
        events.AddRange(charged.Triggers.Select(BattleEvent.ForPassiveTriggered));

        // ===================================================================
        // Steps 6 (GAME_RULES.md §17 steps 15–17): Player → Boss Damage
        // ===================================================================
        // COMBAT_RULES.md §3: this boundary orders the call, supplies the values
        // the earlier stages already produced, and writes back what the Domain
        // pipeline returns — it decides no step of the formula and computes no
        // gameplay value (ARCHITECTURE.md §2.1).
        //
        // The inputs are read from the states the resolution is already committed
        // to, never re-derived:
        //   - step 1's ATK      — PetState.ATK (GAME_STATE.md §2.3, ADR-011 item 3),
        //                         the combat-stat home of the attacking Pet; the
        //                         executor carried it forward in `resolved`
        //   - step 1's pool     — result.Resources.BaseDamagePool, the transient
        //                         pool step 12 generated (GAME_STATE.md §3)
        //   - step 2's selector — BattleState.Combo (GAME_STATE.md §2.2), this
        //                         Swap's Match total, a root member
        //   - step 3's elements — PetState.Element (attacker) and
        //                         BossState.Element (defender), both set at battle
        //                         creation and never rewritten (ELEMENT_RULES.md §5)
        //   - step 5's DEF      — BossState.DEF (GAME_STATE.md §2.4)
        // The pipeline draws no RNG and performs no Crit roll, so no randomness is
        // introduced here (COMBAT_RULES.md §3.3, ADR-009).
        var playerDamage = DamagePipeline.Calculate(
            new DamagePipeline.DamageInputs(
                Attack: resolved.PetState.ATK,
                BaseDamagePool: result.Resources.BaseDamagePool,
                Combo: resolved.Combo,
                AttackerElement: resolved.PetState.Element,
                DefenderElement: bossState.Element,
                DefenderDefense: bossState.DEF,
                DefenderHp: bossState.HP,
                Source: DamageParty.Player,
                Target: DamageParty.Boss),
            ComboModifiers.Default,
            ElementModifiers.Default);

        // §17 step 17 / GAME_STATE.md §5.1: the Boss's HP write is part of the SAME
        // single post-resolution write-back as the board, the counters, the
        // committed pair, the Match/Combo values, the settled Passive progress, and
        // the cooldown decrement. The pipeline returned the post-damage HP, written
        // onto the Boss state here so the write-back is never split in two.
        bossState = bossState with { HP = playerDamage.TargetHp };

        // GAME_EVENTS.md §1/§2: the three Damage events follow the Passive stage's
        // reports, in the order §1 places them — DamageCalculated, DamageDealt,
        // DamageTaken. They are the pipeline's own values, appended unchanged.
        events.Add(BattleEvent.ForDamageCalculated(playerDamage.Calculation));
        events.Add(BattleEvent.ForDamageDealt(playerDamage.DamageDealt));
        events.Add(BattleEvent.ForDamageTaken(playerDamage.DamageTaken));

        // ===================================================================
        // Step 7: Enrage (BOSS_RULES.md §5 item 4, §6.1)
        // ===================================================================
        // "Enrage is a permanent state transition triggered when BossHP <
        // EnrageThreshold." The comparison is strict `<` per §5 item 4's wording,
        // and the threshold is the Boss's own configuration (§6.1's "1500 (30%)" of
        // MaxHP 5000).
        //
        // It is evaluated here — after Player→Boss damage, before the terminal check
        // and before the Boss Response — because §5 item 4 orders it exactly so:
        // "the state transition is applied whenever the HP condition holds,
        // including when Player damage has just reduced Boss HP to 0 (the terminal
        // check then ends the battle with no Boss Response)".
        //
        // No BossEnraged event exists (§5 item 4, GAME_EVENTS.md §2, BOSS_RULES.md
        // §7): the transition is state, inferable from the event sequence. Once
        // Enraged the Boss stays Enraged — §5 item 4 states "no timer, no duration
        // field" — so an already-Enraged Boss does not transition (or re-announce
        // anything) a second time.
        var bossDefinition = _bossConfiguration[battleId].Definition;

        if (bossState.State != BossStateKind.Enraged
            && bossState.HP < bossState.MaxHP * bossDefinition.EnrageThreshold)
        {
            bossState = bossState with { State = BossStateKind.Enraged };
        }

        // ===================================================================
        // Step 8: Boss HP terminal check (GAME_RULES.md §1.4, BOSS_RULES.md §7)
        // ===================================================================
        // §1.4: "a battle ends when either the Boss or Player reaches 0 HP". The
        // Boss is checked FIRST — ahead of the Boss Response — because a Boss the
        // player just killed cannot trigger its Passive, cast its Skill, or make a
        // Basic Attack (BOSS_RULES.md §5 item 4's ordering explicitly ends the
        // battle here "with no Boss Response"). Enrage has already been evaluated
        // above, so the state transition is not skipped on this path.
        if (bossState.HP == 0)
        {
            // SIGNALR_PROTOCOL.md §3.2.19: finalBossHp is the Boss HP at battle end
            // (0 here) and finalPlayerHp the player's. Both are the resolution's own
            // terminal values, read and reported.
            //
            // GAME_STATE.md §2.3 / ADR-011 item 6: finalPlayerHp is a fixed protocol
            // label, and the value it carries is the ACTIVE PET's HP — the only
            // Player-side HP the contract has, since a Player has no combat pool.
            // The label is not renamed; only its source member is the documented one.
            events.Add(BattleEvent.ForBattleWon(bossState.HP, resolved.PetState.HP));

            return result
                .WithEvents(events)
                .WithState(resolved with { BossState = bossState });
        }

        // ===================================================================
        // Step 9: Boss Passive — GAME_RULES.md §17 step 18a
        // ===================================================================
        // BOSS_RULES.md §3.3 item 1: "The Passive fires once per player action,
        // after all player damage is resolved" — so it sees the post-damage state.
        //
        // The charge uses the SAME shared PassiveTracker and the SAME shared
        // PassiveCharged/PassiveTriggered events the Pet Passive uses; §7 states
        // "no Boss-specific passive event name is needed". They carry
        // source="boss" and sourceId=BossId — the canonical technical Identity
        // of BOSS_RULES.md §6.4 (e.g. "boss-hoa-long"), never a display name
        // (SIGNALR_PROTOCOL.md §3.2.16 item 2).
        //
        // Thủy Ma is the documented exception: §6.2 gives it the trigger "Passive
        // (always active)" — an alternate trigger (PASSIVE_RULES.md §3), not a Match
        // count — and states it "is never charged via PassiveTracker.Charge on
        // Player Matches, and emits no PassiveCharged/PassiveTriggered from match
        // progress". Its PassiveThreshold is therefore the Always-Active marker 0
        // rather than a threshold, and the charge is skipped entirely. Passive
        // EFFECT application is out of this task's scope for every Boss
        // (BOSS_RULES.md §3 item 3, §6.2): a trigger emits its event and applies
        // nothing.
        if (bossDefinition.PassiveThreshold > 0)
        {
            var bossPassive = PassiveTracker.Charge(
                bossState.PassiveProgress,
                result.Resolution.TotalMatches,
                bossState.PassiveId,
                bossDefinition.PassiveResetBehavior ?? PassiveResetBehavior.Default);

            bossState = bossState with { PassiveProgress = bossPassive.Progress };

            // The tracker builds its reports with source="pet" (it is the Pet
            // Passive's caller in this stage), so the two members this direction
            // owns — source and sourceId — are stated here rather than re-derived:
            // the reports are otherwise carried through unchanged.
            events.AddRange(bossPassive.Charges.Select(
                charge => BattleEvent.ForPassiveCharged(
                    charge with { Source = PassiveEventSource.Boss, SourceId = bossState.BossId.Value })));

            events.AddRange(bossPassive.Triggers.Select(
                trigger => BattleEvent.ForPassiveTriggered(
                    trigger with { Source = PassiveEventSource.Boss, SourceId = bossState.BossId.Value })));
        }

        // ===================================================================
        // Step 10: Boss Skill OR Boss Basic Attack — GAME_RULES.md §17 steps 18b–18c
        // ===================================================================
        // GAME_STATE.md §2.4.3 / BOSS_RULES.md §6.3: "Matches increment
        // BossState.SkillCharge", so this action's Player Matches are added to the
        // charge BEFORE eligibility is evaluated — the Swap that meets the
        // requirement is the Swap the Skill fires on, exactly as the Passive's own
        // batch is evaluated after its Matches are counted (PASSIVE_RULES.md §2
        // item 3).
        //
        // The counter is independent of the Passive's progress (§2.4.3, TASK-022
        // §3.7): this adds to it and neither resets the other.
        bossState = bossState with
        {
            SkillCharge = bossState.SkillCharge + result.Resolution.TotalMatches,
        };

        // The two are mutually exclusive: §18b is taken when the Skill is eligible
        // and §18c is the fallback whenever it is not, so exactly one Boss attack
        // happens per action.
        //
        // The Skill is eligible on BOTH documented conditions
        // (GAME_STATE.md §2.4.3, BOSS_RULES.md §6.3): SkillCharge has reached the
        // requirement AND the cooldown has run out.
        var skillFires = bossState.SkillCharge >= bossDefinition.SkillChargeRequirement
            && bossState.SkillCooldown == 0;

        // COMBAT_RULES.md §3.4: a Boss Skill's Step 1 base damage is "defined per
        // Skill" — the SkillBaseDamage term — and a Boss Basic Attack's is Boss.ATK.
        // Both pass Combo = 1 ("Boss attacks are not part of a Combo chain"), an
        // empty BaseDamagePool (Bosses match no Gems, so no ATK-Gem pool exists for
        // them), and the Boss's Element as the attacker.
        //
        // The defender side is the player's, per §3.4 ("Source = Boss, Target =
        // Player"): the defending Element is the ACTIVE PET's (§3.4, §3.2 — "the
        // defender is the Pet, not the Player", because a Player has no Element),
        // and the defending DEF and HP are the active Pet's too — PetState.DEF and
        // PetState.HP (GAME_STATE.md §2.3, COMBAT_RULES.md §1.1's MVP 25 and 1000,
        // ADR-011 item 5). The Pet is the combat character and the only Player-side
        // combat-stat home; there is no separate Player DEF or HP pool of the
        // values §3.2 and §3.4 name as the target's.
        var bossAttack = skillFires
            ? bossState.ATK + bossDefinition.SkillBaseDamage
            : bossState.ATK;

        if (skillFires)
        {
            // SIGNALR_PROTOCOL.md §3.2.18: the Skill is announced before its damage
            // instance, which follows in the same batch with source="boss" and
            // target="player" (item 3). Both identities are the Boss's own.
            events.Add(BattleEvent.ForBossSkillCast(bossDefinition.SkillId, bossState.BossId.Value));
        }

        var bossDamage = DamagePipeline.Calculate(
            new DamagePipeline.DamageInputs(
                Attack: bossAttack,
                BaseDamagePool: 0,
                Combo: 1,
                AttackerElement: bossState.Element,
                DefenderElement: resolved.PetState.Element,
                DefenderDefense: resolved.PetState.DEF,
                DefenderHp: resolved.PetState.HP,
                Source: DamageParty.Boss,
                Target: DamageParty.Player),
            ComboModifiers.Default,
            ElementModifiers.Default);

        // COMBAT_RULES.md §3.4 step 6 / GAME_STATE.md §5.1: "Final Damage applied to
        // Player.HP" names the Player side of the instance, and the Pet is that
        // side's combat character (ADR-011 items 3 and 5) — so the write is onto the
        // active Pet's PetState.HP (GAME_STATE.md §2.3). The write is explicit — the
        // pipeline returned the post-damage
        // HP and this boundary writes it onto the active Pet's PetState, in the same
        // single write-back as every other value this action changed. The wire label
        // target="player" (SIGNALR_PROTOCOL.md §3.2 item 3) is unchanged: it names
        // the Player side of the instance, and the Pet is that side's combat
        // character (ADR-011 items 3 and 6).
        resolved = resolved with
        {
            PetState = resolved.PetState with { HP = bossDamage.TargetHp },
        };

        events.Add(BattleEvent.ForDamageCalculated(bossDamage.Calculation));
        events.Add(BattleEvent.ForDamageDealt(bossDamage.DamageDealt));
        events.Add(BattleEvent.ForDamageTaken(bossDamage.DamageTaken));

        if (skillFires)
        {
            // BOSS_RULES.md §6.3 / GAME_STATE.md §2.4.3: "After the Skill fires:
            // SkillCharge resets to 0, SkillCooldown resets to the Boss's cooldown
            // value." The next committed Swap's post-SwapExecutor decrement performs
            // the first cooldown tick, so a freshly-cast Skill blocks the following
            // SkillCooldownTurns Turns.
            //
            // The reset discards this action's Matches along with the accumulated
            // charge: the cast consumes them. It does not touch PassiveProgress —
            // §2.4.3 makes the two independent counters, and TASK-022 §3.7 records
            // that neither resets the other.
            bossState = bossState with
            {
                SkillCharge = 0,
                SkillCooldown = bossDefinition.SkillCooldownTurns,
            };
        }

        // When the Skill did not fire, the charge simply carries what step 10 added
        // above — the accumulating case BOSS_RULES.md §6.3 describes, where the Skill
        // becomes eligible on a later Swap.

        // ===================================================================
        // Step 12: Outcome — GAME_RULES.md §1.4
        // ===================================================================
        // The Pet HP terminal check — the Player side's HP, since the Pet is that
        // side's combat character (ADR-011 items 3 and 5) — runs after the Boss
        // Response, because the
        // Boss has just had its chance to reduce it. §1.4 ends the battle when
        // either side reaches 0; the Boss was checked above, so what remains is the
        // player's side — the active Pet's HP (GAME_STATE.md §2.3, ADR-011 item 5).
        //
        // When BOTH sides are alive, NEITHER outcome event is emitted and the battle
        // continues — the absence of an outcome is itself the documented statement
        // that the action was not terminal.
        if (resolved.PetState.HP <= 0)
        {
            // SIGNALR_PROTOCOL.md §3.2.19: the wire member stays finalPlayerHp — a
            // fixed protocol label — and carries this same Pet HP value
            // (ADR-011 item 6: the label is not renamed).
            events.Add(BattleEvent.ForBattleLost(bossState.HP, resolved.PetState.HP));
        }

        // ===================================================================
        // Step 13: the finished post-resolution state — GAME_STATE.md §5.1
        // ===================================================================
        // The caller performs the one write-back, under the Sequence
        // compare-and-set (REDIS_STATE.md §4 items 2 and 5).
        return result
            .WithEvents(events)
            .WithState(resolved with { BossState = bossState });
    }

    /// <summary>
    /// Stores a resolved state under the documented <c>Sequence</c>
    /// compare-and-set and reports whether it was applied
    /// (<c>REDIS_STATE.md</c> §4 items 1–3, 5).
    ///
    /// The compare-and-set itself belongs to the store — §4 item 2 fixes the
    /// semantics, not this layer's spelling of them — so this is a thin pass of
    /// the resolved state and the <c>Sequence</c> it was resolved from. A
    /// <c>false</c> result means the stored sequence had moved on and the newer
    /// authoritative state was left intact.
    /// </summary>
    /// <param name="resolved">The finished post-resolution state.</param>
    /// <param name="expectedSequence">
    /// The <c>Sequence</c> the resolution read — the pre-resolution value
    /// (§4 item 6).
    /// </param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private Task<bool> TryStoreResolvedAsync(
        BattleState resolved,
        int expectedSequence,
        CancellationToken cancellationToken) =>
        _repository.TryUpdateAsync(resolved, expectedSequence, cancellationToken);
}