using Aarogya.Api.Authorization;
using Aarogya.Api.Controllers;
using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Api.Features.V1.EmergencyContacts;
using Aarogya.Api.Features.V1.Reports;
using FluentValidation;

namespace Aarogya.Api.Validation;

internal sealed class OtpRequestCommandValidator : AbstractValidator<OtpRequestCommand>
{
  public OtpRequestCommandValidator()
  {
    RuleFor(x => x.PhoneNumber).MustBeIndianPhoneNumber();
  }
}

internal sealed class OtpVerifyCommandValidator : AbstractValidator<OtpVerifyCommand>
{
  public OtpVerifyCommandValidator()
  {
    RuleFor(x => x.PhoneNumber).MustBeIndianPhoneNumber();
    RuleFor(x => x.Otp)
      .NotEmpty()
      .Matches("^[0-9]{4,8}$");
  }
}

internal sealed class SocialAuthorizeCommandValidator : AbstractValidator<SocialAuthorizeCommand>
{
  public SocialAuthorizeCommandValidator()
  {
    RuleFor(x => x.Provider).NotEmpty().Must(BeSupportedProvider);
    RuleFor(x => x.RedirectUri).NotNull().Must(uri => uri.IsAbsoluteUri);
    RuleFor(x => x.CodeChallengeMethod)
      .Must(method => string.IsNullOrWhiteSpace(method) || string.Equals(method, "S256", StringComparison.OrdinalIgnoreCase))
      .WithMessage("CodeChallengeMethod must be S256 when provided.");
  }

  private static bool BeSupportedProvider(string provider)
    => provider.Trim().Equals("google", StringComparison.OrdinalIgnoreCase)
      || provider.Trim().Equals("apple", StringComparison.OrdinalIgnoreCase)
      || provider.Trim().Equals("facebook", StringComparison.OrdinalIgnoreCase);
}

internal sealed class SocialTokenCommandValidator : AbstractValidator<SocialTokenCommand>
{
  public SocialTokenCommandValidator()
  {
    RuleFor(x => x.Provider).NotEmpty();
    RuleFor(x => x.RedirectUri).NotNull().Must(uri => uri.IsAbsoluteUri);
    RuleFor(x => x.AuthorizationCode).NotEmpty();
  }
}

internal sealed class PkceAuthorizeCommandValidator : AbstractValidator<PkceAuthorizeCommand>
{
  public PkceAuthorizeCommandValidator()
  {
    RuleFor(x => x.ClientId).NotEmpty();
    RuleFor(x => x.RedirectUri).NotNull().Must(uri => uri.IsAbsoluteUri);
    RuleFor(x => x.CodeChallenge).NotEmpty();
    RuleFor(x => x.CodeChallengeMethod).Equal("S256").WithMessage("Only S256 is supported.");
    RuleFor(x => x.Platform)
      .NotEmpty()
      .Must(platform => platform.Equals("ios", StringComparison.OrdinalIgnoreCase)
        || platform.Equals("android", StringComparison.OrdinalIgnoreCase));
  }
}

internal sealed class PkceTokenCommandValidator : AbstractValidator<PkceTokenCommand>
{
  public PkceTokenCommandValidator()
  {
    RuleFor(x => x.ClientId).NotEmpty();
    RuleFor(x => x.RedirectUri).NotNull().Must(uri => uri.IsAbsoluteUri);
    RuleFor(x => x.AuthorizationCode).NotEmpty();
    RuleFor(x => x.CodeVerifier)
      .NotEmpty()
      .Matches("^[A-Za-z0-9\\-._~]{43,128}$");
  }
}

internal sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
  public RefreshTokenCommandValidator()
  {
    RuleFor(x => x.ClientId).NotEmpty();
    RuleFor(x => x.RefreshToken).NotEmpty();
  }
}

internal sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
  public RevokeTokenCommandValidator()
  {
    RuleFor(x => x.ClientId).NotEmpty();
    RuleFor(x => x.RefreshToken).NotEmpty();
  }
}

internal sealed class RoleAssignmentCommandValidator : AbstractValidator<RoleAssignmentCommand>
{
  public RoleAssignmentCommandValidator()
  {
    RuleFor(x => x.TargetUserSub).NotEmpty();
    RuleFor(x => x.Role)
      .NotEmpty()
      .Must(role => AarogyaRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase));
  }
}

internal sealed class ApiKeyIssueCommandValidator : AbstractValidator<ApiKeyIssueCommand>
{
  public ApiKeyIssueCommandValidator()
  {
    RuleFor(x => x.PartnerId).NotEmpty();
    RuleFor(x => x.PartnerName).NotEmpty();
  }
}

internal sealed class ApiKeyRotateCommandValidator : AbstractValidator<ApiKeyRotateCommand>
{
  public ApiKeyRotateCommandValidator()
  {
    RuleFor(x => x.KeyId).NotEmpty();
  }
}

internal sealed class CreateReportRequestValidator : AbstractValidator<CreateReportRequest>
{
  public CreateReportRequestValidator()
  {
    RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
  }
}

internal sealed class CreateAccessGrantRequestValidator : AbstractValidator<CreateAccessGrantRequest>
{
  public CreateAccessGrantRequestValidator()
  {
    RuleFor(x => x.DoctorSub).NotEmpty();
    RuleFor(x => x.ReportIds).NotNull().Must(ids => ids?.Count > 0).WithMessage("At least one report ID is required.");
    RuleFor(x => x.ExpiresAt).GreaterThan(DateTimeOffset.UtcNow);
  }
}

internal sealed class CreateEmergencyContactRequestValidator : AbstractValidator<CreateEmergencyContactRequest>
{
  public CreateEmergencyContactRequestValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    RuleFor(x => x.PhoneNumber).MustBeIndianPhoneNumber();
    RuleFor(x => x.Relationship).NotEmpty().MaximumLength(60);
  }
}

internal sealed class AadhaarNumberValidator : AbstractValidator<string>
{
  public AadhaarNumberValidator()
  {
    RuleFor(x => x).MustBeAadhaarNumber();
  }
}
