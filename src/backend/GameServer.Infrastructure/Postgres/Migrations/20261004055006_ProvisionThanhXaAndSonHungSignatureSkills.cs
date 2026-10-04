using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <summary>
    /// Provisions the two newly content-defined MVP Pet Signature Skill Cards and
    /// their two owning Pets: <c>card-venomous-bloom</c>, <c>card-earthshaker</c>,
    /// <c>pet-thanh-xa</c>, and <c>pet-son-hung</c> (<c>DATABASE.md</c> §1, §5
    /// item 4; <c>CARD_RULES.md</c> §4.1; <c>PET_RULES.md</c> §8;
    /// <c>PASSIVE_RULES.md</c> §8).
    ///
    /// <b>Data only — no schema operation.</b> The two tables, their primary keys,
    /// the <c>PetDefinition.SignatureSkillCardId</c> foreign key, and every column
    /// constraint already exist (<c>20260924163011_AddPetPersistence</c>,
    /// <c>20260925150903_AddCardPersistence</c>). This migration adds no column,
    /// table, index, or constraint: its <see cref="Up(MigrationBuilder)"/> contains
    /// exactly four <c>InsertData</c> calls and its
    /// <see cref="Down(MigrationBuilder)"/> the mirror four <c>DeleteData</c> calls.
    /// EF generated no schema difference for it — it scaffolded an empty Up/Down and
    /// <c>GameDbContextModelSnapshot.cs</c> is unchanged — which is what makes it a
    /// pure data migration.
    ///
    /// <b>Why these four rows are provisionable now.</b> TASK-085 deferred Thanh Xà
    /// and Sơn Hùng because <c>PetDefinition.SignatureSkillCardId</c> is a required
    /// FK to <c>CardDefinition</c> (<c>DATABASE.md</c> §1/§2) and
    /// <c>CARD_RULES.md</c> §4.1 then authored no content for either Signature
    /// Skill, so no valid, non-invented Card row could exist and therefore no Pet
    /// row either (<c>DATABASE.md</c> §5 item 4 rule (a)). TASK-167 has since
    /// authored both Skills (§4.1, <c>PET_RULES.md</c> §8), so both FK targets now
    /// exist and <c>DATABASE.md</c> §5 item 4 records the rows as
    /// <b>provisioned-later</b>, not content-blocked. This is that separate
    /// provisioning task.
    ///
    /// <b>Migration-level <c>InsertData</c> is the decided mechanism</b>
    /// (<c>DATABASE.md</c> §5 item 4; the TASK-052/TASK-053
    /// <c>ProvisionBossDefinitions</c> and TASK-085
    /// <c>ProvisionPetCardRelicContentDefinitions</c> precedents). <c>HasData</c>/
    /// model seed data, a startup loader, a JSON content pipeline, an external
    /// content service, an API-based provisioning path, and
    /// <c>INSERT ... ON CONFLICT</c>/upsert are all forbidden and are not used.
    /// Idempotency comes solely from EF's migration history: the migration runs once
    /// per database, and each table's primary key is the database-level guarantee
    /// against duplicates.
    ///
    /// <b>Row content is transcribed, never computed or invented</b>
    /// (<c>DATABASE.md</c> §5 item 4 rule (b)); each comment below cites the sentence
    /// it came from.
    ///
    /// <b>Insertion order follows the foreign key.</b>
    /// <c>PetDefinition.SignatureSkillCardId</c> is an FK to
    /// <c>CardDefinition.CardDefinitionId</c> with the existing
    /// <c>OnDelete(DeleteBehavior.Restrict)</c> behavior (<c>DATABASE.md</c> §2), so
    /// the two Card rows are inserted before the two Pet rows that reference them,
    /// and <see cref="Down(MigrationBuilder)"/> removes the Pet rows before the Card
    /// rows.
    ///
    /// <b>Element is not a stored member of either Card row.</b> Venomous Bloom's
    /// damage Element (Mộc) and Burn Element (Hỏa), and Earthshaker's damage Element
    /// (Thổ), are content owned by <c>CARD_RULES.md</c> §4.1, stated there in prose,
    /// exactly as <c>card-inferno</c>'s Hỏa Element already is.
    /// <c>ELEMENT_RULES.md</c> §1.1/§5 establish that a Skill carries an Element and
    /// that an Effect (Burn) carries its own, so no <c>Element</c>,
    /// <c>DamageElement</c>, or <c>BurnElement</c> member exists on
    /// <c>EffectDefinition</c> and no Element column exists on <c>CardDefinition</c>
    /// (<c>DATABASE.md</c> §1, §3 — the closed member set is unchanged).
    ///
    /// <b>Nothing here executes.</b> The stored <c>EffectDefinition</c> payloads are
    /// <b>definition data only</b> (read, never executed, by this contract —
    /// <c>DATABASE.md</c> §1 item 5): no Card is cast, no damage is computed, no Burn
    /// is applied, no status effect is created, and no Crit roll exists.
    /// </summary>
    public partial class ProvisionThanhXaAndSonHungSignatureSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // CardDefinition — DATABASE.md §1; CARD_RULES.md §1, §4.1.
            //
            // Inserted FIRST: PetDefinition.SignatureSkillCardId is an FK to this
            // table (DATABASE.md §2), so these two rows must exist before the two
            // Pet rows below reference them.
            //
            // Category is encoded as the Domain CardCategory enum's numeric value
            // — the same HasConversion<int>() mapping
            // CardDefinitionConfiguration applies (Basic = 0, PetSkill = 1).
            // Per CARD_RULES.md §1 a Pet Skill Card is PetSkill, and
            // LoadoutCopyLimit = 1 per §1 item 5, which fixes that value for
            // EVERY CardDefinition the document defines. §1 item 4 records that
            // the value is persisted for a PetSkill row only because the column
            // is required and non-nullable; it is never read for the submitted
            // Basic loadout, so it introduces no additional gameplay behavior.
            //
            // The EffectDefinition payloads are written as inline JSON literals in
            // the ARRAY shape DATABASE.md §1 items 1–9 defines (TASK-111 D-1/D-1a/
            // D-1b) — the same shape the six existing rows were re-encoded into by
            // 20261002090000_EncodeCardEffectDefinitionArrayRows (TASK-112). A
            // one-effect Card stores a one-element array; a two-effect Card stores
            // one element per effect. Every member is drawn from the EXISTING
            // closed sets: effectType ∈ {Heal, Shield, Power, Damage, Burn, Crit},
            // valueType ∈ {Flat, PercentMaxHp, PercentagePoints, Undetermined}.
            // ---------------------------------------------------------------

            // Venomous Bloom — CARD_RULES.md §4.1 (Thanh Xà's Signature Skill):
            //   "Thanh Xà — Venomous Bloom
            //      Cost:   80 Power
            //      Effect: Deal 80 Mộc (Wood) damage (flat base value, entering
            //              the Damage Pipeline as the Card/Skill base value —
            //              COMBAT_RULES.md §3 step 1); apply Burn 25 damage per
            //              tick for 2 Turns, Element Hỏa (Fire)"
            //
            // §4.1 adds: "Venomous Bloom's two effects therefore carry DIFFERENT
            // Elements: its damage is Mộc (the Pet's own Element, ELEMENT_RULES.md
            // §6) while its Burn is Hỏa — the Element COMBAT_RULES.md §5.1 assigns
            // to Burn generally. The Skill's Mộc Element is NOT inherited by its
            // Burn effect."
            //
            // So this row stores a TWO-ELEMENT array, in §4.1's effect order
            // (damage, then Burn); per DATABASE.md §1 item 3 that order is a
            // storage sequence only and carries no gameplay meaning:
            //   damage → Damage, Flat, 80    (§4.1's stated flat base value)
            //   burn   → Burn,   Flat, 25, duration 2
            //                                (§4.1: 25 damage per tick, 2 Turns;
            //                                 `duration` is the Burn element's
            //                                 REQUIRED extra member —
            //                                 DATABASE.md §1 item 1 / §3, and
            //                                 omitting it is a contract violation
            //                                 the strict reader rejects loudly,
            //                                 §1 item 6)
            //
            // NO Element member is stored: neither the damage effect's Mộc nor the
            // Burn effect's Hỏa is a member of EffectDefinition (see the class
            // summary). The two effects are recorded as carrying distinct Elements
            // by the prose owner, not by a storage member that does not exist.
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "card-venomous-bloom", "Venomous Bloom", 1, 80, 1, "[{\"effectType\":\"Damage\",\"valueType\":\"Flat\",\"value\":80},{\"effectType\":\"Burn\",\"valueType\":\"Flat\",\"value\":25,\"duration\":2}]" });

            // Earthshaker — CARD_RULES.md §4.1 (Sơn Hùng's Signature Skill):
            //   "Sơn Hùng — Earthshaker
            //      Cost:   100 Power
            //      Effect: Deal 150 Thổ (Earth) damage (flat base value, entering
            //              the Damage Pipeline as the Card/Skill base value —
            //              COMBAT_RULES.md §3 step 1)"
            //
            // §4.1 states exactly ONE effect for this Skill and NO Burn ("§4.1
            // states no Burn for this Skill"), so this row stores a ONE-ELEMENT
            // array (TASK-111 D-1b) with no element 2 and therefore no `duration`
            // member anywhere:
            //   damage → Damage, Flat, 150   (§4.1's stated flat base value)
            //
            // Its single effect's Element is Thổ (§4.1; ELEMENT_RULES.md §6) and,
            // as above, is not a stored member.
            migrationBuilder.InsertData(
                table: "CardDefinition",
                columns: new[] { "CardDefinitionId", "Name", "Category", "PowerCost", "LoadoutCopyLimit", "EffectDefinition" },
                values: new object[] { "card-earthshaker", "Earthshaker", 1, 100, 1, "[{\"effectType\":\"Damage\",\"valueType\":\"Flat\",\"value\":150}]" });

            // ---------------------------------------------------------------
            // PetDefinition — DATABASE.md §1; PET_RULES.md §1, §8;
            // PASSIVE_RULES.md §8.
            //
            // Each row carries its Passive directly (PassiveId +
            // PassiveThreshold): DATABASE.md §1 names the field "PassiveDefinition
            // (threshold/effect reference)" and there is NO PassiveDefinition
            // table or entity — none is created here.
            //
            // Element is encoded as the Domain Element enum's numeric value (the
            // same conversion PetDefinitionConfiguration applies): Moc = 0,
            // Tho = 1, Thuy = 2, Hoa = 3, Kim = 4.
            //
            // Identity carries the display text with its diacritics verbatim
            // (PET_RULES.md §1 — PetDefinitionId is the technical identity,
            // Identity is the display text). A transliterated value would be an
            // invented value.
            // ---------------------------------------------------------------

            // Thanh Xà — PET_RULES.md §8: Element Mộc (0); Passive "Every 7
            // Matches → Restore 8% HP", threshold 7 (PASSIVE_RULES.md §8);
            // Signature Skill Venomous Bloom (CARD_RULES.md §4.1).
            // PassiveId per PASSIVE_RULES.md §8's derivation rule —
            // `passive-<ascii-kebab-case-name>` of the owning Pet's documented
            // name ("Thanh Xà" → passive-thanh-xa).
            migrationBuilder.InsertData(
                table: "PetDefinition",
                columns: new[] { "PetDefinitionId", "Identity", "Element", "PassiveId", "PassiveThreshold", "SignatureSkillCardId" },
                values: new object[] { "pet-thanh-xa", "Thanh Xà", 0, "passive-thanh-xa", 7, "card-venomous-bloom" });

            // Sơn Hùng — PET_RULES.md §8: Element Thổ (1); Passive "Every 5
            // Matches → Temp Defense", threshold 5 (PASSIVE_RULES.md §8);
            // Signature Skill Earthshaker (CARD_RULES.md §4.1).
            // PassiveId per the same §8 derivation rule ("Sơn Hùng" →
            // passive-son-hung).
            migrationBuilder.InsertData(
                table: "PetDefinition",
                columns: new[] { "PetDefinitionId", "Identity", "Element", "PassiveId", "PassiveThreshold", "SignatureSkillCardId" },
                values: new object[] { "pet-son-hung", "Sơn Hùng", 1, "passive-son-hung", 5, "card-earthshaker" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mirror of Up: remove exactly the four rows this migration inserted,
            // keyed by the same canonical primary-key values, and no other row (no
            // broad table delete). Every other provisioned row — the six Cards, three
            // Pets, and four Relics of TASK-085, and the three BossDefinitions of
            // TASK-053 — is left untouched.

            // ---------------------------------------------------------------
            // PetDefinition first: it holds the FK to CardDefinition, so it is
            // deleted before the Cards it references (DATABASE.md §2). The
            // Card/Pet relationship is Restrict, so the reverse order would be
            // rejected by the database.
            // ---------------------------------------------------------------

            migrationBuilder.DeleteData(
                table: "PetDefinition",
                keyColumn: "PetDefinitionId",
                keyValue: "pet-thanh-xa");

            migrationBuilder.DeleteData(
                table: "PetDefinition",
                keyColumn: "PetDefinitionId",
                keyValue: "pet-son-hung");

            // ---------------------------------------------------------------
            // CardDefinition — no Pet row references these any more.
            // ---------------------------------------------------------------

            migrationBuilder.DeleteData(
                table: "CardDefinition",
                keyColumn: "CardDefinitionId",
                keyValue: "card-venomous-bloom");

            migrationBuilder.DeleteData(
                table: "CardDefinition",
                keyColumn: "CardDefinitionId",
                keyValue: "card-earthshaker");
        }
    }
}
