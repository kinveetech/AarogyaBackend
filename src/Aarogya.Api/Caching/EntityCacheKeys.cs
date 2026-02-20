using System.Security.Cryptography;
using System.Text;

namespace Aarogya.Api.Caching;

internal static class EntityCacheNamespaces
{
  public const string AccessGrantListings = "access-grant-listings";
  public const string ReportListings = "report-listings";
}

internal static class EntityCacheKeys
{
  public static string UserProfile(string userSub)
    => $"cache:user-profile:{Hash(userSub)}";

  public static string AccessGrantListForPatient(string patientSub, string version)
    => $"cache:access-grants:patient:{version}:{Hash(patientSub)}";

  public static string AccessGrantListForDoctor(string doctorSub, string version)
    => $"cache:access-grants:doctor:{version}:{Hash(doctorSub)}";

  public static string ReportListing(string userSub, string fingerprint, string version)
    => $"cache:reports:list:{version}:{Hash(userSub)}:{Hash(fingerprint)}";

  private static string Hash(string value)
  {
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
    return Convert.ToHexString(bytes);
  }
}
