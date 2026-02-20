using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aarogya.Infrastructure.Aws;

internal sealed class CognitoUserPoolHealthCheck(
  IAmazonCognitoIdentityProvider cognitoClient,
  IConfiguration configuration) : IHealthCheck
{
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken = default)
  {
    var userPoolId = configuration["Aws:Cognito:UserPoolId"]?.Trim();
    if (string.IsNullOrWhiteSpace(userPoolId) || IsPlaceholder(userPoolId))
    {
      return HealthCheckResult.Unhealthy("Cognito user pool ID is not configured.");
    }

    try
    {
      var response = await cognitoClient.DescribeUserPoolAsync(new DescribeUserPoolRequest
      {
        UserPoolId = userPoolId
      }, cancellationToken);

      if (response.UserPool is null)
      {
        return HealthCheckResult.Unhealthy($"Cognito user pool '{userPoolId}' was not found.");
      }

      return HealthCheckResult.Healthy($"Cognito user pool '{userPoolId}' is reachable.");
    }
    catch (ResourceNotFoundException ex)
    {
      return HealthCheckResult.Unhealthy($"Cognito user pool '{userPoolId}' was not found.", ex);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      return HealthCheckResult.Unhealthy("Cognito health check timed out.");
    }
    catch (AmazonCognitoIdentityProviderException ex)
    {
      return HealthCheckResult.Unhealthy("Cognito health check failed.", ex);
    }
    catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
    {
      return HealthCheckResult.Unhealthy("Cognito health check failed.", ex);
    }
  }

  private static bool IsPlaceholder(string value)
  {
    return value.Contains("SET_VIA_ENV_VAR", StringComparison.OrdinalIgnoreCase)
      || value.Contains("SET_VIA_USER_SECRETS_OR_ENV_VAR", StringComparison.OrdinalIgnoreCase);
  }
}
