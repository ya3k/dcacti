using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;

namespace GameServer.Domain.Battle;

/// <summary>
/// The active Pet's battle state, through the combat-stat / Pet / Passive /
/// Element stage (<c>GAME_STATE.md</c> §2.3).
///
/// <code>
/// BattleState
/// └── PetState
///     ├── PetId                  the owned Pet instance (Pet.PetInstanceId)   (§2.3)
///     ├── HP                     current health (COMBAT_RULES.md §1.1)      (§2.3)
///     ├── MaxHP                  maximum health (COMBAT_RULES.md §1.1)
///     ├── ATK                    attack power (COMBAT_RULES.md §1.1)
///     ├── DEF                    defense (COMBAT_RULES.md §1.1)
///     ├── Crit                   critical hit chance, as a percentage
///     │                          (COMBAT_RULES.md §1.1, §3.3)
///     ├── Power                  resource for Cards / Skills, 0–100
///     │                          (COMBAT_RULES.md §1.1, §6, GAME_RULES.md §12)
///     ├── Element                the Pet's one Element (PET_RULES.md §1,
///     │                          ELEMENT_RULES.md §1.1)
///     ├── PassiveId              the Pet's one Passive — the identity the
///     │                          Passive events report (PASSIVE_RULES.md §1)
///     ├── PassiveProgress        current count vs. threshold                (§2)
///     ├── PassiveResetOverride?  only present for a non-default reset      (§4)
///     ├── StatusEffects[]        active Status Effect instances           (§2.3.1)
///     ├── NextAttackCritModifiers[]  temporary Crit modifiers awaiting a
///     │                          qualifying attack's consumption           (§2.3.4)
///     ├── ATKModifiers[]         applied ATK modifiers, both lifetimes,
///     │                          ordered by SourceIdentity                 (§2.3.7)
///     ├── CardCostModifiers[]    applied, Battle-scoped Card-cost
///     │                          modifiers, in written order               (§2.3.5)
///     └── BurnDamageModifiers[]  applied, Battle-scoped Burn-damage
///                                modifiers, in written order
/// </code>
///
/// <b>This is the documented owner, not a new decision.</b> <c>GAME_STATE.md</c>
/// §2.3 places <c>PetId</c> and the combat stats <c>HP</c>/<c>MaxHP</c>,
/// <c>ATK</c>/<c>DEF</c>/<c>Crit</c>, and <c>Power</c> here together with
/// <c>Element</c>, <c>PassiveId</c>, <c>PassiveProgress</c>, and
/// <c>PassiveResetOverride</c>, and §2 nests <c>PetState</c> inside
/// <c>BattleState</c> (§2.3, §2). The Pet is the combat character and the Player
/// is the account/owner with no authoritative battle-time combat pool
/// (<c>ADR-011</c> items 3 and 5): this type is the <b>only</b> Player-side
/// combat-stat home. All of them are therefore ordinary <b>Active Battle
/// State</b>: authoritative, server-produced, and written in the same
/// single post-resolution write-back as <c>Turn</c>, <c>Sequence</c>,
/// <c>BoardState</c>, <c>RngState</c>, and <c>BattleState.Combo</c> /
/// <c>BattleState.MatchCount</c> (§5.1).
///
/// <b><c>PetId</c> is the owned Pet instance identity.</b> It is the same value
/// as <see cref="GameServer.Domain.Pets.Pet.PetInstanceId"/> (<c>DATABASE.md</c>
/// §1), the <c>petId</c> submitted to <c>POST /api/battle/start</c>
/// (<c>API_CONTRACTS.md</c> §3), and <c>BattleResult.PetInstanceId</c> at battle
/// end (<c>DATABASE.md</c> §1). It is <b>not</b> a Pet definition id and not the
/// display <c>Identity</c> name (<c>PET_RULES.md</c> §2,
/// <c>PetDefinition.Identity</c>) — those are persistent definition-side values,
/// not battle state. It is <b>set once at battle creation and never changes</b>
/// (§2.3 item 2): selecting a Pet locks it in for the duration of the battle
/// (<c>PET_RULES.md</c> §2 item 3), so no resolution writes it and no event
/// changes it. <c>ADR-014</c> decision 4 evaluated whether a separate
/// <c>PetInstanceId</c> member was required and recorded that it is not: this
/// member already is the instance, and a second member would duplicate a value
/// the record already owns (<c>GAME_STATE.md</c> §0 item 5).
///
/// <b>Only the fields this stage requires exist.</b> §2.3 also lists
/// <c>Tier</c>/<c>Star</c>/<c>Level</c>. Those belong to the Pet progression stage
/// and are <b>not yet
/// implemented</b>, not <b>not required</b> (§0 item 4, §2.0.5.3,
/// <c>SIGNALR_PROTOCOL.md</c> §4.3 item 2): each is added by its own owning task,
/// exactly as this stage adds these. They are not stubbed, defaulted, or
/// represented by a placeholder, because a placeholder for a field no rule yet
/// reads would be a representation of its own (§0 item 5).
///
/// <b><c>EquippedRelics</c> is the Relic stage's member.</b> It is the
/// battle-scoped loadout snapshot (<c>RELIC_RULES.md</c> §2 item 3, §2.5;
/// <c>GAME_STATE.md</c> §2.3):
///
/// <code>
/// EquippedRelics[]  3–5 elements, each ONE owned Relic instance identity
///                   (RelicInstanceId) — RELIC_RULES.md §2.2
///                   in the submitted relicLoadout order, so element i is
///                   equip slot i + 1 — RELIC_RULES.md §2.3, §2.5
/// </code>
///
/// It is <b>identity-only</b>. A Relic's <c>Trigger</c>, <c>Condition</c>,
/// and <c>EffectDefinition</c> are its definition on
/// <see cref="GameServer.Domain.Relics.RelicDefinition"/> and are not copied
/// into an element — the same "identity, not a definition" rule §2.3 records
/// for <see cref="PassiveId"/> (§2.3 item 1) and §2.4 records for
/// <c>BossState.BossId</c>. No trigger state, effect, stack, or cooldown is
/// carried here: Relic trigger evaluation and effect resolution are
/// <c>RELIC_RULES.md</c> §4–§5's concern and are <b>not</b> implemented
/// (TASK-027 Scope).
///
/// The array is <b>fixed at battle start</b> and never changes: re-equipping
/// is impossible mid-battle (<c>RELIC_RULES.md</c> §2 item 2). Order is
/// gameplay-significant — §4.2 resolves a single event's eligible Relics in
/// this slot order — so the element order is preserved exactly as submitted,
/// never sorted.
///
/// <b>The combat stats are the MVP baseline configuration of
/// <c>COMBAT_RULES.md</c> §1.1, not permanent invariants.</b> §1.1 defines
/// <c>HP</c>/<c>MaxHP</c> = 1000, <c>ATK</c> = 50, <c>DEF</c> = 25, <c>Power</c>
/// in the range 0–100 (starting at 0), and <c>Crit</c> = 5%, and states
/// explicitly that these "are not permanent invariants — future Pet progression
/// (Level, Star, Tier) may produce different actual Battle Stats" and that
/// "changing balance values is a configuration change". The defaults are
/// therefore constants of the <b>initial</b> state only: nothing here reads them
/// as the current value of a running battle, and no formula is derived from
/// them. <c>Power</c>'s 0–100 range is likewise not enforced here: §1.1 and
/// <c>GAME_RULES.md</c> §12 own it as a documented invariant, and the clamp
/// belongs to the write site that owns it
/// (<see cref="GameServer.Domain.Match3.ResourceGenerator.ApplyPower"/>).
///
/// <b>No second representation of the Passive.</b> The Passive's
/// <c>Threshold</c>, <c>Trigger Type</c>, <c>Effect</c>, and <c>Reset Behavior</c>
/// are its <b>definition</b> (<c>PASSIVE_RULES.md</c> §1) and are not stored
/// beside it (§2.3 item 1). <see cref="PassiveProgress"/> carries the Threshold
/// because the documented progress <i>pair</i> is "current count vs. threshold"
/// (§2.3) and <c>PASSIVE_RULES.md</c> §6 item 1 requires the pair to be
/// rendered together; the identity travels separately and names which definition
/// the values are read from (§2.3: "it is an identity, not a definition").
///
/// <b>The combat stats exist in the state but are not delivered on the wire.</b>
/// <c>SIGNALR_PROTOCOL.md</c> §4.3 item 2 fixes the <c>petState</c> payload
/// member to its enumerated five — the Passive identity, its progress pair, the
/// non-default override, <c>equippedCards</c>, and the active Pet's
/// <c>statusEffects[]</c> — and <c>GAME_STATE.md</c> §2.3 records that not
/// all members here are part of any wire payload. This type does not change
/// that: the combat stats are authoritative state, exactly as
/// <c>LastCommittedSwapPair</c> is (§2.1.10 item 9, <c>SIGNALR_PROTOCOL.md</c>
/// §4 item 12), and carrying them here adds no member to
/// <c>BattleStateUpdated</c> and no message, method, or subscription. Delivering
/// them is a protocol change owned by its own task.
///
/// <b>No <c>Status</c> field and no lifecycle value.</b> A battle has no
/// lifecycle state machine (<c>GAME_STATE.md</c> §2.0.3), and this stage adds
/// none.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR, EF
/// Core, Redis, HTTP, Phaser, or Discord concern.
/// </summary>
/// <param name="HP">
/// The active Pet's current health (<c>COMBAT_RULES.md</c> §1.1,
/// <c>GAME_STATE.md</c> §2.3). It starts equal to <see cref="MaxHP"/> — a battle
/// begins at full health — and is the value Boss→Player damage reduces and
/// healing effects restore
/// (<c>COMBAT_RULES.md</c> §3, §4).
///
/// <b>Nothing in this type computes, clamps, or compares it.</b> The healing that
/// writes it is <c>COMBAT_RULES.md</c> §4 item 1's rule, applied by
/// <see cref="GameServer.Domain.Match3.ResourceGenerator.ApplyHeal"/> as
/// <c>GAME_RULES.md</c> §17 step 14 and clamped there to this field's
/// <c>MaxHP</c>; damage is applied by the Damage Pipeline's caller
/// (<c>COMBAT_RULES.md</c> §3.4 step 6). Victory/Defeat remains owned by its own
/// stage.
/// </param>
/// <param name="MaxHP">
/// The active Pet's maximum health (<c>COMBAT_RULES.md</c> §1.1,
/// <c>GAME_STATE.md</c> §2.3) — the ceiling heal effects restore up to
/// (<c>COMBAT_RULES.md</c> §4 item 1). Its documented MVP default is
/// <see cref="DefaultMaxHP"/>.
/// </param>
/// <param name="ATK">
/// The active Pet's attack power (<c>COMBAT_RULES.md</c> §1.1,
/// <c>GAME_STATE.md</c> §2.3) — the stat the Damage Pipeline's base damage is
/// read from (<c>COMBAT_RULES.md</c> §3 step 1). Its documented MVP default is
/// <see cref="DefaultATK"/>.
///
/// <b>It is the permanent/base ATK, and the Battle-scoped
/// <see cref="ATKModifiers"/> collection never mutates it.</b> <c>GAME_STATE.md</c>
/// §2.3.7 item 9 keeps this member the permanent/base value, and §5.1.4 item 6 is
/// explicit: it "holds the base value for the whole battle and is never written by
/// this lifecycle, never reset to the configuration default, and never adjusted by
/// an arithmetic inverse", so <c>PetState.ATK = DefaultATK</c> is forbidden for the
/// same source-blind reason §3.3 item 10 forbids <c>DefaultCrit</c>
/// (<c>TASK-136</c> D5/D8). The value the Damage Pipeline consumes is the composed
/// <c>EffectivePetATK</c>, which <c>COMBAT_RULES.md</c> §5.6.6 derives at attack
/// resolution and which is <b>never stored</b> here or anywhere in
/// <c>BattleState</c> (§5.6.6 item 8, §0 item 5). <see cref="DefaultATK"/> is
/// therefore initialization data only, exactly as <see cref="DefaultCrit"/> is.
/// </param>
/// <param name="DEF">
/// The active Pet's defense (<c>COMBAT_RULES.md</c> §1.1,
/// <c>GAME_STATE.md</c> §2.3) — the value the mitigation formula consumes when
/// the Pet is the damage instance's target (<c>COMBAT_RULES.md</c> §3.2). Its
/// documented MVP default is <see cref="DefaultDEF"/>.
/// </param>
/// <param name="Crit">
/// The critical hit chance as a <b>percentage</b> (<c>COMBAT_RULES.md</c> §1.1,
/// §3.3; <c>GAME_STATE.md</c> §2.3) — its documented MVP default is
/// <see cref="DefaultCrit"/> = <c>5</c>, the "5%" of §1.1.
///
/// It stays a percentage rather than becoming a <c>0.05</c> probability because
/// §1.1 defines the unit (Crit is "critical hit chance (%)") and every modifier
/// of it is expressed in the same unit — "increase Crit chance"
/// (<c>COMBAT_RULES.md</c> §3.3 item 3, <c>RELIC_RULES.md</c> §5's Assassin Eye,
/// <c>PASSIVE_RULES.md</c> §8's Bạch Hổ). Storing a fraction here would need
/// converting at every one of those boundaries and would invite the two
/// representations §0 item 5 forbids.
///
/// No Crit roll is performed here: §3.3 owns it and remains unimplemented.
/// </param>
/// <param name="Power">
/// The resource spent to cast Cards and Skills, in the documented range 0–100
/// (<c>COMBAT_RULES.md</c> §1.1, §6; <c>GAME_STATE.md</c> §2.3;
/// <c>GAME_RULES.md</c> §12) — the field <c>GameServer.Domain.Match3.ResourceGenerator.ApplyPower</c>
/// writes, clamped to that range.
///
/// A battle starts at <see cref="DefaultPower"/> = <c>0</c>, not at the cap:
/// Power is generated by Match-3 (<c>COMBAT_RULES.md</c> §2) and none has been
/// generated before the first Match. The range is <b>not</b> enforced by this
/// type; §1.1 and <c>GAME_RULES.md</c> §12 own it as a documented invariant,
/// and the cast validation and consumption are unimplemented
/// (<c>COMBAT_RULES.md</c> §6, <c>CARD_RULES.md</c> §3).
/// </param>
/// <param name="Element">
/// The active Pet's Element (<c>GAME_STATE.md</c> §2.3, <c>PET_RULES.md</c> §1)
/// — the attacking Element the Element Modifier is resolved from
/// (<c>ELEMENT_RULES.md</c> §2.1, §5; <c>COMBAT_RULES.md</c> §3 step 3).
///
/// Every Pet has <b>exactly one</b> Element and MVP supports no dual/multi
/// element entity (<c>ELEMENT_RULES.md</c> §1.2, §7), so this is a single value
/// and not a collection. It is a non-nullable <see cref="Element"/>: §1.1 lists
/// "Pet" among the entities that carry an Element, so there is no elementless-Pet
/// case for a nullable member to spell. (An elementless <i>damage source</i> is
/// the absence <c>ElementMatchups.Resolve</c> accepts as <c>null</c> on its
/// attacker side; §1.1 does not make a Pet such a source by omitting the field.)
///
/// The MVP Pet assignments are <c>ELEMENT_RULES.md</c> §6's (Thanh Xà = Mộc,
/// Xích Lang = Hỏa, Sơn Hùng = Thổ, Bạch Hổ = Kim, Huyền Quy = Thủy) and are not
/// restated or reassigned here.
///
/// It is <b>set at battle creation and never changes</b>: selecting a Pet locks
/// in its Element for the duration of the battle (<c>PET_RULES.md</c> §2 item 3;
/// mid-battle Pet swapping is out of MVP scope), so no resolution writes it and
/// no event changes it.
///
/// <b>It does not restrict the Pet's build.</b> <c>ELEMENT_RULES.md</c> §4 makes
/// Element purely a matchup system: it determines no Card, Relic, or Passive the
/// Pet may use, and nothing here gates any of them.
///
/// This type performs no matchup resolution and applies no modifier:
/// <see cref="ElementMatchups"/> owns the matchup and the Damage Pipeline owns
/// the multiplication, and both remain unimplemented.
/// </param>
/// <param name="PassiveId">
/// Which Passive definition the active Pet carries (<c>GAME_STATE.md</c> §2.3).
/// A Pet has <b>exactly one</b> Passive (<c>PASSIVE_RULES.md</c> §1,
/// <c>GAME_RULES.md</c> §9.2 item 2), so this names one Passive — it does not
/// select among several, and there is no collection, slot, or ordering of
/// Passives here.
///
/// It is <b>set at battle creation and never changes</b> (§2.3 item 2): selecting
/// a Pet locks in its Passive for the duration of the battle
/// (<c>PET_RULES.md</c> §2 item 3), so no resolution writes it and no event
/// changes it. It is the value <c>GAME_EVENTS.md</c> §2's
/// <c>PassiveCharged</c>/<c>PassiveTriggered</c> report, read and reported rather
/// than re-derived (§2 item 1).
///
/// It is <b>not</b> an absent-when-unset convention: a battle always has its one
/// active Pet and therefore its one Passive (§2.3 item 3), so it is present from
/// battle creation with no null or "no Passive yet" form. <see cref="PetState"/>
/// is therefore not nullable on <see cref="BattleState"/>.
/// </param>
/// <param name="PassiveProgress">
/// The Passive's charging position — the progress reached and the Threshold it is
/// measured against (<c>GAME_STATE.md</c> §2.3;
/// <c>PASSIVE_RULES.md</c> §2).
///
/// It starts at <see cref="PassiveProgress.AtStart"/> — the Passive's own
/// Threshold with <c>Current = 0</c> (<c>GAME_STATE.md</c> §2.3 item 3,
/// <c>SIGNALR_PROTOCOL.md</c> §4.3 item 4: <c>current = 0</c> "is what a battle
/// begins with") — and is written after each committed Swap's resolution
/// (§5.1, <c>GAME_EVENTS.md</c> §2, <c>PassiveChargeResult.Progress</c>).
///
/// <c>Current = 0</c> is a real publishable value here, so absence is never used
/// for it: neither member is nullable and neither is omitted
/// (<c>SIGNALR_PROTOCOL.md</c> §4.3 item 4).
/// </param>
/// <param name="PassiveResetOverride">
/// The Passive's non-default <b>Reset Behavior</b>, or <c>null</c> for the
/// default (<c>GAME_STATE.md</c> §2.3, <c>PASSIVE_RULES.md</c> §4).
///
/// It is "only present if this Pet's Passive uses non-default reset behavior"
/// (§2.3), so <c>null</c> is not "unset" and not a third behavior: it is the
/// documented representation of <see cref="PassiveResetBehavior.Default"/> —
/// §4 item 1's "progress resets to 0 immediately after the Passive triggers",
/// which is what §4 item 3 means by a behavior not declared on the Passive's
/// definition and what all five MVP Pet Passives use (§8). A caller that supplies
/// no override and a caller that supplies
/// <see cref="PassiveResetBehavior.Default"/> therefore describe the same
/// behavior, deliberately.
///
/// It is the value <see cref="PassiveTracker.Charge"/> receives as its
/// <c>reset</c> argument, and it is delivered on the wire only when non-default —
/// the member is omitted, never written as JSON <c>null</c>
/// (<c>SIGNALR_PROTOCOL.md</c> §4.3 items 6–7).
/// </param>
/// <b>The Relic loadout is the battle-scoped snapshot.</b> It is set once at
/// battle creation and never changes (<c>RELIC_RULES.md</c> §2 item 2, §2.3
/// item 4): the battle-time authority. It is never re-read from the Player's
/// owned collection during the battle and is never written back to the
/// ownership rows (<c>RELIC_RULES.md</c> §2.5, ADR-012 item 8).
///
/// Order is preserved exactly as submitted — element <c>i</c> is equip slot
/// <c>i + 1</c> (<c>RELIC_RULES.md</c> §2.3, §2.5). The collection is a
/// reading of the identity array the validator produced, not a re-sorted
/// projection of it (<c>RELIC_RULES.md</c> §2.3 item 2 forbids ordering slots
/// by <c>RelicInstanceId</c>, <c>RelicDefinitionId</c>, <c>AcquiredAt</c>, or
/// database order).
/// </param>
/// <b><c>EquippedCards</c> is the Card stage's member.</b> It is the
/// battle-scoped Card loadout snapshot (<c>CARD_RULES.md</c> §1;
/// <c>GAME_STATE.md</c> §2.3):
///
/// <code>
/// EquippedCards[]   exactly 4 elements, each ONE CardDefinitionId
///                   (3 submitted Basic Cards + 1 derived Signature Skill)
///                   — CARD_RULES.md §1, API_CONTRACTS.md §3
/// </code>
///
/// It is <b>identity-only and definition-based</b>, which is the deliberate
/// contrast of <c>EquippedRelics</c> above: each element is a
/// <see cref="EquippedCardIdentity"/> naming static content
/// (<c>GameServer.Domain.Cards.CardDefinition</c>), not an owned copy. There
/// are no Card instances at all (ADR-012 item 9), so if the same
/// <c>CardDefinitionId</c> appears more than once, the repeated elements are
/// <b>that same definition repeated</b> — permitted only up to its
/// per-CardDefinition loadout copy limit (<c>CARD_RULES.md</c> §1) — and never
/// separate owned or persistent entities.
///
/// <b>Element order carries no gameplay significance</b> (§2.3). No rule reads
/// card array positions, so this collection must not be read as an equip slot
/// order; the slot semantics belong to <c>EquippedRelics</c> alone. No
/// <c>CardSlot</c>, slot index, or copy index exists anywhere in this type.
///
/// The array is <b>fixed at battle start</b> and never changes: re-equipping is
/// impossible mid-battle (<c>CARD_RULES.md</c> §1; ADR-012 item 10), it is
/// never re-read from the Player's unlocks during the battle, and it is never
/// written back (<c>DATABASE.md</c> §2: "Battle equip of Cards is not persisted
/// here").
/// </param>
/// <b><c>StatusEffects[]</c> is the Status Effect stage's member.</b> It is the
/// collection of active Status Effect instances on this entity
/// (<c>GAME_STATE.md</c> §2.3.1) — the same element shape and the same lifecycle
/// as <c>BossState.StatusEffects[]</c> (§2.4.1: "identical element schema,
/// identical lifecycle"), with no pet-specific variant.
///
/// It is <b>always present</b>: §2.3.2 item 1 makes an entity with no active
/// effect serialize an <b>empty array</b>, because "the collection always exists
/// (§0 item 4), so it is never omitted and never <c>null</c>". A <c>null</c> here
/// would be the absent-collection state §2.3.2 item 1 states is not
/// representable, so this member is non-nullable and defaults to the empty
/// collection rather than to <c>null</c>.
///
/// Its mutation — apply, refresh, consume at <c>GAME_RULES.md</c> §17 step 19a,
/// and expire — is owned by <c>GAME_STATE.md</c> §5.1.1 and implemented by
/// <see cref="StatusEffectLifecycle"/>. The gameplay rule behind the countdown is
/// <c>COMBAT_RULES.md</c> §5.3 and is not restated here.
///
/// It is a wire member, delivered as <c>petState.statusEffects[]</c>
/// (<c>SIGNALR_PROTOCOL.md</c> §4.3 item 14, the <c>TASK-160</c> D-1A Product
/// Owner decision): the active Pet's own active instances ride the existing
/// <c>BattleStateUpdated</c> push, always present as an array and empty when no
/// effect is active. Carrying it here still adds no method, event, or
/// subscription, and the sibling collections below remain undelivered.
/// </param>
/// <param name="PetId">
/// The owned Pet instance this battle's active Pet is (<c>GAME_STATE.md</c>
/// §2.3) — the same value as <see cref="GameServer.Domain.Pets.Pet.PetInstanceId"/>
/// (<c>DATABASE.md</c> §1), the <c>petId</c> submitted to
/// <c>POST /api/battle/start</c> (<c>API_CONTRACTS.md</c> §3), and
/// <c>BattleResult.PetInstanceId</c> at battle end (<c>DATABASE.md</c> §1).
///
/// It is the <b>instance</b>, never <c>PetDefinitionId</c> and never the display
/// <c>Identity</c> name (<c>PET_RULES.md</c> §2, <c>PetDefinition.Identity</c>):
/// the instance is what the battle-end persistence path needs, and <c>ADR-014</c>
/// decision 4 records that this member already is it — no second
/// <c>PetInstanceId</c> member exists in this record (<c>GAME_STATE.md</c> §0
/// item 5).
///
/// It is <b>not optional, not nullable, and never lazily initialized</b>: a
/// battle always has its one active Pet, which was selected from the Player's
/// owned collection at battle creation (§2.3 item 3). A caller therefore
/// supplies the identity of the owned Pet it selected rather than letting one
/// be defaulted with an invented value.
/// </param>
public readonly record struct PetState(
    PetId PetId,
    int HP,
    int MaxHP,
    int ATK,
    int DEF,
    int Crit,
    int Power,
    Element Element,
    PassiveId PassiveId,
    PassiveProgress PassiveProgress,
    PassiveResetBehavior? PassiveResetOverride = null,
    EquippedRelicIdentity[]? EquippedRelics = null,
    EquippedCardIdentity[]? EquippedCards = null)
{
    /// <summary>
    /// The entity's active Status Effect instances (<c>GAME_STATE.md</c> §2.3.1).
    ///
    /// <b>It is never <c>null</c>.</b> §2.3.2 item 1 makes an entity with no
    /// active effect an <b>empty array</b> — the collection "always exists (§0
    /// item 4), so it is never omitted and never <c>null</c>" — so <c>null</c> is
    /// not a state this record can hold: it is initialized to the empty collection
    /// and every write goes through this one member, which <see cref="StatusEffectLifecycle"/>
    /// is the only code that does. That matters for value equality as well as for
    /// reads: two states differing only in "unset" versus "empty" are the same
    /// documented state, so they must compare equal (§2.3.2 item 5's round-trip
    /// obligation).
    ///
    /// Its mutation — apply, refresh, consume at <c>GAME_RULES.md</c> §17 step 19a,
    /// and expire — is owned by <c>GAME_STATE.md</c> §5.1.1 (see
    /// <see cref="StatusEffectLifecycle"/>). It <b>is</b> delivered, as
    /// <c>petState.statusEffects[]</c> (<c>SIGNALR_PROTOCOL.md</c> §4.3 item 14);
    /// this member is the state the projection reads.
    /// </summary>
    public StatusEffect[] ActiveStatusEffects { get; init; } = [];

    /// <summary>
    /// Whether this state's Status Effect collection holds the same instances as
    /// another's — the structural comparison <c>GAME_STATE.md</c> §2.3.2 item 5's
    /// round-trip obligation requires.
    ///
    /// The record's own equality compares an array member by reference, so two
    /// states that a round trip made hold the same elements would compare unequal
    /// on that member alone. This is the same need
    /// <see cref="Match3.BoardState.CellsEqual"/> answers for the board (§2.1.7
    /// item 5), applied to this collection.
    /// </summary>
    /// <param name="other">The state to compare the collection against.</param>
    public bool StatusEffectsEqual(PetState other) =>
        StatusEffectLifecycle.EffectsEqual(ActiveStatusEffects, other.ActiveStatusEffects);

    /// <summary>
    /// The entity's active temporary Crit modifiers awaiting consumption by a
    /// qualifying owner attack (<c>GAME_STATE.md</c> §2.3.4, <c>ADR-017</c>).
    ///
    /// <code>
    /// NextAttackCritModifier
    /// ├── SourceIdentity     the stable source-scoped removal key
    /// └── CritContribution   the Crit increase in percentage points
    /// </code>
    ///
    /// <b>It is never <c>null</c>.</b> §2.3.4 item 5 makes the collection
    /// always-present — "Absence of the <i>collection</i> is not a representable
    /// state" — so an entity with no active modifier holds an <b>empty
    /// collection</b>, exactly as §2.3.2 item 1 fixes for
    /// <see cref="ActiveStatusEffects"/>. It is initialized to the empty
    /// collection and every write goes through this one member, which
    /// <see cref="NextAttackCritModifiers"/> is the only code that does. That
    /// matters for value equality as well as for reads: two states differing only
    /// in "unset" versus "empty" are the same documented state, so they must
    /// compare equal (the round-trip obligation of §2.3.4 item 8).
    ///
    /// <b>This is not <c>StatusEffects[]</c>, and the two do not overlap.</b>
    /// §2.3.4 item 1 states the boundary: a modifier here carries no
    /// <c>RemainingTurns</c> and no <c>ExpiryCondition</c>, so it does not engage
    /// §2.3.1 item 3's exclusive duration dichotomy (neither widened nor relaxed by
    /// this collection) and does not engage item 6's one-instance-per-identity rule,
    /// which continues to govern <see cref="ActiveStatusEffects"/> alone. §2.3.3's
    /// prohibition is likewise honored: this is not a pending or queued collection
    /// and not a second representation of an in-flight application — a modifier
    /// held here has already been applied, and is awaiting an attack rather than a
    /// write-back.
    ///
    /// <b>Its lifetime is not Turn-based.</b> §5.1.2 items 2–3 state that the step
    /// 19a pass does not touch these elements and that there is no expiry of any
    /// kind and no cleanup pass. An unconsumed modifier therefore persists across
    /// Turns until a qualifying attack consumes it — deliberately, not as a leak.
    ///
    /// Its mutation — create/refresh and consume — is owned by §5.1.2 (see
    /// <see cref="NextAttackCritModifiers"/>). It is <b>not a wire member</b>
    /// (§2.3.4 item 8: <c>SIGNALR_PROTOCOL.md</c> §4.2/§4.3 fix the current payload
    /// member sets), so it is carried here as state, not delivered. It is likewise
    /// not a Redis-only concern: it serializes with <c>BattleState</c> under the
    /// existing round-trip obligation and adds no key and no Redis-only field
    /// (<c>REDIS_STATE.md</c> §7 item 13).
    /// </summary>
    public NextAttackCritModifier[] NextAttackCritModifiers { get; init; } = [];

    /// <summary>
    /// Whether this state's NextAttack Crit modifier collection holds the same
    /// elements in the same order as another's — the structural comparison
    /// <c>GAME_STATE.md</c> §2.3.4 item 8's round-trip obligation requires.
    ///
    /// This is the same need <see cref="StatusEffectsEqual"/> answers for
    /// <c>StatusEffects[]</c>, applied to this collection.
    /// </summary>
    /// <param name="other">The state to compare the collection against.</param>
    public bool NextAttackCritModifiersEqual(PetState other) =>
        Battle.NextAttackCritModifiers.ModifiersEqual(
            NextAttackCritModifiers,
            other.NextAttackCritModifiers);

    /// <summary>
    /// The Pet's applied, Battle-scoped ATK modifiers — one entry per active source
    /// currently modifying this Pet's ATK (<c>GAME_STATE.md</c> §2.3.7;
    /// <c>TASK-136</c> D1/D2).
    ///
    /// <code>
    /// ATKModifier
    /// ├── SourceIdentity          the replace/refresh and removal key
    /// └── ATKModifierPercentage   signed percentage points
    /// </code>
    ///
    /// <b>It is never <c>null</c>.</b> §2.3.7 item 6 makes the collection
    /// always-present — "A Pet with no active ATK modifier holds an <b>empty</b>
    /// <c>ATKModifiers[]</c> — it is never omitted, never <c>null</c>, and never
    /// represented by a stored zero or a stored inactive flag. Absence of the
    /// collection is not a representable state" — so it is initialized to the empty
    /// collection and every write goes through this one member, which
    /// <see cref="ATKModifiers"/> is the only code that does. That matters for value
    /// equality as well as for reads: two states differing only in "unset" versus
    /// "empty" are the same documented state, so they must compare equal
    /// (§2.3.8 item 5's round-trip obligation).
    ///
    /// <b>Its element order is deterministic, and it carries both lifetimes.</b>
    /// §2.3.7 item 7 fixes the element order as a sort on
    /// <see cref="ATKModifier.SourceIdentity"/> — "Unlike the sibling collections,
    /// whose order records application or equip order, this collection's order is a
    /// deterministic sort on the identity key, so the serialized order is reproducible
    /// from the element set alone" — while §2.3.8 item 6 states that no rule reads
    /// element positions. <c>REDIS_STATE.md</c> §7 item 16 draws the storage
    /// consequence, and item 11 there and §2.3.7 item 11 here record that an element
    /// declares <c>Battle</c> or <c>NextAttack</c> and that neither is Turn-based: a
    /// <c>NextAttack</c> element is consumed by the owner's qualifying attack
    /// (<c>COMBAT_RULES.md</c> §3.3 items 7–11) and a <c>Battle</c> element lasts
    /// until its source is removed or the battle ends. The ordering is maintained by
    /// <see cref="ATKModifiers"/>, so a caller never sorts this member itself.
    ///
    /// <b>It is not a second representation of ATK.</b> §2.3.7 item 9 keeps
    /// <see cref="ATK"/> the permanent/base value, never mutated by an entry here, and
    /// makes the composed <c>EffectivePetATK</c> a value <b>derived at attack
    /// resolution</b> by <c>COMBAT_RULES.md</c> §5.6.6's composition rule — not stored
    /// in <c>BattleState</c>, not a member here, and not a second representation of
    /// the ATK stat (<c>GAME_STATE.md</c> §0 item 5).
    ///
    /// <b>Neither lifetime is Turn-based, and the collection is not Battle-only.</b>
    /// §2.3.7 item 8: for <b>both</b> lifetimes there is no Turn countdown, no
    /// <c>RemainingTurns</c>, no <c>ExpiresAt</c>, no step 19a participation, and no
    /// automatic end-of-Turn cleanup, and the collection is not carried into a later
    /// battle — a new battle is a new <c>BattleState</c>. Removal is source-scoped for
    /// a <c>Battle</c> element and consumption-driven by the qualifying attack for a
    /// <c>NextAttack</c> element; both are owned by §5.1.4 (see
    /// <see cref="ATKModifiers"/>).
    ///
    /// Its mutation — apply/refresh and source-specific removal — is owned by §5.1.4.
    /// It is <b>not a wire member</b> (§2.3.7 item 10: no <c>ATKModifierApplied</c>,
    /// <c>ATKModifierExpired</c>, or <c>ATKChanged</c> event or method exists), so it
    /// is carried here as state, not delivered. It is likewise not a Redis-only
    /// concern: it serializes with <c>BattleState</c> under the existing round-trip
    /// obligation and adds no key and no Redis-only field
    /// (<c>REDIS_STATE.md</c> §7 item 16).
    /// </summary>
    public ATKModifier[] ATKModifiers { get; init; } = [];

    /// <summary>
    /// Whether this state's ATK modifier collection holds the same elements in the
    /// same order as another's — the structural comparison <c>GAME_STATE.md</c> §2.3.8
    /// item 5's round-trip obligation requires.
    ///
    /// This is the same need <see cref="NextAttackCritModifiersEqual"/> answers for the
    /// sibling collection, applied to this one.
    /// </summary>
    /// <param name="other">The state to compare the collection against.</param>
    public bool ATKModifiersEqual(PetState other) =>
        Battle.ATKModifiers.ModifiersEqual(ATKModifiers, other.ATKModifiers);

    /// <summary>
    /// The Pet's applied, Battle-scoped Card-cost modifiers — one entry per active
    /// source currently reducing the cost of this Pet's Cards
    /// (<c>GAME_STATE.md</c> §2.3.5; <c>TASK-134</c> D1/D2).
    ///
    /// <code>
    /// CardCostModifier
    /// ├── SourceIdentity           the replace/refresh and removal key
    /// └── CostReductionPercentage  percentage points
    /// </code>
    ///
    /// <b>It is never <c>null</c>.</b> §2.3.5 item 6 makes the collection
    /// always-present — "Absence of the <i>collection</i> is not a representable
    /// state. An empty collection is the statement 'no Card-cost modifier is active',
    /// and there is no sentinel element, no null, and no omitted member standing in
    /// for it" — so it is initialized to the empty collection and every write goes
    /// through this one member, which <see cref="CardCostModifiers"/> is the only code
    /// that does. As with the sibling collections, that makes "unset" and "empty" the
    /// same documented state for value equality (§2.3.6 item 5's round-trip
    /// obligation).
    ///
    /// <b>It lives on <c>PetState</c> because the effect's target is the Pet.</b>
    /// §2.3.5 item 2 and <c>RELIC_RULES.md</c> §8.3 fix the <c>CardCost</c> effect's
    /// <c>target</c> as <c>Pet</c>, so the modifier modifies the <b>active Pet's own
    /// Card costs</b>. The Boss carries no such collection (§2.3.5 item 2), and Cards
    /// are cast by the active Pet (<c>CARD_RULES.md</c> §3).
    ///
    /// <b>Its order is stable but not semantic, and it is not sorted.</b> §2.3.5
    /// item 8 and §2.3.6 item 6 make ordering non-semantic while requiring it to be
    /// preserved: "a round trip must still return the elements in the order they were
    /// written". This is the deliberate contrast with <see cref="ATKModifiers"/>, whose
    /// §2.3.7 item 7 order is a deterministic identity sort; a caller must therefore
    /// not reorder this member, and <see cref="CardCostModifiers"/> preserves position
    /// on refresh and on removal.
    ///
    /// <b>It is not a second representation of a Card's cost.</b> §2.3.5 item 4 leaves
    /// the composition, its cap, and the arithmetic to <c>CARD_RULES.md</c> §3.6, whose
    /// item 2 makes <c>EffectiveCardCost</c> a runtime value derived for the cast being
    /// resolved — explicitly not a <c>PetState</c> member, not a <c>BattleState</c>
    /// member, and not a Redis field. Authored <c>CardDefinition.PowerCost</c> is
    /// likewise never mutated (§2.3.5 preamble).
    ///
    /// <b>Its lifetime is <c>Battle</c>, and it is not Turn-based.</b> §2.3.5 item 9 and
    /// §5.1.3 items 3 and 5: there is no Turn countdown, no <c>RemainingTurns</c>, no
    /// <c>ExpiresAt</c>, and no automatic cleanup, and the collection is not carried
    /// into a later battle — a new battle is a new <c>BattleState</c> with an empty
    /// collection. Removal is source-specific and is owned by §5.1.3 (see
    /// <see cref="CardCostModifiers"/>).
    ///
    /// Its mutation — apply/refresh and source-specific removal — is owned by §5.1.3.
    /// It is <b>not a wire member</b> (§2.3.5 item 10: no
    /// <c>CardCostModifierApplied</c>, <c>CardCostModifierExpired</c>, or
    /// <c>CardCostChanged</c> event or method exists), and it adds no Redis key and no
    /// Redis-only field (<c>REDIS_STATE.md</c> §7 item 15).
    /// </summary>
    public CardCostModifier[] CardCostModifiers { get; init; } = [];

    /// <summary>
    /// Whether this state's Card-cost modifier collection holds the same elements in
    /// the same order as another's — the structural comparison <c>GAME_STATE.md</c>
    /// §2.3.6 item 5's round-trip obligation requires.
    ///
    /// This is the same need <see cref="ATKModifiersEqual"/> answers for the sibling
    /// collection, applied to this one.
    /// </summary>
    /// <param name="other">The state to compare the collection against.</param>
    public bool CardCostModifiersEqual(PetState other) =>
        Battle.CardCostModifiers.ModifiersEqual(CardCostModifiers, other.CardCostModifiers);

    /// <summary>
    /// The Pet's applied, <c>Battle</c>-scoped Burn-damage modifiers — one entry
    /// per active source currently modifying the Pet's own Burn damage ticks.
    ///
    /// <code>
    /// BurnDamageModifier
    /// ├── SourceIdentity         the replace/refresh and removal key
    /// └── BurnDamagePercentage   percentage points
    /// </code>
    ///
    /// <b>It is the applied form of the Relic <c>BurnDamage</c> effect</b>
    /// (<c>RELIC_RULES.md</c> §8.2 item 1, §8.5 item 5): a percentage modifier to
    /// Burn damage-over-time ticks (<c>COMBAT_RULES.md</c> §5). It lives on
    /// <c>PetState</c> because the effect's declared <c>target</c> is <c>Pet</c>
    /// and §6 note 1 reads that value as the Pet being the <b>owner/source
    /// context</b> of the modification — the modifier scales the Burn damage the
    /// Pet owns, so it is carried by the Pet and not by the Boss or by a Burn
    /// instance.
    ///
    /// <b>It is never <c>null</c>.</b> "No Burn-damage modifier active" is an
    /// <b>empty collection</b>, following the same always-present convention the
    /// sibling modifier collections above state; it is initialized to the empty
    /// collection and every write goes through this one member, which
    /// <see cref="BurnDamageModifiers"/> is the only code that does. That matters
    /// for value equality as well as for reads: two states differing only in
    /// "unset" versus "empty" are the same documented state, so they must compare
    /// equal.
    ///
    /// <b>Ownership scoping is the Burn instance's own source, not this member's
    /// placement.</b> <c>COMBAT_RULES.md</c> §5.2 item 4 scopes a
    /// <c>BurnDamage</c> modifier to the Burn instances "owned by the source that
    /// modifier belongs to", and <c>RELIC_RULES.md</c> §6 note 1 fixes which those
    /// are: Pet-owned Burn is modified, Boss-owned Burn is not, and the
    /// distinction is the instance's <see cref="StatusEffect.Source"/> rather than
    /// the entity receiving the tick's damage (TASK-178 Product Owner decision
    /// <b>Q-4 = C</b>). <see cref="BurnDamageModifiers.AppliesTo"/> is that test;
    /// a tick reader must apply it rather than assuming this collection reaches
    /// every Burn the Pet can see.
    ///
    /// <b>Its lifetime is <c>Battle</c>, and it is not Turn-based.</b> Every
    /// element's lifetime is fixed as <c>Battle</c> by
    /// <c>RELIC_RULES.md</c> §8.3's <c>BurnDamage</c> row, so no lifetime member
    /// is carried: there is nothing to distinguish, exactly as the sibling
    /// <see cref="CardCostModifiers"/> collection records for its own single
    /// lifetime. There is no Turn countdown, no <c>RemainingTurns</c>, no
    /// <c>ExpiresAt</c>, and no automatic cleanup, and the collection is not
    /// carried into a later battle — a new battle is a new <c>BattleState</c>.
    /// Removal is source-specific and is owned by
    /// <see cref="BurnDamageModifiers"/>.
    ///
    /// <b>It changes damage and nothing else.</b> <c>COMBAT_RULES.md</c> §5.2
    /// item 4: a <c>BurnDamage</c> modifier must not emit, create, re-enter, or
    /// refresh a Burn event or Burn instance, and must not extend or consume an
    /// instance's duration. This collection therefore carries no Burn instance, no
    /// duration, and no tick state — the instance and its countdown remain
    /// <see cref="ActiveStatusEffects"/>' and <see cref="StatusEffectLifecycle"/>'s.
    ///
    /// <b>It is not a wire member.</b> No <c>BurnDamageModifierApplied</c>,
    /// <c>BurnDamageChanged</c>, or equivalent event or method exists, following
    /// the same "state added is not wire exposure added" convention the sibling
    /// collections state, so it is carried here as state, not delivered. It
    /// introduces no PostgreSQL persistence and no new Redis key: it rides the
    /// existing <c>battle:{battleId}:state</c> record under the unchanged sliding
    /// TTL and unchanged <c>Sequence</c> compare-and-set.
    /// </summary>
    public BurnDamageModifier[] BurnDamageModifiers { get; init; } = [];

    /// <summary>
    /// Whether this state's Burn-damage modifier collection holds the same
    /// elements in the same order as another's — the structural comparison a
    /// round-trip obligation requires for this collection.
    ///
    /// This is the same need <see cref="CardCostModifiersEqual"/> answers for its
    /// sibling, applied to this one.
    /// </summary>
    /// <param name="other">The state to compare the collection against.</param>
    public bool BurnDamageModifiersEqual(PetState other) =>
        Battle.BurnDamageModifiers.ModifiersEqual(BurnDamageModifiers, other.BurnDamageModifiers);

    /// <summary>
    /// The documented MVP starting <c>MaxHP</c> (<c>COMBAT_RULES.md</c> §1.1:
    /// "Max HP — maximum health — MVP default: 1000").
    ///
    /// Configuration, not an invariant: §1.1 states these values "are not
    /// permanent invariants — future Pet progression (Level, Star, Tier) may
    /// produce different actual Battle Stats". It is the <b>initial</b> value
    /// of a created battle, which is why it is a constant here and not a clamp
    /// or a formula.
    /// </summary>
    public const int DefaultMaxHP = 1000;

    /// <summary>
    /// The documented MVP starting <c>HP</c>
    /// (<c>COMBAT_RULES.md</c> §1.1: "HP — current health — MVP default: 1000").
    ///
    /// A battle begins at full health, so this is the same <c>1000</c> as
    /// <see cref="DefaultMaxHP"/> — not because one is derived from the other,
    /// but because §1.1 gives both the same MVP default.
    /// </summary>
    public const int DefaultHP = 1000;

    /// <summary>
    /// The documented MVP starting <c>ATK</c> (<c>COMBAT_RULES.md</c> §1.1:
    /// "ATK — attack power — MVP default: 50"). Configuration, not an invariant
    /// (§1.1).
    /// </summary>
    public const int DefaultATK = 50;

    /// <summary>
    /// The documented MVP starting <c>DEF</c> (<c>COMBAT_RULES.md</c> §1.1:
    /// "DEF — defense — MVP default: 25"). Configuration, not an invariant
    /// (§1.1).
    /// </summary>
    public const int DefaultDEF = 25;

    /// <summary>
    /// The documented MVP starting <c>Crit</c> (<c>COMBAT_RULES.md</c> §1.1:
    /// "Crit — critical hit chance (%) — MVP default: 5%").
    ///
    /// The unit is the percent of §1.1, so the value is <c>5</c> and not
    /// <c>0.05</c> — see the <c>Crit</c> parameter for why the unit is not
    /// converted. Configuration, not an invariant (§1.1).
    /// </summary>
    public const int DefaultCrit = 5;

    /// <summary>
    /// The documented starting <c>Power</c>
    /// (<c>COMBAT_RULES.md</c> §1.1: "Power — resource for casting Cards /
    /// Skills, range 0–100").
    ///
    /// §1.1 gives no MVP default for Power other than the range, and the range's
    /// floor is where a battle begins: Power is generated by Match-3 (§2) and no
    /// Match has occurred at creation. Starting at <c>0</c> is therefore the
    /// documented starting value, not a chosen one. The cap of <c>100</c> is
    /// owned by §1.1 and <c>GAME_RULES.md</c> §12 and is enforced at the write
    /// site (<c>ResourceGenerator.ApplyPower</c>), not here.
    /// </summary>
    public const int DefaultPower = 0;

    /// <summary>
    /// The Reset Behavior the tracker applies for this Pet's Passive
    /// (<c>PASSIVE_RULES.md</c> §4).
    ///
    /// An absent <see cref="PassiveResetOverride"/> reads as
    /// <see cref="PassiveResetBehavior.Default"/> — §2.3 makes absence the
    /// representation of the default, so this is the documented reading rather than
    /// a fallback chosen here.
    /// </summary>
    public PassiveResetBehavior ResetBehavior => PassiveResetOverride ?? PassiveResetBehavior.Default;

    /// <summary>
    /// Whether this Pet's Passive declares a non-default Reset Behavior — the
    /// condition under which <c>GAME_STATE.md</c> §2.3 says
    /// <c>PassiveResetOverride</c> is present, and under which
    /// <c>SIGNALR_PROTOCOL.md</c> §4.3 makes the wire member present.
    /// </summary>
    public bool HasResetOverride => PassiveResetOverride is not null;

    /// <summary>
    /// The documented <c>PetState</c> of a newly created battle: the identity of
    /// the owned Pet instance the battle selected, the active Pet's
    /// combat stats at their <c>COMBAT_RULES.md</c> §1.1 MVP defaults at full
    /// health with no Power generated, its Element, Passive identity, progress at
    /// the start of its first charge, and
    /// the declared Reset Behavior (<c>GAME_STATE.md</c> §2.3;
    /// <c>SIGNALR_PROTOCOL.md</c> §4.3 item 4).
    /// </summary>
    /// <param name="petId">
    /// The identity of the owned Pet instance the battle selected
    /// (<c>GAME_STATE.md</c> §2.3, <c>DATABASE.md</c> §1) — the
    /// <see cref="GameServer.Domain.Pets.Pet.PetInstanceId"/> the battle-start
    /// path resolved from the Player's owned collection. It is supplied by the
    /// battle-creation caller, which already holds the instance, so no value is
    /// invented here and none is taken from client-supplied state
    /// (<c>GAME_RULES.md</c> §18, <c>ADR-001</c>).
    /// </param>
    /// <param name="element">
    /// The active Pet's one Element (<c>GAME_STATE.md</c> §2.3,
    /// <c>PET_RULES.md</c> §1, <c>ELEMENT_RULES.md</c> §6). It comes from the
    /// Pet's definition, which is why it is supplied by the battle-creation
    /// caller rather than invented here.
    /// </param>
    /// <param name="passiveId">The active Pet's Passive identity (§2.3 item 2).</param>
    /// <param name="passiveThreshold">
    /// The Passive's Threshold — "e.g. 'every 5 Matches'" (<c>PASSIVE_RULES.md</c>
    /// §1). It comes from the Passive's definition, which is why it is supplied by
    /// the battle-creation caller rather than invented here.
    /// </param>
    /// <param name="passiveResetOverride">
    /// The declared non-default Reset Behavior, or <c>null</c> for the default
    /// (§4 items 1–3).
    /// </param>
    /// <param name="equippedRelics">
    /// The battle-scoped Relic loadout snapshot — 3–5 owned Relic instance
    /// identities in equip-slot order (<c>RELIC_RULES.md</c> §2.2, §2.3, §2.5;
    /// <c>GAME_STATE.md</c> §2.3). It is supplied by the battle-creation caller,
    /// which obtained it from the Relic loadout validator, so no value is invented
    /// here. It is carried across by reference, in exactly the order given: this
    /// method never sorts, filters, or de-duplicates it
    /// (<c>RELIC_RULES.md</c> §2.3 item 2).
    /// </param>
    /// <param name="equippedCards">
    /// The battle-scoped Card loadout snapshot — exactly 4
    /// <see cref="EquippedCardIdentity"/> values: the 3 submitted Basic Cards
    /// plus the active Pet's derived Signature Skill Card
    /// (<c>CARD_RULES.md</c> §1; <c>API_CONTRACTS.md</c> §3;
    /// <c>GAME_STATE.md</c> §2.3). It is supplied by the battle-creation caller,
    /// which obtained it from the Card loadout validator, so no value is invented
    /// here. It is carried across by reference exactly as given: this method
    /// never sorts, filters, or de-duplicates it — repeated identities are the
    /// documented same-definition repetition, not a defect
    /// (<c>GAME_STATE.md</c> §2.3).
    /// </param>
    /// <param name="statusEffects">
    /// The active Status Effect instances the battle's Pet begins with, or
    /// <c>null</c> for none (<c>GAME_STATE.md</c> §2.3.1).
    ///
    /// A battle begins with no Status Effect applied — no content-defined source
    /// applies one at creation, because application happens during a resolution's
    /// steps (<c>GAME_STATE.md</c> §5.1.1 item 1, <c>GAME_RULES.md</c> §17 step
    /// 18b) and battle creation is not a resolution. The parameter therefore
    /// defaults to none, and a <c>null</c> reads as the documented empty collection
    /// rather than as an absent one (§2.3.2 item 1: the collection "always exists
    /// ... it is never omitted and never null"). It exists so a caller that must
    /// reconstruct a state carrying instances — a recovery, or a test — can do so
    /// without a second construction path.
    /// </param>
    public static PetState AtBattleCreation(
        PetId petId,
        Element element,
        PassiveId passiveId,
        int passiveThreshold,
        PassiveResetBehavior? passiveResetOverride = null,
        EquippedRelicIdentity[]? equippedRelics = null,
        EquippedCardIdentity[]? equippedCards = null,
        StatusEffect[]? statusEffects = null) =>
        // §2.3 item 3 / §4.3 item 4: progress begins at 0 against the Passive's own
        // Threshold. It is never absent, never lazily initialized, and never
        // defaulted with an invented value. The Element is likewise the Pet's own
        // value, carried across unchanged (§2.3, ELEMENT_RULES.md §1.2).
        //
        // COMBAT_RULES.md §1.1 / GAME_STATE.md §2.3: the combat stats begin at
        // their documented MVP defaults at full health with no Power yet
        // generated. The values are read from the constants above and are not
        // restated, so §1.1's balance values have exactly one spelling.
        new(
            PetId: petId,
            HP: DefaultHP,
            MaxHP: DefaultMaxHP,
            ATK: DefaultATK,
            DEF: DefaultDEF,
            Crit: DefaultCrit,
            Power: DefaultPower,
            Element: element,
            PassiveId: passiveId,
            PassiveProgress: PassiveProgress.AtStart(passiveThreshold),
            PassiveResetOverride: passiveResetOverride,
            EquippedRelics: equippedRelics,
            EquippedCards: equippedCards)
        {
            // §2.3.1: the Status Effect collection begins empty — no content-defined
            // source applies one at creation, because application happens during a
            // resolution (GAME_STATE.md §5.1.1 item 1, GAME_RULES.md §17 step 18b)
            // and battle creation is not a resolution. A caller reconstructing a
            // state that carries instances sets the member directly.
            ActiveStatusEffects = statusEffects ?? [],

            // §2.3.4 item 5 / §5.1.2 item 1: the NextAttack Crit modifier
            // collection likewise begins empty, and for the same reason — a
            // modifier is created by a source resolving during a resolution
            // (GAME_RULES.md §17 step 14 for a Card or Pet Skill, step 10 for a
            // Pet Passive), and battle creation resolves nothing. The collection
            // is always present and empty is its no-modifier value, never an
            // omission or a null.
            NextAttackCritModifiers = [],

            // §2.3.7 item 6 / §5.1.4 item 1: the ATK modifier collection begins
            // empty on the same documented basis. A modifier is applied by a source
            // whose Trigger+Condition is met at GAME_RULES.md §17 step 11 during a
            // resolution, and battle creation is not a resolution. The collection is
            // always present — "it is never omitted, never null, and never
            // represented by a stored zero or a stored inactive flag" — so empty is
            // its no-modifier value rather than an omission.
            ATKModifiers = [],

            // §2.3.5 item 6 / §5.1.3 item 5: the Card-cost modifier collection
            // begins empty for the same reason and with the same always-present
            // rule, and §5.1.3 item 5 adds that it "needs no separate expiry step: a
            // new battle is a new BattleState with an empty collection".
            CardCostModifiers = [],

            // The Burn-damage modifier collection begins empty on the same
            // documented basis: it is the applied form of the Relic effect resolved
            // at its own Trigger's firing point during a resolution, and battle
            // creation is not a resolution. "No modifier active" is the empty
            // collection rather than an omission or a null, and a new battle is a
            // new BattleState with an empty collection.
            BurnDamageModifiers = [],
        };
}
