using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Users;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record UserProfileResponse(string Sub, string Email, IReadOnlyList<string> Roles);
