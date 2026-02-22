using Aarogya.Api.Authentication;
using Aarogya.Api.Caching;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Api.Security;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class BreachDetectionHostedServiceTests
{
  [Fact]
  public async Task RunCycleAsync_ShouldNotifyUserAndAuthorities_WhenThresholdBreachedAsync()
  {
    var now = new DateTimeOffset(2026, 02, 21, 12, 0, 0, TimeSpan.Zero);
    var actorId = Guid.NewGuid();
    var actor = new User
    {
      Id = actorId,
      Role = UserRole.Doctor,
      ExternalAuthId = "seed-DOCTOR-1",
      FirstName = "Doc",
      LastName = "One",
      Email = "doctor.one@aarogya.dev"
    };

    var logs = Enumerable.Range(0, 12)
      .Select(index => new AuditLog
      {
        Id = Guid.NewGuid(),
        ActorUserId = actorId,
        ActorRole = UserRole.Doctor,
        Action = "report.viewed",
        EntityType = "report",
        EntityId = Guid.NewGuid(),
        OccurredAt = now.AddMinutes(-5).AddSeconds(index),
        ResultStatus = 200
      })
      .ToArray();

    var auditLogRepository = new Mock<IAuditLogRepository>();
    auditLogRepository
      .Setup(x => x.ListAsync(It.IsAny<ISpecification<AuditLog>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(logs);

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(actor);

    var cacheService = new FakeEntityCacheService();

    var emailSender = new FakeTransactionalEmailSender();
    var pushService = new Mock<IPushNotificationService>();

    var scopeFactory = CreateScopeFactory(
      auditLogRepository.Object,
      userRepository.Object,
      emailSender,
      pushService.Object,
      cacheService);

    var service = new BreachDetectionHostedService(
      scopeFactory,
      Options.Create(new BreachDetectionOptions
      {
        EnableWorker = true,
        LookbackWindowMinutes = 15,
        SuspiciousAccessThresholdPerActor = 10,
        BulkExportThresholdPerActor = 20,
        NotifyImpactedUsers = true,
        NotifyAuthorities = true,
        AuthorityEmails = ["security@aarogya.dev"]
      }),
      new FixedUtcClock(now),
      NullLogger<BreachDetectionHostedService>.Instance);

    await service.RunCycleAsync(CancellationToken.None);

    emailSender.Messages.Should().ContainSingle(message =>
      string.Equals(message.ToEmail, "doctor.one@aarogya.dev", StringComparison.OrdinalIgnoreCase)
      && message.Subject.Contains("Security Alert", StringComparison.OrdinalIgnoreCase));
    emailSender.Messages.Should().ContainSingle(message =>
      string.Equals(message.ToEmail, "security@aarogya.dev", StringComparison.OrdinalIgnoreCase)
      && message.Subject.Contains("Security Incident Alert", StringComparison.OrdinalIgnoreCase));
    pushService.Verify(
      x => x.SendToUserAsync(
        actor.ExternalAuthId!,
        "security_alert",
        It.IsAny<SendPushNotificationRequest>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
    cacheService.StoredKeys.Should().ContainSingle(key => key.StartsWith("security:breach-alert:", StringComparison.Ordinal));
  }

  [Fact]
  public async Task RunCycleAsync_ShouldSkipNotification_WhenDuplicateAlertKeyExistsAsync()
  {
    var now = new DateTimeOffset(2026, 02, 21, 12, 0, 0, TimeSpan.Zero);
    var actorId = Guid.NewGuid();
    var actor = new User
    {
      Id = actorId,
      Role = UserRole.Doctor,
      ExternalAuthId = "seed-DOCTOR-1",
      FirstName = "Doc",
      LastName = "One",
      Email = "doctor.one@aarogya.dev"
    };

    var logs = Enumerable.Range(0, 12)
      .Select(_ => new AuditLog
      {
        Id = Guid.NewGuid(),
        ActorUserId = actorId,
        ActorRole = UserRole.Doctor,
        Action = "report.viewed",
        EntityType = "report",
        EntityId = Guid.NewGuid(),
        OccurredAt = now.AddMinutes(-4),
        ResultStatus = 200
      })
      .ToArray();

    var auditLogRepository = new Mock<IAuditLogRepository>();
    auditLogRepository
      .Setup(x => x.ListAsync(It.IsAny<ISpecification<AuditLog>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(logs);

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByIdAsync(actorId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(actor);

    var cacheService = new FakeEntityCacheService();
    cacheService.AlwaysGetValue = "1";

    var emailSender = new FakeTransactionalEmailSender();
    var pushService = new Mock<IPushNotificationService>();

    var scopeFactory = CreateScopeFactory(
      auditLogRepository.Object,
      userRepository.Object,
      emailSender,
      pushService.Object,
      cacheService);

    var service = new BreachDetectionHostedService(
      scopeFactory,
      Options.Create(new BreachDetectionOptions
      {
        EnableWorker = true,
        LookbackWindowMinutes = 15,
        SuspiciousAccessThresholdPerActor = 10
      }),
      new FixedUtcClock(now),
      NullLogger<BreachDetectionHostedService>.Instance);

    await service.RunCycleAsync(CancellationToken.None);

    emailSender.Messages.Should().BeEmpty();
    pushService.Verify(
      x => x.SendToUserAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<SendPushNotificationRequest>(),
        It.IsAny<CancellationToken>()),
      Times.Never);
  }

  private static IServiceScopeFactory CreateScopeFactory(
    IAuditLogRepository auditLogRepository,
    IUserRepository userRepository,
    ITransactionalEmailSender emailSender,
    IPushNotificationService pushNotificationService,
    IEntityCacheService cacheService)
  {
    var services = new ServiceCollection();
    services.AddScoped(_ => auditLogRepository);
    services.AddScoped(_ => userRepository);
    services.AddScoped(_ => emailSender);
    services.AddScoped(_ => pushNotificationService);
    services.AddScoped(_ => cacheService);
    var provider = services.BuildServiceProvider();
    return provider.GetRequiredService<IServiceScopeFactory>();
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }

  private sealed class FakeEntityCacheService : IEntityCacheService
  {
    private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);
    public string? AlwaysGetValue { get; set; }

    public IReadOnlyCollection<string> StoredKeys => _entries.Keys.ToArray();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
      if (!string.IsNullOrWhiteSpace(AlwaysGetValue))
      {
        return Task.FromResult((T?)(object?)AlwaysGetValue);
      }

      if (_entries.TryGetValue(key, out var value))
      {
        return Task.FromResult((T?)(object?)value);
      }

      return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
      _entries[key] = value?.ToString() ?? string.Empty;
      return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
      _entries.Remove(key);
      return Task.CompletedTask;
    }

    public Task<string> GetNamespaceVersionAsync(string cacheNamespace, CancellationToken cancellationToken = default)
      => Task.FromResult("1");

    public Task BumpNamespaceVersionAsync(string cacheNamespace, CancellationToken cancellationToken = default)
      => Task.CompletedTask;
  }

  private sealed class FakeTransactionalEmailSender : ITransactionalEmailSender
  {
    public List<EmailMessage> Messages { get; } = [];

    public Task SendAsync(
      string toEmail,
      string? toName,
      string subject,
      string htmlBody,
      string textBody,
      CancellationToken cancellationToken = default)
    {
      Messages.Add(new EmailMessage(toEmail, toName, subject, htmlBody, textBody));
      return Task.CompletedTask;
    }
  }

  private sealed record EmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string HtmlBody,
    string TextBody);
}
