using Aarogya.Domain.Specifications;

namespace Aarogya.Domain.Repositories;

public interface IRepository<T>
  where T : class
{
  public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

  public Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

  public Task<IReadOnlyList<T>> ListAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default);

  public Task<int> CountAsync(ISpecification<T>? specification = null, CancellationToken cancellationToken = default);

  public Task AddAsync(T entity, CancellationToken cancellationToken = default);

  public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

  public void Update(T entity);

  public void Delete(T entity);
}
