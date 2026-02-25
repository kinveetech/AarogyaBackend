using Aarogya.Api.Controllers;
using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Api.Features.V1.EmergencyAccess;
using Aarogya.Api.Features.V1.EmergencyContacts;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Api.Features.V1.Users;
using Aarogya.Api.Validation;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests.Validation;

public sealed class RequestValidatorsTests
{
  #region OtpRequestCommandValidator

  [Fact]
  public void OtpRequestCommandValidator_ShouldReject_WhenPhoneIsEmpty()
  {
    var validator = new OtpRequestCommandValidator();
    var result = validator.Validate(new OtpRequestCommand(""));
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void OtpRequestCommandValidator_ShouldAccept_ValidIndianPhone()
  {
    var validator = new OtpRequestCommandValidator();
    var result = validator.Validate(new OtpRequestCommand("+919876543210"));
    result.IsValid.Should().BeTrue();
  }

  #endregion

  #region OtpVerifyCommandValidator

  [Fact]
  public void OtpVerifyCommandValidator_ShouldReject_WhenOtpIsEmpty()
  {
    var validator = new OtpVerifyCommandValidator();
    var result = validator.Validate(new OtpVerifyCommand("+919876543210", ""));
    result.IsValid.Should().BeFalse();
  }

  [Theory]
  [InlineData("1234")]
  [InlineData("12345678")]
  public void OtpVerifyCommandValidator_ShouldAccept_ValidOtp(string otp)
  {
    var validator = new OtpVerifyCommandValidator();
    var result = validator.Validate(new OtpVerifyCommand("+919876543210", otp));
    result.IsValid.Should().BeTrue();
  }

  [Theory]
  [InlineData("abc")]
  [InlineData("123")]
  [InlineData("123456789")]
  public void OtpVerifyCommandValidator_ShouldReject_InvalidOtpFormat(string otp)
  {
    var validator = new OtpVerifyCommandValidator();
    var result = validator.Validate(new OtpVerifyCommand("+919876543210", otp));
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region SocialAuthorizeCommandValidator

  [Theory]
  [InlineData("google")]
  [InlineData("apple")]
  [InlineData("facebook")]
  public void SocialAuthorizeCommandValidator_ShouldAccept_SupportedProviders(string provider)
  {
    var validator = new SocialAuthorizeCommandValidator();
    var command = new SocialAuthorizeCommand(provider, new Uri("https://example.com/callback"), null, null, null);
    var result = validator.Validate(command);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void SocialAuthorizeCommandValidator_ShouldReject_UnsupportedProvider()
  {
    var validator = new SocialAuthorizeCommandValidator();
    var command = new SocialAuthorizeCommand("github", new Uri("https://example.com/callback"), null, null, null);
    var result = validator.Validate(command);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void SocialAuthorizeCommandValidator_ShouldReject_RelativeRedirectUri()
  {
    var validator = new SocialAuthorizeCommandValidator();
    var command = new SocialAuthorizeCommand("google", new Uri("/callback", UriKind.Relative), null, null, null);
    var result = validator.Validate(command);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void SocialAuthorizeCommandValidator_ShouldAccept_S256CodeChallengeMethod()
  {
    var validator = new SocialAuthorizeCommandValidator();
    var command = new SocialAuthorizeCommand(
      "google", new Uri("https://example.com/callback"), null, "challenge", "S256");
    var result = validator.Validate(command);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void SocialAuthorizeCommandValidator_ShouldReject_NonS256CodeChallengeMethod()
  {
    var validator = new SocialAuthorizeCommandValidator();
    var command = new SocialAuthorizeCommand(
      "google", new Uri("https://example.com/callback"), null, "challenge", "plain");
    var result = validator.Validate(command);
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region PkceAuthorizeCommandValidator

  [Fact]
  public void PkceAuthorizeCommandValidator_ShouldAccept_ValidCommand()
  {
    var validator = new PkceAuthorizeCommandValidator();
    var command = new PkceAuthorizeCommand(
      "client-id", new Uri("https://example.com/callback"), "challenge", "S256", "ios", null, null);
    var result = validator.Validate(command);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void PkceAuthorizeCommandValidator_ShouldReject_NonS256Method()
  {
    var validator = new PkceAuthorizeCommandValidator();
    var command = new PkceAuthorizeCommand(
      "client-id", new Uri("https://example.com/callback"), "challenge", "plain", "ios", null, null);
    var result = validator.Validate(command);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void PkceAuthorizeCommandValidator_ShouldReject_UnsupportedPlatform()
  {
    var validator = new PkceAuthorizeCommandValidator();
    var command = new PkceAuthorizeCommand(
      "client-id", new Uri("https://example.com/callback"), "challenge", "S256", "windows", null, null);
    var result = validator.Validate(command);
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region PkceTokenCommandValidator

  [Fact]
  public void PkceTokenCommandValidator_ShouldAccept_ValidCommand()
  {
    var verifier = new string('A', 43);
    var validator = new PkceTokenCommandValidator();
    var command = new PkceTokenCommand(
      "client-id", new Uri("https://example.com/callback"), "auth-code", verifier);
    var result = validator.Validate(command);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void PkceTokenCommandValidator_ShouldReject_ShortCodeVerifier()
  {
    var validator = new PkceTokenCommandValidator();
    var command = new PkceTokenCommand(
      "client-id", new Uri("https://example.com/callback"), "auth-code", "short");
    var result = validator.Validate(command);
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region RoleAssignmentCommandValidator

  [Fact]
  public void RoleAssignmentCommandValidator_ShouldAccept_ValidRole()
  {
    var validator = new RoleAssignmentCommandValidator();
    var result = validator.Validate(new RoleAssignmentCommand("user-sub-123", "Doctor"));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void RoleAssignmentCommandValidator_ShouldReject_UnknownRole()
  {
    var validator = new RoleAssignmentCommandValidator();
    var result = validator.Validate(new RoleAssignmentCommand("user-sub-123", "SuperUser"));
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void RoleAssignmentCommandValidator_ShouldReject_EmptyTargetUserSub()
  {
    var validator = new RoleAssignmentCommandValidator();
    var result = validator.Validate(new RoleAssignmentCommand("", "Patient"));
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region CreateReportRequestValidator

  [Fact]
  public void CreateReportRequestValidator_ShouldAccept_ValidRequest()
  {
    var validator = new CreateReportRequestValidator();
    var request = new CreateReportRequest(
      ReportType: "blood_test",
      ObjectKey: "reports/test-file.pdf",
      LabName: "Apollo Lab",
      LabCode: null,
      CollectedAt: null,
      ReportedAt: null,
      Notes: null,
      PatientSub: null,
      Parameters: [new CreateReportParameterRequest("HB", "Hemoglobin", 14.5m, null, "g/dL", "12-16", false)]);
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void CreateReportRequestValidator_ShouldReject_InvalidReportType()
  {
    var validator = new CreateReportRequestValidator();
    var request = new CreateReportRequest(
      ReportType: "mri_scan",
      ObjectKey: "reports/test-file.pdf",
      LabName: "Apollo Lab",
      LabCode: null,
      CollectedAt: null,
      ReportedAt: null,
      Notes: null,
      PatientSub: null,
      Parameters: [new CreateReportParameterRequest("HB", "Hemoglobin", 14.5m, null, null, null, null)]);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void CreateReportRequestValidator_ShouldReject_ObjectKeyWithoutReportsPrefix()
  {
    var validator = new CreateReportRequestValidator();
    var request = new CreateReportRequest(
      ReportType: "blood_test",
      ObjectKey: "uploads/test-file.pdf",
      LabName: "Apollo Lab",
      LabCode: null,
      CollectedAt: null,
      ReportedAt: null,
      Notes: null,
      PatientSub: null,
      Parameters: [new CreateReportParameterRequest("HB", "Hemoglobin", 14.5m, null, null, null, null)]);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void CreateReportRequestValidator_ShouldReject_EmptyParameters()
  {
    var validator = new CreateReportRequestValidator();
    var request = new CreateReportRequest(
      ReportType: "blood_test",
      ObjectKey: "reports/test-file.pdf",
      LabName: "Apollo Lab",
      LabCode: null,
      CollectedAt: null,
      ReportedAt: null,
      Notes: null,
      PatientSub: null,
      Parameters: []);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region CreateReportParameterRequestValidator

  [Fact]
  public void CreateReportParameterRequestValidator_ShouldReject_WhenBothValueAndValueTextAreMissing()
  {
    var validator = new CreateReportParameterRequestValidator();
    var request = new CreateReportParameterRequest("HB", "Hemoglobin", null, null, null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void CreateReportParameterRequestValidator_ShouldAccept_WhenValueTextIsProvided()
  {
    var validator = new CreateReportParameterRequestValidator();
    var request = new CreateReportParameterRequest("HB", "Hemoglobin", null, "Normal", null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  #endregion

  #region ReportListQueryRequestValidator

  [Fact]
  public void ReportListQueryRequestValidator_ShouldAccept_DefaultValues()
  {
    var validator = new ReportListQueryRequestValidator();
    var result = validator.Validate(new ReportListQueryRequest());
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void ReportListQueryRequestValidator_ShouldReject_PageZero()
  {
    var validator = new ReportListQueryRequestValidator();
    var result = validator.Validate(new ReportListQueryRequest(Page: 0));
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void ReportListQueryRequestValidator_ShouldReject_PageSizeOver100()
  {
    var validator = new ReportListQueryRequestValidator();
    var result = validator.Validate(new ReportListQueryRequest(PageSize: 101));
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void ReportListQueryRequestValidator_ShouldReject_InvalidReportType()
  {
    var validator = new ReportListQueryRequestValidator();
    var result = validator.Validate(new ReportListQueryRequest(ReportType: "xray"));
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void ReportListQueryRequestValidator_ShouldReject_InvalidStatus()
  {
    var validator = new ReportListQueryRequestValidator();
    var result = validator.Validate(new ReportListQueryRequest(Status: "deleted"));
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region CreateReportUploadUrlRequestValidator

  [Fact]
  public void CreateReportUploadUrlRequestValidator_ShouldAccept_ValidPdfRequest()
  {
    var validator = new CreateReportUploadUrlRequestValidator();
    var result = validator.Validate(new CreateReportUploadUrlRequest("report.pdf", "application/pdf"));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void CreateReportUploadUrlRequestValidator_ShouldReject_UnsupportedContentType()
  {
    var validator = new CreateReportUploadUrlRequestValidator();
    var result = validator.Validate(new CreateReportUploadUrlRequest("report.txt", "text/plain"));
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void CreateReportUploadUrlRequestValidator_ShouldReject_ExpiryOver10080()
  {
    var validator = new CreateReportUploadUrlRequestValidator();
    var result = validator.Validate(new CreateReportUploadUrlRequest("report.pdf", "application/pdf", 10081));
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region CreateAccessGrantRequestValidator

  [Fact]
  public void CreateAccessGrantRequestValidator_ShouldAccept_AllReportsGrant()
  {
    var validator = new CreateAccessGrantRequestValidator();
    var request = new CreateAccessGrantRequest("doctor-sub", true, null, "Consultation");
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void CreateAccessGrantRequestValidator_ShouldAccept_SpecificReportIdsGrant()
  {
    var validator = new CreateAccessGrantRequestValidator();
    var request = new CreateAccessGrantRequest("doctor-sub", false, [Guid.NewGuid()], "Second opinion");
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void CreateAccessGrantRequestValidator_ShouldReject_WhenNoScopeProvided()
  {
    var validator = new CreateAccessGrantRequestValidator();
    var request = new CreateAccessGrantRequest("doctor-sub", false, null, "Consultation");
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void CreateAccessGrantRequestValidator_ShouldReject_EmptyDoctorSub()
  {
    var validator = new CreateAccessGrantRequestValidator();
    var request = new CreateAccessGrantRequest("", true, null, "Consultation");
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region CreateEmergencyContactRequestValidator

  [Fact]
  public void CreateEmergencyContactRequestValidator_ShouldAccept_ValidRequest()
  {
    var validator = new CreateEmergencyContactRequestValidator();
    var request = new CreateEmergencyContactRequest("John Doe", "+919876543210", "Brother");
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void CreateEmergencyContactRequestValidator_ShouldReject_InvalidPhone()
  {
    var validator = new CreateEmergencyContactRequestValidator();
    var request = new CreateEmergencyContactRequest("John Doe", "1234567890", "Brother");
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region UpdateUserProfileRequestValidator

  [Fact]
  public void UpdateUserProfileRequestValidator_ShouldAccept_ValidPartialUpdate()
  {
    var validator = new UpdateUserProfileRequestValidator();
    var request = new UpdateUserProfileRequest("John", null, null, null, null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void UpdateUserProfileRequestValidator_ShouldReject_WhenAllFieldsAreNull()
  {
    var validator = new UpdateUserProfileRequestValidator();
    var request = new UpdateUserProfileRequest(null, null, null, null, null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Theory]
  [InlineData("A+")]
  [InlineData("O-")]
  [InlineData("AB+")]
  public void UpdateUserProfileRequestValidator_ShouldAccept_ValidBloodGroups(string bloodGroup)
  {
    var validator = new UpdateUserProfileRequestValidator();
    var request = new UpdateUserProfileRequest(null, null, null, null, null, bloodGroup, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void UpdateUserProfileRequestValidator_ShouldReject_InvalidBloodGroup()
  {
    var validator = new UpdateUserProfileRequestValidator();
    var request = new UpdateUserProfileRequest(null, null, null, null, null, "X+", null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region DataDeletionRequestValidator

  [Fact]
  public void DataDeletionRequestValidator_ShouldAccept_WhenConfirmed()
  {
    var validator = new DataDeletionRequestValidator();
    var result = validator.Validate(new DataDeletionRequest(true));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void DataDeletionRequestValidator_ShouldReject_WhenNotConfirmed()
  {
    var validator = new DataDeletionRequestValidator();
    var result = validator.Validate(new DataDeletionRequest(false));
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region VerifyAadhaarRequestValidator

  [Fact]
  public void VerifyAadhaarRequestValidator_ShouldAccept_ValidAadhaar()
  {
    var validator = new VerifyAadhaarRequestValidator();
    var result = validator.Validate(new VerifyAadhaarRequest("234567890123"));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void VerifyAadhaarRequestValidator_ShouldReject_InvalidAadhaar()
  {
    var validator = new VerifyAadhaarRequestValidator();
    var result = validator.Validate(new VerifyAadhaarRequest("123456789012"));
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region RegisterDeviceTokenRequestValidator

  [Fact]
  public void RegisterDeviceTokenRequestValidator_ShouldAccept_ValidRequest()
  {
    var validator = new RegisterDeviceTokenRequestValidator();
    var request = new RegisterDeviceTokenRequest("fcm-token-abc", "ios");
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void RegisterDeviceTokenRequestValidator_ShouldReject_UnsupportedPlatform()
  {
    var validator = new RegisterDeviceTokenRequestValidator();
    var request = new RegisterDeviceTokenRequest("fcm-token-abc", "windows");
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region CreateEmergencyAccessRequestValidator

  [Fact]
  public void CreateEmergencyAccessRequestValidator_ShouldAccept_ValidRequest()
  {
    var validator = new CreateEmergencyAccessRequestValidator();
    var request = new CreateEmergencyAccessRequest(
      "patient-sub", "+919876543210", "doctor-sub", "Cardiac emergency");
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void CreateEmergencyAccessRequestValidator_ShouldReject_InvalidDurationHours()
  {
    var validator = new CreateEmergencyAccessRequestValidator();
    var request = new CreateEmergencyAccessRequest(
      "patient-sub", "+919876543210", "doctor-sub", "Emergency", 169);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void CreateEmergencyAccessRequestValidator_ShouldReject_InvalidEmergencyContactPhone()
  {
    var validator = new CreateEmergencyAccessRequestValidator();
    var request = new CreateEmergencyAccessRequest(
      "patient-sub", "1234567890", "doctor-sub", "Emergency");
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region EmergencyAccessAuditQueryRequestValidator

  [Fact]
  public void EmergencyAccessAuditQueryRequestValidator_ShouldAccept_DefaultValues()
  {
    var validator = new EmergencyAccessAuditQueryRequestValidator();
    var result = validator.Validate(new EmergencyAccessAuditQueryRequest());
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void EmergencyAccessAuditQueryRequestValidator_ShouldReject_PageSizeOver200()
  {
    var validator = new EmergencyAccessAuditQueryRequestValidator();
    var result = validator.Validate(new EmergencyAccessAuditQueryRequest(PageSize: 201));
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region RefreshTokenCommandValidator

  [Fact]
  public void RefreshTokenCommandValidator_ShouldAccept_ValidCommand()
  {
    var validator = new RefreshTokenCommandValidator();
    var result = validator.Validate(new RefreshTokenCommand("client-id", "refresh-token"));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void RefreshTokenCommandValidator_ShouldReject_EmptyRefreshToken()
  {
    var validator = new RefreshTokenCommandValidator();
    var result = validator.Validate(new RefreshTokenCommand("client-id", ""));
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region RegisterUserRequestValidator

  [Fact]
  public void RegisterUserRequestValidator_ShouldAccept_ValidPatientRequest()
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      "patient", "Test", "Patient", "test@aarogya.dev",
      "+919876543210", new DateOnly(1990, 1, 1), "male", "Pune", "O+",
      null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void RegisterUserRequestValidator_ShouldAccept_ValidDoctorRequest()
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      "doctor", "Test", "Doctor", "doc@aarogya.dev",
      null, null, null, null, null,
      new DoctorRegistrationData("MED-123", "Cardiology", "City Hospital", null),
      null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void RegisterUserRequestValidator_ShouldAccept_ValidLabTechnicianRequest()
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      "lab_technician", "Test", "Lab", "lab@aarogya.dev",
      null, null, null, null, null,
      null, new LabTechnicianRegistrationData("City Lab", null, null),
      null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Theory]
  [InlineData("")]
  [InlineData("admin")]
  [InlineData("superuser")]
  public void RegisterUserRequestValidator_ShouldReject_InvalidRole(string role)
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      role, "Test", "User", "test@aarogya.dev",
      null, null, null, null, null, null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void RegisterUserRequestValidator_ShouldReject_EmptyFirstName()
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      "patient", "", "Patient", "test@aarogya.dev",
      null, null, null, null, null, null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void RegisterUserRequestValidator_ShouldReject_InvalidEmail()
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      "patient", "Test", "Patient", "not-an-email",
      null, null, null, null, null, null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void RegisterUserRequestValidator_ShouldReject_DoctorWithoutDoctorData()
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      "doctor", "Test", "Doctor", "doc@aarogya.dev",
      null, null, null, null, null, null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void RegisterUserRequestValidator_ShouldReject_LabTechnicianWithoutLabData()
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      "lab_technician", "Test", "Lab", "lab@aarogya.dev",
      null, null, null, null, null, null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Theory]
  [InlineData("A+")]
  [InlineData("O-")]
  [InlineData("AB+")]
  public void RegisterUserRequestValidator_ShouldAccept_ValidBloodGroup(string bloodGroup)
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      "patient", "Test", "Patient", "test@aarogya.dev",
      null, null, null, null, bloodGroup, null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void RegisterUserRequestValidator_ShouldReject_InvalidBloodGroup()
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      "patient", "Test", "Patient", "test@aarogya.dev",
      null, null, null, null, "X+", null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeFalse();
  }

  [Theory]
  [InlineData("male")]
  [InlineData("female")]
  [InlineData("other")]
  public void RegisterUserRequestValidator_ShouldAccept_ValidGender(string gender)
  {
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest(
      "patient", "Test", "Patient", "test@aarogya.dev",
      null, null, gender, null, null, null, null, null);
    var result = validator.Validate(request);
    result.IsValid.Should().BeTrue();
  }

  #endregion

  #region RejectRegistrationRequestValidator

  [Fact]
  public void RejectRegistrationRequestValidator_ShouldAccept_ValidRequest()
  {
    var validator = new RejectRegistrationRequestValidator();
    var result = validator.Validate(new RejectRegistrationRequest("Missing credentials"));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void RejectRegistrationRequestValidator_ShouldReject_EmptyReason()
  {
    var validator = new RejectRegistrationRequestValidator();
    var result = validator.Validate(new RejectRegistrationRequest(""));
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void RejectRegistrationRequestValidator_ShouldReject_TooLongReason()
  {
    var validator = new RejectRegistrationRequestValidator();
    var result = validator.Validate(new RejectRegistrationRequest(new string('x', 501)));
    result.IsValid.Should().BeFalse();
  }

  #endregion

  #region ApproveRegistrationRequestValidator

  [Fact]
  public void ApproveRegistrationRequestValidator_ShouldAccept_NullNotes()
  {
    var validator = new ApproveRegistrationRequestValidator();
    var result = validator.Validate(new ApproveRegistrationRequest(null));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void ApproveRegistrationRequestValidator_ShouldAccept_ValidNotes()
  {
    var validator = new ApproveRegistrationRequestValidator();
    var result = validator.Validate(new ApproveRegistrationRequest("Approved after review"));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void ApproveRegistrationRequestValidator_ShouldReject_TooLongNotes()
  {
    var validator = new ApproveRegistrationRequestValidator();
    var result = validator.Validate(new ApproveRegistrationRequest(new string('x', 501)));
    result.IsValid.Should().BeFalse();
  }

  #endregion
}
