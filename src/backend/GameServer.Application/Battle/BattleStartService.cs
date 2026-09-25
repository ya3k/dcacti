using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Relics;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;

namespace GameServer.Application.Battle;

/// <summary>
/// The <c>POST /api/battle/start</c> orchestration boundary
/// (<c>API_CONTRACTS.md</c> §3).
///
/// <code>
/// BattleStartRequest
///         ↓
/// resolve selected Pet            PET_RULES.md §2, DATABASE.md §1
///         ↓
/// validate Pet ownership          API_CONTRACTS.md §3
///         ↓
/// resolve selected Boss           BOSS_RULES.md §6
///         ↓
/// CardLoadoutService              CARD_RULES.md §1  → EquippedCards[4]
///         ↓
/// RelicLoadoutService             RELIC_RULES.md §2 → EquippedRelics[3–5]
///         ↓
/// PetConfiguration                BattleStateService
///         ↓
/// CreateBattle                    GAME_STATE.md §2.0.5, §2.3, §2.4
///         ↓
/// BattleStartResult               API_CONTRACTS.md §3 response
/// </code>
///
/// <b>It coordinates; it implements no rule.</b> The Card rules (count,
/// ownership, category, copy limit, Signature Skill derivation) are
/// <see cref="CardLoadoutService"/>'s, the Relic rules (count, distinctness,
/// ownership, slot order) are <see cref="RelicLoadoutService"/>'s, and the
/// battle composition is <see cref="BattleStateService"/>'s
/// (<c>ARCHITECTURE.md</c> §2.1). This type sequences them, supplies each the
/// values the others produced, and maps outcomes to the documented codes. It
/// contains no copy-limit arithmetic, no duplicate detection, no Signature
/// Skill selection, no combat-stat initialization, and no Boss logic.
///
/// <b>It must never:</b>
/// <list type="bullet">
/// <item>reimplement or duplicate a loadout rule — the services own them and
/// are called, not bypassed,</item>
/// <item>the Card rules (count, ownership, category, copy limit, Signature
/// Skill derivation) are <see cref="CardLoadoutService"/>'s, the Relic rules
/// (count, distinctness, ownership, slot order) are
/// <see cref="RelicLoadoutService"/>'s, and the battle composition is
/// <see cref="BattleStateService"/>'s (<c>ARCHITECTURE.md</c> §2.1),</item>
/// <item>invent an error code — every rejection maps to one of
/// <c>API_CONTRACTS.md</c> §3's documented outcomes, and the domain services'
/// finer-grained internal reasons are never widened into the wire
/// contract,</item>
/// <item>create a partial battle — every validation completes before
/// <see cref="BattleStateService.CreateBattle"/> is called, and a rejection
/// therefore creates nothing at all,</item>
/// <item>read Cards or Relics from the database after the snapshot exists, or
/// persist a loadout/equip row (<c>RELIC_RULES.md</c> §2.5,
/// <c>CARD_RULES.md</c> §1, ADR-012 items 7–10),</item>
/// <item>write <c>BattleState</c> to Redis. <c>REDIS_STATE.md</c> §7 items 1–2
/// and 12 defer that storage, and §7 item 7 makes it required only when a real
/// battle can be created — the deferral gate <c>ROADMAP.md</c> §1 raises with
/// this endpoint is a separate decision, and this task writes no Redis key and
/// introduces no partial store,</item>
/// <item>implement any gameplay: no Match-3, no Card cast, no Card or Relic
/// effect, no Boss AI, no victory/defeat (<c>GAME_RULES.md</c> §17 steps 18–19
/// remain unimplemented),</item>
/// <item>hold the authoritative state — <see cref="BattleStateService"/> does
/// (<c>GAME_STATE.md</c> §5.1). This boundary returns identity and outcome
/// only.</item>
/// </list>
/// </summary>
public sealed class BattleStartService
{
    private readonly IPetRepository _pets;
    private readonly CardLoadoutService _cardLoadout;
    private readonly RelicLoadoutService _relicLoadout;
    private readonly BattleStateService _battles;

