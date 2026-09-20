using Microsoft.AspNetCore.Mvc;

namespace GameServer.Api.Controllers;

public record DiscordAuthRequest(string Code);
public record DiscordAuthResponse(string SessionToken, string PlayerId);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("discord")]
    public IActionResult AuthenticateDiscord([FromBody] DiscordAuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { error = "INVALID_CODE", message = "Discord authorization code is required." });
        }

        // Architectural boundary for Discord OAuth token exchange (ADR-007)
        // Returns a local development session token for integration smoke testing.
        return Ok(new DiscordAuthResponse(
            SessionToken: $"session_{Guid.NewGuid():N}",
            PlayerId: "player_dev"));
    }
}
