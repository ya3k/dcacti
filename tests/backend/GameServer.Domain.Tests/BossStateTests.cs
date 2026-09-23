using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// BossState contract tests (<c>GAME_STATE.md</c> §2.4, <c>BOSS_RULES.md</c>
/// §1, §6.1).
///
/// They verify the documented field set, the documented initial values, the
/// canonical four-member State enum, the approved MVP Boss configuration, and
/// the fields this stage still does not add. Every expected value below traces
/// to a section of <c>BOSS_RULES.md</c> or <c>GAME_STATE.md</c>; none is
/// "whatever the code currently does".
/// </summary>
public class BossStateTests
{
    /// <summary>A fixed seed, so the generated board is deterministic in tests.</summary>
    private const ulong TestSeed = 42UL;

    // =======================================================================
    // BossState.Initial — GAME_STATE.md §2.4, BOSS_RULES.md §6.1
    // =======================================================================

    [Fact]
    public void Initial_ShouldStartAtFullHealth()
    {
        // GAME_STATE.md §2.4 / BOSS_RULES.md §6.1: a battle begins with the Boss at
        // its definition's MaxHP — HP == MaxHP — exactly as PlayerState.Initial
        // starts the player at full health.
        var boss = BossState.Initial(
            BossDefinitions.HoaLong.BossId,
            BossDefinitions.HoaLong.Element,
            maxHp: 5000,
            atk: 100,
            def: 50,
            passiveId: BossDefinitions.HoaLong.PassiveId,
            passiveThreshold: BossDefinitions.HoaLong.PassiveThreshold);

        Assert.Equal(boss.MaxHP, boss.HP);
        Assert.Equal(5000, boss.HP);
        Assert.Equal(5000, boss.MaxHP);
    }

    [Fact]
    public void Initial_ShouldStartInTheIdleState()
    {
        // BOSS_RULES.md §6.1: the Initial State of every MVP Boss is `Idle`.
        var boss = BossState.Initial(
            BossDefinitions.HoaLong.BossId, Element.Hoa, maxHp: 5000, atk: 100, def: 50,
            passiveId: new PassiveId("boss-hoa-long-rage"), passiveThreshold: 5);

        Assert.Equal(BossStateKind.Idle, boss.State);
        Assert.Equal(BossState.InitialState, boss.State);
        Assert.True(boss.IsIdle);
    }

    [Fact]
    public void Initial_ShouldStartThePassiveAtTheDefinitionThresholdAndZeroProgress()
    {
        // GAME_STATE.md §2.4.2 / §2.3 item 3 pattern: the Passive identity is set at
        // battle creation and its progress begins at 0 against the Passive's own
        // Threshold — never absent, never lazily initialized.
        var boss = BossState.Initial(
            new BossId("b"), Element.Hoa, maxHp: 5000, atk: 100, def: 50,
            passiveId: new PassiveId("boss-hoa-long-rage"), passiveThreshold: 5);

        Assert.Equal("boss-hoa-long-rage", boss.PassiveId.Value);
        Assert.Equal(5, boss.PassiveProgress.Threshold);
        Assert.Equal(0, boss.PassiveProgress.Current);
    }

    [Fact]
    public void Initial_ShouldStartTheSkillUnchargedAndOffCooldown()
    {
        // GAME_STATE.md §2.4.3: SkillCharge counts Matches toward the charge
        // requirement and SkillCooldown counts Turns until the Skill can fire again.
        // No Match and no Turn has occurred at battle creation, so both begin at 0.
        var boss = BossState.Initial(
            new BossId("b"), Element.Hoa, maxHp: 5000, atk: 100, def: 50,
            passiveId: new PassiveId("p"), passiveThreshold: 5);

        Assert.Equal(0, boss.SkillCharge);
        Assert.Equal(0, boss.SkillCooldown);
    }

