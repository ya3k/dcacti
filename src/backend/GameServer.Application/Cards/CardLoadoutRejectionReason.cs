namespace GameServer.Application.Cards;

/// <summary>
/// The reasons a submitted <c>cardLoadout</c> is rejected
/// (<c>CARD_RULES.md</c> §1; <c>API_CONTRACTS.md</c> §3).
///
/// Every member maps to the <b>single</b> documented invalid-loadout error
/// code — <c>INVALID_LOADOUT</c> (<c>API_CONTRACTS.md</c> §3, §6), the same
/// code the endpoint already uses for <c>relicLoadout</c>. The distinction
/// exists so the server can log and diagnose a rejection without widening the
/// wire contract; it is <b>not</b> a set of new client-facing error codes, and
/// no API response may expose a distinct code per member.
///
/// The members follow the documented deterministic validation order —
/// count, ownership, category, copy limit (<c>API_CONTRACTS.md</c> §3) — so a
/// rejection names the <b>first</b> rule the selection violated.
/// </summary>
public enum CardLoadoutRejectionReason
{
    /// <summary>
    /// The selection was accepted; no rejection occurred. Present so a
    /// successful result needs no nullable reason.
    /// </summary>
    None = 0,

    /// <summary>
    /// The selection did not contain exactly 3 submitted Basic Cards
    /// (<c>CARD_RULES.md</c> §1; <c>API_CONTRACTS.md</c> §3 step 1).
    ///
    /// The count is of <b>submitted entries</b>: a repeated definition is a
    /// legitimate entry and is never collapsed
    /// (<c>CARD_RULES.md</c> §1 item 1), so a 3-entry loadout that names one
    /// definition three times is in count and is judged by the copy limit, not
    /// here.
    /// </summary>
    CountInvalid = 1,

    /// <summary>
    /// A submitted definition is not unlocked by the requesting Player
    /// (<c>CARD_RULES.md</c> §1; <c>API_CONTRACTS.md</c> §3 step 2) — either it
    /// does not exist, or no <c>PlayerUnlockedCard</c> row relates it to this
    /// Player. The server establishes ownership from persistence, never from a
    /// client-supplied claim (<c>GAME_RULES.md</c> §18, ADR-001).
    /// </summary>
    NotUnlocked = 2,

    /// <summary>
    /// A submitted definition is unlocked but is not
    /// <c>Category = Basic</c> (<c>CARD_RULES.md</c> §1;
    /// <c>API_CONTRACTS.md</c> §3 step 3). A <c>PetSkill</c> Card is the
    /// active Pet's Signature Skill and can never satisfy one of the three
    /// submitted Basic slots (<c>CARD_RULES.md</c> §1 item 4).
    /// </summary>
    NotBasic = 3,

    /// <summary>
    /// A submitted definition appears more times than its
    /// <c>LoadoutCopyLimit</c> permits (<c>CARD_RULES.md</c> §1 item 1;
    /// <c>API_CONTRACTS.md</c> §3 step 4):
    /// <c>occurrence count &gt; LoadoutCopyLimit</c>.
    ///
    /// A definition whose limit cannot be satisfied is rejected rather than
    /// silently accepted: no default limit exists or is substituted
    /// (<c>CARD_RULES.md</c> §1 item 2), and a limit below 1 can never be
    /// satisfied by any submission.
    /// </summary>
    CopyLimitExceeded = 4,

    /// <summary>
    /// The active Pet's derived Signature Skill Card could not be resolved, so
    /// the documented 4-card composition cannot be produced
    /// (<c>CARD_RULES.md</c> §1: "3 Basic Cards + 1 Pet Skill Card";
    /// <c>API_CONTRACTS.md</c> §3).
    ///
    /// This is a <b>definition-data</b> fault, not a client-input fault: the
    /// Skill is derived from <c>PetDefinition.SignatureSkillCardId</c> and is
    /// never submitted, so a client cannot correct it by changing
    /// <c>cardLoadout</c>. It is reported through the same
    /// <c>INVALID_LOADOUT</c> outcome because no other documented code exists
    /// for an incompletable composition, and a battle must not be created with
    /// fewer than its 4 documented Cards.
    /// </summary>
    SignatureSkillUnavailable = 5,
}
