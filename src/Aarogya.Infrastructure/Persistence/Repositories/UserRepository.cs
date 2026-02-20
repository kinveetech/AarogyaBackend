using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Aarogya.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AarogyaDbContext dbContext)
  : Repository<User>(dbContext), IUserRepository
{
  public Task<User?> GetByExternalAuthIdAsync(string externalAuthId, CancellationToken cancellationToken = default)
    => FirstOrDefaultAsync(new UserByExternalAuthIdSpecification(externalAuthId), cancellationToken);

  public async Task<User?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default)
  {
    var normalized = phone.Trim();
    return await dbContext.Users
      .AsNoTracking()
      .Where(user => user.Role == UserRole.Patient)
      .FirstOrDefaultAsync(
        user => user.Phone != null
          && user.Phone.Trim() == normalized,
        cancellationToken);
  }

  public async Task<User?> GetByAadhaarSha256Async(byte[] aadhaarSha256, CancellationToken cancellationToken = default)
  {
    var patients = await dbContext.Users
      .AsNoTracking()
      .Where(user => user.Role == UserRole.Patient && user.AadhaarSha256 != null)
      .ToListAsync(cancellationToken);

    return patients.Find(user => user.AadhaarSha256!.AsSpan().SequenceEqual(aadhaarSha256));
  }

  public Task<User?> GetFirstByRoleAsync(UserRole role, CancellationToken cancellationToken = default)
    => dbContext.Users
      .AsNoTracking()
      .Where(user => user.Role == role)
      .OrderBy(user => user.CreatedAt)
      .FirstOrDefaultAsync(cancellationToken);
}
