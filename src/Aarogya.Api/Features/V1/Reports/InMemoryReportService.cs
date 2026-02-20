using System.Collections.Concurrent;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class InMemoryReportService : IReportService
{
  private readonly ConcurrentDictionary<string, List<ReportSummaryResponse>> _reportsByUser = new(StringComparer.Ordinal);

  public Task<IReadOnlyList<ReportSummaryResponse>> GetForUserAsync(string userSub, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var reports = _reportsByUser.GetOrAdd(userSub, static _ => []);

    lock (reports)
    {
      return Task.FromResult<IReadOnlyList<ReportSummaryResponse>>(reports.OrderByDescending(report => report.CreatedAt).ToArray());
    }
  }

  public Task<ReportSummaryResponse> AddForUserAsync(string userSub, CreateReportRequest request, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var reports = _reportsByUser.GetOrAdd(userSub, static _ => []);
    var created = new ReportSummaryResponse(Guid.NewGuid(), request.Title.Trim(), "uploaded", DateTimeOffset.UtcNow);

    lock (reports)
    {
      reports.Add(created);
    }

    return Task.FromResult(created);
  }
}
