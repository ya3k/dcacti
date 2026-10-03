using GameServer.Domain.Relics;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The structured Relic values the Infrastructure tests use when a fixture needs
/// <b>some</b> valid <c>RelicDefinition.Condition</c>/<c>EffectDefinition</c> and
/// the test is not about the Relic's own contract.
///
/// <b>Why a shared helper.</b> Before TASK-132 a Relic fixture could supply any
/// string. Both members now carry the structured contract
/// (<c>RELIC_RULES.md</c> §8.1/§8.2), so every fixture must supply a real value
/// even though it is irrelevant to what that test asserts. This keeps it in one
/// place instead of restating it per file, following the existing
/// <see cref="TestCardEffects"/> convention.
///
/// <b>It deliberately does not encode any provisioned Relic.</b> Which effect a
/// canonical Relic has is content owned by <c>RELIC_RULES.md</c> §6/§8.5 and
/// provisioned by migration, and no test fixture may become a second source for
/// it (<c>GAME_STATE.md</c> §0 item 5). The values here are well-formed contract
/// values with no content claim attached, and they are deliberately <b>not</b>
/// the values §8.5 records for any provisioned row — so a test that accidentally
/// depends on them being real content fails rather than passing by coincidence.
/// </summary>
internal static class TestRelicEffects
{
    /// <summary>
    /// A valid, well-formed structured condition — a real §8.1 value with no
    /// content claim attached.
    /// </summary>
    internal static RelicCondition Condition =>
        RelicCondition.Create(RelicConditionType.ComboAtLeast, 1);

    /// <summary>
    /// A valid, well-formed one-element effect array using §8.3's allowed
    /// <c>ATK</c> combination, with no content claim attached.
    /// </summary>
    internal static RelicEffectDefinitions Effect =>
        RelicEffectDefinitions.Create(
            RelicEffectDefinition.Create(
                RelicEffectType.ATK,
                RelicEffectValueType.Percentage,
                1,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.Battle));
}
