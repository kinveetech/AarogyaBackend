using System.Security.Cryptography;
using System.Text;

namespace Aarogya.Infrastructure.Aadhaar;

public static class AadhaarHashing
{
  public static string Normalize(string aadhaarNumber)
  {
    var normalized = new string(aadhaarNumber.Where(char.IsDigit).ToArray());
    if (normalized.Length != 12)
    {
      throw new ArgumentException("Aadhaar number must contain exactly 12 digits.", nameof(aadhaarNumber));
    }

    return normalized;
  }

  public static byte[] ComputeSha256(string normalizedAadhaar)
    => SHA256.HashData(Encoding.UTF8.GetBytes(normalizedAadhaar));
}
