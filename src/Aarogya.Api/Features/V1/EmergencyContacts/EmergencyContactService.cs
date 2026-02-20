using Aarogya.Api.Authentication;
using Aarogya.Api.Security;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;

namespace Aarogya.Api.Features.V1.EmergencyContacts;

internal sealed class EmergencyContactService(
  IUserRepository userRepository,
  IEmergencyContactRepository emergencyContactRepository,
  IUnitOfWork unitOfWork,
  IUtcClock clock)
  : IEmergencyContactService
{
  private const int MaxContactsPerPatient = 3;

  public async Task<IReadOnlyList<EmergencyContactResponse>> GetForUserAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    var patient = await ResolvePatientAsync(userSub, cancellationToken);
    var contacts = await emergencyContactRepository.ListByUserAsync(patient.Id, cancellationToken);
    return contacts.Select(Map).ToArray();
  }

  public async Task<EmergencyContactResponse> AddForUserAsync(
    string userSub,
    CreateEmergencyContactRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var patient = await ResolvePatientAsync(userSub, cancellationToken);
    var existingContacts = await emergencyContactRepository.ListByUserAsync(patient.Id, cancellationToken);
    if (existingContacts.Count >= MaxContactsPerPatient)
    {
      throw new InvalidOperationException("Maximum of 3 emergency contacts is allowed.");
    }

    var now = clock.UtcNow;
    var contact = new EmergencyContact
    {
      Id = Guid.NewGuid(),
      UserId = patient.Id,
      Name = InputSanitizer.SanitizePlainText(request.Name),
      Phone = InputSanitizer.SanitizePlainText(request.PhoneNumber),
      Relationship = InputSanitizer.SanitizePlainText(request.Relationship),
      Email = InputSanitizer.SanitizeNullablePlainText(request.Email),
      CreatedAt = now,
      UpdatedAt = now
    };

    await emergencyContactRepository.AddAsync(contact, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Map(contact);
  }

  public async Task<EmergencyContactResponse?> UpdateForUserAsync(
    string userSub,
    Guid contactId,
    UpdateEmergencyContactRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var patient = await ResolvePatientAsync(userSub, cancellationToken);
    var contact = await emergencyContactRepository.FirstOrDefaultAsync(
      new EmergencyContactByIdForUserSpecification(patient.Id, contactId),
      cancellationToken);
    if (contact is null)
    {
      return null;
    }

    contact.Name = InputSanitizer.SanitizePlainText(request.Name);
    contact.Phone = InputSanitizer.SanitizePlainText(request.PhoneNumber);
    contact.Relationship = InputSanitizer.SanitizePlainText(request.Relationship);
    contact.Email = InputSanitizer.SanitizeNullablePlainText(request.Email);
    contact.UpdatedAt = clock.UtcNow;

    emergencyContactRepository.Update(contact);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Map(contact);
  }

  public async Task<bool> DeleteForUserAsync(string userSub, Guid contactId, CancellationToken cancellationToken = default)
  {
    var patient = await ResolvePatientAsync(userSub, cancellationToken);
    var contact = await emergencyContactRepository.FirstOrDefaultAsync(
      new EmergencyContactByIdForUserSpecification(patient.Id, contactId),
      cancellationToken);
    if (contact is null)
    {
      return false;
    }

    emergencyContactRepository.Delete(contact);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return true;
  }

  private async Task<User> ResolvePatientAsync(string userSub, CancellationToken cancellationToken)
  {
    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new InvalidOperationException("Authenticated patient user is not provisioned in the database.");
    if (user.Role != UserRole.Patient)
    {
      throw new InvalidOperationException("Only patient users can manage emergency contacts.");
    }

    return user;
  }

  private static EmergencyContactResponse Map(EmergencyContact contact)
    => new(contact.Id, contact.Name, contact.Phone, contact.Relationship, contact.Email);
}
