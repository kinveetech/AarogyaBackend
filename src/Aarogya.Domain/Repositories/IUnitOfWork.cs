namespace Aarogya.Domain.Repositories;

public interface IUnitOfWork
{
  public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

  public Task BeginTransactionAsync(CancellationToken cancellationToken = default);

  public Task CommitTransactionAsync(CancellationToken cancellationToken = default);

  public Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
