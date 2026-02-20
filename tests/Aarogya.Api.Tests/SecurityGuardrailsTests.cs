using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class SecurityGuardrailsTests
{
  [Fact]
  public void SanitizePlainText_ShouldRemoveHtmlAndControlCharacters()
  {
    var sanitizerType = typeof(UsersControllerTests).Assembly
      .GetReferencedAssemblies()
      .Select(Assembly.Load)
      .First(assembly => assembly.GetName().Name == "Aarogya.Api")
      .GetType("Aarogya.Api.Security.InputSanitizer", throwOnError: true)!;
    var method = sanitizerType.GetMethod("SanitizePlainText", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    var sanitized = (string)method.Invoke(null, [" <script>alert(1)</script> Dr.\u0001 Rao "])!;

    sanitized.Should().Be("alert(1) Dr. Rao");
  }

  [Fact]
  public void SourceCode_ShouldNotContainRawSqlExecutionPatterns()
  {
    var repositoryRoot = FindRepositoryRoot();
    var sourceFiles = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .ToArray();

    var rawSqlIndicators = new[]
    {
      "FromSqlRaw(",
      "FromSqlInterpolated(",
      "ExecuteSqlRaw(",
      "ExecuteSqlInterpolated(",
      "SqlQueryRaw("
    };

    foreach (var filePath in sourceFiles)
    {
      var contents = File.ReadAllText(filePath);
      foreach (var indicator in rawSqlIndicators)
      {
        contents.Should().NotContain(indicator, $"raw SQL should be avoided in {filePath}");
      }
    }
  }

  private static string FindRepositoryRoot()
  {
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
      if (File.Exists(Path.Combine(current.FullName, "Aarogya.sln")))
      {
        return current.FullName;
      }

      current = current.Parent;
    }

    throw new InvalidOperationException("Unable to locate repository root.");
  }
}
