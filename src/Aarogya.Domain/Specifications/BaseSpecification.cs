using System.Linq.Expressions;

namespace Aarogya.Domain.Specifications;

public abstract class BaseSpecification<T> : ISpecification<T>
{
  private readonly List<Expression<Func<T, object>>> _includes = [];

  protected BaseSpecification(Expression<Func<T, bool>>? criteria = null)
  {
    Criteria = criteria;
  }

  public Expression<Func<T, bool>>? Criteria { get; }

  public IReadOnlyCollection<Expression<Func<T, object>>> Includes => _includes;

  public Expression<Func<T, object>>? OrderBy { get; private set; }

  public Expression<Func<T, object>>? OrderByDescending { get; private set; }

  public int? Skip { get; private set; }

  public int? Take { get; private set; }

  public bool AsNoTracking { get; private set; }

  protected void AddInclude(Expression<Func<T, object>> includeExpression)
    => _includes.Add(includeExpression);

  protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
    => OrderBy = orderByExpression;

  protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescendingExpression)
    => OrderByDescending = orderByDescendingExpression;

  protected void ApplyPaging(int skip, int take)
  {
    Skip = skip;
    Take = take;
  }

  protected void ApplyAsNoTracking()
    => AsNoTracking = true;
}
