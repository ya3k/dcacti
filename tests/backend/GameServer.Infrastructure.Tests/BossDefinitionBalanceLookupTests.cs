using System.Text.Json;
using GameServer.Domain.Bosses;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Configurations;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The battle-creation Boss lookup — the Domain Overlay of
/// <c>DATABASE.md</c> §1 note item 2 and <c>ADR-024</c> §2.2/§2.4.
///
/// <code>
/// Identity (client-submitted)
///         ↓  BossDefinitions.All            the structural baseline
///         ↓  BossDefinition row, by Identity  the five overlay values
///         ↓
/// BossDefinition with the row's overlay and the baseline's mechanics
/// </code>
///
/// What is verified is the documented contract: PostgreSQL is authoritative for
/// the five overlay values — <c>MaxHP</c>, <c>ATK</c>, <c>DEF</c>,
/// <c>EnrageThreshold</c>, and the nullable <c>CastPriorityScore</c> (the fifth,
/// added in place by TASK-308 / OD-3a) — the structural definition — Identity,
/// Element, Passive, Skill, and the Skill's <c>SecondaryEffect</c> — is the
/// Domain baseline's and survives the overlay intact; an Identity outside the
/// content-defined MVP set is the client-visible miss; an absent or out-of-range
/// provisioned row fails closed with an exception rather than being answered from
/// the static baseline; a <c>CastPriorityScore</c> of <c>null</c> is the
/// documented "not authored" value and is overlaid as such, while a negative
/// value fails closed; and the battle-end key lookup is unchanged.
///
/// These tests run on the InMemory provider because the overlay is provider
/// independent: the same <see cref="BossDefinitionLookup"/> code path executes
/// here and against PostgreSQL. The applied schema, the range constraints, and
/// the canonical stored values are verified against a live database by
/// <see cref="BossDefinitionPostgresProvisioningTests"/>.
/// </summary>
public class BossDefinitionBalanceLookupTests
{
    // -----------------------------------------------------------------------
    // The overlay — ADR-024 §2.2
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldOverlayTheRowsFiveOverlayValues()
    {
        // ADR-024 §2.2 item 3 (as amended by TASK-308): the returned definition is
        // the Domain baseline with the row's MaxHP, ATK, DEF, EnrageThreshold, and
        // CastPriorityScore applied. Every overlaid value differs from the
        // baseline's, so an implementation that returned the baseline (the
        // forbidden silent fallback) fails.
        const string store = nameof(GetDefinitionByIdentityAsync_ShouldOverlayTheRowsFiveOverlayValues);

        await using (var context = CreateContext(store))
        {
            context.BossDefinitions.Add(
                BossDefinitions.HoaLong with
                {
                    MaxHP = 4321,
                    ATK = 222,
                    DEF = 33,
                    EnrageThreshold = 0.42,
                    // Hỏa Long's baseline score is 85 after TASK-309 Slice 4; the
                    // row's authored value is what the overlay must return.
                    CastPriorityScore = 21,
                });

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var overlaid = await CreateLookup(context)
                .GetDefinitionByIdentityAsync("boss-hoa-long");

            Assert.NotNull(overlaid);

            Assert.Equal(4321, overlaid!.MaxHP);
            Assert.Equal(222, overlaid.ATK);
            Assert.Equal(33, overlaid.DEF);
            Assert.Equal(0.42, overlaid.EnrageThreshold, precision: 9);
            Assert.Equal(21, overlaid.CastPriorityScore);

            // The baseline is NOT the answer for any of the five.
            Assert.NotEqual(BossDefinitions.HoaLong.MaxHP, overlaid.MaxHP);
            Assert.NotEqual(BossDefinitions.HoaLong.ATK, overlaid.ATK);
            Assert.NotEqual(BossDefinitions.HoaLong.DEF, overlaid.DEF);
            Assert.NotEqual(BossDefinitions.HoaLong.EnrageThreshold, overlaid.EnrageThreshold);
            Assert.NotEqual(BossDefinitions.HoaLong.CastPriorityScore, overlaid.CastPriorityScore);
        }
    }

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldResolveEveryCanonicalMvpBoss()
    {
        // BOSS_RULES.md §6.4 fixes five canonical technical Identities. Each is
        // resolvable through persistence at its own canonical balance
        // (DATABASE.md §1 note item 5: all five rows are provisioned by migration).
        const string store = nameof(GetDefinitionByIdentityAsync_ShouldResolveEveryCanonicalMvpBoss);

        await using (var context = CreateContext(store))
        {
            SeedCanonicalRows(context);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var lookup = CreateLookup(context);

            foreach (var baseline in BossDefinitions.All)
            {
                var resolved = await lookup.GetDefinitionByIdentityAsync(baseline.BossId.Value);

                Assert.NotNull(resolved);
                Assert.Equal(baseline.BossDefinitionId, resolved!.BossDefinitionId);
                Assert.Equal(baseline.BossId, resolved.BossId);
                Assert.Equal(baseline.Element, resolved.Element);
                Assert.Equal(baseline.MaxHP, resolved.MaxHP);
                Assert.Equal(baseline.ATK, resolved.ATK);
                Assert.Equal(baseline.DEF, resolved.DEF);
                Assert.Equal(baseline.EnrageThreshold, resolved.EnrageThreshold);

                // The fifth overlay value: every MVP Boss is authored after
                // TASK-309 B-1 — Sơn Thạch Vệ's Slice 3 value 16 (`earthquake`)
                // and 85 for the other four Slice 4 Boss Cards — and the row's
                // value is overlaid as stored rather than re-derived.
                Assert.Equal(baseline.CastPriorityScore, resolved.CastPriorityScore);
                Assert.Equal(
                    baseline.BossId.Value == "boss-son-thach-ve" ? (int?)16 : 85,
                    resolved.CastPriorityScore);
            }
        }
    }

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldCarryTheCanonicalHoaLongBaseline()
    {
        // The invariant TASK-251 names explicitly: Hỏa Long is 5000 / 100 / 50 /
        // 0.30 (BOSS_RULES.md §6.1), i.e. an absolute Enrage threshold of 1500 HP
        // (1500 / 5000 = 30%), and the resolved definition must carry exactly that
        // when the row holds the canonical values.
        const string store = nameof(GetDefinitionByIdentityAsync_ShouldCarryTheCanonicalHoaLongBaseline);

        await using (var context = CreateContext(store))
        {
            SeedCanonicalRows(context);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var resolved = await CreateLookup(context)
                .GetDefinitionByIdentityAsync("boss-hoa-long");

            Assert.NotNull(resolved);
            Assert.Equal(5000, resolved!.MaxHP);
            Assert.Equal(100, resolved.ATK);
            Assert.Equal(50, resolved.DEF);
            Assert.Equal(0.30, resolved.EnrageThreshold, precision: 9);
            Assert.Equal(1500d, resolved.MaxHP * resolved.EnrageThreshold, precision: 9);

            // BOSS_RULES.md §5 item 4's comparison is strict.
            Assert.True(1499 < resolved.MaxHP * resolved.EnrageThreshold);
            Assert.False(1500 < resolved.MaxHP * resolved.EnrageThreshold);
        }
    }