    [Fact]
    public void Initial_ShouldPreserveTheAlwaysActivePassiveMarker()
    {
        // BOSS_RULES.md §6.2: Thủy Ma's trigger is "Passive (always active)", so its
        // stored PassiveThreshold is 0 as the Always-Active marker — NOT a threshold
        // that is instantly reached. Initial must carry it through rather than
        // substituting a default.
        var boss = BossState.Initial(
            BossDefinitions.ThuyMa.BossId, Element.Thuy, maxHp: 5000, atk: 100, def: 50,
            passiveId: BossDefinitions.ThuyMa.PassiveId,
            passiveThreshold: BossDefinitions.ThuyMa.PassiveThreshold);

        Assert.Equal(0, boss.PassiveProgress.Threshold);
        Assert.Equal(0, boss.PassiveProgress.Current);
        Assert.Equal("boss-thuy-ma-heal", boss.PassiveId.Value);
    }

    [Fact]
    public void Initial_ShouldPreserveEverySuppliedValue()
    {
        // GAME_STATE.md §2.4 / BOSS_RULES.md §6.1: the identity, the Element, and
        // the stats come from the Boss's definition and are carried across
        // unchanged. The factory applies exactly the documented initial values
        // (HP = MaxHP, State = Idle, progress = 0, Skill uncharged, cooldown 0) and
        // decides nothing else.
        var bossId = new BossId("test-boss");

        var boss = BossState.Initial(
            bossId, Element.Thuy, maxHp: 4321, atk: 321, def: 123,
            passiveId: new PassiveId("test-passive"), passiveThreshold: 7);

        Assert.Equal(bossId, boss.BossId);
        Assert.Equal(Element.Thuy, boss.Element);
        Assert.Equal(4321, boss.MaxHP);
        Assert.Equal(4321, boss.HP);
        Assert.Equal(321, boss.ATK);
        Assert.Equal(123, boss.DEF);
        Assert.Equal("test-passive", boss.PassiveId.Value);
        Assert.Equal(7, boss.PassiveProgress.Threshold);
        Assert.Equal(0, boss.PassiveProgress.Current);
        Assert.Equal(0, boss.SkillCharge);
        Assert.Equal(0, boss.SkillCooldown);
    }

    [Fact]
    public void Initial_ShouldNotDeriveStatsFromAnyOtherValue()
    {
        // BOSS_RULES.md §6.1: the values "do not represent formulas or scaling
        // rules" — HP is not a multiple of the player's HP and ATK/DEF are not
        // multiples of the player's ATK/DEF. There is no formula to assert
        // against; what is asserted is that the supplied values survive
        // unmodified, whatever they are.
        foreach (var (maxHp, atk, def) in new[] { (1, 0, 0), (7, 3, 2), (99999, 12345, 6789) })
        {
            var boss = BossState.Initial(
                new BossId("b"), Element.Kim, maxHp, atk, def,
                new PassiveId("p"), passiveThreshold: 5);

            Assert.Equal(maxHp, boss.HP);
            Assert.Equal(maxHp, boss.MaxHP);
            Assert.Equal(atk, boss.ATK);
            Assert.Equal(def, boss.DEF);
        }
    }

    [Fact]
    public void Initial_ShouldBeAReadonlyValue()
    {
        // GAME_STATE.md §2.4 / §5.1: BossState is authoritative state, and state is
        // replaced by the post-resolution write-back, never mutated in place. It is
        // a readonly record struct, exactly like PlayerState and PetState, so
        // `with` produces a new value and the original is unchanged.
        var boss = BossState.Initial(
            new BossId("b"), Element.Moc, 5000, 100, 50,
            new PassiveId("p"), passiveThreshold: 5);

        var damaged = boss with { HP = 10, State = BossStateKind.Enraged };

        Assert.Equal(5000, boss.HP);
        Assert.Equal(BossStateKind.Idle, boss.State);
        Assert.Equal(10, damaged.HP);
        Assert.Equal(BossStateKind.Enraged, damaged.State);
        Assert.IsType<BossState>(boss, exactMatch: false);
    }

