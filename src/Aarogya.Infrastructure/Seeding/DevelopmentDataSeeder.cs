using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.ValueObjects;
using Aarogya.Infrastructure.Aadhaar;
using Aarogya.Infrastructure.Persistence;
using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aarogya.Infrastructure.Seeding;

public sealed class DevelopmentDataSeeder(
  AarogyaDbContext dbContext,
  IAadhaarVaultService aadhaarVaultService,
  IOptions<SeedDataOptions> seedOptions)
  : IDataSeeder
{
  private const string SeedUserPrefix = "seed-";
  private const int SeedBase = 20260219;

  public async Task SeedAsync(CancellationToken cancellationToken = default)
  {
    var options = seedOptions.Value;
    if (!options.EnableOnStartup)
    {
      return;
    }

    var hasSeedData = await dbContext.Users.AnyAsync(
      x => x.ExternalAuthId != null && EF.Functions.Like(x.ExternalAuthId, $"{SeedUserPrefix}%"),
      cancellationToken);

    if (hasSeedData)
    {
      return;
    }

    var users = GenerateUsers(options);
    await dbContext.Users.AddRangeAsync(users, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    var adminUserId = users.First(x => x.Role == UserRole.Admin).Id;
    var patients = users.Where(x => x.Role == UserRole.Patient).ToList();
    var doctors = users.Where(x => x.Role == UserRole.Doctor).ToList();
    var labTechs = users.Where(x => x.Role == UserRole.LabTechnician).ToList();
    var aadhaarFaker = CreateFaker(SeedBase + 99);

    foreach (var patient in patients)
    {
      var aadhaar = aadhaarFaker.Random.ReplaceNumbers("############");
      var normalizedAadhaar = AadhaarHashing.Normalize(aadhaar);
      patient.AadhaarRefToken = await aadhaarVaultService.CreateOrGetReferenceTokenAsync(normalizedAadhaar, adminUserId, cancellationToken);
      patient.AadhaarSha256 = AadhaarHashing.ComputeSha256(normalizedAadhaar);
    }

    var contacts = GenerateEmergencyContacts(patients);
    await dbContext.EmergencyContacts.AddRangeAsync(contacts, cancellationToken);

    var reports = GenerateReports(patients, doctors, labTechs, options.ReportsPerPatient);
    await dbContext.Reports.AddRangeAsync(reports, cancellationToken);

    var reportParameters = GenerateReportParameters(reports);
    await dbContext.ReportParameters.AddRangeAsync(reportParameters, cancellationToken);

    var accessGrants = GenerateAccessGrants(patients, doctors, adminUserId);
    await dbContext.AccessGrants.AddRangeAsync(accessGrants, cancellationToken);

    var auditLogs = GenerateAuditLogs(users, reports);
    await dbContext.AuditLogs.AddRangeAsync(auditLogs, cancellationToken);

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  private static List<User> GenerateUsers(SeedDataOptions options)
  {
    var users = new List<User>();

    users.AddRange(CreateUsersForRole(UserRole.Admin, options.AdminsCount));
    users.AddRange(CreateUsersForRole(UserRole.Doctor, options.DoctorsCount));
    users.AddRange(CreateUsersForRole(UserRole.LabTechnician, options.LabTechniciansCount));
    users.AddRange(CreateUsersForRole(UserRole.Patient, options.PatientsCount));

    return users;
  }

  private static IEnumerable<User> CreateUsersForRole(UserRole role, int count)
  {
    var faker = CreateFaker(SeedBase + (int)role + 1);

    return Enumerable.Range(1, Math.Max(count, 0)).Select(index => new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = $"{SeedUserPrefix}{role.ToString().ToUpperInvariant()}-{index}",
      Role = role,
      FirstName = faker.Name.FirstName(),
      LastName = faker.Name.LastName(),
      Email = faker.Internet.Email(provider: "aarogya.dev"),
      Phone = faker.Phone.PhoneNumber("##########"),
      DateOfBirth = role == UserRole.Patient
        ? DateOnly.FromDateTime(faker.Date.Past(70, DateTime.UtcNow.AddYears(-18)))
        : null,
      Gender = faker.PickRandom("male", "female", "other")
    });
  }

  private static IEnumerable<EmergencyContact> GenerateEmergencyContacts(IEnumerable<User> patients)
  {
    var faker = CreateFaker(SeedBase + 201);

    return patients.Select(patient => new EmergencyContact
    {
      Id = Guid.NewGuid(),
      UserId = patient.Id,
      Name = faker.Name.FullName(),
      Relationship = faker.PickRandom("Spouse", "Parent", "Sibling"),
      Phone = faker.Phone.PhoneNumber("##########"),
      IsPrimary = true
    });
  }

  private static List<Report> GenerateReports(
    IReadOnlyList<User> patients,
    IReadOnlyList<User> doctors,
    IReadOnlyList<User> labTechs,
    int reportsPerPatient)
  {
    var faker = CreateFaker(SeedBase + 301);
    var reports = new List<Report>();

    foreach (var patient in patients)
    {
      for (var i = 0; i < Math.Max(reportsPerPatient, 1); i++)
      {
        var collectedAt = faker.Date.RecentOffset(60).ToUniversalTime();
        var reportedAt = collectedAt.AddHours(faker.Random.Int(1, 48));
        var reportType = faker.PickRandom<ReportType>();
        var doctor = doctors[faker.Random.Int(0, doctors.Count - 1)];
        var uploader = labTechs[faker.Random.Int(0, labTechs.Count - 1)];

        reports.Add(new Report
        {
          Id = Guid.NewGuid(),
          ReportNumber = $"RPT-{faker.Random.AlphaNumeric(10).ToUpperInvariant()}",
          PatientId = patient.Id,
          DoctorId = doctor.Id,
          UploadedByUserId = uploader.Id,
          ReportType = reportType,
          Status = faker.PickRandom(ReportStatus.Uploaded, ReportStatus.Validated, ReportStatus.Published),
          SourceSystem = faker.PickRandom("LIS", "PACS", "HospitalEMR"),
          CollectedAt = collectedAt,
          ReportedAt = reportedAt,
          UploadedAt = reportedAt,
          FileStorageKey = $"reports/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}.pdf",
          ChecksumSha256 = faker.Random.Hexadecimal(64, string.Empty),
          Results = new ReportResults
          {
            ReportVersion = 1,
            Notes = faker.Lorem.Sentence(),
            Parameters =
            [
              new ReportResultParameter
              {
                Code = "HGB",
                Name = "Hemoglobin",
                Value = faker.Random.Decimal(9m, 16m),
                Unit = "g/dL",
                ReferenceRange = "12-16",
                AbnormalFlag = faker.Random.Bool(0.2f)
              }
            ]
          },
          Metadata = new ReportMetadata
          {
            SourceSystem = "seed",
            Tags = new Dictionary<string, string>
            {
              ["env"] = "dev",
              ["seed"] = "true"
            }
          }
        });
      }
    }

    return reports;
  }

  private static IEnumerable<ReportParameter> GenerateReportParameters(IEnumerable<Report> reports)
  {
    var faker = CreateFaker(SeedBase + 401);

    return reports.Select(report => new ReportParameter
    {
      Id = Guid.NewGuid(),
      ReportId = report.Id,
      ParameterCode = "HGB",
      ParameterName = "Hemoglobin",
      MeasuredValueNumeric = faker.Random.Decimal(9m, 16m),
      Unit = "g/dL",
      ReferenceRangeText = "12-16",
      IsAbnormal = faker.Random.Bool(0.2f),
      RawParameter = new ReportParameterRaw
      {
        Attributes = new Dictionary<string, string>
        {
          ["method"] = "automated",
          ["device"] = faker.PickRandom("Sysmex", "Abbott", "Roche")
        }
      }
    });
  }

  private static IEnumerable<AccessGrant> GenerateAccessGrants(
    IEnumerable<User> patients,
    IReadOnlyList<User> doctors,
    Guid adminUserId)
  {
    var faker = CreateFaker(SeedBase + 501);

    return patients.Select(patient =>
    {
      var doctor = doctors[faker.Random.Int(0, doctors.Count - 1)];
      var startsAt = faker.Date.RecentOffset(10).ToUniversalTime();

      return new AccessGrant
      {
        Id = Guid.NewGuid(),
        PatientId = patient.Id,
        GrantedToUserId = doctor.Id,
        GrantedByUserId = adminUserId,
        GrantReason = faker.PickRandom("ongoing-treatment", "follow-up", "second-opinion"),
        Scope = new AccessGrantScope
        {
          CanReadReports = true,
          CanDownloadReports = true,
          AllowedReportTypes = ["blood_test", "urine_test", "radiology"]
        },
        Status = AccessGrantStatus.Active,
        StartsAt = startsAt,
        ExpiresAt = startsAt.AddMonths(3)
      };
    });
  }

  private static IEnumerable<AuditLog> GenerateAuditLogs(
    IReadOnlyList<User> users,
    IReadOnlyList<Report> reports)
  {
    var faker = CreateFaker(SeedBase + 601);

    return reports.Take(Math.Min(40, reports.Count)).Select(report =>
    {
      var actor = users[faker.Random.Int(0, users.Count - 1)];

      return new AuditLog
      {
        Id = Guid.NewGuid(),
        OccurredAt = faker.Date.RecentOffset(15).ToUniversalTime(),
        ActorUserId = actor.Id,
        ActorRole = actor.Role,
        Action = faker.PickRandom("report.viewed", "report.downloaded", "report.shared"),
        EntityType = "Report",
        EntityId = report.Id,
        CorrelationId = Guid.NewGuid(),
        RequestPath = $"/api/reports/{report.Id}",
        RequestMethod = "GET",
        ResultStatus = 200,
        UserAgent = "seed-data-generator",
        Details = new AuditLogDetails
        {
          Summary = "Seeded audit event",
          Data = new Dictionary<string, string>
          {
            ["seed"] = "true"
          }
        }
      };
    });
  }

  private static Faker CreateFaker(int seed)
    => new Faker("en_IND")
    {
      Random = new Randomizer(seed)
    };
}
