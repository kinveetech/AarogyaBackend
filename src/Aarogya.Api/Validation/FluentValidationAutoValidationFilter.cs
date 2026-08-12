using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Aarogya.Api.Validation;

/// <summary>
/// Runs registered FluentValidation validators against action arguments and records
/// failures in model state, replacing the discontinued FluentValidation.AspNetCore
/// auto-validation package.
/// </summary>
internal sealed class FluentValidationAutoValidationFilter : IAsyncActionFilter, IOrderedFilter
{
  // Must run before ModelStateInvalidFilter (order -2000) so failures reach model
  // state ahead of the [ApiController] automatic 400 short-circuit.
  public int Order => -2500;

  public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
  {
    var services = context.HttpContext.RequestServices;
    var cancellationToken = context.HttpContext.RequestAborted;

    foreach (var argument in context.ActionArguments.Values)
    {
      if (argument is null)
      {
        continue;
      }

      var argumentType = argument.GetType();
      if (argumentType.IsValueType || argumentType == typeof(string))
      {
        continue;
      }

      if (services.GetService(typeof(IValidator<>).MakeGenericType(argumentType)) is not IValidator validator)
      {
        continue;
      }

      var result = await validator.ValidateAsync(new ValidationContext<object>(argument), cancellationToken);
      foreach (var failure in result.Errors)
      {
        context.ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
      }
    }

    await next();
  }
}
