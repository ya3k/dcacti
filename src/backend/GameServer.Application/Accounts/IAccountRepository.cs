using GameServer.Domain.Accounts;

namespace GameServer.Application.Accounts;

/// <summary>
/// Account persistence boundary for Web authentication (ADR-020).
/// </summary>
public interface IAccountRepository
{
    Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<Account> CreateAsync(Account account, CancellationToken cancellationToken = default);
}
