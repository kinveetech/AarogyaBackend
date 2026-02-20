using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Repositories;

public interface IReportRepository : IRepository<Report>
{
  public const int HardDeleteBatchSize = 100;

  public Task<IReadOnlyList<Report>> ListByPatientAsync(Guid patientId, CancellationToken cancellationToken = default);

  public Task<Report?> GetByReportNumberAsync(string reportNumber, CancellationToken cancellationToken = default);

  public Task<Report?> GetByFileStorageKeyAsync(string fileStorageKey, CancellationToken cancellationToken = default);

  public Task<IReadOnlyList<Report>> ListDueForHardDeleteAsync(
    DateTimeOffset threshold,
    int maxItems = HardDeleteBatchSize,
    CancellationToken cancellationToken = default);
}
