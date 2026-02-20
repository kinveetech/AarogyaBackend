using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;

namespace Aarogya.Domain.Repositories;

public interface IUserRepository : IRepository<User>
{
  public Task<User?> GetByExternalAuthIdAsync(string externalAuthId, CancellationToken cancellationToken = default);

  public Task<User?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);

  public Task<User?> GetByAadhaarSha256Async(byte[] aadhaarSha256, CancellationToken cancellationToken = default);

  public Task<User?> GetFirstByRoleAsync(UserRole role, CancellationToken cancellationToken = default);
}
