using GameServer.Domain.Cards;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="PetDefinition"/> (<c>DATABASE.md</c> §1, §3).
///
/// <code>
/// PetDefinition
/// ├── PetDefinitionId       (PK)
/// ├── Identity
/// ├── Element
/// ├── PetLevelMultiplier    (decimal > 0)
/// ├── PassiveId             (threshold/effect reference)
/// ├── PassiveThreshold
/// └── SignatureSkillCardId  (FK → CardDefinition)
/// </code>
///
/// <b>The multiplier constraint is the documented one.</b>
/// <c>DATABASE.md</c> §3 states <c>PetLevelMultiplier &gt; 0 (decimal)</c>
/// (<c>PET_RULES.md</c> §5 item 1); the check constraint spells that bound
/// directly. No default value is applied: concrete MVP multipliers are
/// balance/config and are deferred (<c>PET_RULES.md</c> §5 item 3).
///
/// <b><c>SignatureSkillCardId</c> completes the TASK-024 deferral.</b>
/// <c>DATABASE.md</c> §1 lists the FK and §2 states
/// <c>PetDefinition 1 ── 1 CardDefinition</c>. TASK-024 deliberately left it
/// unmapped because <c>CardDefinition</c> did not exist yet; TASK-028 now owns
/// the integration and maps it as the documented relationship.
///
/// It is <b>required and non-nullable</b>: <c>CARD_RULES.md</c> §4 item 1
/// states "Each Pet has exactly one Signature Skill, expressed as one Pet Skill
/// Card", so every Pet definition has one, and the battle's fourth Card is
/// derived from it. No default definition is invented — a Pet whose Skill Card
/// cannot be resolved is invalid definition data, which the loadout validator
/// reports rather than substituting (<c>CARD_RULES.md</c> §1).
/// </summary>
public sealed class PetDefinitionConfiguration : IEntityTypeConfiguration<PetDefinition>
{
    public void Configure(EntityTypeBuilder<PetDefinition> builder)
    {
        builder.ToTable("PetDefinition");

        builder.HasKey(definition => definition.PetDefinitionId);

        // DATABASE.md §1: PetDefinitionId (PK). The identifier is the domain
        // string every owned Pet instance references as a FK (§2), stored as
        // a bounded string rather than a database-generated value.
        builder.Property(definition => definition.PetDefinitionId)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1: Identity — the Pet's display name
        // ("Thanh Xà", "Xích Lang", ...). Bounded for a human-readable name;
        // no uniqueness constraint is documented (MVP ships one definition
        // row per Pet, enforced by content, not by a database rule).
        builder.Property(definition => definition.Identity)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1: Element — one of the Five Elements
        // (ELEMENT_RULES.md §1). Stored as the enum's numeric value; the
        // closed Element set lives in the Domain enum, not in a database
        // CHECK (ELEMENT_RULES.md §1, §7).
        builder.Property(definition => definition.Element)
            .IsRequired();

        // DATABASE.md §1/§3: PetLevelMultiplier — decimal > 0
        // (PET_RULES.md §5 item 1). Precision supports fractional
        // multipliers (e.g. 1.5) without silent rounding; the CHECK enforces
        // the lower bound at the database level.
        builder.Property(definition => definition.PetLevelMultiplier)
            .HasPrecision(9, 4)
            .IsRequired();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_PetDefinition_PetLevelMultiplier_Positive",
            "\"PetLevelMultiplier\" > 0"));

        // DATABASE.md §1: PassiveDefinition (threshold/effect reference) —
        // stored as PassiveId (identity) + PassiveThreshold, matching how
        // BossDefinition carries the same pair without inlining effect
        // content (PASSIVE_RULES.md §1). PassiveId is a Domain value type;
        // the column holds its string Value.
        builder.Property(definition => definition.PassiveId)
            .HasConversion(id => id.Value, value => new PassiveId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(definition => definition.PassiveThreshold)
            .IsRequired();

        // DATABASE.md §1/§2: SignatureSkillCardId FK → CardDefinition
        // (PetDefinition 1 ── 1 CardDefinition). Required, because
        // CARD_RULES.md §4 item 1 gives every Pet exactly one Signature Skill
        // and the battle's derived fourth Card is read through this reference.
        // Restrict keeps a Pet definition from losing its Skill Card as a side
        // effect of a CardDefinition delete without an explicit cascade
        // decision, matching the Pet and Relic FK mappings.
        builder.Property(definition => definition.SignatureSkillCardId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<CardDefinition>()
            .WithMany()
            .HasForeignKey(definition => definition.SignatureSkillCardId)
            .OnDelete(DeleteBehavior.Restrict);

        // No index is declared on this FK: DATABASE.md §4 lists no
        // PetDefinition index, and ForeignKeyIndexConvention is removed by
        // GameDbContext, so no implicit
        // IX_PetDefinition_SignatureSkillCardId is scaffolded.
    }
}
