using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Aarogya.Api.Features.V1.Consents;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record UpsertConsentRequest(
  [property: JsonRequired] bool IsGranted,
  [property: MaxLength(80)] string Source = "api");

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record ConsentRecordResponse(
  string Purpose,
  bool IsGranted,
  DateTimeOffset OccurredAt,
  string Source);
