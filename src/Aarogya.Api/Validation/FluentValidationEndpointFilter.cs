using FluentValidation;

namespace Aarogya.Api.Validation;

internal sealed class FluentValidationEndpointFilter(IServiceProvider services) : IEndpointFilter
{
  public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
  {
    foreach (var argument in context.Arguments)
    {
      if (argument is null || ShouldSkipValidation(argument.GetType()))
      {
        continue;
      }

      var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
      var validatorService = services.GetService(validatorType);
      if (validatorService is null)
      {
        continue;
      }
      var validator = (IValidator)validatorService;

      var validationContext = new ValidationContext<object>(argument);
      var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
      if (!result.IsValid)
      {
        return Results.BadRequest(ValidationErrorResponse.FromFailures(result.Errors));
      }
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
