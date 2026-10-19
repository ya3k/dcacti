using System.Collections.Concurrent;
using GameServer.Application.Cards;
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
/// requests (<c>ARCHITECTURE.md</c> §4 — this service loads,
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
/// documented active-state record (<c>REDIS_STATE.md</c> §7 items 1–2 — no
/// partial state, no second key) and, on the terminal path only, invokes the one
/// documented durable battle end (<c>ARCHITECTURE.md</c> §4 item 4,
/// <c>DATABASE.md</c> §1). Nothing else reaches PostgreSQL, and nothing
/// reaches it during an ordinary action resolution: <c>TDD.md</c> §4 item 3
/// keeps PostgreSQL off the hot resolution path, and
/// <c>GAME_STATE.md</c> §2.0.4 item 3 keeps active state out of it entirely,</item>
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
    /// Pet selection is implemented end-to-end on the battle-start path
    /// (<c>BattleStartService</c> resolves the submitted <c>petId</c> against the
    /// Player's owned collection and supplies this configuration), so the battle's
    /// Pet is resolved from the Pet instance and its definition rather than chosen
    /// here. It is
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
    /// Relic selection is implemented end-to-end on the battle-start path
    /// (<c>BattleStartService</c> resolves the submitted <c>relicLoadout</c>
    /// through <c>RelicLoadoutService</c> and supplies this snapshot), so this
    /// boundary carries the value it is given rather than defaulting one of its
    /// own. A <c>null</c> snapshot remains representable for a caller that
    /// composes no loadout; it is not an empty loadout, because the rules define
    /// no zero-Relic battle (<c>RELIC_RULES.md</c> §2.1 item 1).
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
    /// The battle-start path always supplies it
    /// (<c>BattleStartService</c> supplies the Card loadout validator's product),
    /// so this boundary carries the value it is given rather than defaulting one.
    /// A <c>null</c> snapshot remains representable for a caller that composes no
    /// loadout; it is never an invented empty loadout, because
    /// <c>CARD_RULES.md</c> §1 defines no zero-Card battle.
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
    /// The Boss content a battle is fought against (<c>GAME_STATE.md</c> §2.4,
    /// <c>BOSS_RULES.md</c> §6): the Boss definition and, when the Boss ability is
    /// migrated, its provisioned Boss Card (<c>ADR-029</c> §2.5 as amended by
    /// TASK-308).
    ///
    /// The definition is the documented <see cref="BossDefinition"/> itself —
    /// identity, Element, and base stats — rather than a second copy of it: the
    /// definition already is the configuration this layer needs, and restating its
    /// members here would be the duplicate representation <c>GAME_STATE.md</c>
    /// §0 item 5 forbids. The MVP definitions are <see cref="BossDefinitions"/>.
    /// </summary>
    /// <param name="Definition">
    /// The Boss definition the battle is fought against — the same value
    /// <see cref="GetBossDefinition"/> reports and the one battle creation
    /// received.
    /// </param>
    /// <param name="BossCard">
    /// The Boss's provisioned Boss Card definition, or <c>null</c> when the Boss
    /// has none. <b>Presence is the active-form signal</b>
    /// (<c>BOSS_RULES.md</c> §4.4; <c>ADR-029</c> §2.5 as amended by TASK-308):
    /// a provisioned <see cref="CardDefinition"/> whose
    /// <see cref="CardDefinition.CardDefinitionId"/> equals the Boss ability's
    /// <see cref="BossDefinition.SkillId"/> and whose
    /// <see cref="CardDefinition.Category"/> is
    /// <see cref="CardCategory.BossCard"/> means the ability resolves as a Boss
    /// Card cast; its absence means the transitional <c>BossSkill</c> form.
    ///
    /// <b>It is resolved exactly once, at battle creation.</b> It is a content
    /// input cached beside the Boss definition and never re-read, so a turn
    /// resolution performs no content lookup and a CAS retry observes the same
    /// frozen content (<c>REDIS_STATE.md</c> §4 item 6; <c>TDD.md</c> §4 item 3).
    /// It is not battle state: no <c>BattleState</c> member, Redis field, or wire
    /// member carries it (<c>GAME_STATE.md</c> §2.4/§2.4.3).
    /// </param>
    private readonly record struct BossConfiguration(
        BossDefinition Definition,
        CardDefinition? BossCard)
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

    /// <summary>
    /// The durable battle result writer — the documented battle-end persistence
    /// step, invoked from the resolution paths that observed a terminal outcome
    /// (<c>ARCHITECTURE.md</c> §4 item 4, <c>DATABASE.md</c> §1).
    ///
    /// <b>It is optional because the store is not this boundary's.</b> The
    /// durable result and its two PostgreSQL lookups are Infrastructure
    /// concerns reached through their own Application contracts, and the
    /// resolution pipeline is explicitly independent of them
    /// (<c>ARCHITECTURE.md</c> §2.1 item 2 — "Infrastructure implements
    /// persistence and transport. It depends on Domain/Application interfaces,
    /// never the other way around"). A composition without it resolves battles
    /// exactly as before and writes no result; a composition with it records
    /// one, which is what the running server does. It must never be replaced by
    /// an in-process substitute for the active-state store
    /// (<c>REDIS_STATE.md</c> §7 item 5).
    /// </summary>
    /// <summary>
    /// The durable battle result writer — the documented battle-end persistence
    /// step, invoked from the resolution paths that observed a terminal outcome
    /// (<c>ARCHITECTURE.md</c> §4 item 4, <c>DATABASE.md</c> §1).
    ///
    /// <b>It is optional because the store is not this boundary's.</b> The
    /// durable result and its two PostgreSQL lookups are Infrastructure concerns
    /// reached through their own Application contracts
    /// (<c>ARCHITECTURE.md</c> §2.1 item 2). A composition without it resolves
    /// battles exactly as before and records no durable result; a composition
    /// with it records one — which is what the running server does — and the
    /// documented write-then-delete order is <see cref="BattleResultService"/>'s,
    /// not this type's. It is deliberately not a place to substitute an
    /// in-process store for the active-state record
    /// (<c>REDIS_STATE.md</c> §7 item 5), which is why the contract only forwards
    /// the state the resolution already produced.
    /// </summary>
    private readonly IBattleResultPersistence? _battleResults;

    private readonly ICardDefinitionLookup? _cardDefinitions;

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
    private readonly ConcurrentDictionary<string, IReadOnlyList<RelicDefinition?>?> _relicConfiguration = new(StringComparer.Ordinal);

    /// <summary>
    /// The Boss Passive firing eligibility each battle session has already
    /// consumed (<c>BOSS_RULES.md</c> §6.2.4; <c>PASSIVE_RULES.md</c> §4).
    ///
    /// <b>Why a battle-session record rather than a state member.</b>
    /// <c>PASSIVE_RULES.md</c> §4's Boss Passive clause makes a Boss Passive whose
    /// definition declares the non-default <c>Persistent</c> reset behavior
    /// (<c>PassiveResetBehavior.NoReset</c>; <c>DATABASE.md</c> §1 note item 3)
    /// eligible to fire <b>at most once per battle</b>, and
    /// <c>BOSS_RULES.md</c> §6.2.4 states that the firing eligibility is consumed
    /// upon the first activation and is <b>not</b> restored when the applied effect
    /// expires at step 19a. Neither the transient
    /// <c>BossState.StatusEffects[]</c> instance — whose removal with the effect
    /// would re-arm the Passive — nor a new <c>BossState</c> member can carry that:
    /// §6.2.4 introduces no state field and <c>GAME_STATE.md</c> §2.4's tree is
    /// unchanged. The eligibility is therefore tracked for the lifetime of the
    /// battle session a resolution runs in, alongside the other per-battle inputs
    /// this boundary attaches at creation (<see cref="_petConfiguration"/>,
    /// <see cref="_bossConfiguration"/>, <see cref="_relicConfiguration"/>).
    ///
    /// <b>It is keyed by the battle session and the Passive identity.</b> A battle
    /// id identifies one battle session and is authored by the server when that
    /// battle is created (<c>GAME_STATE.md</c> §2.0.1, <c>API_CONTRACTS.md</c> §3),
    /// so a new battle begins with no consumed entry — fresh firing eligibility —
    /// and one battle's consumption can never affect another's.
    ///
    /// <b>It is not a second representation of any stored value.</b> No
    /// active-state record holds it; it is written once per battle, after the
    /// resolution that activated the firing was accepted by the store's
    /// <c>Sequence</c> compare-and-set (<c>REDIS_STATE.md</c> §4 items 2–3, 6); it
    /// is never released or re-armed; and it is read only to answer the eligibility
    /// question itself.
    /// </summary>
    private readonly ConcurrentDictionary<(string BattleId, string PassiveId), bool> _consumedBossPassiveFiring = new();

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
    /// <param name="battleResults">
    /// The durable battle-end step, or <c>null</c> for a composition that records
    /// no durable result.
    /// </param>
    public BattleStateService(
        IBattleStateRepository repository,
        IRngSeedSource seedSource,
        IBattleResultPersistence? battleResults = null,
        ICardDefinitionLookup? cardDefinitions = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));

        // ARCHITECTURE.md §4 item 4: the battle-end step (durable result, then
        // active-state delete) is reached from the terminal paths below. It is
        // expressed through its own contract so this singleton names no
        // persistence lifetime and no PostgreSQL type.
        _battleResults = battleResults;
        _cardDefinitions = cardDefinitions;
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
    /// this is required: the battle-start path performs the Pet selection and
    /// supplies the resulting configuration here.
    /// </param>
    /// <param name="bossDefinition">
    /// The definition of the Boss this battle is fought against
    /// (<c>GAME_STATE.md</c> §2.4, <c>BOSS_RULES.md</c> §6.1). <c>BossState</c> is
    /// present from battle creation (§2.4) and no value may be invented for its
    /// identity, Element, or stats, so this is required: the battle-start path
    /// performs the Boss selection and supplies the resolved definition here —
    /// <see cref="BossDefinitions"/> holds the MVP set.
    /// </param>
    /// <param name="seed">
    /// The battle's server-chosen PRNG seed (<c>GAME_STATE.md</c> §2.6.1). It is
    /// read from this boundary's own entropy source when the caller does not
    /// supply one, which is the only documented behaviour: no client, request
    /// member, or caller value influences it (§2.6.1 items 1–2). It is an
    /// explicit parameter — rather than being generated internally — only so the
    /// RNG stream is reproducible for a caller that must replay it
    /// (<c>TDD.md</c> §6 item 3, ADR-009).
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
        BattleSeed? seed = null,
        CancellationToken cancellationToken = default,
        IReadOnlyList<RelicDefinition?>? equippedRelicDefinitions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        // §2.6.1 item 3: the seed records the battle's origin point and is never
        // rewritten after creation. A caller may supply one for reproducibility;
        // otherwise the server's own entropy produces it (items 1–2).
        var battleSeed = seed ?? new BattleSeed(_seedSource.CreateSeed());

        // ADR-029 §2.5 (as amended by TASK-308) / BOSS_RULES.md §4.4: the Boss
        // ability's active form is provisioned-content presence, and it is resolved
        // exactly ONCE here, at battle creation, before the state exists. The
        // resolved Boss Card is cached with the Boss definition as the battle's
        // content input; no turn resolution re-reads it, so a CAS retry observes
        // the same frozen content (REDIS_STATE.md §4 item 6). A provisioned row
        // that is present but invalid fails creation closed — there is no fallback
        // to the legacy Skill form (AGENTS.md §7, ADR-024 §2.4).
        var bossCard = await ResolveBossCardAsync(bossDefinition, cancellationToken)
            .ConfigureAwait(false);

        // §2.3 / §2.4: the battle's PetState is built from the caller's Pet
        // configuration — carrying the selected owned Pet instance identity and
        // progress at the start of its first charge — and its
        // BossState from the Boss definition at full health in the documented
        // Initial State, carrying the Passive identity §2.4.2 sets at creation.
        // Neither is absent, neither is defaulted with an invented value, and
        // neither is created lazily on the first Swap.
        var bossConfiguration = new BossConfiguration(bossDefinition, bossCard);

        // §2.8 item 2: the owner identity is recorded at creation from the
        // caller's authenticated context — the same Player the ownership check
        // already validated — and never re-derived afterward (§2.8 item 4).
        var state = BattleState.Create(
            battleId,
            battleSeed.Value,
            playerId,
            petConfiguration.ToPetState(),
            bossConfiguration.ToBossState());

        // RELIC_RULES.md §3 / §6 note 1: Burning Curse's declared Trigger is
        // OnBattleStart — "fires once, at battle start" — so the Relic stage's
        // battle-start firing point runs here, once, against the state battle
        // creation produced, before the record is written. The applied modifier is
        // therefore part of the created state's single write-back rather than a
        // second write: a Relic whose effect is standing for the battle
        // (`RELIC_RULES.md` §8.3 item 4) is in the record from its first moment, and
        // no later action re-checks battle state to decide whether it applies.
        //
        // Battle creation emits no Battle Event (`GAME_STATE.md` §2.0.5.2 item 2),
        // so this pass collects no reports: the created state is the whole result,
        // and the effect reaches the client through the existing state projection
        // rather than through a new event.
        if (equippedRelicDefinitions != null)
        {
            state = ApplyRelicFiringPoint(
                equippedRelicDefinitions,
                state,
                RelicFiringPoint.BattleStart,
                matchCount: 0,
                combo: 0,
                events: null);
        }

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
        if (equippedRelicDefinitions != null)
        {
            _relicConfiguration[battleId] = equippedRelicDefinitions;
        }

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
    /// Resolves the Boss ability's provisioned Boss Card, or <c>null</c> when the
    /// ability is not migrated and therefore keeps the transitional
    /// <c>BossSkill</c> form (<c>BOSS_RULES.md</c> §4.4; <c>ADR-029</c> §2.5 as
    /// amended by TASK-308).
    ///
    /// <b>Identity is the lookup key.</b> The Card's identity is the ability's
    /// <see cref="BossDefinition.SkillId"/> (<c>CARD_RULES.md</c> §1.1 item 3;
    /// <c>SIGNALR_PROTOCOL.md</c> §3.2.30), so the id-only content read the
    /// existing <see cref="ICardDefinitionLookup"/> already offers is the whole
    /// resolution — no new repository, column, table, or query shape is
    /// introduced (<c>ARCHITECTURE.md</c> §5).
    ///
    /// <b>Presence is the form signal, and an absent lookup is the absent form.</b>
    /// A composition with no Card content read (the optional dependency is
    /// <c>null</c>) can resolve no Boss Card, which is exactly the transitional
    /// position; a lookup that returns <c>null</c> likewise means no provisioned
    /// row exists for this ability. Neither is an error and neither falls back to
    /// an invented value.
    ///
    /// <b>Present content is validated fail-closed.</b> A provisioned row for the
    /// ability's identity must be a <see cref="CardCategory.BossCard"/>, the Boss
    /// definition must carry a non-null, non-negative
    /// <see cref="BossDefinition.CastPriorityScore"/>, and the authored
    /// <see cref="CardDefinition.PowerCost"/> must be non-negative. Anything else
    /// is invalid definition data — the battle is not created, and the legacy
    /// form is never substituted for it (<c>ADR-024</c> §2.4; <c>BOSS_RULES.md</c>
    /// §4.3 item 5; <c>AGENTS.md</c> §7).
    /// </summary>
    /// <param name="bossDefinition">
    /// The resolved Boss content the battle would be fought against. Its
    /// <see cref="BossDefinition.SkillId"/> is the Boss Card identity.
    /// </param>
    /// <param name="cancellationToken">Cancels the content read.</param>
    /// <returns>
    /// The resolved Boss Card, or <c>null</c> for the transitional
    /// <c>BossSkill</c> form.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// A provisioned Boss Card is present for this ability but its content or the
    /// Boss's authored cast-priority score is invalid. The creation fails closed.
    /// </exception>
    private async Task<CardDefinition?> ResolveBossCardAsync(
        BossDefinition bossDefinition,
        CancellationToken cancellationToken)
    {
        if (_cardDefinitions is null)
        {
            return null;
        }

        var card = await _cardDefinitions
            .GetDefinitionAsync(bossDefinition.SkillId, cancellationToken)
            .ConfigureAwait(false);

        if (card is null)
        {
            return null;
        }

        if (card.Category != CardCategory.BossCard)
        {
            throw new InvalidOperationException(
                $"The provisioned CardDefinition '{card.CardDefinitionId}' resolved for Boss "
                + $"ability '{bossDefinition.SkillId}' is category '{card.Category}', not "
                + "'BossCard'. A provisioned Boss Card must carry Category = BossCard "
                + "(CARD_RULES.md §1.1 item 3; BOSS_RULES.md §4.2 item 2). The battle "
                + "creation fails closed rather than falling back to the legacy Skill form.");
        }

        if (bossDefinition.CastPriorityScore is not >= 0)
        {
            throw new InvalidOperationException(
                $"Boss '{bossDefinition.BossId.Value}' has the provisioned Boss Card "
                + $"'{card.CardDefinitionId}' but an invalid CastPriorityScore "
                + $"({bossDefinition.CastPriorityScore?.ToString() ?? "NULL"}). A migrated "
                + "Boss requires a non-null, non-negative authored cast-priority score "
                + "(BOSS_RULES.md §4.3 item 5; ADR-024 §2.4). The battle creation fails "
                + "closed rather than selecting a default or falling back to another form.");
        }

        if (card.PowerCost < 0)
        {
            throw new InvalidOperationException(
                $"The provisioned Boss Card '{card.CardDefinitionId}' carries an invalid "
                + $"PowerCost ({card.PowerCost}); the authored cost must be non-negative "
                + "(CARD_RULES.md §3.6 item 4; BOSS_RULES.md §4.2 item 3). The battle "
                + "creation fails closed on invalid definition data.");
        }

        return card;
    }

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
    /// Returns the authoritative battle state for <paramref name="battleId"/> only if
    /// the battle is owned by <paramref name="callerPlayerId"/>; otherwise returns <c>null</c>.
    /// </summary>
    public async Task<BattleState?> GetOwnedBattleStateAsync(
        string battleId,
        string callerPlayerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerPlayerId);

        var state = await _repository.GetAsync(battleId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return null;
        }

        if (state.PlayerId.Value != callerPlayerId)
        {
            return null;
        }

        return state;
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
    /// (<c>ARCHITECTURE.md</c> §2.1). The cascade loop, Match Detection, Gravity,
    /// and Spawn all live in Domain (<c>ARCHITECTURE.md</c> §4.1).
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
    /// <param name="cancellationToken">Cancels the store read and write.</param>
    /// <returns>
    /// The resulting authoritative state, or <c>null</c> when no record exists for
    /// that id.
    /// </returns>
    public async Task<BattleState?> ResolveBoardAsync(
        string battleId,
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

        var resolution = CascadeResolver.Resolve(state.BoardState, rng);

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
    /// Detection, Gravity, Spawn, and the
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
    /// <b>The action's state precondition and identity are its own contract.</b>
    /// The request's <c>ExpectedSequence</c> is the state version its caller
    /// observed and <c>ActionId</c> is the identity of the logical action
    /// (<c>ADR-025</c> §1–§2, <c>MATCH3_RULES.md</c> §2.1.4). They are separate
    /// values and never one value read twice: the precondition decides whether the
    /// action was derived from the state it is being applied to, and the identity
    /// decides whether the action has already been applied. A request that states
    /// no precondition — the legacy form
    /// (<c>SIGNALR_PROTOCOL.md</c> §2 item 1) — is resolved against the state the
    /// read returned, and its precondition is treated as consumed if a concurrent
    /// writer moves that state before its write-back: it is rejected as a stale
    /// action rather than re-resolved against a board version its caller never
    /// observed (<c>REDIS_STATE.md</c> §4 item 2, <c>TASK-257</c>).
    ///
    /// <b>The Damage Pipeline runs here, after the Passive charge.</b>
    /// <c>GAME_RULES.md</c> §17 places "Calculate Damage" / "Apply Element
    /// Modifier" / "Apply Final Damage" at steps 15–17, after "Charge Passive"
    /// (step 10) and before "Resolve Boss Response" (step 18).
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
    /// and applies no additional damage of its own. The Boss Response stage that
    /// follows the Damage Pipeline (<c>GAME_RULES.md</c> §17 steps 18a–18c), the
    /// terminal Victory/Defeat check (§1.4), and the End Turn step 19a Status
    /// Effect tick (<c>GAME_STATE.md</c> §5.1.1) all run in
    /// <see cref="ResolveSwapAsync"/>, whose rules are the cited documents' and
    /// not this boundary's: a Boss HP of <c>0</c> is recorded as state and is not
    /// read as an outcome here, and the durable battle end it implies is performed
    /// after the write-back by the caller (<c>ARCHITECTURE.md</c> §4 item 4). The
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

        // ===================================================================
        // ADR-025: Action Idempotency Check
        // ===================================================================
        // The action identity is checked BEFORE the state precondition, and the
        // order is the contract rather than an optimization: a retry of an action
        // that already committed observes a state whose Sequence has moved on, so
        // it arrives carrying either the precondition it originally stated (S) or
        // the sequence the commit produced (S+1) — whichever the client last saw.
        // Both are legitimate spellings of "this logical action", so identity must
        // be able to answer the retry without the precondition rejecting it first
        // (MATCH3_RULES.md §2.1.4 item 2, ADR-025 §2).
        //
        // Recognition is against the MOST RECENTLY COMMITTED Swap and nothing
        // else (GAME_STATE.md §2.1.10 item 5): BattleState records one committed
        // swap, so the window in which a retry is recognized as a replay is one
        // swap wide. A retry that arrives after a later action has committed
        // therefore matches nothing here, and its obsolete precondition rejects it
        // as STALE_ACTION below — it is not re-executed, and no duplicate event is
        // emitted. That window is the documented behavior of §2.1.4 item 2 and is
        // preserved here deliberately; widening it would be a separate state-shape
        // decision (an action history), not a defect fix.
        if (!string.IsNullOrWhiteSpace(request.ActionId) &&
            state.LastCommittedSwapPair is { } initialLast &&
            initialLast.ActionId == request.ActionId)
        {
            if (initialLast.MatchesPair(request.From, request.To))
            {
                return SwapExecutionResult.IdempotentReplay(state);
            }

            return SwapExecutionResult.Rejected(SwapRejectionReason.InvalidActionPayload);
        }

        // ===================================================================
        // MATCH3_RULES.md §2.1.4 item 7: Terminal-battle action guard
        // ===================================================================
        // A battle that has ended admits no further Swap (MATCH3_RULES.md §2.1.4
        // item 7, GAME_RULES.md §1.4). The guard runs at the action boundary —
        // AFTER the action-identity recognition above, so a retry of the very
        // action that produced the terminal state is still answered as a replay
        // rather than rejected, and BEFORE any resolution, so this action runs no
        // gameplay stage, consumes no RNG, and produces no Boss response, no new
        // Round, and no Battle Event.
        //
        // It reuses the established state-precondition rejection of §2.1.4 rather
        // than introducing a rejection code or a wire member: a terminal battle
        // satisfies no precondition of a resolution, and SIGNALR_PROTOCOL.md §5
        // item 3 makes §2.1.2/§2.1.4 the owner of a Swap rejection's code, so the
        // protocol gains no new value and no new response shape.
        //
        // The path is reachable whenever a terminal state survives in the store.
        // The documented battle end deletes the record once the durable result is
        // written (REDIS_STATE.md §3), but a battle whose result boundary is
        // absent or whose durable write failed keeps its state
        // (PersistTerminalResultAsync, DATABASE.md §1 sourcing item 3), and this
        // guard is what keeps such a state from producing gameplay. It is a
        // separate response from the missing-battle one: a battle that is gone
        // returns null here and is reported as BATTLE_NOT_FOUND by the caller,
        // while an ended battle that is still stored is rejected as STALE_ACTION.
        if (IsTerminalBattle(state))
        {
            return SwapExecutionResult.Rejected(SwapRejectionReason.StaleAction);
        }

        // ===================================================================
        // ADR-025: State Version Precondition Check
        // ===================================================================
        if (request.ExpectedSequence.HasValue && request.ExpectedSequence.Value != state.Sequence)
        {
            return SwapExecutionResult.Rejected(SwapRejectionReason.StaleAction);
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

            var resolution = await ResolveSwapAsync(battleId, state, request, cancellationToken)
                .ConfigureAwait(false);
            var committed = resolution.Result;

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
                // BOSS_RULES.md §6.2.4 / PASSIVE_RULES.md §4: the resolution was
                // accepted, so a once-per-battle Boss Passive firing it activated is
                // now the authoritative one and its eligibility is consumed. The
                // consumption follows the commit rather than preceding it for the
                // §4 item 6 reason — a refused attempt must leave the retried
                // resolution able to reproduce the same activation.
                ConsumeOncePerBattleBossPassiveFiring(
                    battleId,
                    resolution.ActivatedOncePerBattleBossPassive);

                // ARCHITECTURE.md §4 item 4: a committed resolution that emitted
                // BattleWon/BattleLost has ended the battle, so its durable result
                // is recorded and its active state is cleared. This runs AFTER the
                // write-back — the commit is what makes the terminal transition
                // authoritative, and the clear must not precede it
                // (REDIS_STATE.md §3, §4 items 2–3, 5).
                await PersistTerminalResultAsync(committed.State, committed.Events, cancellationToken)
                    .ConfigureAwait(false);

                return committed;
            }

            // §4 items 2–3: the stored Sequence had moved on, so this resolution
            // was refused rather than allowed to overwrite a newer authoritative
            // state.
            if (await _repository.GetAsync(battleId, cancellationToken).ConfigureAwait(false)
                is not { } fresh)
            {
                // The record disappeared between the read and the write (its TTL
                // elapsed, or the battle ended). There is no fresh state to retry
                // against, and no battle is invented for it.
                return null;
            }

            // ADR-025 §4: the concurrent winner is checked for identity first, for
            // the same reason the initial read is: a lost acknowledgement means the
            // retry and the winner can be the same logical action, in which case
            // the fresh state's committed record carries this very action id and
            // the retry is answered as a replay rather than executed twice.
            if (!string.IsNullOrWhiteSpace(request.ActionId) &&
                fresh.LastCommittedSwapPair is { } freshLast &&
                freshLast.ActionId == request.ActionId)
            {
                if (freshLast.MatchesPair(request.From, request.To))
                {
                    return SwapExecutionResult.IdempotentReplay(fresh);
                }

                return SwapExecutionResult.Rejected(SwapRejectionReason.InvalidActionPayload);
            }

            // MATCH3_RULES.md §2.1.4 item 7: the fresh record is guarded on the
            // same terms as the record this resolution first read. A concurrent
            // winner can have ended the battle, and an ended battle yields no
            // resolution, no Boss response, no new Round, and no further event —
            // so it is rejected here rather than re-resolved against.
            if (IsTerminalBattle(fresh))
            {
                return SwapExecutionResult.Rejected(SwapRejectionReason.StaleAction);
            }

            // ADR-025: If client submitted an expectedSequence and the state advanced,
            // the precondition was consumed by a concurrent action — reject as STALE_ACTION.
            if (request.ExpectedSequence.HasValue && request.ExpectedSequence.Value != fresh.Sequence)
            {
                return SwapExecutionResult.Rejected(SwapRejectionReason.StaleAction);
            }

            // TASK-257 / MATCH3_RULES.md §2.1.4 item 1, ADR-025 §4: a request that
            // carries NO precondition — the legacy form SIGNALR_PROTOCOL.md §2
            // item 1 documents — states no state version it observed, so the
            // refused write is terminal for it and it is rejected as a stale
            // action. Re-resolving it against `fresh` would execute the action
            // against a state version its caller never named and never observed,
            // which is exactly the bypass the state precondition exists to
            // prevent: the legacy form's missing precondition is a limitation of
            // that form, not a licence for it. Like every other rejection, this
            // writes nothing (§4 item 7).
            if (!request.ExpectedSequence.HasValue)
            {
                return SwapExecutionResult.Rejected(SwapRejectionReason.StaleAction);
            }

            state = fresh;
        }

        // Contention did not settle within the bound.
        if (request.ExpectedSequence.HasValue && request.ExpectedSequence.Value != state.Sequence)
        {
            return SwapExecutionResult.Rejected(SwapRejectionReason.StaleAction);
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

        if (lastAttempt.Result.IsRejected)
        {
            return lastAttempt.Result;
        }

        if (!await _repository
            .TryUpdateAsync(lastAttempt.Result.State, state.Sequence, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        // This final resolution is the authoritative one, so its activation is
        // consumed exactly as the loop's committed path does above.
        ConsumeOncePerBattleBossPassiveFiring(
            battleId,
            lastAttempt.ActivatedOncePerBattleBossPassive);

        // ARCHITECTURE.md §4 item 4 / REDIS_STATE.md §3: this write-back committed a
        // resolution, so a committed resolution whose own event list states
        // BattleWon/BattleLost has ended the battle — and its durable result is
        // recorded and its active state cleared here exactly as the loop's committed
        // path does above. The fallback is not a different kind of commit: it is one
        // more `TryUpdateAsync` of the same resolution shape, and a terminal
        // transition committed here must not stay unpersisted, which would leave a
        // terminal record in the store with no BattleResult row, no XP grant, and no
        // documented battle end (DATABASE.md §1 sourcing item 3).
        //
        // The call is unconditional for the same reason it is on that path: the
        // helper reads the resolution's OWN event list and returns without writing
        // for a non-terminal one, so no terminal predicate is repeated here and no
        // second write, event, reward, or cleanup path is introduced.
        await PersistTerminalResultAsync(lastAttempt.Result.State, lastAttempt.Result.Events, cancellationToken)
            .ConfigureAwait(false);

        return lastAttempt.Result;
    }

    /// <summary>
    /// Executes one requested Basic Card cast against a battle's authoritative state and
    /// records the resulting state (<c>CARD_RULES.md</c> §2, §3).
    /// </summary>
    /// <param name="battleId">The battle the Card cast applies to.</param>
    /// <param name="cardId">The Card definition identity being cast.</param>
    /// <param name="cancellationToken">Cancels the store reads and write.</param>
    /// <returns>
    /// The rejection or commit result, or <c>null</c> when no battle record exists for that id.
    /// </returns>
    public async Task<CardCastExecutionResult?> ExecuteCardCastAsync(
        string battleId,
        string cardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);

        if (await _repository.GetAsync(battleId, cancellationToken).ConfigureAwait(false) is not { } state)
        {
            return null;
        }

        // ===================================================================
        // CARD_RULES.md §3 item 8: Terminal-battle cast guard
        // ===================================================================
        // A battle that has ended admits no Card cast and no Pet Skill cast
        // (CARD_RULES.md §3 item 8, GAME_RULES.md §1.4). The guard runs at the action
        // boundary — AFTER the record read, so a battle that is gone is still reported
        // as absence rather than as a rejection, and BEFORE the definition lookup,
        // before §3 item 2's validation, and before any effect, so this request
        // composes no EffectiveCardCost, spends no Power, changes no board/HP/buff/
        // debuff/status/Sequence/RNG value, resolves no round, emits no Battle Event,
        // produces no Boss response, and writes nothing.
        //
        // It reuses the established state-precondition rejection rather than
        // introducing a rejection code or a wire member: a terminal battle satisfies
        // no precondition of a cast, and SIGNALR_PROTOCOL.md §5 item 3 delegates a
        // cast rejection's code to §3, so the protocol gains no new value and the
        // acknowledgement shape is unchanged. It is the cast-side counterpart of
        // MATCH3_RULES.md §2.1.4 item 7's terminal Swap guard.
        //
        // There is no replay recognition for the guard to follow, and this is the one
        // structural difference from the Swap path: CardCast and PetSkillCast carry no
        // action identity (SIGNALR_PROTOCOL.md §2 item 1 makes clientSequence an
        // opaque, unconsulted correlation value), so there is no already-committed
        // action to answer as a replay. A repeated cast against a battle that has ended
        // is a rejection either way, and no replay semantics are redefined.
        if (IsTerminalBattle(state))
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.StaleAction);
        }

        if (_cardDefinitions is null)
        {
            throw new InvalidOperationException("Card definition lookup is not configured.");
        }

        var cardDefinition = await _cardDefinitions.GetDefinitionAsync(cardId, cancellationToken).ConfigureAwait(false);
        if (cardDefinition is null)
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.InvalidCard);
        }

        const int MaxAttempts = 8;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var expectedSequence = state.Sequence;

            var result = CardCastExecutor.Execute(state, cardDefinition);

            if (result.IsRejected)
            {
                return result;
            }

            var written = await _repository
                .TryUpdateAsync(result.State, expectedSequence, cancellationToken)
                .ConfigureAwait(false);

            if (written)
            {
                if (result.Events.Any(e => e.Type is BattleEventType.BattleWon or BattleEventType.BattleLost))
                {
                    await PersistTerminalResultAsync(result.State, result.Events, cancellationToken)
                        .ConfigureAwait(false);
                }

                return result;
            }

            if (await _repository.GetAsync(battleId, cancellationToken).ConfigureAwait(false)
                is not { } fresh)
            {
                return null;
            }

            // CARD_RULES.md §3 item 8: the fresh record is guarded on the same terms as
            // the record this cast first read. A concurrent winner can have ended the
            // battle, and an ended battle yields no cast — no Power spent, no state
            // change, no Battle Event, no Boss response, no round, and no write — so it
            // is rejected here rather than resolved against.
            if (IsTerminalBattle(fresh))
            {
                return CardCastExecutionResult.Rejected(CardCastRejectionReason.StaleAction);
            }

            state = fresh;
        }

        throw new InvalidOperationException(
            $"Resolution of CardCast on battle '{battleId}' exceeded {MaxAttempts} compare-and-set attempts.");
    }

    /// <summary>
    /// Validates and executes a Pet Signature Skill cast against the battle's authoritative state
    /// (<c>SIGNALR_PROTOCOL.md</c> §2, <c>CARD_RULES.md</c> §4).
    /// </summary>
    public async Task<CardCastExecutionResult?> ExecutePetSkillCastAsync(
        string battleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        if (await _repository.GetAsync(battleId, cancellationToken).ConfigureAwait(false) is not { } state)
        {
            return null;
        }

        // ===================================================================
        // CARD_RULES.md §3 item 8: Terminal-battle cast guard
        // ===================================================================
        // A battle that has ended admits no Card cast and no Pet Skill cast
        // (CARD_RULES.md §3 item 8, GAME_RULES.md §1.4). The guard runs at the action
        // boundary — AFTER the record read, so a battle that is gone is still reported
        // as absence rather than as a rejection, and BEFORE the definition lookup,
        // before §3 item 2's validation, and before any effect, so this request
        // composes no EffectiveCardCost, spends no Power, changes no board/HP/buff/
        // debuff/status/Sequence/RNG value, resolves no round, emits no Battle Event,
        // produces no Boss response, and writes nothing.
        //
        // It reuses the established state-precondition rejection rather than
        // introducing a rejection code or a wire member: a terminal battle satisfies
        // no precondition of a cast, and SIGNALR_PROTOCOL.md §5 item 3 delegates a
        // cast rejection's code to §3, so the protocol gains no new value and the
        // acknowledgement shape is unchanged. It is the cast-side counterpart of
        // MATCH3_RULES.md §2.1.4 item 7's terminal Swap guard.
        //
        // There is no replay recognition for the guard to follow, and this is the one
        // structural difference from the Swap path: CardCast and PetSkillCast carry no
        // action identity (SIGNALR_PROTOCOL.md §2 item 1 makes clientSequence an
        // opaque, unconsulted correlation value), so there is no already-committed
        // action to answer as a replay. A repeated cast against a battle that has ended
        // is a rejection either way, and no replay semantics are redefined.
        if (IsTerminalBattle(state))
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.StaleAction);
        }

        if (_cardDefinitions is null)
        {
            throw new InvalidOperationException("Card definition lookup is not configured.");
        }

        // Identify the active Pet's equipped Pet Skill Card
        CardDefinition? petSkillCard = null;
        if (state.PetState.EquippedCards is not null)
        {
            foreach (var cardId in state.PetState.EquippedCards)
            {
                var def = await _cardDefinitions.GetDefinitionAsync(cardId.Value, cancellationToken).ConfigureAwait(false);
                if (def is not null && def.Category == CardCategory.PetSkill)
                {
                    petSkillCard = def;
                    break;
                }
            }
        }

        if (petSkillCard is null)
        {
            return CardCastExecutionResult.Rejected(CardCastRejectionReason.InvalidCard);
        }

        const int MaxAttempts = 8;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var expectedSequence = state.Sequence;

            var result = CardCastExecutor.Execute(state, petSkillCard);

            if (result.IsRejected)
            {
                return result;
            }

            var written = await _repository
                .TryUpdateAsync(result.State, expectedSequence, cancellationToken)
                .ConfigureAwait(false);

            if (written)
            {
                if (result.Events.Any(e => e.Type is BattleEventType.BattleWon or BattleEventType.BattleLost))
                {
                    await PersistTerminalResultAsync(result.State, result.Events, cancellationToken)
                        .ConfigureAwait(false);
                }

                return result;
            }

            if (await _repository.GetAsync(battleId, cancellationToken).ConfigureAwait(false)
                is not { } fresh)
            {
                return null;
            }

            // CARD_RULES.md §3 item 8: the fresh record is guarded on the same terms as
            // the record this cast first read. A concurrent winner can have ended the
            // battle, and an ended battle yields no cast — no Power spent, no state
            // change, no Battle Event, no Boss response, no round, and no write — so it
            // is rejected here rather than resolved against.
            if (IsTerminalBattle(fresh))
            {
                return CardCastExecutionResult.Rejected(CardCastRejectionReason.StaleAction);
            }

            state = fresh;
        }

        throw new InvalidOperationException(
            $"Resolution of PetSkillCast on battle '{battleId}' exceeded {MaxAttempts} compare-and-set attempts.");
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
    /// Match Detection, Gravity, Spawn, and
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
    /// <returns>
    /// The rejection, or the committed result carrying its finished state, together
    /// with the once-per-battle Boss Passive firing this resolution activated — if
    /// any — for the caller to consume on the commit path
    /// (<see cref="SwapResolution.ActivatedOncePerBattleBossPassive"/>).
    /// </returns>
    private async Task<SwapResolution> ResolveSwapAsync(
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
            return new SwapResolution(result, ActivatedOncePerBattleBossPassive: null);
        }

        // BOSS_RULES.md §6.2.4: the once-per-battle Boss Passive firing this
        // resolution activated, if any. It is reported to the caller rather than
        // consumed here, so the eligibility is spent only when this resolution is
        // the one the store accepted (REDIS_STATE.md §4 item 6).
        PassiveId? activatedOncePerBattleBossPassive = null;

        // ===================================================================
        // Step 3 (BOSS_RULES.md §6.3, MATCH3_RULES.md §8.1): SkillCooldown--
        // ===================================================================
        // §6.3: Cooldown "decrements by 1 at each Turn increment", and
        // MATCH3_RULES.md §8.1 makes one committed atomic Round begin exactly one
        // Turn — whose stored number SwapExecutor already advanced, above, in its own
        // write-back. The decrement therefore runs here, once per committed Round,
        // AFTER SwapExecutor returns and BEFORE the Boss's own board action. It is
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
        // resolution's own Match total (MATCH3_RULES.md §3 item 5: Matches only,
        // so cleared cells charge nothing), the
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
            PetState = result.State.PetState with { PassiveProgress = charged.Progress },
        };

        // GAME_EVENTS.md §1, §1.1 / GAME_RULES.md §17 step 10: the events of this
        // resolution, in the order the pipeline produced them. The executor's list
        // opens with the Player's committed board action and its outcome
        // (`SwapStarted`/`SwapResolved`, §2, SIGNALR_PROTOCOL.md §3.2.26–§3.2.27)
        // and then carries the Match-3 part, which §1 places from MatchCreated
        // through ComboChanged; the tracker's reports follow it, one
        // PassiveCharged per Match in Match order and then, when the Threshold was
        // crossed, the single PassiveTriggered. Nothing is sorted, filtered, or
        // rebuilt — the assembled list is the stages' own output concatenated, and
        // WithEvents carries every other member of the result across unchanged.
        var events = new List<BattleEvent>(
            result.Events.Count
            + charged.Charges.Count
            + charged.Triggers.Count
            + 2   // the Boss board action's SwapStarted/SwapResolved pair
            + 2   // the Player cycle's and the Boss cycle's MatchedGemSummary
            + 3   // Player→Boss damage
            + 8   // Boss Passive charges + trigger (bounded by the Match total) + Boss response
            + 1); // outcome

        events.AddRange(result.Events);

        // GAME_EVENTS.md §1.1 item 9, §2 / SIGNALR_PROTOCOL.md §3.2.29: the
        // Player's board cycle is closed by its MatchedGemSummary, exactly once and
        // as that cycle's last item — after the cycle's last recorded pass's
        // CascadeStepResolved (the tail of `result.Events`) and before step 10's
        // Passive reports below. The counts are the cycle's own cleared-Gem
        // accounting, read from the value the resource stage already generated:
        // every cascade pass of this committed Swap, one per unique cleared cell
        // (COMBAT_RULES.md §2 item 6, MATCH3_RULES.md §5.7, §5.8.2). Nothing is
        // re-counted here, and the event reports counts only — no damage, heal, DEF
        // buff, Power, or SkillCharge value is taken from it or put on it.
        events.Add(BattleEvent.ForMatchedGemSummary(result.Resources.ClearedGemCounts));

        events.AddRange(charged.Charges.Select(BattleEvent.ForPassiveCharged));
        events.AddRange(charged.Triggers.Select(BattleEvent.ForPassiveTriggered));

        // ===================================================================
        // Step 11: Trigger Relics — GAME_RULES.md §17 step 11
        // ===================================================================
        // The committed Swap's resolution state is one root event, and
        // RELIC_RULES.md §8.1 item 8 fixes the point its Conditions are read at:
        // after step 10 and before steps 12–14. `OnMatchCount`, `OnCombo`, and
        // `OnHpBelow` are that event's Triggers.
        //
        // The observation state states that point exactly. The board-resolution
        // stage applied this Swap's step-14 healing to `resolved.PetState` in the
        // same write-back (SwapExecutor's documented "steps 13 and 14 are state
        // writes that land on the same PetState"), but GAME_RULES.md §17 orders step
        // 11 before step 14, so `HpPercentageBelow` must not read the healed value:
        // for the Swap it is evaluated in, it "never reads a post-healing HP"
        // (§8.1 item 8). The Pet's HP at step 11 is therefore the value the Swap
        // began with — nothing between the Swap's start and step 11 writes it — and
        // every other member is read from the resolution's current state, because
        // those are the carriers an effect is applied to.
        resolved = ApplyRelicFiringPoint(
            battleId,
            resolved,
            RelicFiringPoint.BoardResolution,
            resolved.MatchCount,
            resolved.Combo,
            events,
            powerGainSource: null,
            observationState: resolved.PetState with { HP = state.PetState.HP });

        // RELIC_RULES.md §3.2 item 1: cascade iteration identity is
        // MATCH3_RULES.md §4.2's — depth 1 is the pass run on the board the
        // committed Swap produced and is NOT a Cascade, while depth ≥ 2 is, with
        // the Cascade's index within the Swap being d − 1. The resolution's passes
        // are exactly the passes that detected a Match, in depth order, so element
        // i of that list is detection pass i + 1 and therefore a Cascade exactly
        // when i ≥ 1.
        //
        // §3.2 items 2–3: each actual cascade iteration is an INDEPENDENT OnCascade
        // event, so the stage runs once per qualifying iteration and the cascades of
        // one Swap are never collapsed into one aggregated event. §3.2 item 4 adds
        // that this is not a §5 self-loop: §5 item 2's once-per-root-event safeguard
        // already carves out a Trigger that naturally repeats, and one iteration is
        // one such occurrence. Each call is its own root event, so the resolver's
        // per-instance safeguard is per iteration; a Relic that fires once per
        // iteration is firing once per occurrence, which is the documented behavior.
        //
        // Each iteration's own §17 step 11 point is the point the swap-level pass
        // just used (§3.2 item 2: "each is evaluated against the state at that
        // iteration's own GAME_RULES.md §17 step 11 point"), so the same observation
        // point is supplied: the HP the Swap began with, over the resolution's
        // current carriers. The observations are taken per call, so an iteration's
        // pass never rewrites what an earlier pass applied.
        //
        // The loop is empty when the Swap produced no Cascade, which is the
        // documented case of §4.3 item 1's terminating pass detecting no Match:
        // no Cascade occurs, so no OnCascade event is produced.
        if (result.Resolution.Passes.Count > 1
            && _relicConfiguration.TryGetValue(battleId, out var cascadeRelics)
            && cascadeRelics != null)
        {
            for (var cascadeIndex = 1; cascadeIndex < result.Resolution.Passes.Count; cascadeIndex++)
            {
                resolved = ApplyRelicFiringPoint(
                    cascadeRelics,
                    resolved,
                    RelicFiringPoint.CascadeIteration,
                    matchCount: 0,
                    combo: 0,
                    events,
                    powerGainSource: null,
                    observationState: resolved.PetState with { HP = state.PetState.HP });
            }
        }

        // ===================================================================
        // Step 13: Update Power — GAME_RULES.md §17 step 13
        // ===================================================================
        var powerBefore = resolved.PetState.Power;
        var petAfterPower = ResourceGenerator.ApplyPower(resolved.PetState, result.Resources);
        var matchPowerDelta = petAfterPower.Power - powerBefore;
        if (matchPowerDelta != 0)
        {
            resolved = resolved with { PetState = petAfterPower };
            events.Add(BattleEvent.ForPowerChanged(new PowerChangedEvent(
                PowerChangeSource.Match,
                matchPowerDelta,
                petAfterPower.Power)));
        }

        // RELIC_RULES.md §3.1: OnPowerGain reacts to qualifying Power gains — the
        // ones originating OUTSIDE Relic effect resolution. Step 12 generated this
        // Swap's Power from its cleared POWER Gems and step 13 wrote it, so an
        // increase here is exactly such a gain (PowerChangeSource.Match), and the
        // mutation just reported is the event the Trigger observes. A Swap that
        // generated none, or whose generation the 0–100 cap wholly absorbed, leaves
        // Power unchanged and therefore produces no qualifying gain.
        //
        // The source is passed to the stage so the qualification is decided by the
        // canonical rule rather than by this call site: a Power gain whose source is
        // Relic effect resolution resolves no Relic at all (§3.1 items 1–2, §8.5
        // item 7), which is what makes
        // OnPowerGain → Arcane Battery → +5 Power → OnPowerGain impossible. The
        // Power an eligible Relic grants below is itself a Relic-owned mutation and
        // is never re-dispatched as a gain — no depth limit is involved.
        if (matchPowerDelta > 0)
        {
            resolved = ApplyRelicFiringPoint(
                battleId,
                resolved,
                RelicFiringPoint.PowerGain,
                matchCount: 0,
                combo: 0,
                events,
                PowerChangeSource.Match);
        }

        // ===================================================================
        // Steps 15–17 (ADR-026 D1): Player → Boss damage, ATK-gated
        // ===================================================================
        // ADR-026 D1 / COMBAT_RULES.md §2 item 6, §3 (OD-6): damage is calculated
        // and applied ONLY when the acting side's own resolution contains at least
        // one confirmed valid ATK-gem match. The qualification is READ from this
        // Swap's own cascade resolution (`result.Resolution`) — its confirmed
        // Matches, which the cascade loop already produced — and never from the
        // cleared-gem counts, the ATK damage pool, or a Combo value: those are
        // reporting quantities (COMBAT_RULES.md §2 item 6). AttackResolution owns the
        // gate, the Damage Pipeline's formula, the defender's Shield absorption, and
        // the qualifying-attack consumption boundary; this boundary orders the call,
        // supplies the values the earlier stages already produced, and writes back
        // what Domain returned. It decides no step of the formula and computes no
        // gameplay value (ARCHITECTURE.md §2.1).
        //
        // The inputs are read from the states the resolution is already committed
        // to, never re-derived:
        //   - step 1's ATK      — the effective ATK derived from PetState.ATK
        //                         (GAME_STATE.md §2.3, ADR-011 item 3) and the
        //                         Pet's active TargetStat="ATK" BuffDebuff
        //                         instances (COMBAT_RULES.md §5.4.1). Derived for
        //                         THIS call only and never written back — §5.4.4
        //                         forbids overwriting the stored stat and forbids
        //                         persisting the derived value.
        //   - step 1's pool     — result.Resources.BaseDamagePool, the transient
        //                         pool step 12 generated (GAME_STATE.md §3).
        //   - step 2's selector — BattleState.Combo (GAME_STATE.md §2.2), this
        //                         Swap's Match total, a root member
        //   - step 3's elements — PetState.Element (attacker) and
        //                         BossState.Element (defender), both set at battle
        //                         creation and never rewritten (ELEMENT_RULES.md §5)
        //   - step 4's crit     — PetState.Crit (COMBAT_RULES.md §3.3)
        //   - step 5's DEF      — BossState.DEF (GAME_STATE.md §2.4)
        //
        // A Swap whose resolution contains no confirmed ATK-gem match is NOT a
        // qualifying attack: the pipeline does not run, the Pet's ATK stat is not
        // consumed, zero damage is dealt, and no
        // DamageCalculated/DamageDealt/DamageTaken is emitted. The Pet's
        // NextAttack modifiers are preserved unconsumed — the consumption
        // boundary is the qualifying attack, not the Turn (ADR-026 D1,
        // COMBAT_RULES.md §3.3 item 8).
        var playerAttack = AttackResolution.ResolveAndApplyPlayerAttack(
            resolved.PetState,
            bossState,
            AttackQualification.FromResolution(result.Resolution),
            baseDamagePool: result.Resources.BaseDamagePool,
            combo: resolved.Combo,
            rngState: resolved.RngState,
            comboModifiers: ComboModifiers.Default,
            elementModifiers: ElementModifiers.Default);

        resolved = resolved with
        {
            PetState = playerAttack.Pet,
            RngState = playerAttack.Result.RngState,
        };

        // §17 step 17 / GAME_STATE.md §5.1: the Boss's post-damage HP and Shield are
        // part of the SAME single post-resolution write-back as the board, the
        // counters, the committed pair, the Match/Combo values, the settled Passive
        // progress, and the cooldown decrement — never a second write.
        bossState = playerAttack.Boss;

        // GAME_EVENTS.md §1/§2: the three Damage events follow the Passive stage's
        // reports, in the order §1 places them — DamageCalculated, DamageDealt,
        // DamageTaken. They are the resolution's own values, appended unchanged, and
        // the list is empty for a non-qualifying attack (ADR-026 D1).
        events.AddRange(playerAttack.Result.Events);

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
        var bossConfiguration = _bossConfiguration[battleId];
        var bossDefinition = bossConfiguration.Definition;

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
            //
            // ARCHITECTURE.md §4 item 4 / DATABASE.md §1: this is the documented
            // terminal end signal — the resolution reports the outcome as
            // BattleWon (GAME_EVENTS.md §2, because GAME_STATE.md §2.0.3 forbids a
            // lifecycle field on the state). The durable battle-end step itself
            // runs in the caller, after the state has been committed
            // (REDIS_STATE.md §3, §4 items 2–3).
            events.Add(BattleEvent.ForBattleWon(bossState.HP, resolved.PetState.HP));

            // §17 step 19 (End Turn) is ordered AFTER step 18, and BOSS_RULES.md
            // §5 item 4's "no Boss Response" ends only steps 18a–18c — not the
            // Turn itself. The Turn this action began is still resolved, so step
            // 19a's once-per-resolved-Turn consumption runs here too, before the
            // single write-back (§5.1.1 items 2 and 9). Skipping it would make the
            // consumption depend on which side died, which no rule states.
            //
            // No Boss Passive ran on this path (§5 item 4: no Boss Response), so no
            // once-per-battle firing was activated and none is reported.
            return new SwapResolution(
                result
                    .WithEvents(events)
                    .WithState(StatusEffectLifecycle.ConsumeAtStep19a(
                        resolved with { BossState = bossState })),
                ActivatedOncePerBattleBossPassive: null);
        }

        // ===================================================================
        // Step 9: Boss board action — GAME_RULES.md §17 step 18a, ADR-026 §2/§8
        // ===================================================================
        // The shared-board contract makes the Boss an actor on the same 8×8 board
        // the Player acts on: the Boss response phase begins by selecting one legal,
        // match-producing swap and resolving it through the established Match-3
        // pipeline. BossAiEngine owns the selection — the 112 orthogonal candidates
        // scored by the documented weights, tie-broken by the lowest cell index,
        // with a bounded deterministic Pcg32 reshuffle only when the board has no
        // legal swap at all (ADR-026 §8). This boundary supplies the authoritative
        // board and the retained RngState and reads back the move, the board it must
        // be resolved on, and the state the stream reached.
        //
        // The Boss's swap is resolved by the SAME stages the Player's is (exchange →
        // CascadeResolver → ResourceGenerator); this is not a second Match-3
        // pipeline. It deliberately writes nothing else the Player phase owns:
        //   * Turn and Sequence advance exactly once for the whole atomic round, in
        //     the Player action's own write-back — never again here (ADR-026 D4,
        //     MATCH3_RULES.md §8.1–§8.2);
        //   * LastCommittedSwapPair stays the client action's identity, because the
        //     action-idempotency window is the Player's action and nothing else
        //     (ADR-025, GAME_STATE.md §2.1.10 item 5);
        //   * Combo and MatchCount stay the Player action's, because both are
        //     "Player Matches" counters (GAME_STATE.md §2.2, BOSS_RULES.md §6.2.5's
        //     "Player Combo ≥ 4" trigger and §6.2's "every N Player Matches").
        var bossBoardRngState = resolved.RngState;

        var bossMove = BossAiEngine.SelectMove(
            resolved.BoardState,
            new Pcg32(bossBoardRngState.State, bossBoardRngState.Increment));

        // ADR-029 §2.7 / BOSS_RULES.md §4.3 (TASK-308): exactly ONE action is
        // selected for the response phase, before any of its work runs. The policy
        // is a pure Domain function of the committed state and the frozen content
        // inputs — it consumes no PRNG, reads no clock and no client input, and its
        // candidate enumeration, board score and recovery board remain
        // BossAiEngine's (ADR-026 §8).
        //
        //   board available = the post-recovery enumeration found a legal swap
        //   cast available  = the Boss has its provisioned Boss Card, its
        //                     post-decrement Power covers the composed cost, and
        //                     its post-decrement cooldown is 0
        //   selection       = the only available action; otherwise the higher
        //                     score, where the cast must STRICTLY out-score the
        //                     board (a tie is the board's)
        //
        // An unmigrated Boss has no Boss Card, so its cast is never available and
        // the policy reduces to today's board action (BOSS_RULES.md §4.4 item 6).
        var bossCard = bossConfiguration.BossCard;

        var boardActionAvailable = bossMove.LegalCandidatesCount > 0;
        var castAvailable = false;

        if (bossCard is not null)
        {
            // CARD_RULES.md §3 item 9 / §3.6: the Boss actor composes the cost with
            // an EMPTY modifier set (Relics are Player-only), so the composed value
            // equals the authored PowerCost. It is computed once here and is the
            // same value the executor validates and deducts (§3.6 item 7).
            var effectiveCardCost = EffectiveCardCost.Compose(
                bossCard.PowerCost,
                Array.Empty<CardCostModifier>());

            castAvailable = bossState.Power >= effectiveCardCost
                && bossState.SkillCooldown == 0;
        }

        var bossAction = BossActionPolicy.Select(
            boardActionAvailable,
            castAvailable,
            bossMove.Score,
            bossDefinition.CastPriorityScore);

        var bossBoardActionSelected = bossAction.Action == BossActionKind.BoardAction;
        var bossCastSelected = bossAction.Action == BossActionKind.BossCardCast;

        // ADR-026 §8 / BOSS_RULES.md §4.3 item 4: a cast-selected phase may still
        // carry the pre-selection deadlock reshuffle and its seeded RNG
        // consumption. The recovered board and the stream it reached are carried
        // into the round's single write-back; the reshuffle emits no event and
        // resolves no board cycle.
        if (bossCastSelected && bossMove.Reshuffled)
        {
            resolved = resolved with
            {
                BoardState = bossMove.Board,
                RngState = bossMove.RngState,
            };
        }

        // The board action's own resolution values, assigned only on the
        // board-selected path below and read only by that path's step 18d. A
        // cast-selected or neither-action phase resolves no board cycle
        // (BOSS_RULES.md §4.3 item 4), so these stay absent and are never read.
        var bossResolution = default(CascadeResolver.CascadeResult);
        var bossGeneration = default(ResourceGeneration);

        if (bossBoardActionSelected)
        {
            // §8: a deadlock (zero legal swaps) is recovered by the engine's own bounded
            // deterministic reshuffle, which is the only case in which the board and the
            // stream move here; otherwise the board and the retained state are unchanged.
            var bossBoard = bossMove.Board;
            bossBoardRngState = bossMove.Reshuffled ? bossMove.RngState : bossBoardRngState;

            bossResolution = CascadeResolver.Resolve(
                bossBoard.WithSwapped(bossMove.SelectedMove.From, bossMove.SelectedMove.To),
                new Pcg32(bossBoardRngState.State, bossBoardRngState.Increment));

            bossGeneration = ResourceGenerator.Generate(bossResolution);

            // GAME_EVENTS.md §1/§1.1, MATCH3_RULES.md §2.1.6 steps 3 and 7,
            // SIGNALR_PROTOCOL.md §3.2.26–§3.2.27: the Boss's own board action opens
            // with its own boundary pair, exactly as the Player's does. The actor is
            // the Boss side, and the coordinates are the deterministic AI's selected
            // pair — the two cells the step-18a action accepted on this board — placed
            // before the cycle they resolved, which is the order §1 fixes. They are
            // reported, not re-derived: the swap below is applied to the very pair the
            // event names, so the delivered coordinates and the resolved exchange
            // cannot disagree. The outcome is `committed` because a board action that
            // reached this point is committed; a rejected action never gets here and
            // emits nothing at all (§2.1.5 item 6).
            events.Add(BattleEvent.ForSwapStarted(
                DamageParty.Boss,
                bossMove.SelectedMove.From,
                bossMove.SelectedMove.To));
            events.Add(BattleEvent.ForSwapResolved(committed: true));

            // GAME_EVENTS.md §1.1: the Boss's own Match-3 cycle reports exactly as the
            // Player's does — the builder reads the resolution the pipeline produced and
            // imposes no order of its own — **except** for the one counter the Boss
            // cycle does not own. The Boss's Matches are not the Player's, and
            // `BattleState.Combo` is the Player action's swap-scoped counter
            // (GAME_STATE.md §2.2, BOSS_RULES.md §6.2.5's "Player Combo ≥ 4"), so this
            // phase passes no accounted Combo: the cycle's Matches are reported by
            // MatchCreated/GemMatched, and no `ComboChanged` is emitted for them,
            // because the payload of that event is the new value of `BattleState.Combo`
            // (GAME_EVENTS.md §2, SIGNALR_PROTOCOL.md §3.2.8 item 1) — a value this
            // phase does not write (see the Combo/MatchCount note above). A
            // `ComboChanged` carrying the Boss resolution's own Match total would
            // report a Combo the battle's counter never held.
            events.AddRange(BattleEventBuilder.Build(bossResolution, accountedCombo: null));

            // GAME_EVENTS.md §1.1 item 9, §2 / SIGNALR_PROTOCOL.md §3.2.29: the Boss
            // Response phase's board cycle is closed by its own MatchedGemSummary, once
            // and as that cycle's last item — after the cycle's last recorded pass's
            // CascadeStepResolved and before the step-18b/18c/18d events below. It is a
            // SEPARATE item from the Player's, carrying only the Boss's counts: the
            // counts of the two cycles are never combined, and ADR-026 §4's
            // actor-relative ownership means the Boss's cleared cells are the Boss's
            // (the same `bossGeneration` the step-18a effects were applied from). This
            // point is unreachable when the Player's attack ended the battle — that
            // path returns at the Boss HP terminal check above — so a victory before
            // the Boss cycle emits no Boss summary.
            events.Add(BattleEvent.ForMatchedGemSummary(bossGeneration.ClearedGemCounts));

            // ADR-026 §4: cell type determines the effect and the acting side determines
            // ownership and target — for the Boss's DEF Gems the temporary DEF buff is the
            // Boss's (COMBAT_RULES.md §5.5.6, TASK-299), for its HP Gems the heal is the
            // Boss's, and its POWER Gems (and only its
            // POWER Gems) charge BossState.SkillCharge, +1 per match group and clamped to
            // the Skill's ChargeRequirement (D2, T1, T2). The Player's POWER matches never
            // reach this call. ResourceGenerator owns every one of those rules; this
            // boundary only orders the call and writes back what it returned.
            var bossMatchEffects = ResourceGenerator.ApplyActorMatchEffects(
                DamageParty.Boss,
                resolved.PetState,
                bossState,
                bossGeneration,
                bossResolution,
                bossDefinition.SkillChargeRequirement);

            resolved = resolved with
            {
                BoardState = bossResolution.Board,
                PetState = bossMatchEffects.Pet,
                RngState = bossResolution.RngState,
            };

            bossState = bossMatchEffects.Boss;
        }

        // ===================================================================
        // Step 9b: Boss Passive — GAME_RULES.md §17 step 18b
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
        // Thủy Ma is the documented exception: §6.2 gives it the trigger
        // "Battle Start" (the wording was corrected from the retired "always
        // active" form by TASK-124) — an alternate trigger (PASSIVE_RULES.md §3),
        // not a Match count — and states it "is never charged via
        // PassiveTracker.Charge on Player Matches, and emits no
        // PassiveCharged/PassiveTriggered from match progress". Its
        // PassiveThreshold is therefore the non-charged marker (null, read back
        // as 0) rather than a threshold, and the charge is skipped entirely.
        // The match-charged Boss Passives' EFFECTS are applied below, at
        // GAME_RULES.md §17 step 18b (BOSS_RULES.md §6.2.1, §6.2.3): a trigger
        // emits its event and applies that Boss's declared effect.
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

            // GAME_RULES.md §17 step 18b / BOSS_RULES.md §6.2: MVP Boss Passive Effects
            if (bossPassive.Triggers.Count > 0)
            {
                if (string.Equals(bossState.PassiveId.Value, "boss-hoa-long-rage", StringComparison.Ordinal))
                {
                    // BOSS_RULES.md §6.2.1: +20% ATK for 3 turns, Turn-based BuffDebuff with TargetStat = "ATK".
                    // Re-trigger refreshes duration to 3 turns, does not stack.
                    var rageEffect = StatusEffect.TurnBased(
                        "boss-hoa-long-rage",
                        StatusEffectType.BuffDebuff,
                        StatusEffectSource.Boss,
                        magnitude: 20,
                        duration: 3,
                        targetStat: "ATK");

                    bossState = bossState with
                    {
                        ActiveStatusEffects = StatusEffectLifecycle.Apply(bossState.ActiveStatusEffects, rageEffect),
                    };
                }
                else if (string.Equals(bossState.PassiveId.Value, "boss-moc-yeu-regen", StringComparison.Ordinal))
                {
                    // BOSS_RULES.md §6.2.3: heals 5% MaxHP per trigger, truncated toward zero, clamped to MaxHP.
                    var regenPerTrigger = (bossState.MaxHP * 5) / 100;
                    var totalRegen = regenPerTrigger * bossPassive.Triggers.Count;
                    bossState = bossState with
                    {
                        HP = Math.Min(bossState.HP + totalRegen, bossState.MaxHP),
                    };
                }
            }
        }

        // ===================================================================
        // Step 9c: Non-match-charged Boss Passives — GAME_RULES.md §17 step 18b
        // ===================================================================
        // The block above is driven by PassiveTracker.Charge, which is the
        // MATCH-CHARGED path (PASSIVE_RULES.md §2): it only runs when
        // PassiveThreshold > 0. A Boss whose PassiveId records a `null` threshold
        // is by definition NOT match-charged (DATABASE.md §1 note item 3), so it
        // never reaches it — `null` means "no match-charging threshold", NOT
        // "always active" (the retired §6.2 wording TASK-124 corrected), and it
        // must never fall through to an unconditional application.
        //
        // BOSS_RULES.md §6.2.4/§6.2.5 give those two Passives an explicit
        // THRESHOLD trigger instead, evaluated at the same documented point —
        // step 18b, against the POST-DAMAGE battle state (§3.3 item 1). This is
        // the same trigger-evaluation point, not a second one: the Passive fires
        // once per player action, after all player damage is resolved, and before
        // the Skill (step 18c), exactly as §3.3 orders it.
        //
        // Sơn Thạch Vệ (§6.2.4) — `Boss HP ≤ 50%`, category Boss HP:
        //   * §6.2.4 is explicit that this is NOT the Enrage transition. The two
        //     boundaries coincide numerically (EnrageThreshold 1500 with MaxHP 3000
        //     is 50%) but are separate contract concepts with different operators:
        //     §5 item 4's Enrage uses strict `<`, this trigger uses `≤`. So the
        //     guard below reads bossState.HP directly and does NOT test
        //     `State == Enraged`.
        //   * It is ONE-TIME — at most once per battle. §6.2.4 states "it does not
        //     re-trigger once it has activated", and its reset behavior is the
        //     non-default No reset / persistent form whose storage token is
        //     `Persistent` (PASSIVE_RULES.md §4's Boss Passive clause, items 2–3;
        //     DATABASE.md §1 note item 3). That clause makes `Persistent` govern
        //     FIRING ELIGIBILITY for a Boss Passive: eligibility is consumed by the
        //     first activation and is NOT restored when the 3-Turn effect expires at
        //     step 19a, nor by remaining at or below 50% HP, nor by leaving and
        //     re-entering the threshold.
        //   * The guard is therefore the battle session's consumed-firing record for
        //     this Boss Passive, keyed by the battle and the Passive identity
        //     (TryConsumeOncePerBattleBossPassiveFiring below) — NOT the presence of
        //     the temporary instance. The instance is removed with the effect at
        //     step 19a, so reading it as the fired marker re-armed the Passive on
        //     the next Turn that satisfied `HP ≤ 50%`; the consumed-firing record
        //     survives the effect and keeps the Passive one-time. §6.2.4 adds no
        //     `BossState` member and `PassiveProgress.Current` is not repurposed, so
        //     the eligibility is session-scoped rather than state.
        //   * Applying it here rather than in a later Turn is what makes the
        //     one-time property hold: the activation and its consumption happen in
        //     the same resolution that first satisfies the trigger.
        //
        // Kim Lôi Vương (§6.2.5) — `Player Combo ≥ 4`, category Combo:
        //   * `Player Combo` is the resolution's own root Combo counter
        //     (GAME_STATE.md §2.2), read here — not re-derived and not a second
        //     Combo source.
        //   * Reset behavior is the documented Default (PASSIVE_RULES.md §4 item
        //     1) and a re-trigger follows the existing refresh-not-stack default
        //     (COMBAT_RULES.md §5.2 item 2, §5.5.5). StatusEffectLifecycle.Apply
        //     already implements exactly that (one instance per identity, refresh
        //     duration, never stack magnitude), so this reuses it unchanged and
        //     authors no new stacking model.
        //
        // Both effects reuse the existing Boss ATK modifier representation — a
        // Turn-based `BuffDebuff` in BossState.StatusEffects[] with
        // `TargetStat = "ATK"` and `Magnitude = +20` — consumed by the unchanged
        // COMBAT_RULES.md §5.5 rule. No new status type, `TargetStat`, event, or
        // state member is introduced. BOSS_RULES.md §6.2.4/§6.2.5 each state that
        // the application does not retroactively modify damage already resolved
        // earlier in that Turn, which is why it is applied here, after step 6.
        if (string.Equals(bossState.PassiveId.Value, "son-thach-ve-enrage", StringComparison.Ordinal))
        {
            // §6.2.4: `Boss HP ≤ 50%` of MaxHP, evaluated against the post-damage
            // HP. The operator is `≤`, deliberately distinct from §5 item 4's
            // strict `<` for the Enrage transition.
            var halfMaxHp = bossState.MaxHP * 50 / 100;

            // §6.2.4 / PASSIVE_RULES.md §4's Boss Passive clause: the Passive may
            // fire AT MOST ONCE PER BATTLE. The definition declares the non-default
            // Persistent reset behavior (§6.2), which is what selects the
            // once-per-battle firing limit, and this battle session's record of an
            // already-consumed firing is what refuses every later Turn.
            //
            // The guard is deliberately NOT the presence of the applied instance:
            // that instance is the effect, not the fired marker, and it is removed
            // at step 19a — reading it would let the next eligible Turn re-apply the
            // Rage, which is the retrigger defect this contract closes.
            //
            // The eligibility is READ here and consumed by the caller only once this
            // resolution is the committed one, so a refused Sequence compare-and-set
            // leaves the retried resolution able to activate the same firing
            // (REDIS_STATE.md §4 item 6).
            if (bossState.HP <= halfMaxHp
                && IsBossPassiveFiringEligible(battleId, bossDefinition, bossState.PassiveId))
            {
                activatedOncePerBattleBossPassive = bossState.PassiveId;

                // §6.2.4: +20% ATK for 3 Turns, the §6.2.1 Rage representation.
                // Applied with the existing StatusEffectLifecycle.Apply, so at
                // most one instance exists per identity.
                var rageOnHpThreshold = StatusEffect.TurnBased(
                    "son-thach-ve-enrage",
                    StatusEffectType.BuffDebuff,
                    StatusEffectSource.Boss,
                    magnitude: 20,
                    duration: 3,
                    targetStat: "ATK");

                bossState = bossState with
                {
                    ActiveStatusEffects = StatusEffectLifecycle.Apply(
                        bossState.ActiveStatusEffects,
                        rageOnHpThreshold),
                };
            }
        }
        else if (string.Equals(bossState.PassiveId.Value, "kim-loi-vuong-combo", StringComparison.Ordinal))
        {
            // §6.2.5: `Player Combo ≥ 4`, against the post-damage battle state.
            if (resolved.Combo >= 4)
            {
                // §6.2.5: +20% ATK for 1 Turn, the §6.2.1 Rage representation. The
                // existing Apply refreshes the instance to the full 1-Turn
                // duration without stacking magnitude (COMBAT_RULES.md §5.2 item
                // 2, §5.5.5) — the documented Default reset/reapplication behavior.
                var rageOnComboThreshold = StatusEffect.TurnBased(
                    "kim-loi-vuong-combo",
                    StatusEffectType.BuffDebuff,
                    StatusEffectSource.Boss,
                    magnitude: 20,
                    duration: 1,
                    targetStat: "ATK");

                bossState = bossState with
                {
                    ActiveStatusEffects = StatusEffectLifecycle.Apply(
                        bossState.ActiveStatusEffects,
                        rageOnComboThreshold),
                };
            }
        }

        // ===================================================================
        // Step 10: Boss action resolution — GAME_RULES.md §17 steps 18c–18d
        // ===================================================================
        // Step 18c is where the action step 18a selected resolves
        // (BOSS_RULES.md §4.3; ADR-029 §2.7). Exactly ONE form is active per
        // ability (BOSS_RULES.md §4.4; ADR-029 §2.5 as amended by TASK-308), so the
        // legacy Skill gate below runs only for the transitional BossSkill form: a
        // migrated Boss never reads SkillCharge as a gate and never emits
        // BossSkillCast for the same ability resolution. Its SkillCharge stays
        // inert — written only by the transitional generator on a board-selected
        // phase, never read (TASK-307 §2.2 C-7).
        //
        // GAME_STATE.md §2.4.3 / BOSS_RULES.md §6.3: the Skill is eligible on BOTH
        // documented conditions — SkillCharge has reached the ChargeRequirement AND
        // SkillCooldown is 0. The charge itself is the Boss's own POWER matches',
        // accumulated at step 18a above (ADR-026 D2): this step no longer adds the
        // Player's Matches to the counter, because Player POWER matches never charge
        // the Boss and the Player's other Matches never did under D2.
        //
        // A Boss skill deals NO direct damage (ADR-026 D3): it modifies the boss's
        // own combat modifiers, the Pet's Power, or the Pet's combat modifiers, and
        // the only damage a Boss deals is the ATK-match-gated instance at §18d below.
        var skillFires = bossCard is null
            && bossState.SkillCharge >= bossDefinition.SkillChargeRequirement
            && bossState.SkillCooldown == 0;

        if (skillFires)
        {
            // SIGNALR_PROTOCOL.md §3.2.18: the Skill is announced when it fires. Both
            // identities are the Boss's own — the definition's SkillId and the
            // canonical technical Identity BossState.BossId.
            events.Add(BattleEvent.ForBossSkillCast(bossDefinition.SkillId, bossState.BossId.Value));

            // -------------------------------------------------------------------
            // Step 10b: Boss Skill secondary effect — BOSS_RULES.md §6.3.1, ADR-026 D5–D7
            // -------------------------------------------------------------------
            // GAME_RULES.md §17 step 18c: a Skill that fires applies its declared
            // non-damage effect, "outside the pipeline" (COMBAT_RULES.md §3.4). The
            // declaration is read from the Boss's own definition
            // (BossSkillDefinition.SecondaryEffect), so no SkillId string dispatch
            // occurs here and every magnitude and duration stays the definition's.
            //
            // The five MVP Skills' effects are the ADR-026-approved ones:
            //   flame-burst     AtkBuff      temporary Boss ATK buff (D5)
            //   drain-power     PowerDrain   -20 flat Pet Power (instant)
            //   root            AtkDebuff    -30% Pet ATK for 2 Turns
            //   earthquake      BossShield   temporary Boss Shield (D6)
            //   thunder-strike  NextAtkBuff  next qualifying Boss ATK buff (D7)
            // Burn DoT execution is retired (ADR-026 D3/D5) and no longer appears in
            // this resolution: no DoT tick is executed at step 19a either.
            //
            // No event is emitted for an effect: GAME_RULES.md §16's canonical list is
            // closed and BOSS_RULES.md §7 enumerates step 18's events. The effects are
            // authoritative BattleState mutation and reach the client as settled state
            // through the existing BattleStateUpdated projection
            // (SIGNALR_PROTOCOL.md §4.3 item 14). Drain Power is the one exception
            // below, and only because an existing PowerChanged event owns every
            // authoritative Power mutation (GAME_EVENTS.md §2).
            if (bossDefinition.SkillDefinition.SecondaryEffect is { } skillEffect)
            {
                // The Status Effect instance the declaration builds, or null for the one
                // instance-less kind (Drain Power, GAME_STATE.md §2.3.1 item 9).
                // BossSkillDefinition.ToStatusEffect owns the construction — including the
                // Turn-based / trigger-based choice its ExpiryCondition or DurationTurns
                // selects — so no magnitude, duration, identity, or source is authored
                // here.
                var declaredInstance = skillEffect.ToStatusEffect();

                if (declaredInstance is null && skillEffect.Kind != BossSkillSecondaryEffectKind.PowerDrain)
                {
                    throw new InvalidOperationException(
                        $"Boss Skill '{bossDefinition.SkillId}' declares a '{skillEffect.Kind}' "
                        + "secondary effect without the instance values BOSS_RULES.md §6.3.1 requires.");
                }

                switch (skillEffect.Kind)
                {
                    case BossSkillSecondaryEffectKind.AtkBuff:
                    {
                        // ADR-026 D5 (Option 5B): Hỏa Long's Flame Burst applies a
                        // temporary Boss ATK buff to the Boss's own
                        // ActiveStatusEffects. COMBAT_RULES.md §5.5.1 consumes a
                        // Turn-based TargetStat="ATK" BuffDebuff at the Boss attack's
                        // Step 1, so the buff reaches the Boss's next qualifying ATK
                        // match and never deals damage on its own.
                        //
                        // COMBAT_RULES.md §5.2 item 2 / GAME_STATE.md §2.3.1 item 6:
                        // re-applying an already-active identity refreshes that one
                        // instance (duration and magnitude re-set) rather than
                        // appending a second, which is exactly Apply.
                        bossState = bossState with
                        {
                            ActiveStatusEffects = StatusEffectLifecycle.Apply(
                                bossState.ActiveStatusEffects,
                                declaredInstance.GetValueOrDefault()),
                        };
                        break;
                    }

                    case BossSkillSecondaryEffectKind.NextAtkBuff:
                    {
                        // ADR-026 D7 (Option 3A): Kim Lôi Vương's Thunder Strike
                        // installs a trigger-based TargetStat="ATK" BuffDebuff on the
                        // Boss whose ExpiryCondition is "NextQualifyingAttack". It is
                        // not on the step 19a countdown (GAME_STATE.md §5.1.1 item 7),
                        // so it survives every Turn on which the Boss produces no ATK
                        // match and is consumed only by the qualifying attack
                        // AttackResolution resolves — COMBAT_RULES.md §5.5.1 composes
                        // it into that instance's Step 1 in the meantime.
                        bossState = bossState with
                        {
                            ActiveStatusEffects = StatusEffectLifecycle.ApplyTriggerBased(
                                bossState.ActiveStatusEffects,
                                declaredInstance.GetValueOrDefault()),
                        };
                        break;
                    }

                    case BossSkillSecondaryEffectKind.BossShield:
                    {
                        // ADR-026 D6 (Option 2A): Sơn Thạch Vệ's Earthquake grants the
                        // Boss a temporary Shield on its own ActiveStatusEffects. It is
                        // the same trigger-based absorption pool COMBAT_RULES.md §4
                        // defines and the Damage Pipeline already consumes when the
                        // Boss is the defending target, and the same
                        // "at most one Shield per entity, refreshed not accumulated"
                        // operation (§4 item 3).
                        bossState = bossState with
                        {
                            ActiveStatusEffects = StatusEffectLifecycle.ApplyShield(
                                bossState.ActiveStatusEffects,
                                declaredInstance.GetValueOrDefault()),
                        };
                        break;
                    }

                    case BossSkillSecondaryEffectKind.PowerDrain:
                    {
                        // BOSS_RULES.md §6.3.1 item 2: "Instantly subtracts 20 flat
                        // Power from the active Pet (PetState.Power = max(0,
                        // PetState.Power - 20))". The floor at 0 is that item's own
                        // formula and GAME_RULES.md §12's 0-100 range.
                        //
                        // GAME_STATE.md §2.3.1 item 9: this creates NO Status Effect
                        // instance — it is an immediate PetState.Power mutation — so
                        // nothing is written to StatusEffects[] here. No duration,
                        // stacking, refresh, or reset behavior is invented: §6.3.1
                        // item 2 gives the reduction none.
                        var powerBeforeDrain = resolved.PetState.Power;
                        var powerAfterDrain = Math.Max(
                            0,
                            resolved.PetState.Power - (int)skillEffect.Magnitude);

                        resolved = resolved with
                        {
                            PetState = resolved.PetState with { Power = powerAfterDrain },
                        };

                        var drained = powerBeforeDrain - powerAfterDrain;
                        if (drained > 0)
                        {
                            events.Add(BattleEvent.ForPowerChanged(new PowerChangedEvent(
                                PowerChangeSource.Boss,
                                -drained,
                                powerAfterDrain)));
                        }
                        break;
                    }

                    case BossSkillSecondaryEffectKind.AtkDebuff:
                    {
                        // BOSS_RULES.md §6.3.1 item 3: Root applied to the active Pet —
                        // Id "Root", Type BuffDebuff, Source Boss, TargetStat "ATK",
                        // Magnitude 30, RemainingTurns 2.
                        //
                        // StatusEffect.TurnBased enforces the TargetStat-iff-BuffDebuff
                        // pairing at construction, and Apply carries the refresh
                        // semantics (COMBAT_RULES.md §5.3 DR3/DR4). Its one-Turn-of-
                        // duration consumption is the existing step 19a pass
                        // (COMBAT_RULES.md §5.3.2 — Root is Turn-based); no second
                        // consumption path is added.
                        //
                        // COMBAT_RULES.md §5.4 owns what the instance's Magnitude then
                        // does: §5.4.1 consumes it at the Player → Boss Damage Pipeline
                        // Step 1 Attack input through StatusEffectLifecycle.EffectiveAttack.
                        // §5.4.3 places this application AFTER the Player's own step-15
                        // attack, so Root does not retroactively modify the attack this
                        // same round already resolved — it applies to the next round's
                        // Player attack, and step 19a's 2 → 1 is the first of its two
                        // documented consumption Turns.
                        resolved = resolved with
                        {
                            PetState = resolved.PetState with
                            {
                                ActiveStatusEffects = StatusEffectLifecycle.Apply(
                                    resolved.PetState.ActiveStatusEffects,
                                    declaredInstance.GetValueOrDefault()),
                            },
                        };
                        break;
                    }

                    case BossSkillSecondaryEffectKind.Burn:
                        throw new InvalidOperationException(
                            "BOSS_RULES.md §6.3.1 item 1's Burn is retired by ADR-026 D3/D5: "
                            + "no MVP Boss declares it. A definition that still did would need its own "
                            + "recorded decision, not an implementation here.");

                    default:
                        throw new InvalidOperationException(
                            $"Unhandled Boss Skill secondary effect kind '{skillEffect.Kind}'.");
                }
            }

            // BOSS_RULES.md §6.3 / GAME_STATE.md §2.4.3: "After the Skill fires:
            // SkillCharge resets to 0, SkillCooldown resets to the Boss's cooldown
            // value."
            //
            // Cooldown ordering is the documented decrement → check → set sequence
            // (GAME_STATE.md §2.4.3, BOSS_RULES.md §6.3): each committed Round's
            // step 3 decrements the stored value by exactly 1, the gate above then
            // reads it, and a successful cast sets the authored value in this same
            // write-back. An authored N therefore blocks the N−1 subsequent rounds
            // whose post-decrement value is still > 0; the Nth round is eligible
            // again, and N = 0 is eligible every round. The value set here is the
            // authored one — the next Round's step-3 decrement is its first tick.
            //
            // The reset discards the charge the Boss's POWER matches accumulated: the
            // cast consumes it. It does not touch PassiveProgress — §2.4.3 makes the
            // two independent counters, and neither resets the other.
            bossState = bossState with
            {
                SkillCharge = 0,
                SkillCooldown = bossDefinition.SkillCooldownTurns,
            };
        }
        else if (bossCastSelected)
        {
            // -------------------------------------------------------------------
            // Step 10c: Boss Card cast — GAME_RULES.md §17 step 18c, ADR-029 §2.7
            //           (TASK-308)
            // -------------------------------------------------------------------
            // The cast was selected at 18a because the Boss has its provisioned Boss
            // Card, its post-decrement Power covers the composed cost, and its
            // post-decrement cooldown is 0 (BOSS_RULES.md §4.3). It resolves through
            // the shared cast contract's Boss actor, which owns the category check,
            // the empty-modifier cost composition, the Power sufficiency check, the
            // representability gate, the deduction, and the actor-relative effect
            // resolution (CARD_RULES.md §3 item 9). The executor stays policy-free and
            // writes no cooldown; the phase owner applies the authored cooldown below
            // in the same single write-back (TASK-307 §3.5.6).
            //
            // TASK-307 §3.5.5: the 18c re-check is a fail-closed invariant, not a
            // second selection. Only step 18b ran between selection and execution, and
            // no MVP Passive can change the Boss's Power, cooldown or Boss Card, so on
            // valid data the cast cannot be rejected. If it is, the round fails closed
            // rather than silently resolving a board action or dropping the cast —
            // either would violate "exactly one action", the event/state contract and
            // the CAS-retry determinism guarantee (GAME_RULES.md §17 step 18).
            var bossCastState = resolved with { BossState = bossState };

            var cast = CardCastExecutor.Execute(bossCastState, bossCard!, CardCastActor.Boss);

            if (cast.IsRejected)
            {
                throw new InvalidOperationException(
                    $"Boss '{bossState.BossId.Value}' selected its Boss Card "
                    + $"'{bossCard!.CardDefinitionId}' at step 18a, but the 18c cast was "
                    + $"rejected as '{cast.Reason}'. Exactly one action must resolve per "
                    + "response phase (BOSS_RULES.md §4.3; GAME_RULES.md §17 step 18), so "
                    + "the round fails closed rather than performing a board action or "
                    + "dropping the cast.");
            }

            // BOSS_RULES.md §4.3 item 3 / §6.3: a successful cast sets the authored
            // cooldown in the SAME single write-back the executor's committed state
            // already is. Nothing decrements it here — step 3 already decremented this
            // committed Round's stored value exactly once, and it is never decremented
            // twice.
            var castBossState = cast.State.BossState with
            {
                SkillCooldown = bossDefinition.SkillCooldownTurns,
            };

            resolved = cast.State with { BossState = castBossState };
            bossState = castBossState;

            // SIGNALR_PROTOCOL.md §3.2.30 / GAME_EVENTS.md §2: exactly ONE
            // BossCardCast, emitted at 18c after the 18b Passive events and inside the
            // round's single ordered batch. `cardId` is the Boss Card definition
            // identity (equal to the ability SkillId) and `sourceId` is the canonical
            // technical Identity BossState.BossId — no cost, Power, cooldown, or effect
            // summary is carried. The Power spend emits no PowerChanged (C-6); the
            // resulting Boss Power and cooldown reach the client only through the
            // `bossState` projection.
            events.Add(BattleEvent.ForBossCardCast(
                bossCard!.CardDefinitionId,
                bossState.BossId.Value));
        }

        // When the legacy Skill did not fire (and no action was selected), the charge
        // simply carries what step 18a accumulated — the case BOSS_RULES.md §6.3
        // describes, where the Skill becomes eligible on a later round. A migrated
        // Boss's SkillCharge is inert and is never read as a gate.

        // ===================================================================
        // Step 11: Boss → Pet damage — GAME_RULES.md §17 step 18d, ADR-026 D1
        // ===================================================================
        // The legacy fallback basic attack (Base Damage = Boss.ATK independently of the
        // board) is RETIRED (ADR-026 §5). A Boss deals attack damage if and only if its
        // own swap's resolution contains a confirmed ATK-gem match — the qualification
        // is read from `bossResolution`, the Boss's own cascade result, and never from
        // its cleared-gem counts (COMBAT_RULES.md §2 item 6, OD-6) — and the instance
        // is resolved through the same ATK-gated AttackResolution the Player's is — the
        // Boss's Step 1 is EffectiveBossATK plus the ATK-Gem BaseDamagePool the Boss's
        // own cleared ATK Gems generated (COMBAT_RULES.md §3.4), the Skill's authored
        // BaseDamage is 0 by D3, and Step 2's Combo Modifier is 1 because Boss attacks
        // are not part of a Combo chain.
        //
        // EffectiveBossATK is derived within that one pipeline execution from
        // BossState.ATK and the Boss's own TargetStat="ATK" BuffDebuff instances —
        // including Flame Burst's buff applied above and Thunder Strike's
        // next-qualifying modifier while it is active (§5.5.1, §5.5.4: the stored stat
        // is never written back and no EffectiveBossATK member exists).
        //
        // The defender side is the player's (§3.4: Source = Boss, Target = Player): the
        // defending Element is the ACTIVE PET's, and the defending DEF, HP, and Shield
        // pool are the Pet's too (GAME_STATE.md §2.3, ADR-011 item 5). A round in which
        // the Boss's own resolution contains no confirmed ATK-gem match produces no
        // damage instance, no ATK-stat consumption, and no damage events.
        // ADR-029 §2.7 / BOSS_RULES.md §4.3 item 4: a cast-selected phase suppresses
        // the attack entirely — the Boss forgoes its board action and therefore its
        // §18d ATK-gated damage instance. A neither-action phase likewise deals no
        // damage. Only the board-selected action runs this step, which is why the
        // values it reads are the board branch's own (assigned above).
        if (bossBoardActionSelected)
        {
            var bossAttack = AttackResolution.ResolveAndApplyBossAttack(
                bossState,
                resolved.PetState,
                AttackQualification.FromResolution(bossResolution),
                baseDamagePool: bossGeneration.BaseDamagePool,
                combo: 1,
                rngState: resolved.RngState,
                comboModifiers: ComboModifiers.Default,
                elementModifiers: ElementModifiers.Default);

            bossState = bossAttack.Boss;

            resolved = resolved with
            {
                PetState = bossAttack.Pet,
                RngState = bossAttack.Result.RngState,
            };

            events.AddRange(bossAttack.Result.Events);

            // RELIC_RULES.md §3: OnDamageTaken "fires when the active Pet takes
            // damage". The instance above is the round's Boss→Pet damage instance and the
            // Pet's post-damage HP has just been written, so this is the documented firing
            // point when — and only when — that instance exists. A round whose Boss
            // resolution contains no confirmed ATK-gem match resolves no damage instance at
            // all (ADR-026 D1, COMBAT_RULES.md §2 item 6), so the
            // Trigger is not reached: the Pet took no damage.
            //
            // It is the existing damage-taken report, not a second damage-resolution path:
            // nothing here re-runs the pipeline, re-derives the damage, or re-applies HP.
            if (bossAttack.Result.IsQualifyingAttack)
            {
                resolved = ApplyRelicFiringPoint(
                    battleId,
                    resolved,
                    RelicFiringPoint.DamageTaken,
                    matchCount: 0,
                    combo: 0,
                    events);
            }
        }

        // ===================================================================
        // Step 12: Player-side terminal check — GAME_RULES.md §1.4
        // ===================================================================
        // The Pet HP terminal check — the Player side's HP, since the Pet is that
        // side's combat character (ADR-011 items 3 and 5) — runs after the Boss
        // phase, because the Boss has just had its chance to reduce it through the
        // §18d ATK-gated attack. §1.4 ends the battle when either side reaches 0;
        // the Boss was checked before the Boss phase began, so what remains is the
        // player's side — the active Pet's HP (GAME_STATE.md §2.3, ADR-011 item 5).
        //
        // When BOTH sides are alive, NEITHER outcome event is emitted and the battle
        // continues — the absence of an outcome is itself the documented statement
        // that the round was not terminal.
        if (resolved.PetState.HP <= 0)
        {
            // SIGNALR_PROTOCOL.md §3.2.19: the wire member stays finalPlayerHp — a
            // fixed protocol label — and carries this same Pet HP value
            // (ADR-011 item 6: the label is not renamed).
            events.Add(BattleEvent.ForBattleLost(bossState.HP, resolved.PetState.HP));
        }

        // ===================================================================
        // Step 13: End Turn step 19a — Status Effect duration/expiry
        //          GAME_RULES.md §17 step 19a, GAME_STATE.md §5.1.1
        // ===================================================================
        // §17 places End Turn (step 19) last, and step 19a is "the last combat
        // effect of the Turn — it runs after the Boss Response (step 18) — and it
        // fires once per resolved Turn". It therefore runs here, after both phases
        // of the atomic round and after the Boss's opportunity to reduce the Pet's
        // HP — and BEFORE the single write-back, so the committed state is the
        // post-19a state (§5.1.1 item 9). Step 19a remains one step in one fixed
        // position: no step is added, removed, or reordered, and this is the only
        // site that consumes Status Effect duration (COMBAT_RULES.md §5.3 DR2 —
        // "Exactly one decrement occurs per Turn, at GAME_RULES.md §17 step 19a —
        // regardless of how many apply/refresh operations occurred earlier in that
        // same Turn").
        //
        // The damage-over-time tick this step owns is RETAINED. ADR-026 D3/D5 retire
        // the *Boss Skill*'s Burn (BOSS_RULES.md §6.3.1 item 1's 150-damage +
        // 50/tick Burn), and no MVP Boss Skill declares a DoT any more — but
        // GAME_RULES.md §17 step 19a still requires this step to "tick each active
        // damage-over-time Status Effect (e.g. Burn) exactly once, through the Damage
        // Pipeline" and states the change "does not change the damage-over-time tick
        // itself"; COMBAT_RULES.md §5.1–§5.3 own that tick. An MVP Card still applies
        // one (CardCastExecutor's Burn effect writes a Pet-owned DoT onto the Boss),
        // so removing the execution would leave a documented effect inert.
        //
        // Damage-over-time ticks traverse the Damage Pipeline (COMBAT_RULES.md §3,
        // §5.2 item 3) using Fire element, Combo = 1, Crit evaluated, before duration
        // is decremented.
        // 1. Tick DoTs on Boss (if Boss is still alive and Pet did not die)
        if (bossState.HP > 0 && resolved.PetState.HP > 0)
        {
            var bossDots = bossState.ActiveStatusEffects
                .Where(e => e.Type == StatusEffectType.DoT)
                .OrderBy(e => e.Id, StringComparer.Ordinal)
                .ToList();

            foreach (var dot in bossDots)
            {
                if (bossState.HP <= 0)
                {
                    break;
                }

                var dotDamage = DamagePipeline.Calculate(
                    new DamagePipeline.DamageInputs(
                        Attack: (int)dot.Magnitude,
                        BaseDamagePool: 0,
                        Combo: 1,
                        AttackerElement: Element.Hoa,
                        DefenderElement: bossState.Element,
                        // COMBAT_RULES.md §3.2 / §5.5.6 (TASK-299): step 5 reads the
                        // defender's EFFECTIVE DEF, so an active Boss DEF-Gem buff
                        // prices this tick too. The stored BossState.DEF is never
                        // written.
                        DefenderDefense: StatusEffectLifecycle.EffectiveBossDefense(
                            bossState.DEF,
                            bossState.ActiveStatusEffects),
                        DefenderHp: bossState.HP,
                        Source: DamageParty.Player,
                        Target: DamageParty.Boss,
                        DefenderShieldPool: StatusEffectLifecycle.ShieldPool(bossState.ActiveStatusEffects),
                        AttackerCrit: resolved.PetState.Crit,
                        RngState: resolved.RngState,
                        OtherModifiers: BurnTickOtherModifiers(resolved.PetState, dot)),
                    ComboModifiers.Default,
                    ElementModifiers.Default);

                resolved = resolved with { RngState = dotDamage.UpdatedRngState };
                bossState = bossState with { HP = dotDamage.TargetHp };

                if (dotDamage.RemainingShieldPool == 0)
                {
                    bossState = bossState with
                    {
                        ActiveStatusEffects = StatusEffectLifecycle.RemoveDepletedShield(bossState.ActiveStatusEffects),
                    };
                }
                else if (dotDamage.AbsorbedDamage > 0)
                {
                    bossState = bossState with
                    {
                        ActiveStatusEffects = StatusEffectLifecycle.ApplyShield(
                            bossState.ActiveStatusEffects,
                            StatusEffect.TriggerBased("Shield", StatusEffectType.Shield, StatusEffectSource.Player, dotDamage.RemainingShieldPool, StatusEffect.ShieldDepletedCondition)),
                    };
                }

                events.Add(BattleEvent.ForDamageCalculated(dotDamage.Calculation));
                events.Add(BattleEvent.ForDamageDealt(dotDamage.DamageDealt));
                events.Add(BattleEvent.ForDamageTaken(dotDamage.DamageTaken));

                if (bossState.HP == 0)
                {
                    events.Add(BattleEvent.ForBattleWon(bossState.HP, resolved.PetState.HP));
                }
            }
        }

        // 2. Tick DoTs on Pet (if Pet is still alive and Boss did not die)
        if (resolved.PetState.HP > 0 && bossState.HP > 0)
        {
            var petDots = resolved.PetState.ActiveStatusEffects
                .Where(e => e.Type == StatusEffectType.DoT)
                .OrderBy(e => e.Id, StringComparer.Ordinal)
                .ToList();

            foreach (var dot in petDots)
            {
                if (resolved.PetState.HP <= 0)
                {
                    break;
                }

                var dotDamage = DamagePipeline.Calculate(
                    new DamagePipeline.DamageInputs(
                        Attack: (int)dot.Magnitude,
                        BaseDamagePool: 0,
                        Combo: 1,
                        AttackerElement: Element.Hoa,
                        DefenderElement: resolved.PetState.Element,
                        // COMBAT_RULES.md §3.2 / §5.4.6 (TASK-299): step 5 reads the
                        // defender's EFFECTIVE DEF, so the Pet's active DEF-Gem buff
                        // prices this tick too. The stored PetState.DEF is never
                        // written.
                        DefenderDefense: StatusEffectLifecycle.EffectivePetDefense(
                            resolved.PetState.DEF,
                            resolved.PetState.ActiveStatusEffects),
                        DefenderHp: resolved.PetState.HP,
                        Source: DamageParty.Boss,
                        Target: DamageParty.Player,
                        DefenderShieldPool: StatusEffectLifecycle.ShieldPool(resolved.PetState.ActiveStatusEffects),
                        AttackerCrit: 0,
                        RngState: resolved.RngState,
                        OtherModifiers: BurnTickOtherModifiers(resolved.PetState, dot)),
                    ComboModifiers.Default,
                    ElementModifiers.Default);

                resolved = resolved with
                {
                    RngState = dotDamage.UpdatedRngState,
                    PetState = resolved.PetState with { HP = dotDamage.TargetHp },
                };

                if (dotDamage.RemainingShieldPool == 0)
                {
                    resolved = resolved with
                    {
                        PetState = resolved.PetState with
                        {
                            ActiveStatusEffects = StatusEffectLifecycle.RemoveDepletedShield(resolved.PetState.ActiveStatusEffects),
                        },
                    };
                }
                else if (dotDamage.AbsorbedDamage > 0)
                {
                    resolved = resolved with
                    {
                        PetState = resolved.PetState with
                        {
                            ActiveStatusEffects = StatusEffectLifecycle.ApplyShield(
                                resolved.PetState.ActiveStatusEffects,
                                StatusEffect.TriggerBased("Shield", StatusEffectType.Shield, StatusEffectSource.Player, dotDamage.RemainingShieldPool, StatusEffect.ShieldDepletedCondition)),
                        },
                    };
                }

                events.Add(BattleEvent.ForDamageCalculated(dotDamage.Calculation));
                events.Add(BattleEvent.ForDamageDealt(dotDamage.DamageDealt));
                events.Add(BattleEvent.ForDamageTaken(dotDamage.DamageTaken));

                // RELIC_RULES.md §3: a DoT tick whose target is the Pet is damage
                // the active Pet took, so it is an OnDamageTaken firing point on the
                // same terms as the Boss Response's instance above. Each tick is its
                // own damage instance and therefore its own event; a re-triggering
                // source refreshes its own element rather than accumulating one
                // (non-stacking).
                resolved = ApplyRelicFiringPoint(
                    battleId,
                    resolved,
                    RelicFiringPoint.DamageTaken,
                    matchCount: 0,
                    combo: 0,
                    events);

                if (resolved.PetState.HP <= 0)
                {
                    events.Add(BattleEvent.ForBattleLost(bossState.HP, resolved.PetState.HP));
                }
            }
        }

        // 3. Duration decrement and expiry pass
        var afterStep19a = StatusEffectLifecycle.ConsumeAtStep19a(
            resolved with { BossState = bossState });

        resolved = afterStep19a;
        bossState = afterStep19a.BossState;

        // ===================================================================
        // Commit-boundary playable-board invariant — MATCH3_RULES.md §1.6
        // ===================================================================
        // The Round's last evaluation point, and the only one that guarantees the
        // board the Player is about to be given. Every cascade of the Round has
        // terminated and step 19a has run, so the board is the finished, stable
        // board this Round commits (§1.6.1 item 4); the terminal determination is
        // made on that same finished state and a terminal Round is exempt
        // (§1.6.4); and the result — board and RNG state together — is what the
        // caller's single Sequence compare-and-set writes, so recovery is one
        // round's one write-back and never a second write, a second Round, or a
        // separate persistence path (§1.6.2 item 1, GAME_STATE.md §5.1).
        //
        // It complements, and does not replace, the Boss's own pre-move recovery in
        // BossAiEngine.SelectMove (§1.6.5): that one covers the board the Player's
        // action left, so the Boss has a legal move; this one covers the board the
        // Boss's action and cascades left, so the Player has one. A board the
        // Boss's recovery already made playable is preserved untouched and the RNG
        // stream does not move (§1.6.2 item 3).
        //
        // A generated replacement is reported by the state it commits and by
        // nothing else: no Battle Event is added to `events` here and no
        // reshuffle member exists (§1.6.6, GAME_EVENTS.md §3 item 8, TASK-265
        // Option A).
        resolved = EnsurePlayableBoard(resolved with { BossState = bossState });

        // ===================================================================
        // The finished post-resolution state — GAME_STATE.md §5.1
        // ===================================================================
        // The caller performs the one write-back, under the Sequence
        // compare-and-set (REDIS_STATE.md §4 items 2 and 5), and — when this
        // resolution emitted BattleLost — the documented battle-end step that
        // follows it (ARCHITECTURE.md §4 item 4, DATABASE.md §1). A non-terminal
        // resolution emits neither outcome event, so nothing durable follows it.
        //
        // The activated once-per-battle firing (BOSS_RULES.md §6.2.4) travels back
        // with the result: it is consumed by the caller on the accepted-write path,
        // not here, so that a refused write leaves it for the retried resolution.
        return new SwapResolution(
            result
                .WithEvents(events)
                .WithState(resolved),
            activatedOncePerBattleBossPassive);
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

    /// <summary>
    /// The step 4 "Other Modifiers" factor a damage-over-time tick's damage is
    /// scaled by — the applied <c>BurnDamage</c> percentage when the tick's Burn is
    /// one the Pet owns, and the identity factor otherwise.
    ///
    /// <code>
    /// Pet-owned Burn   (Source = Player)  → 1 + (Σ BurnDamageModifiers) / 100
    /// Boss-owned Burn  (Source = Boss)    → 1.00  (unmodified)
    /// </code>
    ///
    /// <b>§5.2 item 4 and §6 note 1 own the scoping, and the test is the Burn
    /// instance's source.</b> "A <c>BurnDamage</c> modifier applies only to Burn
    /// instances owned by the source that modifier belongs to", and for Burning
    /// Curse the owned/source context is the Pet: Burn the Pet applied is modified
    /// and Burn the Boss applied is not, regardless of which entity is receiving the
    /// tick's damage (TASK-178 Product Owner decision <b>Q-4 = C</b>).
    /// <see cref="BurnDamageModifiers.AppliesTo"/> is that predicate, so a
    /// Boss-applied Burn ticking on the Pet reads <c>1.00</c> here and receives no
    /// bonus.
    ///
    /// <b>The modifier changes damage and nothing else.</b> It is supplied as step
    /// 4's existing factor input — <c>COMBAT_RULES.md</c> §3 step 4 already lists
    /// Relic bonuses — so it scales this tick's damage and the tick's own Burn
    /// instance, its magnitude, its identity, and its duration are all untouched:
    /// no Burn event is emitted, no Burn instance is created or refreshed, and the
    /// step 19a pass below is the only thing that consumes a turn of its duration
    /// (§5.2 item 3, §5.3). That is what keeps the MVP Relic case terminating.
    ///
    /// <b>No new pipeline step or input is introduced.</b> Step 4 is already a
    /// factor the caller supplies, and the identity it carries when nothing applies
    /// is <see cref="DamagePipeline.NoOtherModifiers"/>.
    /// </summary>
    /// <param name="petState">
    /// The active Pet's state — the owner whose <c>BurnDamageModifiers[]</c> are the
    /// modifiers in force for the Pet's own Burn.
    /// </param>
    /// <param name="burn">The Status Effect instance this tick resolves.</param>
    /// <returns>The step 4 factor for this tick.</returns>
    private static double BurnTickOtherModifiers(PetState petState, StatusEffect burn)
    {
        if (!BurnDamageModifiers.AppliesTo(burn))
        {
            return DamagePipeline.NoOtherModifiers;
        }

        var percentage = BurnDamageModifiers.AppliedPercentage(petState.BurnDamageModifiers);

        return (100 + percentage) / 100.0;
    }

    /// <summary>
    /// Runs the Relic stage's firing point for a battle whose content this boundary
    /// attached at creation — the instance-side wrapper over
    /// <see cref="ApplyRelicFiringPoint(IReadOnlyList{RelicDefinition?}, BattleState, RelicFiringPoint, int, int, List{BattleEvent}?, PowerChangeSource?)"/>
    /// that reads the battle's own loaded definitions.
    ///
    /// <b>A battle with no attached Relic content resolves nothing.</b> The registry
    /// is populated at creation (<see cref="CreateBattleAsync"/>); an absent or
    /// <c>null</c> entry means the battle carries no Relic definitions this boundary
    /// can resolve, which is the documented staging position and not an invented
    /// empty loadout (<c>RELIC_RULES.md</c> §2.1 item 1 defines no zero-Relic
    /// battle). The state is returned unchanged.
    /// </summary>
    /// <param name="battleId">The battle session whose Relic content is read.</param>
    /// <param name="state">The resolution state at this firing point.</param>
    /// <param name="firingPoint">Which documented event is being processed.</param>
    /// <param name="matchCount">The Match count this point reads, if any.</param>
    /// <param name="combo">The Combo value this point reads, if any.</param>
    /// <param name="events">The resolution's ordered event list, appended to.</param>
    /// <param name="powerGainSource">
    /// The stage that owns the Power mutation, for
    /// <see cref="RelicFiringPoint.PowerGain"/> — the qualification
    /// <c>RELIC_RULES.md</c> §3.1 fixes.
    /// </param>
    /// <returns>The state after this firing point.</returns>
    private BattleState ApplyRelicFiringPoint(
        string battleId,
        BattleState state,
        RelicFiringPoint firingPoint,
        int matchCount,
        int combo,
        List<BattleEvent> events,
        PowerChangeSource? powerGainSource = null,
        PetState? observationState = null)
    {
        if (!_relicConfiguration.TryGetValue(battleId, out var relicDefinitions)
            || relicDefinitions is null)
        {
            return state;
        }

        return ApplyRelicFiringPoint(
            relicDefinitions,
            state,
            firingPoint,
            matchCount,
            combo,
            events,
            powerGainSource,
            observationState);
    }

    /// <summary>
    /// Runs one Relic firing point over a battle's state and merges what the stage
    /// produced — the <c>GAME_RULES.md</c> §17 stage the Relic resolution owns,
    /// called once per documented event (<c>RELIC_RULES.md</c> §3).
    ///
    /// <b>It decides no Relic rule.</b> Which Relics are eligible, whether their
    /// Conditions hold, which effect each applies, and where the effect is carried
    /// are all <see cref="RelicResolver.Resolve(IReadOnlyList{EquippedRelicContent}, PetState, RelicFiringPoint, int, int, PowerChangeSource?)"/>'s
    /// and the declared content's. This method supplies the battle's equipped
    /// content in equip-slot order, hands back the reports the stage produced, and
    /// writes the state it returned into the resolution's single write-back
    /// (<c>GAME_STATE.md</c> §5.1).
    ///
    /// <b>The equipped content is the loadout snapshot in slot order.</b> Element
    /// <c>i</c> of <c>PetState.EquippedRelics[]</c> is equip slot <c>i + 1</c> and is
    /// paired with the definition resolved for that slot
    /// (<c>RELIC_RULES.md</c> §2.3, §2.5); nothing is sorted, filtered,
    /// de-duplicated, or re-ranked here, because §2.3 item 2 forbids ordering slots
    /// by any property of the Relic. A slot whose definition did not resolve is
    /// simply not handed to the stage — the Relic does nothing rather than having a
    /// rule invented for it (<c>AGENTS.md</c> §7).
    ///
    /// <b>Every carrier the effect can reach is written back, and nothing else is.</b>
    /// The five carriers are <c>Power</c> (<c>Immediate</c>,
    /// <c>RELIC_RULES.md</c> §8.3 item 3), <c>ATKModifiers[]</c> and
    /// <c>CardCostModifiers[]</c> (<c>Battle</c>), <c>NextAttackCritModifiers[]</c>
    /// and the <c>NextAttack</c>-lifetime elements of <c>ATKModifiers[]</c>
    /// (<c>NextAttack</c>), and <c>BurnDamageModifiers[]</c> (<c>Battle</c>). The
    /// rest of <paramref name="state"/> is carried across untouched — including the
    /// Pet's Passive progress, its base stats, and the Boss's state — so this stage
    /// cannot disturb a value it does not own.
    ///
    /// <b>Power is merged by its clamped change.</b> The stage writes
    /// <c>PetState.Power</c> through the one write site that enforces
    /// <c>GAME_RULES.md</c> §12's 0–100 range, so the change it reports is the change
    /// the cap allowed; applying that change to the resolution's own Power keeps the
    /// single clamp point and the reported delta identical, and cannot double-apply a
    /// grant.
    ///
    /// <b>One call is one root event.</b> The stage's per-instance safeguard is
    /// scoped to the call, so the caller's granularity is the contract's: once per
    /// Swap for the step-11 event, once per cascade iteration for
    /// <c>OnCascade</c>, once per qualifying gain, and once per resolved damage
    /// instance the Pet took.
    ///
    /// <b>The observation state is the firing point's own, for the step-11
    /// points.</b> A Condition is read from the state the stage is given, so that
    /// state must be the one the firing point's documented observation point
    /// describes (<c>RELIC_RULES.md</c> §8.1 item 8). The step-11 points pass one
    /// whose <c>HP</c> is the Swap's starting value, because this Swap's own step-14
    /// healing has already been written into <paramref name="state"/> by the
    /// board-resolution stage while <c>GAME_RULES.md</c> §17 orders step 11 before
    /// step 14. Only the readings differ: the effects are still applied to
    /// <paramref name="state"/>'s current carriers and merged back into it.
    /// </summary>
    /// <param name="relicDefinitions">
    /// The battle's equipped Relic definitions in equip-slot order, or <c>null</c>
    /// for a battle whose Relic content was not attached.
    /// </param>
    /// <param name="state">The resolution state at this firing point.</param>
    /// <param name="firingPoint">Which documented event is being processed.</param>
    /// <param name="matchCount">The Match count this point reads, if any.</param>
    /// <param name="combo">The Combo value this point reads, if any.</param>
    /// <param name="events">
    /// The resolution's ordered event list, appended to with the stage's reports, or
    /// <c>null</c> when the caller is not a resolution that publishes events (battle
    /// creation emits none — <c>GAME_STATE.md</c> §2.0.5.2 item 2).
    /// </param>
    /// <param name="powerGainSource">
    /// The stage that owns the Power mutation, for
    /// <see cref="RelicFiringPoint.PowerGain"/>.
    /// </param>
    /// <param name="observationState">
    /// The Pet state the stage's Condition evaluation and effect application are
    /// read against, or <c>null</c> to read <paramref name="state"/>'s current Pet
    /// state. It is supplied only by the step-11 firing points; every other point
    /// observes the resolution as it stands, which is its own observation point.
    /// </param>
    /// <returns>The state after this firing point.</returns>
    private static BattleState ApplyRelicFiringPoint(
        IReadOnlyList<RelicDefinition?> relicDefinitions,
        BattleState state,
        RelicFiringPoint firingPoint,
        int matchCount,
        int combo,
        List<BattleEvent>? events,
        PowerChangeSource? powerGainSource = null,
        PetState? observationState = null)
    {
        var equippedRelics = state.PetState.EquippedRelics ?? [];

        if (equippedRelics.Length == 0)
        {
            return state;
        }

        var equippedRelicContents = new List<EquippedRelicContent>(equippedRelics.Length);

        for (var slot = 0; slot < equippedRelics.Length; slot++)
        {
            var definition = slot < relicDefinitions.Count ? relicDefinitions[slot] : null;

            if (definition != null)
            {
                equippedRelicContents.Add(new EquippedRelicContent(equippedRelics[slot], definition));
            }
        }

        var observed = observationState ?? state.PetState;

        var resolution = RelicResolver.Resolve(
            equippedRelicContents,
            observed,
            firingPoint,
            matchCount,
            combo,
            powerGainSource);

        if (events != null)
        {
            events.AddRange(resolution.Triggered.Select(BattleEvent.ForRelicTriggered));
            events.AddRange(resolution.PowerChanges.Select(BattleEvent.ForPowerChanged));
        }

        var relicPowerDelta = resolution.PetState.Power - observed.Power;
        var currentPower = Math.Clamp(state.PetState.Power + relicPowerDelta, 0, 100);

        return state with
        {
            PetState = state.PetState with
            {
                Power = currentPower,
                ATKModifiers = resolution.PetState.ATKModifiers,
                CardCostModifiers = resolution.PetState.CardCostModifiers,
                NextAttackCritModifiers = resolution.PetState.NextAttackCritModifiers,
                BurnDamageModifiers = resolution.PetState.BurnDamageModifiers,
            },
        };
    }

    /// <summary>
    /// Whether the Boss Passive identified by <paramref name="passiveId"/> is still
    /// eligible to fire in the battle session <paramref name="battleId"/>
    /// (<c>BOSS_RULES.md</c> §6.2.4; <c>PASSIVE_RULES.md</c> §4's Boss Passive
    /// clause).
    ///
    /// <b>The rule is the definition's; this method only reads it.</b>
    /// <c>PASSIVE_RULES.md</c> §4 gives the <b>once-per-battle firing eligibility</b>
    /// to a Boss Passive whose definition declares the non-default <c>Persistent</c>
    /// reset behavior (storage token <c>Persistent</c>, Domain
    /// <see cref="PassiveResetBehavior.NoReset"/>; <c>DATABASE.md</c> §1 note
    /// item 3), and §4 item 3 requires that declaration to be on the definition —
    /// which is where this reads it. A definition declaring the default keeps the
    /// unchanged match-charging reset semantics and has no firing limit, so nothing
    /// about the default behavior is redefined here and no second reset vocabulary
    /// is introduced.
    ///
    /// <b>It reads; it does not consume.</b> Eligibility is consumed by
    /// <see cref="ConsumeOncePerBattleBossPassiveFiring"/> on the resolution's
    /// accepted-write path, so a resolution the store refused does not spend it
    /// (<c>REDIS_STATE.md</c> §4 item 6).
    /// </summary>
    /// <param name="battleId">
    /// The battle session the resolution runs in — the scope the eligibility belongs
    /// to (<c>GAME_STATE.md</c> §2.0.1).
    /// </param>
    /// <param name="bossDefinition">
    /// The Boss's definition, whose <c>PassiveResetBehavior</c> selects whether the
    /// once-per-battle firing limit applies at all (<c>PASSIVE_RULES.md</c> §4
    /// item 3).
    /// </param>
    /// <param name="passiveId">
    /// The Boss Passive's identity (<c>BOSS_RULES.md</c> §6.4), which is what a
    /// battle's own consumption is recorded against.
    /// </param>
    /// <returns>Whether the Passive may fire in this battle.</returns>
    private bool IsBossPassiveFiringEligible(
        string battleId,
        BossDefinition bossDefinition,
        PassiveId passiveId)
    {
        // PASSIVE_RULES.md §4 item 1: the default reset behavior is the
        // match-charging one (progress resets after the trigger), and §4's Boss
        // Passive clause scopes the once-per-battle firing limit to the Persistent
        // declaration alone. A definition that declares the default therefore keeps
        // its existing semantics untouched, and nothing is recorded for it.
        if (bossDefinition.PassiveResetBehavior != PassiveResetBehavior.NoReset)
        {
            return true;
        }

        // The one-way consumption: the first activation that the store accepted
        // inserted the entry, and every later Turn of this battle finds it present
        // and is refused. A different battle is a different key and starts eligible.
        return !_consumedBossPassiveFiring.ContainsKey((battleId, passiveId.Value));
    }

    /// <summary>
    /// Consumes the battle's once-per-battle firing eligibility for the Boss Passive
    /// a <b>committed</b> resolution activated (<c>BOSS_RULES.md</c> §6.2.4;
    /// <c>PASSIVE_RULES.md</c> §4's Boss Passive clause).
    ///
    /// <b>It runs after the write-back, never before it.</b>
    /// <c>REDIS_STATE.md</c> §4 item 6 requires a refused resolution to be retried
    /// against fresh state and to produce the same result; a resolution that spent
    /// the eligibility before the <c>Sequence</c> compare-and-set accepted it could
    /// not, because its activation would have been discarded with the refused state
    /// while the eligibility stayed spent. Consuming on the accepted path is the
    /// same order <see cref="PersistTerminalResultAsync"/> follows for the
    /// battle-end step (<c>REDIS_STATE.md</c> §4 items 2–3).
    ///
    /// <b>It is idempotent and never re-arms.</b> The entry is inserted once per
    /// (battle session, Passive identity) and is never removed, so a second
    /// committed resolution of the same battle cannot authorize the Passive again —
    /// not when the applied effect expires at step 19a, not while the Boss remains
    /// at or below the trigger, and not when the Boss leaves and later re-enters it.
    ///
    /// <b>A new battle is a new key.</b> A battle id identifies one battle session
    /// and is authored by the server when that battle is created
    /// (<c>GAME_STATE.md</c> §2.0.1, <c>API_CONTRACTS.md</c> §3), so a new battle
    /// session has no consumed entry and the Passive is eligible again.
    /// </summary>
    /// <param name="battleId">The battle session the committed resolution belongs to.</param>
    /// <param name="passiveId">
    /// The Passive the committed resolution activated, or <c>null</c> when it
    /// activated none — the value <see cref="SwapResolution"/> reports. A battle that
    /// activated no once-per-battle Passive records nothing.
    /// </param>
    private void ConsumeOncePerBattleBossPassiveFiring(string battleId, PassiveId? passiveId)
    {
        if (passiveId is not { } activated)
        {
            return;
        }

        _consumedBossPassiveFiring.TryAdd((battleId, activated.Value), true);
    }

    /// <summary>
    /// One Swap resolution's result together with the once-per-battle Boss Passive
    /// firing that resolving it activated, if any (<c>BOSS_RULES.md</c> §6.2.4;
    /// <c>PASSIVE_RULES.md</c> §4's Boss Passive clause).
    ///
    /// <b>Why the activation is reported rather than consumed inside the
    /// resolution.</b> <c>REDIS_STATE.md</c> §4 item 6 makes a refused
    /// <c>Sequence</c> compare-and-set retry the same computation against fresh
    /// state and produce the same result. The resolution is therefore kept a pure
    /// function of the state it read: it reports the firing it activated, and
    /// <see cref="ExecuteSwapAsync"/> consumes the eligibility only on the path
    /// where the store accepted that resolution.
    /// </summary>
    /// <param name="Result">The rejection, or the finished post-resolution result.</param>
    /// <param name="ActivatedOncePerBattleBossPassive">
    /// The identity of the once-per-battle Boss Passive this resolution activated, or
    /// <c>null</c> when it activated none. It is non-null only for a firing the
    /// resolution's own eligibility read authorized.
    /// </param>
    private readonly record struct SwapResolution(
        SwapExecutionResult Result,
        PassiveId? ActivatedOncePerBattleBossPassive);

    /// <summary>
    /// True when the battle has ended — the Boss's HP or the active Pet's HP has
    /// reached 0 (<c>GAME_RULES.md</c> §1.4, <c>BOSS_RULES.md</c> §5 item 4).
    ///
    /// <b>It is the resolution's own terminal condition, read from state.</b> The
    /// resolution's terminal branches are ordered Boss-first and each ends the
    /// resolution, and <c>GAME_STATE.md</c> §2.0.3 forbids a lifecycle field on
    /// <c>BattleState</c>, so the HP values are what the condition is read from —
    /// this method decides no rule, it evaluates the documented one
    /// (<c>GAME_RULES.md</c> §1.4: "a battle ends when either the Boss or the
    /// active Pet reaches 0 HP").
    ///
    /// <b>Both comparisons are <c>&lt;= 0</c>.</b> The Damage Pipeline clamps HP and
    /// never lets it become negative (<c>COMBAT_RULES.md</c> §3), so <c>0</c> is
    /// the only reachable terminal value and the two spellings cannot disagree; the
    /// resolution's own checks read Boss <c>== 0</c> and Pet <c>&lt;= 0</c> for the
    /// same reason. The asymmetric spelling is not reproduced here because this is
    /// the battle's terminal condition, not one of its two ordered check sites.
    /// </summary>
    /// <param name="state">The battle state to evaluate.</param>
    private static bool IsTerminalBattle(BattleState state) =>
        state.BossState.HP <= 0 || state.PetState.HP <= 0;

    /// <summary>
    /// Applies the commit-boundary playable-board invariant
    /// (<c>MATCH3_RULES.md</c> §1.6) to a finished Round's state: the returned
    /// state's board contains at least one legal match-producing adjacent swap
    /// whenever the battle is non-terminal, and is otherwise the state it was
    /// given.
    ///
    /// <code>
    /// terminal battle                        → unchanged        §1.6.4
    /// board already has a legal move         → unchanged        §1.6.2 item 3
    /// board deadlocked                       → regenerated board
    ///                                          + advanced RngState
    /// </code>
    ///
    /// <b>The legality predicate is the gameplay one, reused.</b>
    /// <see cref="BossAiEngine.EnumerateLegalMoves"/> is the existing enumeration
    /// of the 112 orthogonal adjacent pairs whose exchange produces at least one
    /// §3 Match — the same predicate <see cref="SwapValidator"/> accepts a Player
    /// Swap against (<c>MATCH3_RULES.md</c> §2.1.2 item 5) and the same one
    /// <see cref="BossAiEngine.SelectMove"/> already uses for the Boss's own
    /// pre-move recovery (§1.6.5). No second deadlock detector is introduced, and
    /// <see cref="BoardGenerationValidator.HasValidSwap"/> is deliberately not
    /// used: it is a generation-time helper (§1.4.1, the <c>TASK-265</c> amendment
    /// to <c>ADR-026</c> §8) that the runtime path does not call. Enumeration
    /// resolves no Swap, mutates nothing, and draws no RNG.
    ///
    /// <b>Recovery is the documented generation path over the battle's own
    /// stream.</b> <see cref="BoardGenerator.Generate(Pcg32)"/> continues the
    /// caller's <see cref="Pcg32"/> stream and accepts a candidate only when it
    /// satisfies §1.3 and §1.4, so the replacement board has a legal move by
    /// construction using the unchanged algorithm, the unchanged Gem
    /// distribution, and the unchanged <see cref="BoardGenerator.MaxAttempts"/>
    /// bound (§1.6.3). The generator is advanced in place, and the state it
    /// reaches is what the Round commits, so the board and the RNG state it
    /// consumed travel together (§1.6.2 item 1).
    ///
    /// <b>Exhaustion fails closed.</b> <see cref="BoardGenerator.Generate(Pcg32)"/>
    /// throws <see cref="BoardGenerationFailedException"/> when no candidate
    /// satisfies the constraints within the bound, and it throws before the
    /// caller's compare-and-set, so no partially recovered board and no advanced
    /// RNG state are ever stored (§1.6.3 item 4, the failure contract of §1.5
    /// item 3 — the same one generation has at battle creation).
    ///
    /// <b>It writes a board and an RNG state and nothing else.</b> No Match is
    /// created, no Swap resolved, no pass of §4 run, no Special Gem placed, and no
    /// Combo, Match, Power, Passive, or Relic progression awarded (§1.6.3 item 2);
    /// nothing is appended to the resolution's event list (§1.6.6).
    /// </summary>
    /// <param name="state">
    /// The finished post-resolution state — the value the Round is about to
    /// commit.
    /// </param>
    /// <returns>
    /// The state to commit: the same state when no recovery is required, or the
    /// same state with a regenerated <c>BoardState</c> and the advanced
    /// <c>RngState</c>.
    /// </returns>
    /// <exception cref="BoardGenerationFailedException">
    /// The deadlocked board could not be replaced within
    /// <see cref="BoardGenerator.MaxAttempts"/> attempts (<c>MATCH3_RULES.md</c>
    /// §1.6.3 item 4).
    /// </exception>
    private static BattleState EnsurePlayableBoard(BattleState state)
    {
        // §1.6.4 / §1.6.1 item 3: a terminal board is exempt. The outcome is
        // already authoritative and the Player never acts again, so the invariant
        // has no subject — and because recovery is not attempted, a generation
        // failure can never invalidate a valid terminal result.
        if (IsTerminalBattle(state))
        {
            return state;
        }

        // §1.6.2 item 3: a playable board is preserved exactly — the board and the
        // RngState are returned untouched, so the new check cannot consume entropy
        // or reshuffle a board the Boss's own recovery already made playable.
        if (BossAiEngine.EnumerateLegalMoves(state.BoardState).Count > 0)
        {
            return state;
        }

        // §1.6.3: the board is deadlocked, so it is replaced by the documented
        // deterministic generation on the battle's existing stream, bounded by
        // §1.5 item 2. The generator is advanced in place and `CurrentState` is the
        // state it reached after the accepted attempt — never a reset, never a
        // second generator (§1.6.3 item 3, ADR-009).
        var rng = new Pcg32(state.RngState.State, state.RngState.Increment);
        var generation = BoardGenerator.Generate(rng);

        return state with
        {
            BoardState = generation.Board!,
            RngState = rng.CurrentState,
        };
    }

    /// <summary>
    /// Persists the durable result for a resolution that reached a terminal
    /// outcome, then clears the battle's active state — the documented battle-end
    /// step of <c>ARCHITECTURE.md</c> §4 item 4, in the documented order
    /// (<c>REDIS_STATE.md</c> §3: "Deleted: explicitly, when
    /// <c>BattleWon</c>/<c>BattleLost</c> is resolved and the result has been
    /// written to PostgreSQL").
    ///
    /// <b>The terminal check is the events, not a state field.</b>
    /// <c>GAME_STATE.md</c> §2.0.3 forbids a <c>Status</c>/lifecycle member on
    /// <c>BattleState</c>, and <c>GAME_EVENTS.md</c> §2 makes
    /// <c>BattleWon</c>/<c>BattleLost</c> the outcome statement: the presence of
    /// one of those two events in the resolution's own list is the only
    /// documented end signal, so it is what this method reads. Both terminal
    /// branches of the resolution already append exactly one and then return, so
    /// a non-terminal resolution simply never reaches this method.
    ///
    /// <b>Why the state passed is the committed one.</b> The caller invokes this
    /// after the <c>Sequence</c> compare-and-set accepted the resolution, so the
    /// result records the terminal values of the transition that became
    /// authoritative — not a state a concurrent resolution refused
    /// (<c>REDIS_STATE.md</c> §4 items 2–3). That order matters in both
    /// directions: the durable write must not happen for a resolution the store
    /// refused, and the active-state clear must not happen before the write.
    ///
    /// <b>Resolution never fails because the result could not be written.</b>
    /// The resolution has already committed to the store by the time this runs;
    /// a persistence failure must not turn a committed action into a reported
    /// failure, and <c>REDIS_STATE.md</c> §3 already governs the consequential
    /// case — the state stays in Redis under its sliding TTL, the battle is not
    /// lost, the outcome events have been returned, and no automatic retry,
    /// worker, or queue is introduced for it. The one outcome this method
    /// deliberately does not swallow is a resolved
    /// <c>BossDefinition</c> whose row is absent: that path writes no row and
    /// performs no delete, so the battle remains intact and retryable
    /// (<c>DATABASE.md</c> §1 sourcing item 3).
    /// </summary>
    /// <param name="committed">
    /// The post-resolution state the store accepted — the terminal transition
    /// whose values the result records.
    /// </param>
    /// <param name="events">
    /// The resolution's own ordered event list, whose terminal member selects
    /// the outcome (<c>GAME_EVENTS.md</c> §2).
    /// </param>
    /// <param name="cancellationToken">Cancels the lookups, the write, and the delete.</param>
    private async Task PersistTerminalResultAsync(
        BattleState committed,
        IReadOnlyList<BattleEvent> events,
        CancellationToken cancellationToken)
    {
        // No durable result boundary is composed: the battle resolves exactly as
        // it did before this step existed and nothing durable is recorded. This
        // is the only case in which termination is observed but unrecorded, and
        // it cannot arise in the running server (Program.cs composes the
        // boundary).
        if (_battleResults is null)
        {
            return;
        }

        var outcome = TerminalOutcome(events);

        if (outcome is null)
        {
            return;
        }

        try
        {
            await _battleResults
                .PersistTerminalResultAsync(committed, outcome.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
        {
            // ARCHITECTURE.md §4 item 3: the ordered event list is this
            // resolution's result, and the action has already committed to the
            // store. A failed durable write must therefore leave the resolution
            // reported as it happened — the outcome events included — while the
            // authoritative state stays in Redis and the battle remains
            // recoverable and retryable (REDIS_STATE.md §3, DATABASE.md §1
            // sourcing item 3). No retry, rollback, worker, or compensating
            // action is introduced: none is documented (ARCHITECTURE.md §5).
            //
            // The failure is not silently discarded: it is recorded against the
            // battle so an operator can see that a battle ended without a durable
            // result. That is observation only — the record below changes no
            // state, is never the source of a value, and is the same information
            // REDIS_STATE.md §3 already exposes through the surviving key.
            MarkResultNotPersisted(committed.BattleId, failure);
        }
    }

    /// <summary>
    /// The battles whose terminal resolution produced no durable result, with the
    /// failure that prevented it.
    ///
    /// <b>It is a diagnostic record, not state.</b> No value here is ever read to
    /// produce a battle value: the authoritative state is the active-state
    /// record's (<c>REDIS_STATE.md</c> §2 item 2), the durable result is
    /// PostgreSQL's (<c>DATABASE.md</c> §1), and a battle is never resolved,
    /// returned, or delivered from this map. It exists because the documented
    /// fail-closed path (<c>DATABASE.md</c> §1 sourcing item 3) otherwise leaves a
    /// battle end that reached no database completely unobservable — the one case
    /// the contract deliberately does not surface to the client.
    ///
    /// <b>Why it is not surfaced as a wire contract.</b> <c>DATABASE.md</c> §1
    /// sourcing item 3 states that no new error code, API response, or wire
    /// contract is introduced for this path: the endpoint simply finds no row and
    /// returns its documented <c>404</c> until the write succeeds.
    /// </summary>
    private readonly ConcurrentDictionary<string, Exception> _unpersistedResults = new(StringComparer.Ordinal);

    /// <summary>
    /// The battles whose terminal resolution reached no durable result, for
    /// operators and tests — see <see cref="_unpersistedResults"/> for why this is
    /// not battle state and not a client-facing contract.
    /// </summary>
    public IReadOnlyCollection<string> UnpersistedResultBattleIds => _unpersistedResults.Keys.ToArray();

    /// <summary>
    /// The failure recorded for a battle whose terminal resolution reached no
    /// durable result, or <c>null</c> when it reached one. Observation only.
    /// </summary>
    public Exception? UnpersistedResultFailure(string battleId) =>
        _unpersistedResults.TryGetValue(battleId, out var failure) ? failure : null;

    /// <summary>
    /// Records the failure that prevented a terminal resolution from reaching a
    /// durable result (<c>DATABASE.md</c> §1 sourcing item 3). Observation only.
    /// </summary>
    private void MarkResultNotPersisted(string battleId, Exception failure) =>
        _unpersistedResults[battleId] = failure;

    /// <summary>
    /// The outcome a resolution's event list states, or <c>null</c> when it
    /// states none (<c>GAME_EVENTS.md</c> §2).
    ///
    /// The battle's terminal checks are ordered Boss-first and each ends the
    /// resolution, so at most one of the two events is ever present
    /// (<c>GAME_RULES.md</c> §1.4, <c>BOSS_RULES.md</c> §5 item 4). This reads
    /// the list the resolution produced and decides nothing: an absent outcome
    /// is the documented statement that the action was not terminal, and no
    /// outcome is inferred from HP values, from <c>Turn</c>, or from anything
    /// else.
    /// </summary>
    private static BattleOutcome? TerminalOutcome(IReadOnlyList<BattleEvent> events)
    {
        for (var index = 0; index < events.Count; index++)
        {
            switch (events[index].Type)
            {
                case BattleEventType.BattleWon:
                    return BattleOutcome.Victory;

                case BattleEventType.BattleLost:
                    return BattleOutcome.Defeat;
            }
        }

        return null;
    }
}