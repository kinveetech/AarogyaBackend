using Aarogya.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal static class SpecificationEvaluator
{
  public static IQueryable<T> ApplySpecification<T>(
    IQueryable<T> query,
    ISpecification<T> specification)
    where T : class
  {
    ArgumentNullException.ThrowIfNull(query);
    ArgumentNullException.ThrowIfNull(specification);

    if (specification.Criteria is not null)
    {
      query = query.Where(specification.Criteria);
    }

    query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));

    if (specification.OrderBy is not null)
    {
      query = query.OrderBy(specification.OrderBy);
    }
    else if (specification.OrderByDescending is not null)
    {
      query = query.OrderByDescending(specification.OrderByDescending);
    }

    if (specification.Skip.HasValue)
    {
      query = query.Skip(specification.Skip.Value);
    }

    if (specification.Take.HasValue)
    {
      query = query.Take(specification.Take.Value);
    }

    if (specification.AsNoTracking)
    {
      query = query.AsNoTracking();
    }

    return query;
  }
}
