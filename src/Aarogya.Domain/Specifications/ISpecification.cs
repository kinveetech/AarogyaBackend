using System.Linq.Expressions;

namespace Aarogya.Domain.Specifications;

public interface ISpecification<T>
{
  public Expression<Func<T, bool>>? Criteria { get; }

  public IReadOnlyCollection<Expression<Func<T, object>>> Includes { get; }

  public Expression<Func<T, object>>? OrderBy { get; }

  public Expression<Func<T, object>>? OrderByDescending { get; }

  public int? Skip { get; }

  public int? Take { get; }

  public bool AsNoTracking { get; }
}
