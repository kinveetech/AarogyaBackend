namespace Aarogya.Infrastructure.Security;

public interface IBlindIndexService
{
  public byte[]? Compute(string? value, string scope, bool normalize = true);
}
