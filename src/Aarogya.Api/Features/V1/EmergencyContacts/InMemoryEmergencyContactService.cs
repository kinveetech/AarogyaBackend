using System.Collections.Concurrent;

namespace Aarogya.Api.Features.V1.EmergencyContacts;

internal sealed class InMemoryEmergencyContactService : IEmergencyContactService
{
  private readonly ConcurrentDictionary<string, List<EmergencyContactResponse>> _contactsByUser = new(StringComparer.Ordinal);

  public Task<IReadOnlyList<EmergencyContactResponse>> GetForUserAsync(string userSub, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var contacts = _contactsByUser.GetOrAdd(userSub, static _ => []);
    lock (contacts)
    {
      return Task.FromResult<IReadOnlyList<EmergencyContactResponse>>(contacts.ToArray());
    }
  }

  public Task<EmergencyContactResponse> AddForUserAsync(string userSub, CreateEmergencyContactRequest request, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var contacts = _contactsByUser.GetOrAdd(userSub, static _ => []);
    var created = new EmergencyContactResponse(
      Guid.NewGuid(),
      request.Name.Trim(),
      request.PhoneNumber.Trim(),
      request.Relationship.Trim());

    lock (contacts)
    {
      contacts.Add(created);
    }

    return Task.FromResult(created);
  }

  public Task<bool> DeleteForUserAsync(string userSub, Guid contactId, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var contacts = _contactsByUser.GetOrAdd(userSub, static _ => []);
    lock (contacts)
    {
      var removed = contacts.RemoveAll(contact => contact.ContactId == contactId) > 0;
      return Task.FromResult(removed);
    }
  }
}