    // -----------------------------------------------------------------------
    // Structural preservation — ADR-024 §2.2 item 4
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldPreserveEveryStructuralField()
    {
        // ADR-024 §2.2 item 4: "SecondaryEffect (Burn, Drain Power, Root), Element,
        // PassiveDefinition, and SkillDefinition mechanical typings remain intact
        // from the Domain definition". The overlaid definition must equal the
        // baseline in every member except the four balance values — asserted
        // member by member, because a materialized row would null the
        // non-persisted members rather than change them.
        const string store = nameof(GetDefinitionByIdentityAsync_ShouldPreserveEveryStructuralField);

        await using (var context = CreateContext(store))
        {
            SeedCanonicalRows(context);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var lookup = CreateLookup(context);

            foreach (var baseline in BossDefinitions.All)
            {
                var resolved = (await lookup.GetDefinitionByIdentityAsync(baseline.BossId.Value))!;

                // DATABASE.md §1 note items 2–4 and BOSS_RULES.md §6.4: the
                // persistence key, the canonical Identity, the Element, and the two
                // configuration groups are the baseline's, unchanged.
                Assert.Equal(baseline.BossDefinitionId, resolved.BossDefinitionId);
                Assert.Equal(baseline.BossId, resolved.BossId);
                Assert.Equal(baseline.Element, resolved.Element);
                Assert.Equal(baseline.PassiveDefinition, resolved.PassiveDefinition);
                Assert.Equal(baseline.SkillDefinition, resolved.SkillDefinition);

                // The Passive's projections and the Skill's four stored members
                // plus its declared secondary effect.
                Assert.Equal(baseline.PassiveId, resolved.PassiveId);
                Assert.Equal(baseline.PassiveThreshold, resolved.PassiveThreshold);
                Assert.Equal(baseline.PassiveResetBehavior, resolved.PassiveResetBehavior);
                Assert.Equal(baseline.SkillId, resolved.SkillId);
                Assert.Equal(baseline.SkillBaseDamage, resolved.SkillBaseDamage);
                Assert.Equal(baseline.SkillChargeRequirement, resolved.SkillChargeRequirement);
                Assert.Equal(baseline.SkillCooldownTurns, resolved.SkillCooldownTurns);
                Assert.Equal(baseline.SkillDefinition.SecondaryEffect, resolved.SkillDefinition.SecondaryEffect);

                // The fifth overlay value is overlaid (the row here holds the
                // canonical value) and is never re-derived by the overlay.
                Assert.Equal(baseline.CastPriorityScore, resolved.CastPriorityScore);

                // The Initial State the definition produces carries the row's
                // balance (GAME_STATE.md §2.4, BOSS_RULES.md §6.1).
                var initialState = resolved.ToInitialState();

                Assert.Equal(baseline.BossId, initialState.BossId);
                Assert.Equal(baseline.Element, initialState.Element);
                Assert.Equal(resolved.MaxHP, initialState.MaxHP);
                Assert.Equal(resolved.MaxHP, initialState.HP);
                Assert.Equal(resolved.ATK, initialState.ATK);
                Assert.Equal(resolved.DEF, initialState.DEF);
            }
        }
    }

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects()
    {
        // The hazard ADR-024 §1 names: BossSkillDefinition.SecondaryEffect has no
        // column, so a definition materialized from the row alone would carry
        // null and silently drop Flame Burst's Burn, Drain Power's Power
        // reduction, and Root's ATK debuff (BOSS_RULES.md §6.3.1). The stored
        // document is verified to carry no secondary effect, and the overlaid
        // definition is verified to carry the declared one — which is exactly what
        // the Domain Overlay exists to achieve.
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects);

