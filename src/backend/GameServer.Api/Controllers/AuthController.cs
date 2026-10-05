using System.Text.RegularExpressions;
using GameServer.Api.Authentication;
using GameServer.Application.Accounts;
using GameServer.Application.Players;
using GameServer.Domain.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Api.Controllers;

public record RegisterRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);
public record AuthResponse(string SessionToken, string PlayerId, string Username);

/// <summary>
/// Web authentication boundary (ADR-020, API_CONTRACTS.md §2).
/// Exposes public endpoints for registration and login using username & password.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_]{3,32}$", RegexOptions.Compiled);

    private readonly IAccountRepository _accountRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly PlayerStarterGrantFactory _starterGrants;
    private readonly ApplicationSessionTokenService _sessions;

    public AuthController(
        IAccountRepository accountRepository,
        IPlayerRepository playerRepository,
        IPasswordHasher passwordHasher,
        PlayerStarterGrantFactory starterGrants,
        ApplicationSessionTokenService sessions)
    {
        _accountRepository = accountRepository;
        _playerRepository = playerRepository;
        _passwordHasher = passwordHasher;
        _starterGrants = starterGrants;
        _sessions = sessions;
    }

    /// <summary>
    /// Registers a new web user account, creates Player with starter kit,
    /// and issues an authenticated session token (API_CONTRACTS.md §2.1).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { error = "INVALID_INPUT", message = "Username is required." });
        }

        var trimmedUsername = request.Username.Trim();
        if (!UsernameRegex.IsMatch(trimmedUsername))
        {
            return BadRequest(new
            {
                error = "INVALID_INPUT",
                message = "Username must be between 3 and 32 alphanumeric or underscore characters."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < Account.MinPasswordLength)
        {
            return BadRequest(new
            {
                error = "INVALID_INPUT",
                message = $"Password must be at least {Account.MinPasswordLength} characters."
            });
        }

        var existing = await _accountRepository.GetByUsernameAsync(trimmedUsername, cancellationToken);
        if (existing is not null)
        {
            return Conflict(new
            {
                error = "USERNAME_ALREADY_EXISTS",
                message = "Username is already registered."
            });
        }

        var account = new Account
        {
            AccountId = Guid.NewGuid(),
            Username = trimmedUsername,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _accountRepository.CreateAsync(account, cancellationToken);

        var player = await _playerRepository.GetOrCreateForAccountAsync(
            account.AccountId,
            composeStarterGrant: cancellation => _starterGrants.CreateAsync(
                DateTimeOffset.UtcNow,
                cancellation),
            cancellationToken);

        var token = _sessions.Issue(player.PlayerId, DateTimeOffset.UtcNow);

        return Ok(new AuthResponse(
            SessionToken: token,
            PlayerId: player.PlayerId,
            Username: account.Username));
    }

    /// <summary>
    /// Authenticates with username and password, issuing an application session token
    /// (API_CONTRACTS.md §2.2).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "INVALID_INPUT", message = "Username and password are required." });
        }

        var account = await _accountRepository.GetByUsernameAsync(request.Username, cancellationToken);
        if (account is null)
        {
            return Unauthorized(new { error = "INVALID_CREDENTIALS", message = "Invalid username or password." });
        }

        if (!_passwordHasher.VerifyPassword(request.Password, account.PasswordHash))
        {
            return Unauthorized(new { error = "INVALID_CREDENTIALS", message = "Invalid username or password." });
        }

        var player = await _playerRepository.GetByAccountIdAsync(account.AccountId, cancellationToken);
        if (player is null)
        {
            player = await _playerRepository.GetOrCreateForAccountAsync(
                account.AccountId,
                composeStarterGrant: cancellation => _starterGrants.CreateAsync(
                    DateTimeOffset.UtcNow,
                    cancellation),
                cancellationToken);
        }

        var token = _sessions.Issue(player.PlayerId, DateTimeOffset.UtcNow);

        return Ok(new AuthResponse(
            SessionToken: token,
            PlayerId: player.PlayerId,
            Username: account.Username));
    }
}
