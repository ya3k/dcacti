using GameServer.Domain.Relics;

namespace GameServer.Api.Tests;

/// <summary>
/// The structured Relic values the API tests use when a fixture needs <b>some</b>
/// valid <c>RelicDefinition.Condition</c>/<c>EffectDefinition</c> and the test is
/// not about the Relic's own contract.
///
/// Before TASK-132 a Relic fixture could supply any string; both members now
/// carry the structured contract (<c>RELIC_RULES.md</c> §8.1/§8.2), so a fixture
/// must supply a real value even when it is irrelevant to what the test asserts.
/// This follows the existing <see cref="TestCardEffects"/> convention and is
/// deliberately <b>not</b> the value §8.5 records for any provisioned row, so a
/// test cannot accidentally depend on fixture data being real content.
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
