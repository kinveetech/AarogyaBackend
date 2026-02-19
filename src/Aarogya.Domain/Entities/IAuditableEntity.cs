namespace Aarogya.Domain.Entities;

public interface IAuditableEntity
{
  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }
}
