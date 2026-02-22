using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.ValueObjects;
using Aarogya.Infrastructure.Persistence;
using Aarogya.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aarogya.Infrastructure.Tests;

[Collection(PostgreSqlIntegrationFixtureGroup.CollectionName)]
public sealed class ReportExtractionPersistenceTests(PostgreSqlContainerFixture fixture)
{
  [Fact]
  public async Task SaveChanges_ShouldPersistExtractionMetadataAsJsonbAsync()
  {
    await using var serviceProvider = await fixture.CreateServiceProviderAsync();

    var userId = Guid.NewGuid();
    var reportId = Guid.NewGuid();
    var extraction = new ExtractionMetadata
    {
      ExtractionMethod = "pdfpig",
      StructuringModel = "qwen2.5:14b-instruct",
      ExtractedParameterCount = 5,
      OverallConfidence = 0.92,
      PageCount = 3,
      ExtractedAt = new DateTimeOffset(2026, 2, 22, 10, 0, 0, TimeSpan.Zero),
      StructuredAt = new DateTimeOffset(2026, 2, 22, 10, 0, 5, TimeSpan.Zero),
      AttemptCount = 1,
      ProviderMetadata = new Dictionary<string, string> { ["pdf_version"] = "1.7" }
    };

    using (var arrangeScope = serviceProvider.CreateScope())
    {
      var dbContext = arrangeScope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var user = CreateUser(userId);
      dbContext.Users.Add(user);

      var report = new Report
      {
        Id = reportId,
        ReportNumber = $"RPT-{Guid.NewGuid():N}"[..12],
        PatientId = userId,
        UploadedByUserId = userId,
        Status = ReportStatus.Extracted,
        Extraction = extraction
      };

      dbContext.Reports.Add(report);
      await dbContext.SaveChangesAsync();
    }

    using (var assertScope = serviceProvider.CreateScope())
    {
      var dbContext = assertScope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var report = await dbContext.Reports.AsNoTracking().SingleAsync(r => r.Id == reportId);

      report.Extraction.Should().NotBeNull();
      report.Extraction!.ExtractionMethod.Should().Be("pdfpig");
      report.Extraction.StructuringModel.Should().Be("qwen2.5:14b-instruct");
      report.Extraction.ExtractedParameterCount.Should().Be(5);
      report.Extraction.OverallConfidence.Should().Be(0.92);
      report.Extraction.PageCount.Should().Be(3);
      report.Extraction.AttemptCount.Should().Be(1);
      report.Extraction.ProviderMetadata.Should().ContainKey("pdf_version").WhoseValue.Should().Be("1.7");
    }
  }

  [Fact]
  public async Task SaveChanges_ShouldPersistNullExtractionMetadataAsync()
  {
    await using var serviceProvider = await fixture.CreateServiceProviderAsync();

    var userId = Guid.NewGuid();
    var reportId = Guid.NewGuid();

    using (var arrangeScope = serviceProvider.CreateScope())
    {
      var dbContext = arrangeScope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var user = CreateUser(userId);
      dbContext.Users.Add(user);

      var report = new Report
      {
        Id = reportId,
        ReportNumber = $"RPT-{Guid.NewGuid():N}"[..12],
        PatientId = userId,
        UploadedByUserId = userId,
        Extraction = null
      };

      dbContext.Reports.Add(report);
      await dbContext.SaveChangesAsync();
    }

    using (var assertScope = serviceProvider.CreateScope())
    {
      var dbContext = assertScope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var report = await dbContext.Reports.AsNoTracking().SingleAsync(r => r.Id == reportId);

      report.Extraction.Should().BeNull();
    }
  }

  [Fact]
  public async Task SaveChanges_ShouldPersistReportParameterSourceAndConfidenceAsync()
  {
    await using var serviceProvider = await fixture.CreateServiceProviderAsync();

    var userId = Guid.NewGuid();
    var reportId = Guid.NewGuid();
    var parameterId = Guid.NewGuid();

    using (var arrangeScope = serviceProvider.CreateScope())
    {
      var dbContext = arrangeScope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var user = CreateUser(userId);
      dbContext.Users.Add(user);

      var report = new Report
      {
        Id = reportId,
        ReportNumber = $"RPT-{Guid.NewGuid():N}"[..12],
        PatientId = userId,
        UploadedByUserId = userId,
        Parameters =
        [
          new ReportParameter
          {
            Id = parameterId,
            ParameterCode = "HGB",
            ParameterName = "Hemoglobin",
            MeasuredValueNumeric = 14.5m,
            Unit = "g/dL",
            Source = "extracted",
            Confidence = 0.95
          }
        ]
      };

      dbContext.Reports.Add(report);
      await dbContext.SaveChangesAsync();
    }

    using (var assertScope = serviceProvider.CreateScope())
    {
      var dbContext = assertScope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var parameter = await dbContext.ReportParameters.AsNoTracking().SingleAsync(p => p.Id == parameterId);

      parameter.Source.Should().Be("extracted");
      parameter.Confidence.Should().Be(0.95);
    }
  }

  [Fact]
  public async Task SaveChanges_ShouldPersistReportParameterWithNullSourceAndConfidenceAsync()
  {
    await using var serviceProvider = await fixture.CreateServiceProviderAsync();

    var userId = Guid.NewGuid();
    var reportId = Guid.NewGuid();
    var parameterId = Guid.NewGuid();

    using (var arrangeScope = serviceProvider.CreateScope())
    {
      var dbContext = arrangeScope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var user = CreateUser(userId);
      dbContext.Users.Add(user);

      var report = new Report
      {
        Id = reportId,
        ReportNumber = $"RPT-{Guid.NewGuid():N}"[..12],
        PatientId = userId,
        UploadedByUserId = userId,
        Parameters =
        [
          new ReportParameter
          {
            Id = parameterId,
            ParameterCode = "HGB",
            ParameterName = "Hemoglobin",
            Source = null,
            Confidence = null
          }
        ]
      };

      dbContext.Reports.Add(report);
      await dbContext.SaveChangesAsync();
    }

    using (var assertScope = serviceProvider.CreateScope())
    {
      var dbContext = assertScope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var parameter = await dbContext.ReportParameters.AsNoTracking().SingleAsync(p => p.Id == parameterId);

      parameter.Source.Should().BeNull();
      parameter.Confidence.Should().BeNull();
    }
  }

  private static User CreateUser(Guid id) => new()
  {
    Id = id,
    ExternalAuthId = $"auth-{id:N}"[..20],
    FirstName = "Test",
    LastName = "User",
    Email = $"test-{id:N}"[..10] + "@example.com",
    Phone = "+919876543210",
    Role = UserRole.Patient
  };
}
