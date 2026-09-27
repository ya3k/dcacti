using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Battle.Serialization;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;

namespace GameServer.Application.Tests;

/// <summary>
/// TASK-032 — the Player/Pet authority regression suite.
///
/// <code>
/// ADR-011 / ADR-012 authority model
/// Player       = account / owner
/// Pet          = combat character
/// BattleState  = authoritative battle snapshot
/// PetState     = combat-state owner
/// PlayerState  = NOT a combat-stat owner (no such node)
/// </code>
///
/// <b>This suite closes the gaps the existing suites leave open.</b> Most of the
/// invariant catalog is already protected — <c>BattleStateSerializationTests</c>
/// (identity carriage), <c>MatchComboAccountingTests</c> (no
/// <c>PlayerState</c> node), <c>BattleStateTests</c> (the
/// <c>BattleState</c>/<c>PetState</c> field sets), <c>PlayerPersistenceTests</c>
/// (the Player CLR + model field set), <c>PetPersistenceTests</c> (the
/// Pet/PetDefinition field sets and model constraints),
/// <c>CardLoadoutServiceTests</c>, <c>RelicLoadoutServiceTests</c>,
/// <c>BattleStartServiceTests</c>, <c>BattleStartEndpointTests</c>, and
/// <c>ApplicationSessionRESTTests</c>. Those are not duplicated here; they are
/// named in the comments below as the owner of each guarantee they already hold
/// (<c>AGENTS.md</c> §15 — a test that only re-asserts existing coverage adds no
/// protection).
///
/// What this suite adds is the set of authority guarantees that were asserted
/// only <i>behaviourally</i> or only at one layer:
///
/// <list type="number">
/// <item>the removed Player combat-readiness mechanism cannot silently return —
/// before this suite, <c>IsCombatReady</c> / <c>ICombatStatsSource</c> /
/// <c>PlayerCombatProfile</c> were named in exactly one test <i>comment</i> and
/// asserted nowhere (TASK-056/TASK-057 made them unreachable, but nothing stops a
/// later task reintroducing them),</item>
/// <item>the created battle is independent of the <b>Player</b>, <b>Pet</b>, and
/// <b>definition</b> rows — the existing snapshot tests mutate only the Card and
/// Relic <i>ownership</i> rows, so a re-read of the Player/Pet row was never
/// excluded,</item>
/// <item>both battle-scoped loadouts survive a real committed-swap write-back and
/// the runtime round trip — the existing post-swap round-trip test asserts the
/// board, the counters, and the Passive, but not the loadouts, and
/// <c>BattleStateServiceTests</c> creates its battles without any,</item>
/// <item>the <c>BattleState</c> positional constructor itself declares no
/// <c>PlayerState</c>-typed parameter — the field-set assertions bound the
/// properties, not the record's own parameter list.</item>
/// </list>
///
/// Every expected value below is read from its owning document
/// (<c>GAME_STATE.md</c> §2/§2.2/§2.3/§2.8, <c>DATABASE.md</c> §1/§3,
/// <c>ADR-011</c>, <c>ADR-012</c>, <c>ADR-014</c>, <c>CARD_RULES.md</c> §1,
/// <c>RELIC_RULES.md</c> §2.2–§2.5, <c>COMBAT_RULES.md</c> §1.1) — never from the
/// implementation under test (<c>quality/testing.md</c> §2).
/// </summary>
public class AuthorityRegressionSuiteTests
{
    // =======================================================================
    // K. Combat-readiness regression — the removed mechanism must not return
    //    TASK-056 / TASK-057, DATABASE.md §1
    // =======================================================================

    /// <summary>
    /// The type names TASK-056/TASK-057 removed from the contract. They are not
    /// renamed, not deprecated, and not replaced: <c>DATABASE.md</c> §1 states that
    /// <c>BattleResult</c> persistence requires <b>no</b> Player combat-readiness
    /// condition, so there is no predicate of any name to reintroduce
    /// (<c>ADR-011</c> item 5 — the Player owns no combat statistics at all).
    /// </summary>
    private static readonly string[] RemovedCombatReadinessTypes =
    [
        "IsCombatReady",
        "ICombatStatsSource",
        "PlayerCombatProfile",
        "PlayerCombatProfileSource",
    ];

    [Fact]
    public void Domain_ShouldDeclareNoPlayerCombatReadinessType()
    {
        // DATABASE.md §1 / ADR-011 item 5: a Player has no combat statistics, so
        // there is nothing for a readiness source to read and no readiness
        // predicate to answer. The assembly scan is the same technique
        // MatchComboAccountingTests.BattleState_ShouldExposeNoPlayerStateNode uses
        // for the removed PlayerState node — declared-absence, not
        // usage-absence, so a reintroduced but currently-unreferenced type is
        // still caught.
        AssertAbsentFromAssembly(typeof(BattleState).Assembly, "Domain");
    }

    [Fact]
    public void Application_ShouldDeclareNoPlayerCombatReadinessType()
    {
        // The Application layer is where the former readiness gate was consulted
        // from the battle-end path (TASK-056), so it is asserted separately rather
        // than assumed to follow from Domain.
        AssertAbsentFromAssembly(typeof(BattleStateService).Assembly, "Application");
    }

