using System.Text.Json;

namespace GameServer.Api.Authentication;

/// <summary>
/// Writes the documented unauthenticated response (<c>API_CONTRACTS.md</c> §2.8
/// "Failure behavior", §4 note 6, §6).
///
/// <code>
/// missing session  |  invalid/tampered session  |  expired session
///         →  401  +  { "error": "UNAUTHENTICATED" }
/// </code>
///
/// All three conditions share this one response. No distinct code distinguishes
/// them and no token-validation detail is disclosed — a response that separated
/// "expired" from "bad signature" from "no token" would be a validation oracle
/// (D5).
///
/// It is used from two places, so the response is produced in one: the
/// authentication pipeline's challenge (which the framework raises exactly when
/// an <c>[Authorize]</c> endpoint is reached without a valid session), and the
/// authorization policy's own rejection.
/// </summary>
public static class UnauthenticatedResponse
{
    /// <summary>The one public error code for every unauthenticated outcome.</summary>
    public const string ErrorCode = "UNAUTHENTICATED";

    /// <summary>
    /// Writes <c>401</c> with the §6 error envelope.
    /// </summary>
    /// <remarks>
    /// The body is serialized explicitly rather than left to the framework's
    /// default challenge, which emits no body at all and would therefore not
    /// satisfy the documented response.
    /// </remarks>
    public static Task WriteAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            // The response is already on the wire; a challenge arriving this late
            // cannot add a body without corrupting it.
            return Task.CompletedTask;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json; charset=utf-8";

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(new { error = ErrorCode }));
    }
}
