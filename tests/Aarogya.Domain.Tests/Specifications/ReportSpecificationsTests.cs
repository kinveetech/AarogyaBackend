using Aarogya.Domain.Entities;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Xunit;

namespace Aarogya.Domain.Tests.Specifications;

public sealed class ReportSpecificationsTests
{
  [Fact]
  public void ReportByIdSpecification_ShouldMatchByIdAndNotDeleted()
  {
    var reportId = Guid.NewGuid();

    var specification = new ReportByIdSpecification(reportId);
    var predicate = specification.Criteria!.Compile();

    var match = CreateReport(reportId, "RPT-001", Guid.NewGuid(), Guid.NewGuid(), null, false);
    var deleted = CreateReport(reportId, "RPT-001", Guid.NewGuid(), Guid.NewGuid(), null, true);
    var wrongId = CreateReport(Guid.NewGuid(), "RPT-002", Guid.NewGuid(), Guid.NewGuid(), null, false);

    predicate(match).Should().BeTrue();
    predicate(deleted).Should().BeFalse();
    predicate(wrongId).Should().BeFalse();
  }

  [Fact]
  public void ReportByIdSpecification_ShouldHaveOneIncludeAndAsNoTracking()
  {
    var specification = new ReportByIdSpecification(Guid.NewGuid());

    specification.Includes.Count.Should().Be(1);
    specification.AsNoTracking.Should().BeTrue();
  }

  [Fact]
  public void ReportByNumberSpecification_ShouldMatchByReportNumberAndNotDeleted()
  {
    var specification = new ReportByNumberSpecification("RPT-123");
    var predicate = specification.Criteria!.Compile();

    var match = CreateReport(Guid.NewGuid(), "RPT-123", Guid.NewGuid(), Guid.NewGuid(), null, false);
    var deleted = CreateReport(Guid.NewGuid(), "RPT-123", Guid.NewGuid(), Guid.NewGuid(), null, true);
    var wrongNumber = CreateReport(Guid.NewGuid(), "RPT-999", Guid.NewGuid(), Guid.NewGuid(), null, false);

    predicate(match).Should().BeTrue();
    predicate(deleted).Should().BeFalse();
    predicate(wrongNumber).Should().BeFalse();
  }

  [Fact]
  public void ReportByNumberSpecification_ShouldHaveOneIncludeAndAsNoTracking()
  {
    var specification = new ReportByNumberSpecification("RPT-123");

    specification.Includes.Count.Should().Be(1);
    specification.AsNoTracking.Should().BeTrue();
  }

  [Fact]
  public void ReportsByPatientSpecification_ShouldMatchByPatientIdAndNotDeleted()
  {
    var patientId = Guid.NewGuid();

    var specification = new ReportsByPatientSpecification(patientId);
    var predicate = specification.Criteria!.Compile();

    var match = CreateReport(Guid.NewGuid(), "RPT-001", patientId, Guid.NewGuid(), null, false);
    var deleted = CreateReport(Guid.NewGuid(), "RPT-002", patientId, Guid.NewGuid(), null, true);
    var otherPatient = CreateReport(Guid.NewGuid(), "RPT-003", Guid.NewGuid(), Guid.NewGuid(), null, false);

    predicate(match).Should().BeTrue();
    predicate(deleted).Should().BeFalse();
    predicate(otherPatient).Should().BeFalse();
  }

  [Fact]
  public void ReportsByPatientSpecification_ShouldHaveOneIncludeOrderByDescendingAndAsNoTracking()
  {
    var specification = new ReportsByPatientSpecification(Guid.NewGuid());

    specification.Includes.Count.Should().Be(1);
    specification.OrderByDescending.Should().NotBeNull();
    specification.AsNoTracking.Should().BeTrue();
  }

