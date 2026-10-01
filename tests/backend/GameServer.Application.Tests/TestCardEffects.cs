using GameServer.Domain.Cards;

namespace GameServer.Application.Tests;

/// <summary>
/// The structured Card effect values the Application tests use when a fixture
/// needs <b>some</b> valid <c>CardDefinition.EffectDefinition</c> and the test
/// is not about the effect itself.
///
/// <b>Why a shared helper.</b> Before TASK-109 a Card fixture could supply any
/// string. The member now carries the structured contract
/// (<c>DATABASE.md</c> §1), so every fixture must supply a real effect even
/// though it is irrelevant to what that test asserts. This keeps the value in
/// one place instead of restating it per file, and it deliberately does
/// <b>not</b> define which effect a canonical Card has — that is content owned
/// by <c>CARD_RULES.md</c> §2/§4.1 and provisioned by migration, and no test
/// fixture may become a second source for it (<c>GAME_STATE.md</c> §0 item 5).
/// </summary>
internal static class TestCardEffects
{
    /// <summary>
    /// A valid, well-formed flat Power effect — a real contract value with no
    /// content claim attached to it. A one-element array per TASK-111 D-1b: the
    /// stored shape is an array even for a Card with a single effect.
    /// </summary>
    internal static CardEffectDefinitions FlatPower =>
        CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 1));

    /// <summary>
    /// A valid, well-formed proportional effect, for a fixture that needs to
    /// exercise the non-flat interpretation.
    /// </summary>
    internal static CardEffectDefinitions PercentMaxHp =>
        CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 1));
}
