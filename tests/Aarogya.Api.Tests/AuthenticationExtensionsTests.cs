using Aarogya.Api.Configuration;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class AuthenticationExtensionsTests
{
  [Fact]
  public void ResolveCognitoIssuer_ShouldUseExplicitIssuer_WhenConfigured()
  {
    var options = CreateOptions();
    options.Cognito.Issuer = "https://issuer.example.com/";

    var issuer = AuthenticationExtensions.ResolveCognitoIssuer(options);

    issuer.Should().Be("https://issuer.example.com");
  }

  [Fact]
  public void ResolveCognitoIssuer_ShouldUseLocalStackServiceUrl_WhenEnabled()
  {
    var options = CreateOptions();
    options.UseLocalStack = true;
    options.ServiceUrl = "http://localhost:4566";

    var issuer = AuthenticationExtensions.ResolveCognitoIssuer(options);

    issuer.Should().Be("http://localhost:4566/ap-south-1_examplePoolId");
  }

  [Fact]
  public void ResolveCognitoIssuer_ShouldIgnorePlaceholderIssuer_AndUseDerivedLocalStackIssuer()
  {
    var options = CreateOptions();
    options.UseLocalStack = true;
    options.ServiceUrl = "http://localhost:4566";
    options.Cognito.Issuer = "http://localhost:4566/SET_VIA_USER_SECRETS_OR_ENV_VAR";

    var issuer = AuthenticationExtensions.ResolveCognitoIssuer(options);

    issuer.Should().Be("http://localhost:4566/ap-south-1_examplePoolId");
  }

  [Fact]
  public void ResolveCognitoIssuer_ShouldUseAwsIssuer_WhenLocalStackDisabled()
  {
    var options = CreateOptions();

    var issuer = AuthenticationExtensions.ResolveCognitoIssuer(options);

    issuer.Should().Be("https://cognito-idp.ap-south-1.amazonaws.com/ap-south-1_examplePoolId");
  }

  [Fact]
  public void ResolveCognitoIssuer_ShouldThrow_WhenUserPoolIdMissing()
  {
    var options = CreateOptions();
    options.Cognito.UserPoolId = null;

    var action = () => AuthenticationExtensions.ResolveCognitoIssuer(options);

    action.Should().Throw<InvalidOperationException>()
      .WithMessage("*UserPoolId*");
  }

  [Fact]
  public void ShouldRequireHttpsMetadata_ShouldReturnFalse_ForLocalStack()
  {
    var options = CreateOptions();
    options.UseLocalStack = true;

    var requireHttps = AuthenticationExtensions.ShouldRequireHttpsMetadata(options, "http://localhost:4566/pool");

    requireHttps.Should().BeFalse();
  }

  [Fact]
  public void ShouldRequireHttpsMetadata_ShouldReturnTrue_ForAwsIssuer()
  {
    var options = CreateOptions();
    options.UseLocalStack = false;

    var requireHttps = AuthenticationExtensions.ShouldRequireHttpsMetadata(
      options,
      "https://cognito-idp.ap-south-1.amazonaws.com/ap-south-1_examplePoolId");

    requireHttps.Should().BeTrue();
  }

  private static AwsOptions CreateOptions()
  {
    return new AwsOptions
    {
      Region = "ap-south-1",
      Cognito = new CognitoOptions
      {
        UserPoolId = "ap-south-1_examplePoolId",
        AppClientId = "example-app-client-id",
        UserPoolName = "aarogya-dev-users"
      }
    };
  }
}