    [Fact]
    public void BossState_ShouldCarryExactlyTheDocumentedFields()
    {
        // GAME_STATE.md §2.4 / §2.4.1: BossId, Element, HP, MaxHP, ATK, DEF, State,
        // PassiveId, PassiveProgress, SkillCharge, SkillCooldown — the eleven fields
        // §2.4.1 marks "Implement now". StatusEffects[] is listed by §2.4 but belongs
        // to the Status Effects system and is not stubbed here (§0 item 4, §0 item 5).
        var dataMembers = typeof(BossState)
            .GetConstructors()
            .SelectMany(c => c.GetParameters().Select(p => p.Name!))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "ATK", "BossId", "DEF", "Element", "HP", "MaxHP", "PassiveId",
                "PassiveProgress", "SkillCharge", "SkillCooldown", "State",
            },
            dataMembers);

        // IsIdle and InitialState are a derived reading and a documented constant,
        // not additional state: the members above are the whole representation
        // (§0 item 5).
        var declared = typeof(BossState)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "ATK", "BossId", "DEF", "Element", "HP", "IsIdle", "MaxHP",
                "PassiveId", "PassiveProgress", "SkillCharge", "SkillCooldown", "State",
            },
            declared);
    }

    [Fact]
    public void BossState_ShouldDeclareNoDeferredField()
    {
        // GAME_STATE.md §2.4.1: StatusEffects[] is the one field §2.4 lists as
        // "Deferred" — it is owned by the Status Effects system (COMBAT_RULES.md §5)
        // and is "not yet implemented", not "not required" (§0 item 4). It is not
        // stubbed, defaulted, or represented by a placeholder collection (§0 item 5).
        var declared = typeof(BossState)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(BossState).GetFields().Select(f => f.Name))
            .ToArray();

        Assert.DoesNotContain("StatusEffects", declared);

        // No battle lifecycle value is introduced either (GAME_STATE.md §2.0.3):
        // State is the Boss's own enum (BOSS_RULES.md §1), not a battle status.
        Assert.DoesNotContain("Status", declared);
    }

    [Fact]
    public void BossState_ShouldNotImplementBossMechanics()
    {
        // BOSS_RULES.md §3–§5, COMBAT_RULES.md §3, GAME_RULES.md §17 steps 15–19:
        // the Passive's charge/threshold/reset, the Skill's cast, Enrage, Stun, the
        // Damage Pipeline, and Victory/Defeat are the resolution's, not this type's.
        // BossState carries the fields they read and write and nothing more, so no
        // method on it computes damage, mitigates DEF, applies an Element modifier,
        // charges a Passive, casts a Skill, transitions State, or decides an outcome.
        var methods = typeof(BossState)
            .GetMethods()
            .Where(m => m.DeclaringType == typeof(BossState))
            .Select(m => m.Name)
            .ToArray();

        foreach (var mechanic in new[]
                 {
                     "Damage", "ApplyDamage", "Mitigate", "Defend", "ElementModifier",
                     "Transition", "Enrage", "Stun", "Charge", "CastSkill", "IsDefeated",
                     "HasWon", "HasLost",
                 })
        {
            Assert.DoesNotContain(mechanic, methods);
        }
    }

    // =======================================================================
    // BossStateKind — BOSS_RULES.md §1 (authoritative), COMBAT_RULES.md §1.2
    // =======================================================================

    [Fact]
    public void BossStateKind_ShouldBeExactlyTheFourCanonicalStates()
    {
        // BOSS_RULES.md §1 defines the Boss State as "an internal enum, e.g.
        // Idle / Charging / Enraged / Stunned", and COMBAT_RULES.md §1.2 states the
        // same four names. The set is closed: no fifth member exists.
        Assert.Equal(
            new[]
            {
                BossStateKind.Idle,
                BossStateKind.Charging,
                BossStateKind.Enraged,
                BossStateKind.Stunned,
            },
            Enum.GetValues<BossStateKind>());

        Assert.Equal(4, Enum.GetNames<BossStateKind>().Length);
    }

    [Fact]
    public void BossStateKind_ShouldNotContainChargingSkill()
    {
        // BOSS_RULES.md §1 owns the vocabulary. COMBAT_RULES.md §1.2's former
        // "ChargingSkill" spelling was a documentation bug, corrected to
        // "Charging" by TASK-020A; the corrected name is the only one that exists.
        Assert.DoesNotContain("ChargingSkill", Enum.GetNames<BossStateKind>());
        Assert.Contains("Charging", Enum.GetNames<BossStateKind>());
    }

    // =======================================================================
    // MVP Boss definitions — BOSS_RULES.md §6.1 (project-owner approved)
    // =======================================================================

    [Fact]
    public void Definitions_ShouldCarryTheApprovedHoaLongValues()
    {
        // BOSS_RULES.md §6.1: Hỏa Long — Hỏa — 5000 / 5000 / 100 / 50 — Idle.
        var boss = BossDefinitions.HoaLong;

        Assert.Equal("Hỏa Long", boss.BossId.Value);
        Assert.Equal(Element.Hoa, boss.Element);
        Assert.Equal(5000, boss.MaxHP);
        Assert.Equal(100, boss.ATK);
        Assert.Equal(50, boss.DEF);

        var initial = boss.ToInitialState();
        Assert.Equal(5000, initial.HP);
        Assert.Equal(BossStateKind.Idle, initial.State);
    }

    [Fact]
    public void Definitions_ShouldCarryTheApprovedThuyMaValues()
    {
        // BOSS_RULES.md §6.1: Thủy Ma — Thủy — 5000 / 5000 / 100 / 50 — Idle.
        var boss = BossDefinitions.ThuyMa;

        Assert.Equal("Thủy Ma", boss.BossId.Value);
        Assert.Equal(Element.Thuy, boss.Element);
        Assert.Equal(5000, boss.MaxHP);
        Assert.Equal(100, boss.ATK);
        Assert.Equal(50, boss.DEF);

        var initial = boss.ToInitialState();
        Assert.Equal(5000, initial.HP);
        Assert.Equal(BossStateKind.Idle, initial.State);
    }

    [Fact]
    public void Definitions_ShouldCarryTheApprovedMocYeuValues()
    {
        // BOSS_RULES.md §6.1: Mộc Yêu — Mộc — 5000 / 5000 / 100 / 50 — Idle.
        var boss = BossDefinitions.MocYeu;

        Assert.Equal("Mộc Yêu", boss.BossId.Value);
        Assert.Equal(Element.Moc, boss.Element);
        Assert.Equal(5000, boss.MaxHP);
        Assert.Equal(100, boss.ATK);
        Assert.Equal(50, boss.DEF);

        var initial = boss.ToInitialState();
        Assert.Equal(5000, initial.HP);
        Assert.Equal(BossStateKind.Idle, initial.State);
    }

    [Fact]
    public void Definitions_ShouldBeExactlyTheThreeContentDefinedBosses()
    {
        // BOSS_RULES.md §6 defines exactly three Bosses; §6.1 records that two
        // further MVP Bosses are "not yet content-defined". They are deliberately
        // absent rather than invented to reach GAME_RULES.md §19's five-Boss scope.
        var ids = BossDefinitions.All.Select(b => b.BossId.Value).ToArray();

        Assert.Equal(new[] { "Hỏa Long", "Thủy Ma", "Mộc Yêu" }, ids);
    }

    [Fact]
    public void Definitions_ShouldGiveEveryMvpBossTheSameApprovedBaseStats()
    {
        // BOSS_RULES.md §6.1 gives all three content-defined Bosses the same base
        // stats (5000 / 100 / 50) — they are configuration, not per-Boss balance
        // differentiation, and no Boss receives different stats.
        Assert.All(
            BossDefinitions.All,
            boss =>
            {
                Assert.Equal(5000, boss.MaxHP);
                Assert.Equal(100, boss.ATK);
                Assert.Equal(50, boss.DEF);
            });
    }

    [Fact]
    public void Definitions_ShouldAssignTheDocumentedElementToEachBoss()
    {
        // BOSS_RULES.md §6 assigns one Element per Boss: Hỏa Long = Hỏa, Thủy Ma =
        // Thủy, Mộc Yêu = Mộc. ELEMENT_RULES.md §1.2 states each Boss has exactly
        // one Element, and §7 defers dual-element entities to a future expansion.
        var byId = BossDefinitions.All.ToDictionary(b => b.BossId.Value, b => b.Element);

        Assert.Equal(Element.Hoa, byId["Hỏa Long"]);
        Assert.Equal(Element.Thuy, byId["Thủy Ma"]);
        Assert.Equal(Element.Moc, byId["Mộc Yêu"]);
    }

    // =======================================================================
    // Boss mechanic configuration — BOSS_RULES.md §6.2, §6.3, §6.4
    // =======================================================================

    [Theory]
    // BOSS_RULES.md §6.4's identity contract, transcribed — the canonical values
    // "fixed here so no task invents its own". BossId is the DISPLAY NAME, not a
    // slug; PassiveId follows the Pet PassiveId kebab-case pattern; SkillId is the
    // Skill's own name.
    [InlineData("Hỏa Long", "boss-hoa-long-rage", "flame-burst")]
    [InlineData("Thủy Ma", "boss-thuy-ma-heal", "drain-power")]
    [InlineData("Mộc Yêu", "boss-moc-yeu-regen", "root")]
    public void Definitions_ShouldCarryTheDocumentedIdentities(
        string bossId,
        string passiveId,
        string skillId)
    {
        var boss = BossDefinitions.All.Single(b => b.BossId.Value == bossId);

        Assert.Equal(bossId, boss.BossId.Value);
        Assert.Equal(passiveId, boss.PassiveId.Value);
        Assert.Equal(skillId, boss.SkillId);
    }

    [Theory]
    // BOSS_RULES.md §6.2's Passive thresholds, with §6.2's Thủy Ma note: its trigger
    // is "Passive (always active)" — an alternate trigger (PASSIVE_RULES.md §3), not
    // a Match count — so 0 is stored as the Always-Active marker and it is never
    // charged via PassiveTracker.Charge.
    [InlineData("Hỏa Long", 5)]
    [InlineData("Thủy Ma", 0)]
    [InlineData("Mộc Yêu", 5)]
    public void Definitions_ShouldCarryTheDocumentedPassiveThreshold(string bossId, int threshold)
    {
        var boss = BossDefinitions.All.Single(b => b.BossId.Value == bossId);

        Assert.Equal(threshold, boss.PassiveThreshold);
    }

    [Theory]
    // BOSS_RULES.md §6.3's Skill timing table: Charge Req. / CD / Skill Base Dmg.
    [InlineData("Hỏa Long", 5, 2, 150)]
    [InlineData("Thủy Ma", 4, 3, 120)]
    [InlineData("Mộc Yêu", 6, 2, 100)]
    public void Definitions_ShouldCarryTheDocumentedSkillTiming(
        string bossId,
        int chargeRequirement,
        int cooldownTurns,
        int skillBaseDamage)
    {
        var boss = BossDefinitions.All.Single(b => b.BossId.Value == bossId);

        Assert.Equal(chargeRequirement, boss.SkillChargeRequirement);
        Assert.Equal(cooldownTurns, boss.SkillCooldownTurns);
        Assert.Equal(skillBaseDamage, boss.SkillBaseDamage);
    }

    [Fact]
    public void Definitions_ShouldCarryTheDocumentedEnrageThreshold()
    {
        // BOSS_RULES.md §6.1 gives every content-defined MVP Boss "1500 (30%)" of
        // MaxHP 5000 — the same fraction for all three, so no Boss receives a
        // different Enrage threshold.
        Assert.All(
            BossDefinitions.All,
            boss => Assert.Equal(0.30, boss.EnrageThreshold, precision: 9));

        // 30% of 5000 is the 1500 §6.1 states, which is what makes the fraction and
        // the absolute value in the table the same rule.
        Assert.All(
            BossDefinitions.All,
            boss => Assert.Equal(1500d, boss.MaxHP * boss.EnrageThreshold, precision: 9));
    }

    [Fact]
    public void Definitions_ShouldDeclareNoNonDefaultPassiveResetBehavior()
    {
        // BOSS_RULES.md §6.2 declares no non-default Reset Behavior for any MVP Boss,
        // so each definition leaves it absent — which PASSIVE_RULES.md §4 item 1
        // reads as the default (progress resets to 0 after the trigger). A
        // non-default behavior would have to be declared here, never assumed.
        Assert.All(BossDefinitions.All, boss => Assert.Null(boss.PassiveResetBehavior));
    }

    [Fact]
    public void Definitions_ShouldGiveThuyMaTheAlwaysActiveMarkerAndNoMatchThreshold()
    {
        // BOSS_RULES.md §6.2 is explicit for Thủy Ma: its trigger "is never charged
        // via PassiveTracker.Charge on Player Matches, and emits no
        // PassiveCharged/PassiveTriggered from match progress". A stored
        // PassiveThreshold of 0 is that Always-Active marker — it is NOT a threshold
        // that is reached immediately, which is why the resolution skips the charge
        // rather than calling the tracker with it.
        //
        // PassiveTracker itself rejects Threshold < 1 (PASSIVE_RULES.md §1 defines a
        // Threshold as a Match count), which is the second, independent reason the
        // call is skipped.
        var thuyMa = BossDefinitions.ThuyMa;

        Assert.Equal(0, thuyMa.PassiveThreshold);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PassiveTracker.Charge(
                PassiveProgress.AtStart(thuyMa.PassiveThreshold),
                matchCount: 3,
                thuyMa.PassiveId));

        // Its Skill timing is nonetheless fully declared — the Always-Active Passive
        // is not an absent Passive.
        Assert.Equal("drain-power", thuyMa.SkillId);
        Assert.Equal(4, thuyMa.SkillChargeRequirement);
        Assert.Equal(3, thuyMa.SkillCooldownTurns);
        Assert.Equal(120, thuyMa.SkillBaseDamage);
    }

    [Fact]
    public void Definitions_ShouldCarryThePassiveIntoTheInitialState()
    {
        // GAME_STATE.md §2.4.2: PassiveId "is set at battle creation and never
        // changes" and PassiveProgress "tracks progress toward the Passive's
        // threshold". ToInitialState is the path that sets both, so every MVP Boss's
        // created state carries its own definition's identity and threshold.
        Assert.All(
            BossDefinitions.All,
            boss =>
            {
                var state = boss.ToInitialState();

                Assert.Equal(boss.PassiveId, state.PassiveId);
                Assert.Equal(boss.PassiveThreshold, state.PassiveProgress.Threshold);
                Assert.Equal(0, state.PassiveProgress.Current);
                Assert.Equal(0, state.SkillCharge);
                Assert.Equal(0, state.SkillCooldown);
                Assert.Equal(BossStateKind.Idle, state.State);
                Assert.Equal(boss.MaxHP, state.HP);
            });
    }

    [Fact]
    public void BossId_ShouldBeAnIdentityValueNotADefinition()
    {
        // GAME_STATE.md §2.4 / §2.3 item 1: the identity carries the identifier
        // only; the Element, stats, and State are the definition's and live in
        // BossDefinition. Following the PassiveId pattern, the wrapper holds the
        // string verbatim and imposes no format.
        var id = new BossId("Hỏa Long");

        Assert.Equal("Hỏa Long", id.Value);
        Assert.Equal("Hỏa Long", id.ToString());

        var members = typeof(BossId)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(new[] { "Value" }, members);
    }

    // =======================================================================
    // BattleState integration — GAME_STATE.md §2, §2.4
    // =======================================================================

    private static readonly Element XichLangElement = Element.Hoa;

    private static readonly PassiveId XichLang = new("xich-lang");

    [Fact]
    public void BattleStateCreate_ShouldPreserveTheSuppliedBossState()
    {
        // GAME_STATE.md §2 / §2.4: BossState is a field of BattleState, created with
        // the battle and carried through unchanged. Battle creation is not a
        // resolution: it damages no Boss and transitions no State
        // (BOSS_RULES.md §3–§5).
        var bossState = BossState.Initial(
            BossDefinitions.MocYeu.BossId,
            BossDefinitions.MocYeu.Element,
            BossDefinitions.MocYeu.MaxHP,
            BossDefinitions.MocYeu.ATK,
            BossDefinitions.MocYeu.DEF,
            BossDefinitions.MocYeu.PassiveId,
            BossDefinitions.MocYeu.PassiveThreshold);

        var state = BattleState.Create(
            "battle-boss",
            TestSeed,
            PetState.AtBattleCreation(XichLangElement, XichLang, 5),
            bossState);

        Assert.Equal(bossState, state.BossState);
        Assert.Equal("Mộc Yêu", state.BossState.BossId.Value);
        Assert.Equal(Element.Moc, state.BossState.Element);
        Assert.Equal(5000, state.BossState.HP);
        Assert.Equal(5000, state.BossState.MaxHP);
        Assert.Equal(100, state.BossState.ATK);
        Assert.Equal(50, state.BossState.DEF);
        Assert.Equal(BossStateKind.Idle, state.BossState.State);
    }

    [Fact]
    public void BattleStateCreate_ShouldBuildTheBossFromItsDefinition()
    {
        // The documented initialization path: bossId → Boss definition resolution →
        // BossState.Initial(...) → BattleState.Create(...). The definition owns
        // every Boss value, so the created state equals the definition's initial
        // state exactly.
        var state = BattleState.Create(
            "battle-boss-definition",
            TestSeed,
            XichLangElement,
            XichLang,
            passiveThreshold: 5,
            BossDefinitions.ThuyMa);

        Assert.Equal(BossDefinitions.ThuyMa.ToInitialState(), state.BossState);
    }

    [Fact]
    public void BattleStateCreate_ShouldNotChangeTheBossAcrossCreation()
    {
        // BOSS_RULES.md §3–§5 / COMBAT_RULES.md §3: creation applies no damage, so
        // the Boss is at full health with no State transition — the Damage
        // Pipeline that would write HP is a later stage.
        var state = BattleState.Create(
            "battle-boss-intact",
            TestSeed,
            XichLangElement,
            XichLang,
            passiveThreshold: 5,
            BossDefinitions.HoaLong);

        Assert.Equal(state.BossState.MaxHP, state.BossState.HP);
        Assert.True(state.BossState.IsIdle);
    }

    [Fact]
    public void BattleStateCreate_ShouldRequireABoss()
    {
        // GAME_RULES.md §1.1: a battle has exactly one Boss, and GAME_EVENTS.md §2
        // makes BattleStarted require a created battle with a Pet and a Boss. There
        // is therefore no creation overload that omits BossState.
        var parameters = typeof(BattleState)
            .GetMethods()
            .Where(m => m is { IsStatic: true, Name: "Create" })
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType))
            .ToArray();

        // Every creation overload reaches a BossState — directly, or through the
        // BossDefinition that builds one.
        Assert.Contains(typeof(BossState), parameters);
        Assert.Contains(typeof(BossDefinition), parameters);
    }

    // =======================================================================
    // Element compatibility — ELEMENT_RULES.md §2.1, §2.2, §6
    // =======================================================================

    [Theory]
    // Pet (attacker) → Boss (defender), read off ELEMENT_RULES.md §2's cycle
    // (Mộc → Thổ → Thủy → Hỏa → Kim → Mộc), using each Boss's §6 assignment.
    [InlineData(Element.Thuy, Element.Hoa, ElementMatchup.Advantage)]    // Thủy → Hỏa
    [InlineData(Element.Tho, Element.Thuy, ElementMatchup.Advantage)]    // Thổ → Thủy
    [InlineData(Element.Kim, Element.Moc, ElementMatchup.Advantage)]     // Kim → Mộc
    [InlineData(Element.Hoa, Element.Hoa, ElementMatchup.Neutral)]       // A == D
    [InlineData(Element.Tho, Element.Moc, ElementMatchup.Disadvantage)]  // Mộc → Thổ
    [InlineData(Element.Hoa, Element.Thuy, ElementMatchup.Disadvantage)] // Thủy → Hỏa
    public void ElementMatchups_ShouldResolveUsingPetAndBossElements(
        Element petElement,
        Element bossElement,
        ElementMatchup expected)
    {
        // ELEMENT_RULES.md §2.1: IF A counters D → Advantage; ELSE IF D counters A →
        // Disadvantage; ELSE → Neutral. The attacker's Element comes from
        // PetState.Element and the defender's from BossState.Element (§5) — no new
        // Element rule is added by this stage, and Resolve is unchanged.
        var pet = PetState.AtBattleCreation(petElement, XichLang, 5);
        var boss = BossState.Initial(
            new BossId("b"), bossElement, 5000, 100, 50,
            new PassiveId("p"), passiveThreshold: 5);

        Assert.Equal(expected, ElementMatchups.Resolve(pet.Element, boss.Element));
    }

    [Fact]
    public void ElementMatchups_ShouldResolveEveryPetAndMvpBossPair()
    {
        // ELEMENT_RULES.md §6 assigns the five MVP Pets an Element, and
        // BOSS_RULES.md §6 assigns each Boss one. Every pairing resolves to exactly
        // one of the three documented outcomes (§2.1: "There is no partial or
        // graduated advantage"), so the calculation the Damage Pipeline will run is
        // already well-defined from the two state fields.
        var petElements = new[]
        {
            Element.Moc, Element.Hoa, Element.Tho, Element.Kim, Element.Thuy,
        };

        foreach (var petElement in petElements)
        {
            foreach (var boss in BossDefinitions.All)
            {
                var pet = PetState.AtBattleCreation(petElement, XichLang, 5);
                var bossState = boss.ToInitialState();

                var matchup = ElementMatchups.Resolve(pet.Element, bossState.Element);

                Assert.Contains(
                    matchup,
                    new[]
                    {
                        ElementMatchup.Advantage, ElementMatchup.Neutral,
                        ElementMatchup.Disadvantage,
                    });

                // The Pet and the Boss each hold exactly one Element
                // (ELEMENT_RULES.md §1.2), so the same pairing always resolves the
                // same way — Resolve is a total function of the two arguments.
                Assert.Equal(
                    matchup,
                    ElementMatchups.Resolve(pet.Element, bossState.Element));
            }
        }
    }

    [Fact]
    public void Element_ShouldBeHeldByBothPetStateAndBossState()
    {
        // ELEMENT_RULES.md §1.1 lists exactly the entities that carry an Element,
        // and Pet and Boss are the first two. GAME_STATE.md §2.3 and §2.4 place the
        // field on both records, which is what makes the Element Modifier
        // calculable (COMBAT_RULES.md §3 step 3) — this stage supplies the fields
        // and implements no modifier.
        Assert.Contains(
            "Element",
            typeof(PetState).GetProperties().Select(p => p.Name));
        Assert.Contains(
            "Element",
            typeof(BossState).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public void PetStateAtBattleCreation_ShouldPreserveTheElement()
    {
        // GAME_STATE.md §2.3: Element is a PetState field, set at battle creation
        // and never changed (PET_RULES.md §2 item 3).
        foreach (var element in Enum.GetValues<Element>())
        {
            var pet = PetState.AtBattleCreation(element, XichLang, 5);

            Assert.Equal(element, pet.Element);
        }
    }

    [Fact]
    public void PetStateAtBattleCreation_ShouldKeepTheElementIndependentOfThePassive()
    {
        // ELEMENT_RULES.md §4 / §1.2: Element is purely a matchup system and does
        // not gate or alter Passive design, and a Pet carries exactly one of each.
        // The Element is therefore carried beside the Passive values, not derived
        // from them.
        var pet = PetState.AtBattleCreation(
            Element.Thuy,
            new PassiveId("bach-ho"),
            passiveThreshold: 4,
            PassiveResetBehavior.NoReset);

        Assert.Equal(Element.Thuy, pet.Element);
        Assert.Equal("bach-ho", pet.PassiveId.Value);
        Assert.Equal(4, pet.PassiveProgress.Threshold);
        Assert.Equal(0, pet.PassiveProgress.Current);
        Assert.Equal(PassiveResetBehavior.NoReset, pet.ResetBehavior);
    }
}
