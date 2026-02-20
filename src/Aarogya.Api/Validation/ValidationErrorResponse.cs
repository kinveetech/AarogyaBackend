using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Aarogya.Api.Validation;

internal sealed record ValidationErrorResponse(
  string Message,
  IReadOnlyDictionary<string, IReadOnlyList<string>> Errors)
{
  public static ValidationErrorResponse FromModelState(ModelStateDictionary modelState)
  {
    var errors = modelState
      .Where(pair => pair.Value?.Errors.Count > 0)
      .ToDictionary(
        pair => string.IsNullOrWhiteSpace(pair.Key) ? "request" : pair.Key,
        pair => (IReadOnlyList<string>)pair.Value!.Errors
          .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage)
          .Distinct(StringComparer.Ordinal)
          .ToArray(),
        StringComparer.OrdinalIgnoreCase);

    return new ValidationErrorResponse("Validation failed.", errors);
  }

  public static ValidationErrorResponse FromFailures(IEnumerable<ValidationFailure> failures)
  {
    var errors = failures
      .GroupBy(failure => string.IsNullOrWhiteSpace(failure.PropertyName) ? "request" : failure.PropertyName)
      .ToDictionary(
        group => group.Key,
        group => (IReadOnlyList<string>)group
          .Select(failure => failure.ErrorMessage)
          .Distinct(StringComparer.Ordinal)
          .ToArray(),
        StringComparer.OrdinalIgnoreCase);

    return new ValidationErrorResponse("Validation failed.", errors);
  }
}
