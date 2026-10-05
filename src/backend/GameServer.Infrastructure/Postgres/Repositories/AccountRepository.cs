using GameServer.Application.Accounts;
using GameServer.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Postgres.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private readonly GameDbContext _dbContext;

    public AccountRepository(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Username.ToLower() == normalized, cancellationToken);
    }

    public async Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);
    }

    public async Task<Account> CreateAsync(Account account, CancellationToken cancellationToken = default)
    {
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }
}
