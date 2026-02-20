using FluentValidation;

namespace Aarogya.Api.Validation;

internal sealed class FluentValidationEndpointFilter(IServiceProvider services) : IEndpointFilter
{
  public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
  {
    var failures = new List<FluentValidation.Results.ValidationFailure>();

    foreach (var argument in context.Arguments)
    {
      if (argument is null || ShouldSkipValidation(argument.GetType()))
      {
        continue;
      }

      var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
      var validator = services.GetService(validatorType) as IValidator;
      if (validator is null)
      {
        continue;
      }

      var validationContext = new ValidationContext<object>(argument);
      var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
      if (!result.IsValid)
      {
        failures.AddRange(result.Errors);
      }
    }

    if (failures.Count > 0)
    {
      return Results.BadRequest(ValidationErrorResponse.FromFailures(failures));
    }

    return await next(context);
  }

  private static bool ShouldSkipValidation(Type type)
  {
    return type == typeof(string)
      || typeof(HttpContext).IsAssignableFrom(type)
      || typeof(CancellationToken).IsAssignableFrom(type)
      || typeof(System.Security.Claims.ClaimsPrincipal).IsAssignableFrom(type)
      || type.IsPrimitive
      || type.IsEnum
      || type == typeof(Guid)
      || type == typeof(DateTimeOffset);
  }
}