  [Fact]
  public void ReportsByUploaderSpecification_ShouldMatchByUploaderIdAndNotDeleted()
  {
    var uploaderId = Guid.NewGuid();

    var specification = new ReportsByUploaderSpecification(uploaderId);
    var predicate = specification.Criteria!.Compile();

    var match = CreateReport(Guid.NewGuid(), "RPT-001", Guid.NewGuid(), uploaderId, null, false);
    var deleted = CreateReport(Guid.NewGuid(), "RPT-002", Guid.NewGuid(), uploaderId, null, true);
    var otherUploader = CreateReport(Guid.NewGuid(), "RPT-003", Guid.NewGuid(), Guid.NewGuid(), null, false);

    predicate(match).Should().BeTrue();
    predicate(deleted).Should().BeFalse();
    predicate(otherUploader).Should().BeFalse();
  }

  [Fact]
  public void ReportsByUploaderSpecification_ShouldHaveOneIncludeOrderByDescendingAndAsNoTracking()
  {
    var specification = new ReportsByUploaderSpecification(Guid.NewGuid());

    specification.Includes.Count.Should().Be(1);
    specification.OrderByDescending.Should().NotBeNull();
    specification.AsNoTracking.Should().BeTrue();
  }

  [Fact]
  public void ReportsByRelatedUserSpecification_ShouldMatchByPatientOrUploaderOrDoctor()
  {
    var userId = Guid.NewGuid();

    var specification = new ReportsByRelatedUserSpecification(userId, includeDeleted: false);
    var predicate = specification.Criteria!.Compile();

    var asPatient = CreateReport(Guid.NewGuid(), "RPT-001", userId, Guid.NewGuid(), null, false);
    var asUploader = CreateReport(Guid.NewGuid(), "RPT-002", Guid.NewGuid(), userId, null, false);
    var asDoctor = CreateReport(Guid.NewGuid(), "RPT-003", Guid.NewGuid(), Guid.NewGuid(), userId, false);
    var noMatch = CreateReport(Guid.NewGuid(), "RPT-004", Guid.NewGuid(), Guid.NewGuid(), null, false);

    predicate(asPatient).Should().BeTrue();
    predicate(asUploader).Should().BeTrue();
    predicate(asDoctor).Should().BeTrue();
    predicate(noMatch).Should().BeFalse();
  }

  [Fact]
  public void ReportsByRelatedUserSpecification_ShouldExcludeDeletedWhenIncludeDeletedIsFalse()
  {
    var userId = Guid.NewGuid();

    var specification = new ReportsByRelatedUserSpecification(userId, includeDeleted: false);
    var predicate = specification.Criteria!.Compile();

    var deleted = CreateReport(Guid.NewGuid(), "RPT-001", userId, Guid.NewGuid(), null, true);

    predicate(deleted).Should().BeFalse();
  }

  [Fact]
  public void ReportsByRelatedUserSpecification_ShouldIncludeDeletedWhenIncludeDeletedIsTrue()
  {
    var userId = Guid.NewGuid();

    var specification = new ReportsByRelatedUserSpecification(userId, includeDeleted: true);
    var predicate = specification.Criteria!.Compile();

    var deleted = CreateReport(Guid.NewGuid(), "RPT-001", userId, Guid.NewGuid(), null, true);

    predicate(deleted).Should().BeTrue();
  }

  [Fact]
  public void ReportsByRelatedUserSpecification_ShouldHaveOneIncludeOrderByDescendingAndNoAsNoTracking()
  {
    var specification = new ReportsByRelatedUserSpecification(Guid.NewGuid());

    specification.Includes.Count.Should().Be(1);
    specification.OrderByDescending.Should().NotBeNull();
    specification.AsNoTracking.Should().BeFalse();
  }

  private static Report CreateReport(
    Guid id,
    string reportNumber,
    Guid patientId,
    Guid uploadedByUserId,
    Guid? doctorId,
    bool isDeleted)
  {
    return new Report
    {
      Id = id,
      ReportNumber = reportNumber,
      PatientId = patientId,
      UploadedByUserId = uploadedByUserId,
      DoctorId = doctorId,
      IsDeleted = isDeleted,
      Parameters = []
    };
  }
}
