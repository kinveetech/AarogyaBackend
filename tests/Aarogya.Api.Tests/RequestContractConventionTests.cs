using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Aarogya.Api.Features.V1.Consents;
using AwesomeAssertions;
using Xunit;

namespace Aarogya.Api.Tests;

/// <summary>
/// ASP.NET Core MVC validates positional records through their constructor parameters and throws
/// <see cref="InvalidOperationException"/> at request time when a validation attribute targets a
/// record property instead (ThrowIfRecordTypeHasValidationOnProperties). A misplaced
/// <c>[property: ...]</c> target therefore turns every request against that endpoint into a 500.
/// </summary>
public sealed class RequestContractConventionTests
{
  [Fact]
  public void RequestContractRecords_ShouldNotCarryValidationAttributesOnProperties()
  {
    var apiAssembly = typeof(UpsertConsentRequest).Assembly;

    var requestRecords = apiAssembly
      .GetTypes()
      .Where(t => t.IsClass
        && !t.IsAbstract
        && t.Name.EndsWith("Request", StringComparison.Ordinal)
        && t.Namespace?.Contains(".Features.", StringComparison.Ordinal) == true)
      .ToList();

    requestRecords.Should().NotBeEmpty();

    var offenders = new List<string>();
    foreach (var type in requestRecords)
    {
      var constructorParameterNames = type
        .GetConstructors()
        .SelectMany(c => c.GetParameters())
        .Select(p => p.Name)
        .Where(n => n is not null)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

      foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        if (!constructorParameterNames.Contains(property.Name))
        {
          continue;
        }

        if (property.GetCustomAttributes<ValidationAttribute>(inherit: true).Any())
        {
          offenders.Add($"{type.Name}.{property.Name}");
        }
      }
    }

    offenders.Should().BeEmpty(
      "validation attributes on positional record contracts must target the constructor parameter "
      + "(e.g. [MaxLength(80)]), not the property (e.g. [property: MaxLength(80)]), "
      + "or MVC throws at request time");
  }
}
