namespace Aarogya.Domain.Entities;

public sealed class ConsentRecord : IAuditableEntity
{
  public Guid Id { get; set; }

  public Guid UserId { get; set; }

  public string Purpose { get; set; } = string.Empty;

  public bool IsGranted { get; set; }

  public string Source { get; set; } = "api";

  public DateTimeOffset OccurredAt { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; }

  public User User { get; set; } = null!;
}
