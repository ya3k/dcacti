using GameServer.Domain.Relics;

namespace GameServer.Application.Relics;

/// <summary>
/// Look up the shared/static Relic content a Relic instance references, by the
/// definition's own identity (<c>DATABASE.md</c> §1: <c>RelicDefinitionId</c>).
///
/// <code>
/// Application consumer (e.g. BattleStateService)
///         ↓  IRelicDefinitionLookup (this contract)
/// RelicDefinition row, by RelicDefinitionId       PostgreSQL (Infrastructure)
///         ↓
/// Domain RelicDefinition
/// </code>
///
/// <b>It is the definition read, not the ownership read.</b> A Player's owned
/// Relic instances are a different value from the static content they reference
/// (<c>DATABASE.md</c> §2: Relic N ── 1 RelicDefinition;
/// <c>RELIC_RULES.md</c> §2.2 item 4), so this lookup takes no
/// <c>PlayerId</c>, no Discord identity, and no equipped/inventory state: a
/// definition is shared content, identical for every owner
/// (<c>DATABASE.md</c> §2). Ownership is established through
/// <see cref="IRelicRepository"/>'s Player-scoped reads at loadout validation
/// time (<c>RELIC_RULES.md</c> §2.1 item 2), never here.
///
/// <b>The Domain definition crosses this boundary; persistence does not.</b>
/// The returned value is the authoritative <see cref="RelicDefinition"/>, whose
/// structured <c>Condition</c> and <c>EffectDefinition</c> are the values
/// <c>RELIC_RULES.md</c> §8.1/§8.2 define (<c>ARCHITECTURE.md</c> §2 item 3:
/// Infrastructure depends on Domain/Application interfaces, never the other way
/// around). No EF entity, <c>DbContext</c>, database DTO, or JSON payload is
/// exposed to an Application consumer.
///
/// <b>It follows the established definition-lookup contract.</b> This mirrors
/// the Card boundary the repository already fixed for the same shape of read —
/// <c>ICardDefinitionLookup</c> / <c>ScopedCardDefinitionLookup</c> in
/// <c>GameServer.Application.Cards</c> — so the singleton-consumer adaptation is
/// the same proven one and no new pattern is introduced
/// (<c>ARCHITECTURE.md</c> §5 item 3: no separate read-model store;
/// <c>AGENTS.md</c> §9).
///
/// <b>Not found is a defined outcome, not an error to absorb.</b> A definition
/// identity with no row returns <see langword="null"/> — the same convention
/// <c>ICardDefinitionLookup</c>, <see cref="IRelicRepository.GetDefinitionAsync"/>,
/// and <c>IBossDefinitionLookup</c> follow. A caller must never fabricate a
/// definition, a default threshold, a fallback effect array, or a synthetic
/// Relic for it (<c>AGENTS.md</c> §7), and a stored value that is not well formed
/// is rejected at the read rather than defaulted (<c>DATABASE.md</c> §1's Relic
/// note item 6).
///
/// <b>It retrieves content; it resolves nothing.</b> No Relic trigger is
/// evaluated, no condition is compared, no effect is applied, and no
/// <c>RelicTriggered</c> event is emitted by this contract or its
/// implementation: Relic resolution is <c>GAME_RULES.md</c> §17 step 11 and
/// remains <b>NOT IMPLEMENTED</b> (<c>RELIC_RULES.md</c> §8.7). This boundary
/// only makes a Relic's declared content readable.
/// </summary>
public interface IRelicDefinitionLookup
{
    /// <summary>
    /// Looks up a Relic definition by its identity
    /// (<c>DATABASE.md</c> §1: <c>RelicDefinitionId</c>, value form
    /// <c>relic-&lt;ascii-kebab-case-name&gt;</c> — e.g.
    /// <c>"relic-berserker-core"</c>).
    ///
    /// <b>It is the definition identity, never the owned instance identity.</b>
    /// <c>PetState.EquippedRelics[]</c> carries an owned <c>RelicInstanceId</c>
    /// (<c>RELIC_RULES.md</c> §2.2), which references this row through
    /// <c>Relic.RelicDefinitionId</c> (<c>DATABASE.md</c> §1); the two identities
    /// are separate values and must not be substituted for each other.
    /// </summary>
    /// <param name="relicDefinitionId">
    /// The static content identity to resolve. A null, empty, or whitespace
    /// value names no row and is rejected rather than treated as a lookup miss.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>
    /// The Relic definition, or <see langword="null"/> when no definition
    /// carries that identity.
    /// </returns>
    Task<RelicDefinition?> GetDefinitionAsync(
        string relicDefinitionId,
        CancellationToken cancellationToken = default);
}
