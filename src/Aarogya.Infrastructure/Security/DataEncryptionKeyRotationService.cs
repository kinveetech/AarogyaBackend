using Aarogya.Domain.Entities;
using Aarogya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aarogya.Infrastructure.Security;

public sealed class DataEncryptionKeyRotationService(
  AarogyaDbContext dbContext,
  IPiiFieldEncryptionService encryptionService,
  IOptions<DataEncryptionRotationOptions> rotationOptions)
  : IDataEncryptionKeyRotationService
{
  private readonly DataEncryptionRotationOptions _rotationOptions = rotationOptions.Value;

  public async Task<DataEncryptionReEncryptionSummary> ReEncryptAllAsync(CancellationToken cancellationToken = default)
  {
    var batchSize = _rotationOptions.BatchSize;
    var usersTouched = await ReEncryptUsersAsync(batchSize, cancellationToken);
    var emergencyContactsTouched = await ReEncryptEmergencyContactsAsync(batchSize, cancellationToken);
    var aadhaarRecordsTouched = await ReEncryptAadhaarRecordsAsync(batchSize, cancellationToken);

    return new DataEncryptionReEncryptionSummary(
      encryptionService.ActiveKeyId,
      usersTouched,
      emergencyContactsTouched,
      aadhaarRecordsTouched);
  }

  private async Task<int> ReEncryptUsersAsync(int batchSize, CancellationToken cancellationToken)
  {
    Guid? cursor = null;
    var touched = 0;

    while (true)
    {
      var batch = await dbContext.Users
        .OrderBy(x => x.Id)
        .Where(x => !cursor.HasValue || x.Id.CompareTo(cursor.Value) > 0)
        .Take(batchSize)
        .ToListAsync(cancellationToken);

      if (batch.Count == 0)
      {
        return touched;
      }

      foreach (var user in batch)
      {
        MarkUserEncryptedFieldsModified(user);
      }

      await dbContext.SaveChangesAsync(cancellationToken);
      touched += batch.Count;
      cursor = batch[^1].Id;
      dbContext.ChangeTracker.Clear();
    }
  }

  private async Task<int> ReEncryptEmergencyContactsAsync(int batchSize, CancellationToken cancellationToken)
  {
    Guid? cursor = null;
    var touched = 0;

    while (true)
    {
      var batch = await dbContext.EmergencyContacts
        .OrderBy(x => x.Id)
        .Where(x => !cursor.HasValue || x.Id.CompareTo(cursor.Value) > 0)
        .Take(batchSize)
        .ToListAsync(cancellationToken);

      if (batch.Count == 0)
      {
        return touched;
      }

      foreach (var contact in batch)
      {
        MarkEmergencyContactEncryptedFieldsModified(contact);
      }

      await dbContext.SaveChangesAsync(cancellationToken);
      touched += batch.Count;
      cursor = batch[^1].Id;
      dbContext.ChangeTracker.Clear();
    }
  }

  private async Task<int> ReEncryptAadhaarRecordsAsync(int batchSize, CancellationToken cancellationToken)
  {
    Guid? cursor = null;
    var touched = 0;

    while (true)
    {
      var batch = await dbContext.AadhaarVaultRecords
        .OrderBy(x => x.Id)
        .Where(x => !cursor.HasValue || x.Id.CompareTo(cursor.Value) > 0)
        .Take(batchSize)
        .ToListAsync(cancellationToken);

      if (batch.Count == 0)
      {
        return touched;
      }

      foreach (var record in batch)
      {
        MarkAadhaarRecordEncryptedFieldsModified(record);
      }

      await dbContext.SaveChangesAsync(cancellationToken);
      touched += batch.Count;
      cursor = batch[^1].Id;
      dbContext.ChangeTracker.Clear();
    }
  }

  private void MarkUserEncryptedFieldsModified(User user)
  {
    var entry = dbContext.Entry(user);
    entry.Property(x => x.FirstName).IsModified = true;
    entry.Property(x => x.LastName).IsModified = true;
    entry.Property(x => x.Email).IsModified = true;
    entry.Property(x => x.Phone).IsModified = true;
    entry.Property(x => x.Address).IsModified = true;
    entry.Property(x => x.BloodGroup).IsModified = true;
  }

  private void MarkEmergencyContactEncryptedFieldsModified(EmergencyContact contact)
  {
    var entry = dbContext.Entry(contact);
    entry.Property(x => x.Name).IsModified = true;
    entry.Property(x => x.Phone).IsModified = true;
    entry.Property(x => x.Email).IsModified = true;
  }

  private void MarkAadhaarRecordEncryptedFieldsModified(AadhaarVaultRecord record)
  {
    var entry = dbContext.Entry(record);
    entry.Property(x => x.AadhaarNumber).IsModified = true;
    entry.Property(x => x.DemographicsJson).IsModified = true;
  }
}
