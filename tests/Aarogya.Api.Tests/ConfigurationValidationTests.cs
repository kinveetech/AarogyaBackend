using Aarogya.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Aarogya.Api.Tests;

public class ConfigurationValidationTests
{
  [Fact]
  public void ValidateRequiredConfiguration_ShouldThrowInProduction_WhenCognitoUserPoolIdMissing()
  {
    var values = WithDefaultsForSocialProviderConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] = "Host=db;Username=user;Password=strong-password"
    });
    var configuration = BuildConfiguration(values);

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().Throw<InvalidOperationException>()
      .WithMessage("*Missing Aws:Cognito:UserPoolId*");
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldThrowInProduction_WhenDefaultDbPasswordIsUsed()
  {
    var values = WithDefaultsForSocialProviderConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] =
        "Host=db;Port=5432;Database=aarogya;Username=aarogya;Password=aarogya_dev_password",
      ["Aws:Cognito:UserPoolId"] = "ap-south-1_examplePoolId",
      ["Aws:Cognito:AppClientId"] = "example-app-client-id"
    });
    var configuration = BuildConfiguration(values);

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().Throw<InvalidOperationException>()
      .WithMessage("*insecure default credentials*");
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldThrowInProduction_WhenCognitoAppClientIdMissing()
  {
    var values = WithDefaultsForSocialProviderConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] = "Host=db;Username=user;Password=strong-password",
      ["Aws:Cognito:UserPoolId"] = "ap-south-1_examplePoolId"
    });
    var configuration = BuildConfiguration(values);

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().Throw<InvalidOperationException>()
      .WithMessage("*Missing Aws:Cognito:AppClientId*");
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldThrowInProduction_WhenCognitoValuesUseEnvPlaceholders()
  {
    var values = WithDefaultsForSocialProviderConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] = "Host=db;Username=user;Password=strong-password",
      ["Aws:Cognito:UserPoolId"] = "SET_VIA_ENV_VAR",
      ["Aws:Cognito:AppClientId"] = "SET_VIA_ENV_VAR"
    });
    var configuration = BuildConfiguration(values);

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().Throw<InvalidOperationException>()
      .WithMessage("*Missing Aws:Cognito:UserPoolId*")
      .WithMessage("*Missing Aws:Cognito:AppClientId*");
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldThrowInProduction_WhenAwsServiceUrlIsInvalid()
  {
    var values = WithDefaultsForSocialProviderConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] = "Host=db;Username=user;Password=strong-password",
      ["Aws:Cognito:UserPoolId"] = "ap-south-1_examplePoolId",
      ["Aws:Cognito:AppClientId"] = "example-app-client-id",
      ["Aws:ServiceUrl"] = "not-a-url"
    });
    var configuration = BuildConfiguration(values);

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().Throw<InvalidOperationException>()
      .WithMessage("*Aws:ServiceUrl must be a valid absolute HTTP/HTTPS URL*");
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldNotThrowInDevelopment_WhenConfigurationIsIncomplete()
  {
    var configuration = BuildConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Password=aarogya_dev_password"
    });

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Development"));

    action.Should().NotThrow();
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldNotThrowInProduction_WhenProviderIsDisabled()
  {
    var values = WithDefaultsForSocialProviderConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] = "Host=db;Username=user;Password=strong-password",
      ["Aws:Cognito:UserPoolId"] = "ap-south-1_examplePoolId",
      ["Aws:Cognito:AppClientId"] = "example-app-client-id"
    });

    values["Aws:Cognito:SocialIdentityProviders:Apple:Enabled"] = "false";
    values.Remove("Aws:Cognito:SocialIdentityProviders:Apple:ClientId");
    values.Remove("Aws:Cognito:SocialIdentityProviders:Apple:ClientSecret");

    var configuration = BuildConfiguration(values);
    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().NotThrow();
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldThrowInProduction_WhenTlsEnforcementEnabledAndConnectionsAreInsecure()
  {
    var values = WithDefaultsForSocialProviderConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] = "Host=db;Port=5432;Database=aarogya;Username=aarogya;Password=strong-password",
      ["ConnectionStrings:Redis"] = "redis:6379,abortConnect=false",
      ["Aws:Cognito:UserPoolId"] = "ap-south-1_examplePoolId",
      ["Aws:Cognito:AppClientId"] = "example-app-client-id",
      ["Aws:UseLocalStack"] = "false",
      ["Aws:ServiceUrl"] = "http://s3.ap-south-1.amazonaws.com",
      ["TransportSecurity:EnforceTls13"] = "true"
    });
    var configuration = BuildConfiguration(values);

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().Throw<InvalidOperationException>()
      .WithMessage("*DefaultConnection must set SSL Mode*")
      .WithMessage("*ConnectionStrings:Redis must include ssl=true*")
      .WithMessage("*Aws:ServiceUrl must use HTTPS*");
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldNotThrowInProduction_WhenTlsEnforcementEnabledAndConnectionsAreSecure()
  {
    var values = WithDefaultsForSocialProviderConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] =
        "Host=db;Port=5432;Database=aarogya;Username=aarogya;Password=strong-password;SSL Mode=VerifyFull;Trust Server Certificate=false",
      ["ConnectionStrings:Redis"] = "cache.example.com:6380,ssl=true,abortConnect=false",
      ["Aws:Cognito:UserPoolId"] = "ap-south-1_examplePoolId",
      ["Aws:Cognito:AppClientId"] = "example-app-client-id",
      ["Aws:UseLocalStack"] = "false",
      ["Aws:ServiceUrl"] = "https://s3.ap-south-1.amazonaws.com",
      ["TransportSecurity:EnforceTls13"] = "true"
    });
    var configuration = BuildConfiguration(values);

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().NotThrow();
  }

  private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
  {
    return new ConfigurationBuilder()
      .AddInMemoryCollection(values)
      .Build();
  }

  private static Dictionary<string, string?> WithDefaultsForSocialProviderConfiguration(Dictionary<string, string?> values)
  {
    foreach (var provider in new[] { "Google", "Apple", "Facebook" })
    {
      var providerKey = $"Aws:Cognito:SocialIdentityProviders:{provider}";
      var providerId = provider switch
      {
        "Google" => "google",
        "Apple" => "apple",
        "Facebook" => "facebook",
        _ => throw new InvalidOperationException($"Unsupported provider '{provider}'.")
      };

      values[$"{providerKey}:Enabled"] = "true";
      values[$"{providerKey}:ClientId"] = $"{providerId}-client-id";
      values[$"{providerKey}:ClientSecret"] = $"{providerId}-client-secret";
    }

    values["Aws:Cognito:SocialIdentityProviders:MobileRedirectUris:0"] = "aarogya://auth/callback";
    values.TryAdd("Aws:Cognito:Domain", "aarogya-test");
    return values;
  }

  private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "Aarogya.Api.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
}
