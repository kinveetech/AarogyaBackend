using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Repositories;

public interface IReportRepository : IRepository<Report>
{
  public Task<IReadOnlyList<Report>> ListByPatientAsync(Guid patientId, CancellationToken cancellationToken = default);

  public Task<Report?> GetByReportNumberAsync(string reportNumber, CancellationToken cancellationToken = default);
}
