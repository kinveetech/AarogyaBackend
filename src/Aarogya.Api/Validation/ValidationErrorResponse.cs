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
        pair => ToReadOnlyErrorList(pair.Value!.Errors
          .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage)),
        StringComparer.OrdinalIgnoreCase);

    return new ValidationErrorResponse("Validation failed.", errors);
  }

  public static ValidationErrorResponse FromFailures(IEnumerable<ValidationFailure> failures)
  {
    var errors = failures
      .GroupBy(failure => string.IsNullOrWhiteSpace(failure.PropertyName) ? "request" : failure.PropertyName)
      .ToDictionary(
        group => group.Key,
        group => ToReadOnlyErrorList(group.Select(failure => failure.ErrorMessage)),
        StringComparer.OrdinalIgnoreCase);

    return new ValidationErrorResponse("Validation failed.", errors);
  }

  private static IReadOnlyList<string> ToReadOnlyErrorList(IEnumerable<string> messages)
    => messages
      .Distinct(StringComparer.Ordinal)
      .ToArray();
}