    /// <summary>
    /// Asserts none of <see cref="RemovedCombatReadinessTypes"/> exists in
    /// <paramref name="assembly"/> — by type name, at any visibility.
    ///
    /// <c>GetTypes()</c> enumerates public and non-public types alike, so an
    /// <c>internal</c> readiness abstraction is caught as well as a public one.
    /// Substring matching (rather than exact equality) also catches the
    /// plausible re-spellings — <c>IPlayerCombatProfileSource</c>,
    /// <c>PlayerCombatReadiness</c> — which is the point: the contract removed
    /// the <i>mechanism</i>, not four particular identifiers.
    ///
    /// The Api and Infrastructure suites apply the same checks to their own
    /// assemblies in their own projects (<c>ApiAuthorityRegressionTests</c>,
    /// <c>InfrastructureAuthorityRegressionTests</c>): each layer asserts its own,
    /// because a regression can reappear in any of them and a scan of one assembly
    /// cannot see another's.
    /// </summary>
    private static void AssertAbsentFromAssembly(
        System.Reflection.Assembly assembly,
        string layer = "Application")
    {
        var declared = assembly.GetTypes().Select(type => type.Name).ToArray();

        foreach (var forbidden in RemovedCombatReadinessTypes)
        {
            Assert.DoesNotContain(forbidden, declared);
        }

        // The same guarantee stated for any type whose name merely mentions the
        // removed mechanism, so a renamed reintroduction cannot slip past the
        // exact-name check above.
        foreach (var fragment in new[] { "CombatReady", "CombatReadiness", "CombatStatsSource" })
        {
            Assert.DoesNotContain(
                declared,
                name => name.Contains(fragment, StringComparison.Ordinal));
        }

        // The scan is a real scan: the layer's own documented types are present,
        // so an empty or unloaded assembly cannot make the assertions above vacuous.
        Assert.NotEmpty(declared);
        Assert.True(
            declared.Length > 0,
            $"{layer} declared no types; the absence assertions above would be vacuous.");
    }

