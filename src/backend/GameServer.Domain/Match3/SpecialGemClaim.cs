namespace GameServer.Domain.Match3;

/// <summary>
/// One Special Gem creation a detection pass requires, before collision
/// resolution decides whether it is actually committed
/// (<c>MATCH3_RULES.md</c> §5.5.1, §5.5.4).
///
/// This is <b>Transient Resolution State</b> (<c>GAME_STATE.md</c> §3): it is
/// pass-local bookkeeping for the §4.1 step 2 creation step, is never a
/// <c>BattleState</c> field, is never serialized or delivered, and does not
/// survive the pass that built it. It is not a second board representation — it
/// names cells, it does not hold them.
/// </summary>
/// <param name="CellIndex">
/// The cell the creation claims (<c>MATCH3_RULES.md</c> §5.5.3). Always a cell
/// that was part of the shape that created it (§5.5.3 item 5).
/// </param>
/// <param name="SpecialGem">
/// The Special Gem to create there, including a Line Clear Gem's orientation
/// (§5.2 item 3).
/// </param>
/// <param name="ShapeIndex">
/// The §3.2 position of the shape that requires this creation — the primary key
/// of the collision order (<c>MATCH3_RULES.md</c> §5.5.1 items 2–3, §5.5.4
/// item 3).
/// </param>
/// <param name="IntraShapeOrder">
/// The creation's position within its own shape's §5.5.1 item 4 sequence —
/// <c>0</c> for the L/T pattern's Area Gem, <c>1</c> for the horizontal arm's
/// gem, <c>2</c> for the vertical arm's gem, and <c>0</c> for the single creation
/// of a straight shape. The secondary key of the collision order.
/// </param>
/// <param name="Source">
/// Which of the shape's creation sources produced this claim. Used for
/// diagnostics and for asserting the documented intra-shape sequence in tests.
/// </param>
/// <param name="GemType">
/// The Gem type the created Special Gem carries — the type of the Gem removed
/// from that cell in this pass (<c>MATCH3_RULES.md</c> §5.5.4 item 4,
/// <c>GAME_STATE.md</c> §2.1.3 item 2).
/// </param>
public readonly record struct SpecialGemClaim(
    int CellIndex,
    SpecialGem SpecialGem,
    int ShapeIndex,
    int IntraShapeOrder,
    CreationSource Source,
    GemType GemType);

/// <summary>
/// The creation source of a Special Gem claim — the intra-shape ordering of
/// <c>MATCH3_RULES.md</c> §5.5.1 item 4.
///
/// This is an ordering of <b>sources</b>, never a ranking of Special Gem types.
/// No Special Gem type outranks another and a global ranking such as
/// <c>Area &gt; Line &gt; Burst</c> does not exist and may not be introduced
/// (§5.5.1 item 4 item 1, §5.8.4 item 4).
/// </summary>
public enum CreationSource
{
    /// <summary>
    /// The L/T pattern's own Area Gem at the intersection — first in the
    /// intra-shape sequence (<c>MATCH3_RULES.md</c> §5.5.1 item 4, §5.4 item 2).
    /// </summary>
    LtPattern = 0,

    /// <summary>
    /// The horizontal arm's gem — second (<c>MATCH3_RULES.md</c> §5.5.1 item 4
    /// item 3: §3.2 item 1's horizontal-before-vertical key applied one level
    /// down).
    /// </summary>
    HorizontalArm = 1,

    /// <summary>The vertical arm's gem — third.</summary>
    VerticalArm = 2,

    /// <summary>
    /// The single creation of a straight Match 4 / Match 5 shape, which has no
    /// intra-shape sequence (<c>MATCH3_RULES.md</c> §5.2 item 1, §5.3 item 1).
    /// </summary>
    StraightLine = 3,
}