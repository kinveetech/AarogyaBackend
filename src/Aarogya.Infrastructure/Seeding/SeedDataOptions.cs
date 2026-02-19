namespace Aarogya.Infrastructure.Seeding;

public sealed class SeedDataOptions
{
  public const string SectionName = "SeedData";

  public bool EnableOnStartup { get; set; }

  public int PatientsCount { get; set; } = 16;

  public int DoctorsCount { get; set; } = 4;

  public int LabTechniciansCount { get; set; } = 3;

  public int AdminsCount { get; set; } = 1;

  public int ReportsPerPatient { get; set; } = 2;
}
