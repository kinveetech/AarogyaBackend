using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal class Repository<T>(AarogyaDbContext dbContext) : IRepository<T>
  where T : class
{
  private readonly DbSet<T> _dbSet = dbContext.Set<T>();

  public virtual Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    => _dbSet.FindAsync([id], cancellationToken).AsTask();

  public virtual Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    => ApplySpecification(specification).FirstOrDefaultAsync(cancellationToken);

  public virtual async Task<IReadOnlyList<T>> ListAsync(
    ISpecification<T>? specification = null,
    CancellationToken cancellationToken = default)
    => specification is null
      ? await _dbSet.AsNoTracking().ToListAsync(cancellationToken)
      : await ApplySpecification(specification).ToListAsync(cancellationToken);

  public virtual Task<int> CountAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default)
    => specification is null
      ? _dbSet.CountAsync(cancellationToken)
      : ApplySpecification(specification).CountAsync(cancellationToken);

  public virtual Task AddAsync(T entity, CancellationToken cancellationToken = default)
    => _dbSet.AddAsync(entity, cancellationToken).AsTask();

  public virtual Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    => _dbSet.AddRangeAsync(entities, cancellationToken);

  public virtual void Update(T entity)
    => _dbSet.Update(entity);

  public virtual void Delete(T entity)
    => _dbSet.Remove(entity);

  protected IQueryable<T> ApplySpecification(ISpecification<T> specification)
    => SpecificationEvaluator.ApplySpecification(_dbSet.AsQueryable(), specification);
}
