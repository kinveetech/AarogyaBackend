namespace Aarogya.Api.Features.V1.Reports;

internal sealed record ExtractedParameter(
  string Code,
  string Name,
  decimal? NumericValue,
  string? TextValue,
  string? Unit,
  string? ReferenceRange,
  bool? IsAbnormal,
  double Confidence);

internal sealed record StructuredExtractionResult(
  IReadOnlyList<ExtractedParameter> Parameters,
  string? Notes,
  double OverallConfidence,
  string ModelId);