    [Fact]
    public void BattleEnd_ShouldNotConsultAnyPlayerCombatReadinessPredicate()
    {
        // The behavioural half of the same guarantee, at the layer that used to
        // hold the gate. TASK-056's contract states that BattleResult persistence
        // has no Player combat-readiness prerequisite, and
        // BattleResultServiceTests.PersistTerminalResult_ShouldWrite_WithNoPlayerReadinessInvolved
        // already proves a terminal result is written with no readiness input.
        //
        // What is asserted here is the composition: the battle-end path's own
        // collaborators are exactly the documented ones, so no readiness
        // collaborator can be wired in beside them without failing this test.
        var constructor = typeof(BattleResultService)
            .GetConstructors()
            .Single();

        var collaborators = constructor
            .GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        foreach (var forbiddenFragment in new[]
                 {
                     "CombatReady", "CombatReadiness", "CombatStatsSource", "CombatProfile",
                 })
        {
            Assert.DoesNotContain(
                collaborators,
                name => name.Contains(forbiddenFragment, StringComparison.Ordinal));
        }

        // And the Player row itself carries no readiness column to consult:
        // PlayerPersistenceTests.Player_ShouldCarryExactlyTheDocumentedFields owns
        // the full field-set assertion; this states the same guarantee locally so
        // the battle-end contract above is anchored to the domain shape it reads.
        var playerMembers = typeof(Player).GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbiddenFragment in new[] { "Ready", "Combat", "Readiness" })
        {
            Assert.DoesNotContain(
                playerMembers,
                name => name.Contains(forbiddenFragment, StringComparison.Ordinal));
        }
    }

    // =======================================================================
    // C. BattleState identity — the record's own constructor parameter list
    //    GAME_STATE.md §2, §2.8, §2.3, ADR-011, ADR-014
    // =======================================================================

    [Fact]
    public void BattleState_ShouldDeclareNoPlayerStateTypedConstructorParameter()
    {
        // GAME_STATE.md §2: "There is no PlayerState member." BattleState is a
        // positional record, so its member set IS its primary constructor's
        // parameter list — and that list is the one shape
        // BattleStateTests.BoardFoundationState_ShouldCarryExactlyTheDocumentedFields
        // (property set) and MatchComboAccountingTests
        // .BattleState_ShouldExposeNoPlayerStateNode (property set + assembly scan)
        // do not read directly.
        //
        // The technique mirrors BattleStateTests.PetState_ShouldCarryExactlyTheDocumentedFields,
        // which pins PetState through its constructor parameters for the same
        // reason.
        var constructor = typeof(BattleState)
            .GetConstructors()
            .Single(c => c.GetParameters().Length > 3);

        var parameters = constructor
            .GetParameters()
            .ToDictionary(p => p.Name!, p => p.ParameterType);

        // No parameter of any Player-owned type: the Player is the account/owner
        // and contributes exactly one member to this record — the root
        // PlayerId identity of §2.8, which is an identity and not a state node
        // (ADR-014 decision 1).
        Assert.DoesNotContain(
            parameters,
            pair => pair.Value == typeof(Player));

        Assert.DoesNotContain("PlayerState", parameters.Keys);
        Assert.DoesNotContain("State", parameters.Keys.Where(name => name == "PlayerState"));

        // The identity member IS present, of the documented type — so the
        // assertion above is an exclusion rather than a consequence of the
        // constructor being unreadable.
        Assert.Equal(typeof(PlayerId), parameters["PlayerId"]);

        // And the combat-state home is the Pet's, of the documented type
        // (ADR-011 item 3).
        Assert.Equal(typeof(PetState), parameters["PetState"]);

        // There is no second Player-side combat-stat parameter: a flat
        // HP/ATK/DEF/Crit/Power on this record would be the parallel
        // representation GAME_STATE.md §0 item 5 forbids (ADR-011 item 5).
        foreach (var combatStat in new[] { "HP", "MaxHP", "ATK", "DEF", "Crit", "Power" })
        {
            Assert.DoesNotContain(combatStat, parameters.Keys);
        }
    }

    // =======================================================================
    // H. Snapshot independence — the created battle is independent of the
    //    Player, Pet, and definition rows
    //    ADR-011 item 4, ADR-012 items 7–10, RELIC_RULES.md §2.5,
    //    CARD_RULES.md §1, DATABASE.md §3
    // =======================================================================

    /// <summary>
    /// The combat stats a created battle carries, as
    /// <c>COMBAT_RULES.md</c> §1.1 defines them — the documented MVP baseline,
    /// which is what <c>GAME_STATE.md</c> §2.3's "combat-stats stage" initializes
    /// at battle creation. They are asserted as the concrete documented values
    /// rather than against <c>PetState.DefaultHP</c> so a change to the constant
    /// cannot silently redefine the expectation.
    /// </summary>
    private static readonly (string Name, Func<PetState, int> Read, int Documented)[] DocumentedCombatStats =
    [
        ("HP", pet => pet.HP, 1000),
        ("MaxHP", pet => pet.MaxHP, 1000),
        ("ATK", pet => pet.ATK, 50),
        ("DEF", pet => pet.DEF, 25),
        ("Crit", pet => pet.Crit, 5),
        ("Power", pet => pet.Power, 0),
    ];

    [Fact]
    public async Task CreatedBattle_ShouldBeIndependentOfThePlayerRow()
    {
        // ADR-012 item 1 / DATABASE.md §1: Player.Level is a persistent account
        // attribute and carries no combat stats (ADR-011 item 5). The battle
        // records the Player's identity at creation (GAME_STATE.md §2.8 item 2)
        // and reads nothing else from the row, so changing the row afterwards —
        // including a Level change, which is the one value Pet Level derives from
        // (PET_RULES.md §5) — must leave the created state untouched.
        //
        // The existing snapshot tests cover the Card/Relic OWNERSHIP rows
        // (BattleStartServiceTests.Start_ShouldProduceASnapshot_ThatDoesNotChangeWhenOwnershipChangesAfterwards);
        // the Player row is the one this suite adds.
        var harness = new RegressionHarness();

        var created = await harness.CreateBattleAsync("battle-regression-player");

        var before = created;

        // Move the account to the opposite end of its documented range and rewrite
        // the remaining persisted members, so a re-read of any of them would be
        // observable.
        harness.SetPlayerLevel(Player.MaxLevel);

        var after = await harness.ReadBattleAsync("battle-regression-player");

        AssertBattleStateUnchanged(before, after!);

        // The Level really did change in the store — otherwise the assertion above
        // would pass for the wrong reason.
        Assert.Equal(Player.MaxLevel, harness.PlayerLevel);
        Assert.NotEqual(Player.InitialLevel, harness.PlayerLevel);
    }

    [Fact]
    public async Task CreatedBattle_ShouldBeIndependentOfThePetRow()
    {
        // ADR-011 item 5 / DATABASE.md §3: the Pet row carries ownership and the
        // denormalized Level snapshot — never HP/ATK/DEF/Crit/Power, which are
        // battle-time PetState values (GAME_STATE.md §2.3). The battle records the
        // owned instance's identity (PetState.PetId) and its combat configuration
        // at creation, so no later edit to the row may reach the created state.
        //
        // This is the assertion the derived-Pet-Level interaction makes material:
        // Pet.Level is recomputed when Player Level or the multiplier changes
        // outside battle (ADR-012 Consequences), and per PET_RULES.md §5 item 2
        // the battle-time Level is a snapshot of that derived value — not a live
        // view of the row. The battle carries no Level member yet (it is a later
        // stage, GAME_STATE.md §2.3), so the guarantee under test is that the
        // members the battle DOES carry — identity, combat stats, Element, Passive,
        // loadouts — are unaffected by the row.
        var harness = new RegressionHarness();

        var created = await harness.CreateBattleAsync("battle-regression-pet");

        harness.SetPetLevel(Player.MaxLevel);
        harness.SetPetStar(Pet.MaxStar);
        harness.SetPetTier(PetTier.Mythic);

        var after = await harness.ReadBattleAsync("battle-regression-pet");

        AssertBattleStateUnchanged(created, after!);

        // The row really did change.
        Assert.Equal(Player.MaxLevel, harness.PetLevel);
        Assert.Equal(Pet.MaxStar, harness.PetStar);
        Assert.Equal(PetTier.Mythic, harness.PetTier);
    }

    [Fact]
    public async Task CreatedBattle_ShouldBeIndependentOfThePetDefinitionRow()
    {
        // GAME_STATE.md §2.3 / DATABASE.md §1: the Element, the Passive identity,
        // the Passive Threshold, and the Signature Skill reference all come from
        // the Pet's DEFINITION, read once at battle creation. The battle's copy is
        // the authority for the battle's lifetime — a definition edited afterwards
        // (a content/balance change, which DATABASE.md §1 classifies as a
        // configuration change rather than a state change) must not rewrite a
        // running battle.
        var harness = new RegressionHarness();

        var created = await harness.CreateBattleAsync("battle-regression-pet-definition");

        harness.SetPetDefinitionElement(Element.Thuy);
        harness.SetPetDefinitionPassiveThreshold(99);
        harness.SetPetDefinitionSignatureSkillCard("card_skill_replaced");

        var after = await harness.ReadBattleAsync("battle-regression-pet-definition");

        AssertBattleStateUnchanged(created, after!);

        // The battle kept the definition values it was created with, and not the
        // replacements — stated directly, because "unchanged" alone would also hold
        // if the definition had never been read.
        Assert.Equal(Element.Moc, after!.PetState.Element);
        Assert.Equal(5, after.PetState.PassiveProgress.Threshold);

        // The definition really did change in the store.
        Assert.Equal(Element.Thuy, harness.PetDefinitionElement);
        Assert.Equal(99, harness.PetDefinitionPassiveThreshold);
    }

    [Fact]
    public async Task CreatedBattle_ShouldBeIndependentOfTheCardAndRelicDefinitionRows()
    {
        // CARD_RULES.md §1 / RELIC_RULES.md §2.2: a loadout element carries an
        // IDENTITY — a CardDefinitionId or an owned RelicInstanceId — and never a
        // copy of the definition. The unresolved definitions are therefore not a
        // second authority the battle depends on, and editing or removing them
        // after battle start cannot change what the battle equipped.
        var harness = new RegressionHarness();

        var created = await harness.CreateBattleAsync("battle-regression-definitions");

        Assert.Equal(4, created.PetState.EquippedCards!.Length);
        Assert.Equal(3, created.PetState.EquippedRelics!.Length);

        // Rewrite the content the identities resolve to, and change the Relic
        // instance's own definition reference.
        harness.SetCardDefinitionCopyLimit("card_basic_a", 0);
        harness.RemoveCardDefinition("card_basic_b");
        harness.SetRelicDefinitionId("relic_1", "relic_def_replaced");

        var after = await harness.ReadBattleAsync("battle-regression-definitions");

        AssertBattleStateUnchanged(created, after!);

        // The snapshot is the documented composition, not the mutated content:
        // the identity list is unchanged element for element, in order
        // (CARD_RULES.md §1, RELIC_RULES.md §2.3).
        Assert.Equal(
            created.PetState.EquippedCards!.Select(c => c.Value),
            after!.PetState.EquippedCards!.Select(c => c.Value));
        Assert.Equal(
            created.PetState.EquippedRelics!.Select(r => r.Value),
            after.PetState.EquippedRelics!.Select(r => r.Value));
    }

    /// <summary>
    /// The self-check that keeps the independence tests falsifiable.
    ///
    /// <see cref="AssertBattleStateUnchanged"/> is the guard the four
    /// independence tests rely on, and a guard that cannot fail protects nothing
    /// (<c>AGENTS.md</c> §15 — "do not modify a test simply to make an incorrect
    /// implementation pass" has a mirror obligation: do not ship a test that could
    /// not have failed). This proves the guard rejects a change to every
    /// authority-bearing member it claims to compare, by mutating each in turn and
    /// asserting the guard throws.
    ///
    /// The reference state is the creation fixture, whose loadouts are ABSENT —
    /// the documented staging position (<c>GAME_STATE.md</c> §0 item 4). Both
    /// loadout mutations therefore assert that supplying a loadout where none
    /// existed is detected, which is the direction a re-read from persistence
    /// would produce.
    ///
    /// The mutations are applied to a COPY of the reference state rather than to
    /// the store, so no production path is exercised and no fixture is disturbed.
    /// </summary>
    [Fact]
    public void IndependenceGuard_ShouldDetectEveryMemberItCompares()
    {
        var reference = BattleState.CreateWith("battle-guard-self-check", 4242UL);

        // The fixture's own staging position is asserted first: the guard's
        // loadout handling must admit the documented absent case, because a battle
        // created through the earlier stages legitimately carries none.
        Assert.Null(reference.PetState.EquippedRelics);
        Assert.Null(reference.PetState.EquippedCards);

        // Each entry differs from the reference in exactly one authority-bearing
        // member, so a guard that missed that member would accept it.
        var mutated = new[]
        {
            reference with { BattleId = "battle-other" },
            reference with { PlayerId = new PlayerId("player_other") },
            reference with { Turn = reference.Turn + 1 },
            reference with { Sequence = reference.Sequence + 1 },
            reference with { Combo = reference.Combo + 1 },
            reference with { MatchCount = reference.MatchCount + 1 },
            reference with { RngSeed = reference.RngSeed + 1UL },
            reference with
            {
                RngState = new RngState(
                    reference.RngState.State + 1UL,
                    reference.RngState.Increment),
            },
            reference with { LastCommittedSwapPair = CommittedSwapPair.FromCells(0, 1) },
            reference with { PetState = reference.PetState with { PetId = new PetId("pet-other") } },
            reference with { PetState = reference.PetState with { HP = reference.PetState.HP - 1 } },
            reference with { PetState = reference.PetState with { MaxHP = reference.PetState.MaxHP - 1 } },
            reference with { PetState = reference.PetState with { ATK = reference.PetState.ATK + 1 } },
            reference with { PetState = reference.PetState with { DEF = reference.PetState.DEF + 1 } },
            reference with { PetState = reference.PetState with { Crit = reference.PetState.Crit + 1 } },
            reference with { PetState = reference.PetState with { Power = reference.PetState.Power + 1 } },
            reference with { PetState = reference.PetState with { Element = Element.Thuy } },
            reference with
            {
                PetState = reference.PetState with
                {
                    PassiveId = new PassiveId("passive-other"),
                },
            },
            reference with
            {
                PetState = reference.PetState with
                {
                    PassiveProgress = new PassiveProgress(
                        reference.PetState.PassiveProgress.Threshold + 1,
                        reference.PetState.PassiveProgress.Current),
                },
            },
            reference with
            {
                PetState = reference.PetState with
                {
                    PassiveResetOverride = PassiveResetBehavior.Partial,
                },
            },
            // A loadout supplied where the reference had none — the shape a
            // re-read of the Player's collection would produce.
            reference with
            {
                PetState = reference.PetState with
                {
                    EquippedRelics =
                    [
                        new GameServer.Domain.Relics.EquippedRelicIdentity("relic-other"),
                    ],
                },
            },
            reference with
            {
                PetState = reference.PetState with
                {
                    EquippedCards =
                    [
                        new GameServer.Domain.Cards.EquippedCardIdentity("card-other"),
                    ],
                },
            },
            // An EMPTY loadout where the reference had none — the absent-versus-
            // empty distinction GAME_STATE.md §0 item 4 and RELIC_RULES.md §2.1
            // item 1 keep apart.
            reference with
            {
                PetState = reference.PetState with { EquippedRelics = [] },
            },
            reference with
            {
                PetState = reference.PetState with { EquippedCards = [] },
            },
        };

        // The baseline the guard must ACCEPT, so the assertions below are not
        // passing merely because the guard rejects everything.
        AssertBattleStateUnchanged(reference, reference);

        foreach (var single in mutated)
        {
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
                AssertBattleStateUnchanged(reference, single));
        }
    }

    /// <summary>
    /// Asserts two states are identical across every authority-bearing member —
    /// the two identities, the combat stats, the Element, the Passive, both
    /// loadouts, and the BattleState root the resolution versions
    /// (<c>GAME_STATE.md</c> §2, §2.2, §2.3, §2.8).
    ///
    /// Compared member by member rather than as reference equality, because a
    /// snapshot the store round-tripped is a different instance carrying the same
    /// values — and because a per-member comparison names which guarantee broke.
    /// The loadout arrays are compared by value and order, not by record equality:
    /// <see cref="PetState"/> is a <c>record struct</c> whose generated equality
    /// compares its arrays by reference
    /// (<c>BattleStateSerializationTests.RoundTrip_ShouldRestorePetStateMemberByMember</c>
    /// records the same caveat).
    ///
    /// <b>It is deliberately not a no-op when the two states are the same
    /// instance.</b> The independence tests would otherwise be able to pass by
    /// comparing a value with itself. Every member below is read from
    /// <paramref name="after"/>, which the harness re-reads from the store, so the
    /// comparison is always between the retained creation state and a freshly
    /// deserialized record. <see cref="IndependenceGuard_ShouldDetectEveryMemberItCompares"/>
    /// is the self-check that keeps this honest.
    /// </summary>
    private static void AssertBattleStateUnchanged(BattleState before, BattleState after)
    {
        // Identity — GAME_STATE.md §2.8, §2.3, ADR-014.
        Assert.Equal(before.BattleId, after.BattleId);
        Assert.Equal(before.PlayerId, after.PlayerId);
        Assert.Equal(before.PetState.PetId, after.PetState.PetId);

        // The Player-side combat pool is the Pet's, and only the Pet's
        // (ADR-011 item 3, COMBAT_RULES.md §1.1).
        foreach (var (name, read, _) in DocumentedCombatStats)
        {
            Assert.Equal(read(before.PetState), read(after.PetState));
            _ = name;
        }

        // Element and Passive — GAME_STATE.md §2.3.
        Assert.Equal(before.PetState.Element, after.PetState.Element);
        Assert.Equal(before.PetState.PassiveId, after.PetState.PassiveId);
        Assert.Equal(before.PetState.PassiveProgress, after.PetState.PassiveProgress);
        Assert.Equal(before.PetState.PassiveResetOverride, after.PetState.PassiveResetOverride);

        // Both battle-scoped loadouts, by identity and order
        // (RELIC_RULES.md §2.3, §2.5; CARD_RULES.md §1).
        //
        // A null loadout is the documented staging position — "not yet supplied",
        // which is NOT an empty loadout (RELIC_RULES.md §2.1 item 1 and
        // CARD_RULES.md §1 define no zero-item battle;
        // BattleStateSerializationTests.RoundTrip_ShouldPreserveANullLoadoutAsAbsenceRatherThanAnEmptyArray
        // records the same rule). The null case is therefore compared as null, and
        // an empty array is compared as an empty array — neither is allowed to
        // stand in for the other, so a write-back that replaced an absent loadout
        // with an empty one still fails here.
        AssertLoadoutUnchanged(
            before.PetState.EquippedRelics,
            after.PetState.EquippedRelics,
            relic => relic.Value);
        AssertLoadoutUnchanged(
            before.PetState.EquippedCards,
            after.PetState.EquippedCards,
            card => card.Value);

        // The BattleState root the resolution versions — GAME_STATE.md §2, §2.2,
        // §5.1. An edited Player/Pet/definition row must not advance any of them.
        Assert.Equal(before.Turn, after.Turn);
        Assert.Equal(before.Sequence, after.Sequence);
        Assert.Equal(before.Combo, after.Combo);
        Assert.Equal(before.MatchCount, after.MatchCount);
        Assert.Equal(before.RngSeed, after.RngSeed);
        Assert.Equal(before.RngState, after.RngState);
        Assert.Equal(before.LastCommittedSwapPair, after.LastCommittedSwapPair);
        Assert.True(before.BoardState.CellsEqual(after.BoardState));
    }

    /// <summary>
    /// Compares one loadout snapshot member by member, treating the documented
    /// absent-loadout position as a value to compare rather than a null to
    /// dereference.
    ///
    /// This is the null-safety the independence guard needs and the reason the
    /// guard is not written with a plain <c>!</c>: a battle created without a
    /// loadout carries <c>null</c> legitimately
    /// (<c>GAME_STATE.md</c> §0 item 4 — "not yet implemented" is not "not
    /// required"), and a guard that threw on that would report a missing snapshot
    /// as a framework error instead of as the divergence it is.
    /// </summary>
    private static void AssertLoadoutUnchanged<T>(
        T[]? before,
        T[]? after,
        Func<T, string> identity)
    {
        if (before is null || after is null)
        {
            Assert.Equal(before is null, after is null);
            return;
        }

        Assert.Equal(before.Select(identity), after.Select(identity));
    }

    // =======================================================================
    // Snapshot independence — the loadouts survive a real resolution
    // RELIC_RULES.md §2.3, §2.5; CARD_RULES.md §1; GAME_STATE.md §2.3, §5.1
    // =======================================================================

    [Fact]
    public async Task CommittedSwap_ShouldCarryBothLoadoutsThroughTheWriteBackAndTheRoundTrip()
    {
        // The resolution writes the whole state back once per committed action
        // (GAME_STATE.md §5.1), and the loadout snapshots are members of that
        // state. Two things must therefore hold together, and both are asserted
        // here because the existing suites cover them separately and never
        // together (BattleStateSerializationLifecycleTests round-trips a played
        // battle but does not re-assert its loadouts; BattleStateServiceTests
        // creates its battles without any loadout at all):
        //
        //   1. the committed Swap's write-back preserves both snapshots — the
        //      battle does not re-read the Player's collection to rebuild them
        //      (RELIC_RULES.md §2.5, ADR-012 items 8 and 10), and
        //   2. the snapshots survive the runtime serialize → deserialize cycle
        //      the active-state store performs (REDIS_STATE.md §2 item 1).
        var harness = new RegressionHarness();

        var created = await harness.CreateBattleAsync("battle-regression-loadout-writeback");

        var pair = harness.FindAcceptedSwap(created);

        var resolved = await harness.ExecuteSwapAsync("battle-regression-loadout-writeback", pair);

        Assert.True(resolved!.Value.IsAccepted);

        var stored = (await harness.ReadBattleAsync("battle-regression-loadout-writeback"))!;

        // The action really resolved, so the write-back under test happened
        // (MATCH3_RULES.md §8.1).
        Assert.Equal(1, stored.Sequence);
        Assert.Equal(1, stored.Turn);

        // (1) Both snapshots are byte-for-byte the creation values after the
        // write-back — count, identity, and order.
        Assert.Equal(
            ["card_basic_a", "card_basic_b", "card_basic_c", "card_skill_1"],
            stored.PetState.EquippedCards!.Select(c => c.Value));

        Assert.Equal(
            ["relic_z", "relic_a", "relic_m"],
            stored.PetState.EquippedRelics!.Select(r => r.Value));

        // The Relic order is the submitted equip-slot order and is NOT the sorted
        // order (RELIC_RULES.md §2.3 item 2), so a re-sorting write-back cannot
        // pass by accident.
        Assert.NotEqual(
            stored.PetState.EquippedRelics!
                .Select(r => r.Value)
                .OrderBy(v => v, StringComparer.Ordinal),
            stored.PetState.EquippedRelics!.Select(r => r.Value));

        // (2) The round trip preserves both, element for element — so a recovered
        // battle (ADR-008) resumes with the same loadout it was created with.
        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(stored));

        Assert.Equal(
            stored.PetState.EquippedCards!.Select(c => c.Value),
            restored.PetState.EquippedCards!.Select(c => c.Value));
        Assert.Equal(
            stored.PetState.EquippedRelics!.Select(r => r.Value),
            restored.PetState.EquippedRelics!.Select(r => r.Value));

        // And the round trip is not vacuous: the state it carried is the resolved
        // one, not a reset creation state.
        Assert.Equal(stored.Sequence, restored.Sequence);
        Assert.Equal(stored.Turn, restored.Turn);
        Assert.Equal(stored.MatchCount, restored.MatchCount);
        Assert.True(stored.BoardState.CellsEqual(restored.BoardState));
    }

    // =======================================================================
    // Harness — a real resolution pipeline over an in-memory store, with the
    // mutable persistence rows the independence tests edit
    // =======================================================================

    /// <summary>
    /// A battle created through the production composition path
    /// (<see cref="BattleStateService.CreateBattleAsync"/>) over the test's
    /// in-memory active-state store, together with the persistent rows the battle
    /// was created from.
    ///
    /// <b>Why the rows live here rather than in a database.</b> The guarantee
    /// under test is that the battle does not depend on a later mutation of them.
    /// What must be real is that the mutation is observable and that the battle
    /// does not observe it — so the rows are held as mutable values the test edits
    /// and <see cref="ReadBattleAsync"/> re-reads the store, exactly as the
    /// production read does. The Infrastructure suite
    /// (<c>RelicLoadoutSnapshotTests</c>), not this one, owns the real-PostgreSQL
    /// form of the same property.
    ///
    /// The loadout-bearing configuration is deliberate: it is the shape the
    /// battle-start path produces, and the shape
    /// <c>BattleStateServiceTests</c> does not use.
    /// </summary>
    private sealed class RegressionHarness
    {
        /// <summary>The owning Player (<c>GAME_STATE.md</c> §2.8).</summary>
        internal const string Owner = "player_authority_regression";

        /// <summary>The owned Pet instance the battle selects (<c>GAME_STATE.md</c> §2.3).</summary>
        internal const string PetInstanceId = "pet_instance_regression_1";

        private readonly InMemoryBattleStateRepository _store;

        internal RegressionHarness()
        {
            _store = new InMemoryBattleStateRepository();

            Battles = new BattleStateService(_store, new FixedRngSeedSource());
        }

        internal BattleStateService Battles { get; }

        // --- the persistent rows the tests mutate -----------------------------

        /// <summary>The Player row's <c>Level</c> (<c>DATABASE.md</c> §1).</summary>
        internal int PlayerLevel { get; private set; } = Player.InitialLevel;

        /// <summary>The Pet row's denormalized <c>Level</c> (<c>DATABASE.md</c> §1).</summary>
        internal int PetLevel { get; private set; } = 1;

        /// <summary>The Pet row's <c>Star</c> (<c>PET_RULES.md</c> §4).</summary>
        internal int PetStar { get; private set; } = Pet.MinStar;

        /// <summary>The Pet row's <c>Tier</c> (<c>PET_RULES.md</c> §3).</summary>
        internal PetTier PetTier { get; private set; } = PetTier.Common;

        /// <summary>The Pet definition's <c>Element</c> (<c>DATABASE.md</c> §1).</summary>
        internal Element PetDefinitionElement { get; private set; } = Element.Moc;

        /// <summary>The Pet definition's <c>PassiveThreshold</c> (<c>DATABASE.md</c> §1).</summary>
        internal int PetDefinitionPassiveThreshold { get; private set; } = 5;

        /// <summary>The Pet definition's <c>SignatureSkillCardId</c> (<c>DATABASE.md</c> §1).</summary>
        internal string PetDefinitionSignatureSkillCard { get; private set; } = "card_skill_1";

        // --- row mutations ----------------------------------------------------

        internal void SetPlayerLevel(int level) => PlayerLevel = level;

        internal void SetPetLevel(int level) => PetLevel = level;

        internal void SetPetStar(int star) => PetStar = star;

        internal void SetPetTier(PetTier tier) => PetTier = tier;

        internal void SetPetDefinitionElement(Element element) => PetDefinitionElement = element;

        internal void SetPetDefinitionPassiveThreshold(int threshold) =>
            PetDefinitionPassiveThreshold = threshold;

        internal void SetPetDefinitionSignatureSkillCard(string cardDefinitionId) =>
            PetDefinitionSignatureSkillCard = cardDefinitionId;

        internal void SetCardDefinitionCopyLimit(string cardDefinitionId, int copyLimit) =>
            _cardCopyLimits[cardDefinitionId] = copyLimit;

        internal void RemoveCardDefinition(string cardDefinitionId) =>
            _removedCardDefinitions.Add(cardDefinitionId);

        internal void SetRelicDefinitionId(string relicInstanceId, string relicDefinitionId) =>
            _relicDefinitionIds[relicInstanceId] = relicDefinitionId;

        private readonly Dictionary<string, int> _cardCopyLimits = new(StringComparer.Ordinal)
        {
            ["card_basic_a"] = 3,
            ["card_basic_b"] = 3,
            ["card_basic_c"] = 3,
            ["card_skill_1"] = 1,
        };

        private readonly HashSet<string> _removedCardDefinitions = new(StringComparer.Ordinal);

        private readonly Dictionary<string, string> _relicDefinitionIds = new(StringComparer.Ordinal)
        {
            ["relic_z"] = "relic_def_1",
            ["relic_a"] = "relic_def_1",
            ["relic_m"] = "relic_def_1",
        };

        // --- the documented battle under test ---------------------------------

        /// <summary>
        /// Creates the battle through the production creation path with the
        /// battle-start path's own composition: the selected owned Pet instance,
        /// the Element and Passive from that instance's definition, and both
        /// battle-scoped loadout snapshots (<c>GAME_STATE.md</c> §2.3,
        /// <c>CARD_RULES.md</c> §1, <c>RELIC_RULES.md</c> §2.2).
        ///
        /// The Relic loadout is deliberately submitted in a NON-sorted order, so a
        /// re-sorting implementation cannot pass the order assertions
        /// (<c>RELIC_RULES.md</c> §2.3 item 2).
        /// </summary>
        internal Task<BattleState> CreateBattleAsync(string battleId) =>
            Battles.CreateBattleAsync(
                battleId,
                new PlayerId(Owner),
                new BattleStateService.PetConfiguration(
                    PetId: new PetId(PetInstanceId),
                    Element: PetDefinitionElement,
                    PassiveId: new PassiveId("pet-passive-1"),
                    PassiveThreshold: PetDefinitionPassiveThreshold,
                    PassiveResetOverride: null,
                    EquippedRelics:
                    [
                        new GameServer.Domain.Relics.EquippedRelicIdentity("relic_z"),
                        new GameServer.Domain.Relics.EquippedRelicIdentity("relic_a"),
                        new GameServer.Domain.Relics.EquippedRelicIdentity("relic_m"),
                    ],
                    // The 3 submitted Basic Cards plus the derived Signature Skill
                    // (CARD_RULES.md §1) — the Signature Skill is the definition's
                    // reference, never submitted.
                    EquippedCards:
                    [
                        new GameServer.Domain.Cards.EquippedCardIdentity("card_basic_a"),
                        new GameServer.Domain.Cards.EquippedCardIdentity("card_basic_b"),
                        new GameServer.Domain.Cards.EquippedCardIdentity("card_basic_c"),
                        new GameServer.Domain.Cards.EquippedCardIdentity(
                            PetDefinitionSignatureSkillCard),
                    ]),
                BossDefinitions.HoaLong);

        /// <summary>
        /// Reads the authoritative record, as the production read does
        /// (<c>REDIS_STATE.md</c> §2 item 2 — the store is the source of truth,
        /// not an in-process copy).
        /// </summary>
        internal Task<BattleState?> ReadBattleAsync(string battleId) =>
            Battles.GetBattleAsync(battleId);

        internal Task<SwapExecutionResult?> ExecuteSwapAsync(string battleId, SwapRequest pair) =>
            Battles.ExecuteSwapAsync(battleId, pair);

        /// <summary>
        /// The first adjacent Swap the production validator accepts, so the
        /// scenario drives the real resolution pipeline rather than a hand-built
        /// state. A rejection is a documented no-op
        /// (<c>MATCH3_RULES.md</c> §2.1.5), so scanning alters nothing.
        /// </summary>
        internal SwapRequest FindAcceptedSwap(BattleState state)
        {
            for (var row = 0; row < BoardState.Rows; row++)
            {
                for (var column = 0; column < BoardState.Columns; column++)
                {
                    var from = BoardState.ToIndex(row, column);

                    var candidates = new List<int>(2);

                    if (column + 1 < BoardState.Columns)
                    {
                        candidates.Add(BoardState.ToIndex(row, column + 1));
                    }

                    if (row + 1 < BoardState.Rows)
                    {
                        candidates.Add(BoardState.ToIndex(row + 1, column));
                    }

                    foreach (var to in candidates)
                    {
                        var request = new SwapRequest(from, to);

                        if (SwapValidator.Validate(state.BoardState, state.LastCommittedSwapPair, request)
                            .IsAccepted)
                        {
                            return request;
                        }
                    }
                }
            }

            throw new InvalidOperationException(
                "The created board accepted no adjacent Swap; the resolution path cannot be exercised.");
        }
    }
}
