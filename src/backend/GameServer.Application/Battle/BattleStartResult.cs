namespace GameServer.Application.Battle;

/// <summary>
/// The outcome of a <c>POST /api/battle/start</c> request
/// (<c>API_CONTRACTS.md</c> §3, §6).
///
/// The members are exactly the documented <c>§6</c> error codes plus the one
/// success outcome. No other code is introduced, and the rejection reasons the
/// domain services report internally — the Card and Relic rejection enums —
/// are deliberately <b>not</b> surfaceable here: <c>API_CONTRACTS.md</c> §3
/// maps every invalid Card or Relic loadout to the single documented
/// <c>INVALID_LOADOUT</c> code, so a member per internal reason would widen the
/// wire contract the endpoint already owns.
/// </summary>
public enum BattleStartOutcome
{
    /// <summary>
    /// The battle was created. <see cref="BattleStartResult.BattleId"/> carries
    /// its identity.
    /// </summary>
    Started = 0,

    /// <summary>
    /// The request named no Pet, or named one that does not exist, or named one
    /// that is not owned by the requesting Player
    /// (<c>API_CONTRACTS.md</c> §3: "petId must be owned by the player";
    /// <c>PET_RULES.md</c> §2). The endpoint's documented code is
    /// <c>PET_NOT_OWNED</c>.
    ///
    /// The three conditions are one outcome because they are one client
    /// remedy and because distinguishing them to the caller would disclose
    /// whether another Player's Pet instance exists — §3 states the ownership
    /// requirement, not a per-case error surface.
    /// </summary>
    PetNotOwned = 1,

    /// <summary>
    /// The request named no Boss, or named one that is not one of the
    /// content-defined MVP Bosses (<c>API_CONTRACTS.md</c> §3: "bossId must be
    /// a valid MVP Boss — <c>BOSS_RULES.md</c> §6").
    /// </summary>
    BossNotFound = 2,

    /// <summary>
    /// The submitted <c>cardLoadout</c> or <c>relicLoadout</c> failed its
    /// documented validation (<c>API_CONTRACTS.md</c> §3). Both loadouts share
    /// the one documented code, <c>INVALID_LOADOUT</c>, exactly as §3 states
    /// for the Card case ("the same documented code this endpoint uses for
    /// <c>relicLoadout</c>").
    ///
    /// A rejection writes no battle state: nothing is created, and no partial
    /// <c>BattleState</c> exists after it (§3: "A rejected request equips
    /// nothing and writes no battle state").
    /// </summary>
    InvalidLoadout = 3,
}

/// <summary>
/// The result of resolving one <c>POST /api/battle/start</c> request
/// (<c>API_CONTRACTS.md</c> §3).
///
/// <b>It is the orchestration result, not authoritative state.</b> On success
/// the created battle — including its authoritative <c>BattleState</c> and the
/// loadout snapshots it carries — is recorded by
/// <see cref="BattleStateService"/>, which is the documented owner; this value
/// carries only the identity the endpoint returns and the outcome that decides
/// the HTTP response. It deliberately holds no <c>BattleState</c> copy, because
/// a second copy of the state would be the parallel representation
/// <c>GAME_STATE.md</c> §0 item 5 forbids.
/// </summary>
public readonly record struct BattleStartResult
{
    private BattleStartResult(
        BattleStartOutcome outcome,
        string? battleId,
        string? detail)
    {
        Outcome = outcome;
        BattleId = battleId;
        Detail = detail;
    }

    /// <summary>What the request resolved to.</summary>
    public BattleStartOutcome Outcome { get; }

    /// <summary>
    /// The created battle's identity, or <c>null</c> when the request was
    /// rejected. It is the value the <c>§3</c> response returns as
    /// <c>battleId</c> and the one the client joins the battle group with
    /// (<c>SIGNALR_PROTOCOL.md</c> §1 items 1–2).
    /// </summary>
    public string? BattleId { get; }

    /// <summary>
    /// A human-readable detail for the §6 error envelope's <c>message</c>
    /// member, or <c>null</c> on success. It never carries an internal
    /// rejection reason as a code — the codes are
    /// <see cref="BattleStartOutcome"/>'s, and <c>§6</c> types <c>message</c>
    /// as human-readable detail, not as a second machine-readable code.
    /// </summary>
    public string? Detail { get; }

    /// <summary>Whether a battle was created.</summary>
    public bool Succeeded => Outcome == BattleStartOutcome.Started;

    /// <summary>A successful result carrying the created battle's identity.</summary>
    /// <param name="battleId">The created battle's id.</param>
    public static BattleStartResult Started(string battleId) =>
        new(BattleStartOutcome.Started, battleId, null);

    /// <summary>A rejected result carrying the documented outcome and no battle.</summary>
    /// <param name="outcome">The documented rejection.</param>
    /// <param name="detail">Human-readable detail for the §6 envelope.</param>
    public static BattleStartResult Rejected(BattleStartOutcome outcome, string detail) =>
        new(outcome, null, detail);
}