        await using (var context = CreateContext(store))
        {
            SeedCanonicalRows(context);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            // The stored SkillDefinition document is the four-member persistence
            // contract (DATABASE.md §1 note item 4), so the row offers no member
            // that could carry a secondary effect at all. The stored document is
            // asserted through the persistence converter — the component that owns
            // the mapping — because the InMemory provider stores no column text.
            var stored = await context.BossDefinitions
                .AsNoTracking()
                .SingleAsync(definition => definition.BossDefinitionId == "boss-def-hoa-long");

            var storedSkillJson = BossDefinitionJson.WriteSkill(stored.SkillDefinition);

            Assert.Equal(
                new[] { "baseDamage", "chargeRequirement", "cooldownTurns", "skillId" },
                JsonDocument.Parse(storedSkillJson).RootElement.EnumerateObject()
                    .Select(member => member.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray());

            var lookup = CreateLookup(context);

            var flameBurst = (await lookup.GetDefinitionByIdentityAsync("boss-hoa-long"))!;
            var drainPower = (await lookup.GetDefinitionByIdentityAsync("boss-thuy-ma"))!;
            var root = (await lookup.GetDefinitionByIdentityAsync("boss-moc-yeu"))!;

            // §6.3.1 item 1 — Flame Burst's Burn: 50 fixed damage/tick for 2 Turns.
            var burn = flameBurst.SkillDefinition.SecondaryEffect;

            Assert.NotNull(burn);
            Assert.Equal(BossSkillSecondaryEffectKind.Burn, burn!.Value.Kind);
            Assert.Equal(50, burn.Value.Magnitude);
            Assert.Equal(2, burn.Value.DurationTurns);

            // §6.3.1 item 2 — Drain Power's instant flat Pet Power reduction.
            var drain = drainPower.SkillDefinition.SecondaryEffect;

            Assert.NotNull(drain);
            Assert.Equal(BossSkillSecondaryEffectKind.PowerDrain, drain!.Value.Kind);
            Assert.Equal(20, drain.Value.Magnitude);
            Assert.Null(drain.Value.DurationTurns);

            // §6.3.1 item 3 — Root's Turn-based Pet ATK debuff.
            var atkDebuff = root.SkillDefinition.SecondaryEffect;

            Assert.NotNull(atkDebuff);
            Assert.Equal(BossSkillSecondaryEffectKind.AtkDebuff, atkDebuff!.Value.Kind);
            Assert.Equal(30, atkDebuff.Value.Magnitude);
            Assert.Equal(2, atkDebuff.Value.DurationTurns);
            Assert.Equal("ATK", atkDebuff.Value.TargetStat);
        }
    }

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldNotMutateTheStaticBaseline()
    {
        // ADR-024 §2.1 item 1: BossDefinitions.All stays the structural Domain
        // baseline and the offline fixture. The overlay must therefore produce a
        // new value rather than altering the canonical content every test and
        // every other battle reads.
        const string store = nameof(GetDefinitionByIdentityAsync_ShouldNotMutateTheStaticBaseline);

        var before = BossDefinitions.All
            .Select(definition => (
                definition.BossDefinitionId,
                definition.MaxHP,
                definition.ATK,
                definition.DEF,
                definition.EnrageThreshold,
                definition.CastPriorityScore))
            .ToArray();

        await using (var context = CreateContext(store))
        {
            context.BossDefinitions.Add(
                BossDefinitions.HoaLong with
                {
                    MaxHP = 7,
                    ATK = 7,
                    DEF = 7,
                    EnrageThreshold = 0.7,
                    CastPriorityScore = 7,
                });

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var overlaid = await CreateLookup(context)
                .GetDefinitionByIdentityAsync("boss-hoa-long");

            Assert.Equal(7, overlaid!.MaxHP);
            Assert.Equal(7, overlaid.CastPriorityScore);
        }

        var after = BossDefinitions.All
            .Select(definition => (
                definition.BossDefinitionId,
                definition.MaxHP,
                definition.ATK,
                definition.DEF,
                definition.EnrageThreshold,
                definition.CastPriorityScore))
            .ToArray();

        Assert.Equal(before, after);
        Assert.Equal(5000, BossDefinitions.HoaLong.MaxHP);
        // BOSS_RULES.md §4.3 item 5 / TASK-309 B-1: all five MVP Bosses now carry
        // their approved score — 85 for the Slice 4 Boss Cards and 16 for
        // `earthquake` — and none is authored by a row rather than the baseline.
        Assert.Equal(85, BossDefinitions.HoaLong.CastPriorityScore);
        Assert.Equal(16, BossDefinitions.SonThachVe.CastPriorityScore);
    }

