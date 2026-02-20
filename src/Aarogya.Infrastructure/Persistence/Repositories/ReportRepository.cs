using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class ReportRepository(AarogyaDbContext dbContext)
  : Repository<Report>(dbContext), IReportRepository
{
  public Task<IReadOnlyList<Report>> ListByPatientAsync(Guid patientId, CancellationToken cancellationToken = default)
    => ListAsync(new ReportsByPatientSpecification(patientId), cancellationToken);

  public Task<Report?> GetByReportNumberAsync(string reportNumber, CancellationToken cancellationToken = default)
    => FirstOrDefaultAsync(new ReportByNumberSpecification(reportNumber), cancellationToken);

  public Task<Report?> GetByFileStorageKeyAsync(string fileStorageKey, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(fileStorageKey);

    return dbContext.Reports
      .FirstOrDefaultAsync(x => x.FileStorageKey == fileStorageKey, cancellationToken);
  }
}
