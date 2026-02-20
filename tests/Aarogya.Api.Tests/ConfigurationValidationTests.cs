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

  private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
  {
    return new ConfigurationBuilder()
      .AddInMemoryCollection(values)
      .Build();
  }

  private static Dictionary<string, string?> WithDefaultsForSocialProviderConfiguration(Dictionary<string, string?> values)
  {
    values["Aws:Cognito:SocialIdentityProviders:Google:Enabled"] = "true";
    values["Aws:Cognito:SocialIdentityProviders:Google:ClientId"] = "google-client-id";
    values["Aws:Cognito:SocialIdentityProviders:Google:ClientSecret"] = "google-client-secret";
    values["Aws:Cognito:SocialIdentityProviders:Apple:Enabled"] = "true";
    values["Aws:Cognito:SocialIdentityProviders:Apple:ClientId"] = "apple-client-id";
    values["Aws:Cognito:SocialIdentityProviders:Apple:ClientSecret"] = "apple-client-secret";
    values["Aws:Cognito:SocialIdentityProviders:Facebook:Enabled"] = "true";
    values["Aws:Cognito:SocialIdentityProviders:Facebook:ClientId"] = "facebook-client-id";
    values["Aws:Cognito:SocialIdentityProviders:Facebook:ClientSecret"] = "facebook-client-secret";
    values["Aws:Cognito:SocialIdentityProviders:MobileRedirectUris:0"] = "aarogya://auth/callback";
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