    // -----------------------------------------------------------------------
    // The client-visible miss — ADR-024 §2.1 item 1
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Not A Real Boss")]
    [InlineData("boss-hoa-long ")]
    [InlineData("boss-def-hoa-long")]
    [InlineData("\" OR 1=1 --")]
    public async Task GetDefinitionByIdentityAsync_ShouldReturnNullForANonCanonicalIdentity(string identity)
    {
        // ADR-024 §2.1 item 1: an identity that is not a content-defined MVP Boss
        // is the documented client miss, answered with null so battle start
        // produces BOSS_NOT_FOUND / HTTP 400 — never with an invented definition,
        // and never by matching the persistence key instead of the Identity
        // (DATABASE.md §1 note item 2: the two are never substituted for each
        // other).
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldReturnNullForANonCanonicalIdentity);

        await using (var context = CreateContext(store))
        {
            SeedCanonicalRows(context);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            Assert.Null(await CreateLookup(context).GetDefinitionByIdentityAsync(identity));
        }
    }

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldNotQueryForANonCanonicalIdentity()
    {
        // The miss is decided from the structural baseline, so an unknown identity
        // is rejected without reading the table at all. The store is empty here:
        // an implementation that queried first and then invented a result would
        // still have to answer, and it must answer null.
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldNotQueryForANonCanonicalIdentity);

        await using var context = CreateContext(store);

        Assert.Null(await CreateLookup(context).GetDefinitionByIdentityAsync("boss-nobody"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task GetDefinitionByIdentityAsync_ShouldRejectAnUnnamedIdentity(string identity)
    {
        // A blank identity cannot name a Boss. It is rejected as a programming
        // error rather than sent to persistence as an unnamed query; the caller
        // that cannot name an identity decides the documented BOSS_NOT_FOUND
        // outcome itself (BattleStartService does exactly that).
        const string store = nameof(GetDefinitionByIdentityAsync_ShouldRejectAnUnnamedIdentity);

        await using var context = CreateContext(store);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            CreateLookup(context).GetDefinitionByIdentityAsync(identity));
    }

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldRejectANullIdentity()
    {
        const string store = nameof(GetDefinitionByIdentityAsync_ShouldRejectANullIdentity);

        await using var context = CreateContext(store);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            CreateLookup(context).GetDefinitionByIdentityAsync(null!));
    }

    // -----------------------------------------------------------------------
    // Fail-closed paths — ADR-024 §2.1 item 3, §2.4
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("boss-hoa-long")]
    [InlineData("boss-thuy-ma")]
    [InlineData("boss-moc-yeu")]
    [InlineData("boss-son-thach-ve")]
    [InlineData("boss-kim-loi-vuong")]
    public async Task GetDefinitionByIdentityAsync_ShouldFailClosedWhenTheProvisionedRowIsMissing(
        string identity)
    {
        // ADR-024 §2.1 item 3: a valid canonical MVP Boss whose row is absent is a
        // server-side provisioning failure. It must NOT be answered from the
        // static baseline (the silent split-brain configuration §2.1 forbids) and
        // must not become the client's BOSS_NOT_FOUND. The exception names the
        // Identity so an operator can act on it.
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldFailClosedWhenTheProvisionedRowIsMissing);

        await using var context = CreateContext(store);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateLookup(context).GetDefinitionByIdentityAsync(identity));

        Assert.Contains(identity, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldFailClosedRatherThanFallBackToTheBaseline()
    {
        // The single most important invariant: an empty store never yields the
        // canonical 5000 / 100 / 50 / 0.30. The lookup throws, and the baseline's
        // numbers appear nowhere in the result because there is no result.
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldFailClosedRatherThanFallBackToTheBaseline);

        await using var context = CreateContext(store);

        Assert.Empty(await context.BossDefinitions.ToListAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateLookup(context).GetDefinitionByIdentityAsync("boss-hoa-long"));
    }

    [Theory]
    // ADR-024 §2.4: each documented invariant, including both exclusive
    // EnrageThreshold boundaries.
    [InlineData(0, 100, 50, 0.30, "MaxHP")]
    [InlineData(-1, 100, 50, 0.30, "MaxHP")]
    [InlineData(int.MinValue, 100, 50, 0.30, "MaxHP")]
    [InlineData(5000, -1, 50, 0.30, "ATK")]
    [InlineData(5000, int.MinValue, 50, 0.30, "ATK")]
    [InlineData(5000, 100, -1, 0.30, "DEF")]
    [InlineData(5000, 100, int.MinValue, 0.30, "DEF")]
    [InlineData(5000, 100, 50, 0.0, "EnrageThreshold")]
    [InlineData(5000, 100, 50, 1.0, "EnrageThreshold")]
    [InlineData(5000, 100, 50, -0.5, "EnrageThreshold")]
    [InlineData(5000, 100, 50, 1.5, "EnrageThreshold")]
    [InlineData(5000, 100, 50, double.NaN, "EnrageThreshold")]
    public async Task GetDefinitionByIdentityAsync_ShouldFailClosedOnAnOutOfRangeBalanceValue(
        int maxHp,
        int atk,
        int def,
        double enrageThreshold,
        string offendingColumn)
    {
        // ADR-024 §2.4: the four range invariants are enforced before any value is
        // used, so a corrupt row drives no combat math. The message names the
        // column, the stored value, and the violated invariant.
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldFailClosedOnAnOutOfRangeBalanceValue);

        await using (var context = CreateContext(store))
        {
            context.BossDefinitions.Add(
                BossDefinitions.HoaLong with
                {
                    MaxHP = maxHp,
                    ATK = atk,
                    DEF = def,
                    EnrageThreshold = enrageThreshold,
                });

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateLookup(context).GetDefinitionByIdentityAsync("boss-hoa-long"));

            Assert.Contains(offendingColumn, thrown.Message, StringComparison.Ordinal);
            Assert.Contains("boss-hoa-long", thrown.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    // The inclusive lower bounds and the exclusive interval edges' interior: the
    // constraints bound the range without forbidding values inside it.
    [InlineData(1, 0, 0, 0.0000001)]
    [InlineData(5000, 0, 0, 0.9999999)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, 0.5)]
    public async Task GetDefinitionByIdentityAsync_ShouldAcceptInRangeBoundaryValues(
        int maxHp,
        int atk,
        int def,
        double enrageThreshold)
    {
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldAcceptInRangeBoundaryValues);

        await using (var context = CreateContext(store))
        {
            context.BossDefinitions.Add(
                BossDefinitions.HoaLong with
                {
                    MaxHP = maxHp,
                    ATK = atk,
                    DEF = def,
                    EnrageThreshold = enrageThreshold,
                });

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var resolved = await CreateLookup(context)
                .GetDefinitionByIdentityAsync("boss-hoa-long");

            Assert.NotNull(resolved);
            Assert.Equal(maxHp, resolved!.MaxHP);
            Assert.Equal(atk, resolved.ATK);
            Assert.Equal(def, resolved.DEF);
            Assert.Equal(enrageThreshold, resolved.EnrageThreshold);
        }
    }

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldOverlayANullCastPriorityScoreAsNotAuthored()
    {
        // DATABASE.md §1/§3 (TASK-308 / OD-3a): NULL is the explicit "no
        // cast-priority score authored" marker and is a legal overlay result. The
        // row is authoritative here too: Sơn Thạch Vệ's Domain baseline carries 16,
        // so a lookup that fell back to the baseline would answer 16 instead of the
        // row's documented NULL. The stricter "a present Boss Card requires a
        // score" rule is enforced by battle creation, which sees the Card and the
        // score together — not by this overlay.
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldOverlayANullCastPriorityScoreAsNotAuthored);

        await using (var context = CreateContext(store))
        {
            context.BossDefinitions.Add(
                BossDefinitions.SonThachVe with { CastPriorityScore = null });

            await context.SaveChangesAsync();
        }

        Assert.Equal(16, BossDefinitions.SonThachVe.CastPriorityScore);

        await using (var context = CreateContext(store))
        {
            var resolved = await CreateLookup(context)
                .GetDefinitionByIdentityAsync("boss-son-thach-ve");

            Assert.NotNull(resolved);
            Assert.Null(resolved!.CastPriorityScore);

            // Everything else is still the row's canonical overlay.
            Assert.Equal(3000, resolved.MaxHP);
            Assert.Equal(120, resolved.ATK);
        }
    }

    [Theory]
    // ADR-024 §2.4 as amended by TASK-308: the score's nullable range invariant.
    // NULL is documented as "not authored" and never a failure at this layer.
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(int.MaxValue)]
    public async Task GetDefinitionByIdentityAsync_ShouldAcceptANullableCastPriorityScoreInRange(
        int? castPriorityScore)
    {
        // DATABASE.md §3: `CastPriorityScore IS NULL OR CastPriorityScore >= 0` —
        // the absent form and the inclusive lower bound are both legal, and a large
        // present value is bounded by no upper limit.
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldAcceptANullableCastPriorityScoreInRange);

        await using (var context = CreateContext(store))
        {
            context.BossDefinitions.Add(
                BossDefinitions.SonThachVe with { CastPriorityScore = castPriorityScore });

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var resolved = await CreateLookup(context)
                .GetDefinitionByIdentityAsync("boss-son-thach-ve");

            Assert.NotNull(resolved);
            Assert.Equal(castPriorityScore, resolved!.CastPriorityScore);
        }
    }

    [Theory]
    // ADR-024 §2.4 / DATABASE.md §3 (TASK-308 / OD-3a): a present, negative score
    // is corrupt definition data and fails closed. int.MinValue is included so the
    // boundary of the CLR type is covered, not just -1.
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task GetDefinitionByIdentityAsync_ShouldFailClosedOnANegativeCastPriorityScore(
        int castPriorityScore)
    {
        // The same fail-closed shape as the four balance values: an
        // InvalidOperationException naming the Boss, the column, the stored value,
        // and the violated invariant. NULL is NOT this case — only a real negative.
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldFailClosedOnANegativeCastPriorityScore);

        await using (var context = CreateContext(store))
        {
            context.BossDefinitions.Add(
                BossDefinitions.SonThachVe with { CastPriorityScore = castPriorityScore });

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateLookup(context).GetDefinitionByIdentityAsync("boss-son-thach-ve"));

            Assert.Contains("CastPriorityScore", thrown.Message, StringComparison.Ordinal);
            Assert.Contains("boss-son-thach-ve", thrown.Message, StringComparison.Ordinal);
            Assert.Contains(
                castPriorityScore.ToString(System.Globalization.CultureInfo.InvariantCulture),
                thrown.Message,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldReadTheRowByTheCanonicalIdentity()
    {
        // DATABASE.md §1 note item 2 / §3: the lookup is by Identity alone, which
        // is NOT NULL and UNIQUE. A row whose persistence key differs from its
        // Identity resolves by the Identity, and the two are never substituted for
        // each other.
        const string store =
            nameof(GetDefinitionByIdentityAsync_ShouldReadTheRowByTheCanonicalIdentity);

        await using (var context = CreateContext(store))
        {
            SeedCanonicalRows(context);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var resolved = await CreateLookup(context)
                .GetDefinitionByIdentityAsync("boss-son-thach-ve");

            Assert.NotNull(resolved);
            Assert.Equal("boss-def-son-thach-ve", resolved!.BossDefinitionId);
            Assert.Equal("boss-son-thach-ve", resolved.BossId.Value);
            Assert.NotEqual(resolved.BossDefinitionId, resolved.BossId.Value);

            // BOSS_RULES.md §6.1: Sơn Thạch Vệ is 3000 / 120 / 0 / 50%.
            Assert.Equal(3000, resolved.MaxHP);
            Assert.Equal(120, resolved.ATK);
            Assert.Equal(0, resolved.DEF);
            Assert.Equal(0.50, resolved.EnrageThreshold, precision: 9);

            // BOSS_RULES.md §4.3 item 5 / TASK-308 OD-3b: the authored initial
            // cast-priority score for its provisioned Boss Card.
            Assert.Equal(16, resolved.CastPriorityScore);
        }
    }

    [Fact]
    public async Task GetDefinitionByIdentityAsync_ShouldNotWriteAnything()
    {
        // The lookup is a read (DATABASE.md §1 note item 2: "the lookup is a query
        // on the existing persistence boundary"): resolving a Boss must not stage
        // or write a row, so the change tracker stays clean.
        const string store = nameof(GetDefinitionByIdentityAsync_ShouldNotWriteAnything);

        await using (var context = CreateContext(store))
        {
            SeedCanonicalRows(context);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            await CreateLookup(context).GetDefinitionByIdentityAsync("boss-hoa-long");

            Assert.Empty(context.ChangeTracker.Entries());
        }
    }

    // -----------------------------------------------------------------------
    // The battle-end key lookup is unchanged — DATABASE.md §1 note item 2
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FindBossDefinitionIdByIdentityAsync_ShouldStillResolveOnlyThePersistenceKey()
    {
        // TASK-051's battle-end contract is untouched by ADR-024: the FK needs the
        // row's persistence key and nothing else, so the lookup still returns the
        // key (not a definition) and still returns null when no row carries the
        // Identity.
        const string store =
            nameof(FindBossDefinitionIdByIdentityAsync_ShouldStillResolveOnlyThePersistenceKey);

        await using (var context = CreateContext(store))
        {
            SeedCanonicalRows(context);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(store))
        {
            var lookup = CreateLookup(context);

            Assert.Equal(
                "boss-def-hoa-long",
                await lookup.FindBossDefinitionIdByIdentityAsync("boss-hoa-long"));

            Assert.Null(await lookup.FindBossDefinitionIdByIdentityAsync("boss-nobody"));

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                lookup.FindBossDefinitionIdByIdentityAsync(" "));
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// A unique suffix for this test case's store.
    ///
    /// The InMemory provider is keyed by database name against a process-wide root
    /// (<see cref="TestGameDbContextFactory"/> shares one internal service
    /// provider), so a name derived from the method alone is re-used by every case
    /// of a <c>[Theory]</c> and the rows would accumulate across cases. One suffix
    /// per class instance — xUnit builds a fresh instance per case — keeps every
    /// case isolated while still letting a test open a second context to read back
    /// what the first one wrote.
    /// </summary>
    private readonly string _caseId = Guid.NewGuid().ToString("N");

    private GameDbContext CreateContext(string scenario) =>
        TestGameDbContextFactory.Create($"{scenario}-{_caseId}");

    private static BossDefinitionLookup CreateLookup(GameDbContext context) => new(context);

    /// <summary>
    /// Seeds every canonical MVP Boss at its Domain baseline — the state a
    /// database with all provisioning migrations applied holds
    /// (<c>DATABASE.md</c> §1 note item 5).
    /// </summary>
    private static void SeedCanonicalRows(GameDbContext context)
    {
        foreach (var definition in BossDefinitions.All)
        {
            context.BossDefinitions.Add(definition with { });
        }
    }
}