    /// <summary>
    /// Creates the battle-start orchestrator over the persistence, loadout, and
    /// battle-composition boundaries it coordinates.
    /// </summary>
    /// <param name="pets">The Pet persistence boundary (ownership source).</param>
    /// <param name="cardLoadout">
    /// The completed Card loadout validator (<c>CARD_RULES.md</c> §1).
    /// </param>
    /// <param name="relicLoadout">
    /// The completed Relic loadout validator (<c>RELIC_RULES.md</c> §2).
    /// </param>
    /// <param name="battles">
    /// The authoritative battle-composition boundary
    /// (<c>GAME_STATE.md</c> §2.0.5).
    /// </param>
    public BattleStartService(
        IPetRepository pets,
        CardLoadoutService cardLoadout,
        RelicLoadoutService relicLoadout,
        BattleStateService battles)
    {
        _pets = pets;
        _cardLoadout = cardLoadout;
        _relicLoadout = relicLoadout;
        _battles = battles;
    }

    /// <summary>
    /// Resolves one battle-start request for <paramref name="playerId"/>
    /// (<c>API_CONTRACTS.md</c> §3).
    ///
    /// <b>The validation order is the documented one.</b> The Pet is resolved
    /// and its ownership checked first, because the Pet is what the Card
    /// loadout's Signature Skill derivation depends on: the derived fourth Card
    /// is read through <c>PetDefinition.SignatureSkillCardId</c>
    /// (<c>CARD_RULES.md</c> §1, §4), so no Card loadout can be validated
    /// without it. The Boss and both loadouts are then validated, all of them
    /// <b>before</b> any battle state is created.
    ///
    /// <b>A rejected request creates nothing.</b> Every branch below returns
    /// before <see cref="BattleStateService.CreateBattle"/> is reached, so no
    /// battle is registered and no partially populated <c>BattleState</c> can
    /// exist (<c>API_CONTRACTS.md</c> §3: "A rejected request equips nothing and
    /// writes no battle state").
    ///
    /// <b>The battle id is server-authored.</b> The client submits no
    /// <c>battleId</c> (§1, <c>GAME_RULES.md</c> §18): the id is generated here
    /// and every other authoritative value — <c>Turn</c>, <c>Sequence</c>,
    /// <c>RngSeed</c>, <c>RngState</c>, <c>BoardState</c>, <c>Combo</c>,
    /// <c>MatchCount</c>, and the combat stats — is produced by
    /// <see cref="BattleStateService"/> from its own entropy and configuration.
    /// </summary>
    /// <param name="playerId">
    /// The requesting Player, resolved from the authenticated session
    /// (<c>API_CONTRACTS.md</c> §1, §2.5). It is required: ownership cannot be
    /// established without it, and defaulting it would be a
    /// client-authoritative claim (<c>GAME_RULES.md</c> §18).
    /// </param>
    /// <param name="request">The submitted selection.</param>
    /// <param name="cancellationToken">Cancels the reads and the composition.</param>
    /// <returns>
    /// A successful result carrying the created <c>battleId</c>, or the
    /// documented rejection.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="playerId"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> is <c>null</c>.
    /// </exception>
    public async Task<BattleStartResult> StartAsync(
        string playerId,
        BattleStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentNullException.ThrowIfNull(request);

        // ===============================================================
        // Step 1: resolve the selected Pet and validate ownership
        // ===============================================================
        // API_CONTRACTS.md §3 ("petId must be owned by the player —
        // PET_RULES.md §2") / §1 ("Ownership vs. equip: the Player owns the
        // Pet … collection"). Ownership is established from persistence, never
        // from the client's claim (GAME_RULES.md §18, ADR-001).
        //
        // The instance is resolved by its own identity and the Player check is
        // applied here, so "not found" and "belongs to another Player" both
        // become the one documented PET_NOT_OWNED outcome. The consequence is
        // deliberate: §3 states an ownership requirement, and a request cannot
        // distinguish a non-existent Pet from another Player's.
        if (string.IsNullOrWhiteSpace(request.PetId))
        {
            return BattleStartResult.Rejected(
                BattleStartOutcome.PetNotOwned,
                "A Pet must be selected and it must be owned by the player.");
        }

        var pet = await _pets.GetByIdAsync(request.PetId, cancellationToken);

        if (pet is null || !string.Equals(pet.PlayerId, playerId, StringComparison.Ordinal))
        {
            return BattleStartResult.Rejected(
                BattleStartOutcome.PetNotOwned,
                "A Pet must be selected and it must be owned by the player.");
        }

        // The Pet's combat-character configuration is read through its
        // DEFINITION, which is where Element, Passive configuration, and the
        // Signature Skill reference live (DATABASE.md §1–§2: Pet N ── 1
        // PetDefinition). The instance carries no combat stats at all
        // (ADR-011 item 5): the battle's combat values are PetState's and are
        // initialized by BattleStateService, never read from this row.
        var petDefinition = await _pets.GetDefinitionAsync(
            pet.PetDefinitionId,
            cancellationToken);

        // A Pet whose definition does not resolve cannot supply the Element,
        // the Passive, or the Signature Skill Card the battle is composed from
        // (GAME_STATE.md §2.3; CARD_RULES.md §1). No value may be invented for
        // any of them (AGENTS.md §7), so the battle is not created.
        if (petDefinition is null)
        {
            return BattleStartResult.Rejected(
                BattleStartOutcome.PetNotOwned,
                "The selected Pet's definition could not be resolved.");
        }

        // ===============================================================
        // Step 2: resolve the selected Boss
        // ===============================================================
        // API_CONTRACTS.md §3 ("bossId must be a valid MVP Boss —
        // BOSS_RULES.md §6"). BOSS_RULES.md §6 defines exactly three
        // content-defined MVP Bosses and BossDefinitions holds exactly those
        // (BOSS_RULES.md §6.1 records the remaining two as "not yet
        // content-defined"), so resolution is a lookup against the transcribed
        // content — not a new registry, and not an invented Boss.
        //
        // Boss COMBAT BEHAVIOR is not implemented here or anywhere in this
        // task (BOSS_RULES.md §3–§5): this step only establishes which Boss the
        // battle is fought against.
        if (ResolveBoss(request.BossId) is not { } bossDefinition)
        {
            return BattleStartResult.Rejected(
                BattleStartOutcome.BossNotFound,
                "The selected Boss is not a valid MVP Boss.");
        }

        // ===============================================================
        // Step 3: validate the Card loadout and derive the Signature Skill
        // ===============================================================
        // Delegated in full to CardLoadoutService (CARD_RULES.md §1;
        // API_CONTRACTS.md §3): count → ownership → category → per-definition
        // copy limit, then the derived Signature Skill Card. None of those
        // rules is restated, reordered, or duplicated here.
        //
        // The Signature Skill source is the Pet definition's own reference, so
        // the derived fourth Card is read through the Pet and is never
        // client-submitted (CARD_RULES.md §1 item 4).
        var cards = await _cardLoadout.ValidateAsync(
            playerId,
            request.CardLoadout ?? [],
            petDefinition.SignatureSkillCardId,
            cancellationToken);

        if (!cards.IsValid)
        {
            // Every Card rejection maps to the one documented INVALID_LOADOUT
            // code (API_CONTRACTS.md §3). The service's own rejection reason is
            // internal diagnostics and is deliberately not exposed as a code:
            // the message stays human-readable detail per §6.
            return BattleStartResult.Rejected(
                BattleStartOutcome.InvalidLoadout,
                "The Card loadout is invalid.");
        }

        // ===============================================================
        // Step 4: validate the Relic loadout
        // ===============================================================
        // Delegated in full to RelicLoadoutService (RELIC_RULES.md §2.1–§2.5;
        // API_CONTRACTS.md §3): count (3–5) → distinctness → ownership, with
        // the submitted order preserved as the equip-slot order. The Relic
        // contract is unchanged by this task and none of its rules is restated
        // here.
        var relics = await _relicLoadout.ValidateAsync(
            playerId,
            request.RelicLoadout ?? [],
            cancellationToken);

        if (!relics.IsValid)
        {
            // The same documented INVALID_LOADOUT code the Card path uses —
            // API_CONTRACTS.md §3 states it for both loadouts.
            return BattleStartResult.Rejected(
                BattleStartOutcome.InvalidLoadout,
                "The Relic loadout is invalid.");
        }

        // ===============================================================
        // Step 5: compose PetState from both snapshots and create the battle
        // ===============================================================
        // PetConfiguration is the documented carrier of the battle's Pet
        // configuration (GAME_STATE.md §2.3): the Element and Passive come from
        // the Pet's definition, and both battle-scoped loadout snapshots —
        // EquippedCards from the Card validator and EquippedRelics from the
        // Relic validator — are carried into PetState.AtBattleCreation
        // unchanged, in the order each validator produced.
        //
        // Both snapshots are now fixed values: nothing after this point re-reads
        // PlayerUnlockedCard, Relic, or any inventory row (RELIC_RULES.md §2.5,
        // CARD_RULES.md §1, ADR-012 items 8 and 10).
        var petConfiguration = new BattleStateService.PetConfiguration(
            Element: petDefinition.Element,
            PassiveId: petDefinition.PassiveId,
            PassiveThreshold: petDefinition.PassiveThreshold,
            PassiveResetOverride: null,
            EquippedRelics: relics.EquippedRelics,
            EquippedCards: cards.EquippedCards);

        // The battle id is the server's (API_CONTRACTS.md §3 response's
        // `battleId`; GAME_STATE.md §2.0.1). It is generated after every
        // validation has passed, so a rejected request never produces one, and
        // it is never supplied by the client.
        var battleId = Guid.NewGuid().ToString("N");

        // BattleStateService owns the composition: it chooses the seed from
        // server entropy, generates the board deterministically, and builds
        // PetState and BossState at their documented creation values
        // (GAME_STATE.md §2.0.5, §2.3 item 3, §2.4).
        _battles.CreateBattle(battleId, petConfiguration, bossDefinition);

        return BattleStartResult.Started(battleId);
    }

    /// <summary>
    /// Resolves a submitted <c>bossId</c> against the content-defined MVP Boss
    /// set (<c>API_CONTRACTS.md</c> §3, <c>BOSS_RULES.md</c> §6).
    ///
    /// The comparison is against <see cref="BossId.Value"/> — the display-name
    /// identity <c>BOSS_RULES.md</c> §6.4 fixes ("fixed here so no task invents
    /// its own") — and is ordinal, because the identity is an opaque stable name
    /// and not a culture-sensitive string.
    ///
    /// It is a lookup over the three transcribed definitions, not a registry:
    /// <c>BOSS_RULES.md</c> §6 defines exactly those three, and a Boss that is
    /// not among them is rejected rather than fabricated
    /// (<c>AGENTS.md</c> §7).
    /// </summary>
    /// <param name="bossId">The submitted Boss identity.</param>
    /// <returns>The matching definition, or <c>null</c> when none matches.</returns>
    private static BossDefinition? ResolveBoss(string? bossId)
    {
        if (string.IsNullOrWhiteSpace(bossId))
        {
            return null;
        }

        foreach (var definition in BossDefinitions.All)
        {
            if (string.Equals(definition.BossId.Value, bossId, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }
}
