using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.EmergencyContacts;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IEmergencyContactService
{
  public Task<IReadOnlyList<EmergencyContactResponse>> GetForUserAsync(string userSub, CancellationToken cancellationToken = default);

  public Task<EmergencyContactResponse> AddForUserAsync(string userSub, CreateEmergencyContactRequest request, CancellationToken cancellationToken = default);

  public Task<EmergencyContactResponse?> UpdateForUserAsync(
    string userSub,
    Guid contactId,
    UpdateEmergencyContactRequest request,
    CancellationToken cancellationToken = default);

  public Task<bool> DeleteForUserAsync(string userSub, Guid contactId, CancellationToken cancellationToken = default);
}
