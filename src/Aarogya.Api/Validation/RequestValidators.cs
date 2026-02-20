using Aarogya.Api.Authorization;
using Aarogya.Api.Controllers;
using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Api.Features.V1.EmergencyContacts;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Api.Features.V1.Users;
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
  private static readonly string[] AllowedReportTypes =
  [
    "blood_test",
    "urine_test",
    "radiology",
    "cardiology",
    "other"
  ];

  public CreateReportRequestValidator()
  {
    RuleFor(x => x.ReportType)
      .NotEmpty()
      .Must(value => AllowedReportTypes.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
      .WithMessage("ReportType must be one of blood_test, urine_test, radiology, cardiology, other.");

    RuleFor(x => x.ObjectKey).NotEmpty().MaximumLength(1024).Must(value => value.Trim().StartsWith("reports/", StringComparison.Ordinal));

    RuleFor(x => x.LabName).NotEmpty().MaximumLength(200);
    RuleFor(x => x.LabCode).MaximumLength(100).When(x => x.LabCode is not null);
    RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
    RuleFor(x => x.PatientSub).NotEmpty().MaximumLength(200).When(x => x.PatientSub is not null);

    RuleFor(x => x.CollectedAt)
      .LessThanOrEqualTo(DateTimeOffset.UtcNow)
      .When(x => x.CollectedAt.HasValue);

    RuleFor(x => x.ReportedAt)
      .GreaterThanOrEqualTo(x => x.CollectedAt)
      .When(x => x.CollectedAt.HasValue && x.ReportedAt.HasValue);

    RuleFor(x => x.Parameters)
      .NotNull()
      .Must(parameters => parameters.Count > 0)
      .WithMessage("At least one parameter is required.");

    RuleForEach(x => x.Parameters).SetValidator(new CreateReportParameterRequestValidator());
  }
}

internal sealed class CreateReportParameterRequestValidator : AbstractValidator<CreateReportParameterRequest>
{
  public CreateReportParameterRequestValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    RuleFor(x => x.Unit).MaximumLength(50).When(x => x.Unit is not null);
    RuleFor(x => x.ReferenceRange).MaximumLength(200).When(x => x.ReferenceRange is not null);
    RuleFor(x => x.ValueText).MaximumLength(200).When(x => x.ValueText is not null);
    RuleFor(x => x).Must(HaveAnyValue).WithMessage("Either Value or ValueText must be provided.");
  }

  private static bool HaveAnyValue(CreateReportParameterRequest request)
    => request.Value.HasValue || !string.IsNullOrWhiteSpace(request.ValueText);
}

internal sealed class CreateReportUploadUrlRequestValidator : AbstractValidator<CreateReportUploadUrlRequest>
{
  private static readonly string[] AllowedContentTypes =
  [
    "application/pdf",
    "application/dicom",
    "image/jpeg",
    "image/png"
  ];

  public CreateReportUploadUrlRequestValidator()
  {
    RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
    RuleFor(x => x.ContentType)
      .NotEmpty()
      .Must(contentType => AllowedContentTypes.Contains(contentType.Trim(), StringComparer.OrdinalIgnoreCase));
    RuleFor(x => x.ExpiryMinutes).InclusiveBetween(1, 10080).When(x => x.ExpiryMinutes.HasValue);
  }
}

internal sealed class CreateReportDownloadUrlRequestValidator : AbstractValidator<CreateReportDownloadUrlRequest>
{
  public CreateReportDownloadUrlRequestValidator()
  {
    RuleFor(x => x.ObjectKey).NotEmpty().MaximumLength(1024);
    RuleFor(x => x.ExpiryMinutes).InclusiveBetween(1, 10080).When(x => x.ExpiryMinutes.HasValue);
  }
}

internal sealed class CreateVerifiedReportDownloadRequestValidator : AbstractValidator<CreateVerifiedReportDownloadRequest>
{
  public CreateVerifiedReportDownloadRequestValidator()
  {
    RuleFor(x => x.ReportId).NotEmpty();
    RuleFor(x => x.ExpiryMinutes).InclusiveBetween(1, 10080).When(x => x.ExpiryMinutes.HasValue);
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

internal sealed class UpdateUserProfileRequestValidator : AbstractValidator<UpdateUserProfileRequest>
{
  private static readonly DateOnly MinimumBirthDate = new(1900, 1, 1);
  private static readonly string[] AllowedBloodGroups =
  [
    "A+",
    "A-",
    "B+",
    "B-",
    "AB+",
    "AB-",
    "O+",
    "O-"
  ];

  public UpdateUserProfileRequestValidator()
  {
    RuleFor(x => x)
      .Must(HaveAtLeastOneField)
      .WithMessage("At least one profile field must be supplied for update.");

    RuleFor(x => x.FirstName)
      .Must(value => value is not null && !string.IsNullOrWhiteSpace(value))
      .WithMessage("First name cannot be empty.")
      .MaximumLength(120)
      .When(x => x.FirstName is not null);

    RuleFor(x => x.LastName)
      .Must(value => value is not null && !string.IsNullOrWhiteSpace(value))
      .WithMessage("Last name cannot be empty.")
      .MaximumLength(120)
      .When(x => x.LastName is not null);

    RuleFor(x => x.Email)
      .EmailAddress()
      .MaximumLength(256)
      .When(x => x.Email is not null);

    RuleFor(x => x.Phone)
      .MustBeIndianPhoneNumber()
      .When(x => x.Phone is not null);

    RuleFor(x => x.Address)
      .Must(value => value is not null && !string.IsNullOrWhiteSpace(value))
      .WithMessage("Address cannot be empty.")
      .MaximumLength(500)
      .When(x => x.Address is not null);

    RuleFor(x => x.BloodGroup)
      .Must(value => value is not null && AllowedBloodGroups.Contains(value.Trim().ToUpperInvariant(), StringComparer.Ordinal))
      .WithMessage("Blood group must be one of A+, A-, B+, B-, AB+, AB-, O+, O-.")
      .When(x => x.BloodGroup is not null);

    RuleFor(x => x.DateOfBirth)
      .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
      .GreaterThanOrEqualTo(MinimumBirthDate)
      .When(x => x.DateOfBirth.HasValue);
  }

  private static bool HaveAtLeastOneField(UpdateUserProfileRequest request)
  {
    return request.FirstName is not null
      || request.LastName is not null
      || request.Email is not null
      || request.Phone is not null
      || request.Address is not null
      || request.BloodGroup is not null
      || request.DateOfBirth.HasValue;
  }
}
