using GameServer.Api.Authentication;
using GameServer.Application.Collection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Api.Controllers;

/// <summary>
/// The collection read boundary — the four endpoints of
/// <c>API_CONTRACTS.md</c> §5.
///
/// <code>
/// GET /api/pets               → [{ petId, identity, element, tier, star, level }]
/// GET /api/pets/{petId}       →  { petId, identity, element, tier, star, level }
///                                 | 404 { "error": "PET_NOT_FOUND" }
/// GET /api/cards              → [{ cardId, name, category }]
/// GET /api/relics             → [{ relicId, name }]
/// </code>
///
/// <b>It is a thin boundary</b> (<c>ARCHITECTURE.md</c> §2.1 item 4): it resolves
/// the authenticated caller's identity, delegates the whole read to
/// <see cref="CollectionQueryService"/>, and translates what comes back into the
/// documented response records. It contains no ownership rule of its own, no
/// element mapping, no tier or category spelling, and no ordering, paging, or
/// filtering — each of those belongs to the layer that owns it.
///
/// <b>Ownership comes solely from the authenticated session.</b> §5: "a caller
/// reads only their own collection, and no request member, query parameter, or
/// header selects a <c>playerId</c>." None of the four actions binds an identity
/// input at all — the only route value any of them accepts is <c>petId</c>, which
/// names a Pet rather than a Player — and the identity is read from the request
/// context the authentication boundary published (<see cref="AuthenticatedPlayer"/>,
/// <c>ADR-015</c> D3), exactly as <c>BattleController</c> does.
///
/// <b>It is read-only.</b> §5's endpoints are collection queries: they write
/// nothing, resolve no Swap, start no battle, and touch no gameplay system
/// (<c>GAME_RULES.md</c> §18, ADR-001). The server remains authoritative for
/// ownership and for every value reported.
///
/// <b>Responses are bare.</b> §5.5 defines no envelope and no pagination: a list
/// response is the array itself, and an empty collection is <c>200 []</c> — not
/// <c>204</c>, not <c>404</c>, and not a wrapper object.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public class CollectionController : ControllerBase
{
    /// <summary>
    /// The documented <c>§5.2</c> error code for a <c>petId</c> the caller cannot
    /// read — a Pet that does not exist, or one that is not theirs
    /// (<c>API_CONTRACTS.md</c> §5.2, §6).
    /// </summary>
    private const string PetNotFoundErrorCode = "PET_NOT_FOUND";

    private readonly CollectionQueryService _collection;

    public CollectionController(CollectionQueryService collection)
    {
        _collection = collection;
    }

    /// <summary>
    /// Lists the authenticated Player's owned Pets
    /// (<c>API_CONTRACTS.md</c> §5.1).
    ///
    /// <b>200.</b> A raw JSON array with no envelope; each element carries
    /// exactly <c>petId</c>, <c>identity</c>, <c>element</c>, <c>tier</c>,
    /// <c>star</c>, and <c>level</c>.
    ///
    /// <b>Empty collection.</b> §5.5: a Player owning nothing gets <c>200</c> with
    /// <c>[]</c> — the same status and the same bare array, not a different one.
    ///
    /// <b>No ordering, paging, or filtering.</b> §5.5 defines no ordering and
    /// states clients must not rely on any, and MVP has no pagination: none of
    /// <c>page</c>, <c>limit</c>, <c>cursor</c>, <c>sort</c>, <c>filter</c>, or
    /// <c>search</c> is accepted or invented, and the array is the full
    /// collection.
    ///
    /// <b>Unauthenticated (401).</b> The §6 envelope with <c>UNAUTHENTICATED</c>,
    /// produced by the class-level <c>[Authorize]</c> and the session pipeline
    /// before this method runs — one outcome for a missing, invalid, tampered, or
    /// expired session (<c>§2.8</c> "Failure behavior", <c>ADR-015</c> D5). The
    /// guard below covers the remaining case the pipeline allows through: a
    /// principal that carries no <c>player_id</c>, which identifies nobody and
    /// must therefore not be answered with a collection.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read with the request.</param>
    [HttpGet("pets")]
    public async Task<IActionResult> Pets(CancellationToken cancellationToken)
    {
        var playerId = ResolveRequestingPlayerId();

        if (playerId is null)
        {
            // API_CONTRACTS.md §5: ownership comes solely from the authenticated
            // session, and §2.8 gives an identity that identifies nobody the
            // documented unauthenticated response rather than a default Player
            // (GAME_RULES.md §18, ADR-001).
            return Unauthenticated("An authenticated session is required to read a collection.");
        }

        var pets = await _collection.ListPetsAsync(playerId, cancellationToken);

        return Ok(pets.Select(PetResponse.From));
    }

    /// <summary>
    /// Returns one owned Pet (<c>API_CONTRACTS.md</c> §5.2).
    ///
    /// <b>200.</b> The same bare object as one <c>/api/pets</c> element — no
    /// wrapper.
    ///
    /// <b>Not found (404).</b> The §6 envelope with <c>PET_NOT_FOUND</c>, for a
    /// <c>petId</c> that does not exist <b>or</b> that belongs to another Player.
    /// §5.2 makes those one answer deliberately, so the endpoint never discloses
    /// whether a Pet exists. A foreign <c>petId</c> is therefore <b>never</b>
    /// answered with <c>403</c>: a distinguishable authorization failure would be
    /// exactly the disclosure §5.2 forbids.
    ///
    /// <b>Unauthenticated (401).</b> As for the list above
    /// (<c>§2.8</c>, <c>ADR-015</c> D5).
    ///
    /// <b>The caller's identity comes from the session, never the request.</b>
    /// §5 permits no request member, query parameter, header, or body field to
    /// select, override, or stand in for it, so this action binds no identity
    /// input: its only value is the <c>petId</c> route segment.
    /// </summary>
    /// <param name="petId">
    /// The owned instance to read (<c>DATABASE.md</c> §1:
    /// <c>PetInstanceId</c>) — the same id <c>POST /api/battle/start</c> submits
    /// as <c>petId</c> (<c>§3</c>).
    /// </param>
    /// <param name="cancellationToken">Cancels the read with the request.</param>
    [HttpGet("pets/{petId}")]
    public async Task<IActionResult> Pet(string petId, CancellationToken cancellationToken)
    {
        var playerId = ResolveRequestingPlayerId();

        if (playerId is null)
        {
            // §5.2's 404 is reserved for an authenticated caller — it states
            // whether a Pet exists, which is an answer only a caller who owns a
            // collection may receive. An identity that identifies nobody gets the
            // documented unauthenticated response instead, so an authorization
            // failure is never mistakable for a missing Pet.
            return Unauthenticated("An authenticated session is required to read a Pet.");
        }

        if (string.IsNullOrWhiteSpace(petId))
        {
            // A route segment that names no Pet can only be the not-found answer.
            // It is not a distinguishable case: the same single outcome follows.
            return PetNotFound();
        }

        var pet = await _collection.GetOwnedPetAsync(petId, playerId, cancellationToken);

        if (pet is null)
        {
            // §5.2: the documented failure for a Pet that does not exist and for
            // one owned by another Player. The two are one answer by design, so
            // there is one return statement for both and no branch, header, or
            // body distinguishes them.
            return PetNotFound();
        }

        return Ok(PetResponse.From(pet));
    }

    /// <summary>
    /// Lists the Card definitions the authenticated Player has unlocked
    /// (<c>API_CONTRACTS.md</c> §5.3).
    ///
    /// <b>200.</b> A raw JSON array; each element carries exactly <c>cardId</c>,
    /// <c>name</c>, and <c>category</c>. There is no <c>unlocked</c> member —
    /// membership of the array <b>is</b> the unlocked state — and
    /// <c>playerId</c>, <c>powerCost</c>, <c>loadoutCopyLimit</c>, and
    /// <c>effectDefinition</c> are not exposed.
    ///
    /// <b>Empty collection.</b> A Player with no unlocks gets <c>200</c> with
    /// <c>[]</c> (§5.5).
    ///
    /// <b>Unauthenticated (401).</b> As for the Pet list above.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read with the request.</param>
    [HttpGet("cards")]
    public async Task<IActionResult> Cards(CancellationToken cancellationToken)
    {
        var playerId = ResolveRequestingPlayerId();

        if (playerId is null)
        {
            return Unauthenticated("An authenticated session is required to read a collection.");
        }

        var cards = await _collection.ListCardsAsync(playerId, cancellationToken);

        return Ok(cards.Select(CardResponse.From));
    }

    /// <summary>
    /// Lists the Relic instances the authenticated Player owns
    /// (<c>API_CONTRACTS.md</c> §5.4).
    ///
    /// <b>200.</b> A raw JSON array; each element carries exactly <c>relicId</c>
    /// and <c>name</c>, where <c>relicId</c> is the owned <b>instance</b>
    /// identity. <c>playerId</c>, <c>acquiredAt</c>, <c>definitionId</c>, and the
    /// definition's Trigger/Condition/Effect are not exposed.
    ///
    /// <b>Empty collection.</b> A Player owning no Relic gets <c>200</c> with
    /// <c>[]</c> (§5.5).
    ///
    /// <b>No equip state.</b> §5.6: which Relics are equipped is battle-scoped
    /// and unpersisted, so no member of this response reports it.
    ///
    /// <b>Unauthenticated (401).</b> As for the Pet list above.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read with the request.</param>
    [HttpGet("relics")]
    public async Task<IActionResult> Relics(CancellationToken cancellationToken)
    {
        var playerId = ResolveRequestingPlayerId();

        if (playerId is null)
        {
            return Unauthenticated("An authenticated session is required to read a collection.");
        }

        var relics = await _collection.ListRelicsAsync(playerId, cancellationToken);

        return Ok(relics.Select(RelicResponse.From));
    }

    /// <summary>
    /// The documented not-found response for a Pet the caller cannot read
    /// (<c>API_CONTRACTS.md</c> §5.2, §6):
    /// <c>{ "error": "PET_NOT_FOUND", "message": "human-readable detail" }</c>.
    ///
    /// It is one method so there is one spelling of the response: §5.2 makes the
    /// missing and the foreign case identical, and two construction sites would
    /// be two chances for them to drift into distinguishable bodies. The message
    /// is deliberately generic and names no Pet, no definition, and no owner.
    /// </summary>
    private NotFoundObjectResult PetNotFound() =>
        NotFound(new
        {
            error = PetNotFoundErrorCode,
            message = "The requested Pet was not found.",
        });

    /// <summary>
    /// The documented unauthenticated response (<c>API_CONTRACTS.md</c> §2.8
    /// "Failure behavior", §6) for a principal that carries no <c>player_id</c>.
    ///
    /// It is the §2.8 single code — never a collection-specific one, never a
    /// distinct "identity missing" outcome, and never <c>PET_NOT_FOUND</c>, which
    /// §5.2 reserves for an authenticated caller.
    /// </summary>
    private UnauthorizedObjectResult Unauthenticated(string message) =>
        Unauthorized(new
        {
            error = UnauthenticatedResponse.ErrorCode,
            message,
        });

    /// <summary>
    /// The resolved requesting Player, or <c>null</c> when the caller presents no
    /// authenticated identity.
    ///
    /// It reads the request-context item the authentication boundary publishes
    /// from the validated session's <c>player_id</c> claim
    /// (<see cref="AuthenticatedPlayer"/>, <c>API_CONTRACTS.md</c> §2.8,
    /// <c>ADR-015</c> D3) — the same server-derived identity
    /// <c>BattleController</c> reads, with no second mechanism and no fallback.
    /// </summary>
    private string? ResolveRequestingPlayerId() =>
        HttpContext.Items[AuthenticatedPlayer.RequestContextKey] as string;
}
