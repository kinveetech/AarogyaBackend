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
    var configuration = BuildConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] = "Host=db;Username=user;Password=strong-password"
    });

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().Throw<InvalidOperationException>()
      .WithMessage("*Missing Aws:Cognito:UserPoolId*");
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldThrowInProduction_WhenDefaultDbPasswordIsUsed()
  {
    var configuration = BuildConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] =
        "Host=db;Port=5432;Database=aarogya;Username=aarogya;Password=aarogya_dev_password",
      ["Aws:Cognito:UserPoolId"] = "ap-south-1_examplePoolId",
      ["Aws:Cognito:AppClientId"] = "example-app-client-id"
    });

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().Throw<InvalidOperationException>()
      .WithMessage("*insecure default credentials*");
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldThrowInProduction_WhenCognitoAppClientIdMissing()
  {
    var configuration = BuildConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] = "Host=db;Username=user;Password=strong-password",
      ["Aws:Cognito:UserPoolId"] = "ap-south-1_examplePoolId"
    });

    var action = () => StartupExtensions.ValidateRequiredConfiguration(configuration, new TestHostEnvironment("Production"));

    action.Should().Throw<InvalidOperationException>()
      .WithMessage("*Missing Aws:Cognito:AppClientId*");
  }

  [Fact]
  public void ValidateRequiredConfiguration_ShouldThrowInProduction_WhenAwsServiceUrlIsInvalid()
  {
    var configuration = BuildConfiguration(new Dictionary<string, string?>
    {
      ["ConnectionStrings:DefaultConnection"] = "Host=db;Username=user;Password=strong-password",
      ["Aws:Cognito:UserPoolId"] = "ap-south-1_examplePoolId",
      ["Aws:Cognito:AppClientId"] = "example-app-client-id",
      ["Aws:ServiceUrl"] = "not-a-url"
    });

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

  private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "Aarogya.Api.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
}
