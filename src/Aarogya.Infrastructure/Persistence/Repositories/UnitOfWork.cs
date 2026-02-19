using Aarogya.Domain.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class UnitOfWork(AarogyaDbContext dbContext) : IUnitOfWork
{
  private IDbContextTransaction? _currentTransaction;

  public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    => dbContext.SaveChangesAsync(cancellationToken);

  public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
  {
    if (_currentTransaction is not null)
    {
      return;
    }

    _currentTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
  }

  public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
  {
    if (_currentTransaction is null)
    {
      return;
    }

    await _currentTransaction.CommitAsync(cancellationToken);
    await _currentTransaction.DisposeAsync();
    _currentTransaction = null;
  }

  public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
  {
    if (_currentTransaction is null)
    {
      return;
    }

    await _currentTransaction.RollbackAsync(cancellationToken);
    await _currentTransaction.DisposeAsync();
    _currentTransaction = null;
  }
}
