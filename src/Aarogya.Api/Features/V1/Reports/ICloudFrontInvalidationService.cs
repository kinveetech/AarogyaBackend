using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by DI and test mocking.")]
public interface ICloudFrontInvalidationService
{
  public Task InvalidateObjectAsync(string objectKey, CancellationToken cancellationToken = default);
}
