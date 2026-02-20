using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aarogya.Api.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class CoreApiIntegrationTests(ApiPostgreSqlWebApplicationFactory factory) : IClassFixture<ApiPostgreSqlWebApplicationFactory>
{
  [Fact]
  public async Task ProtectedEndpoint_ShouldReturnUnauthorized_WhenTokenMissingAsync()
  {
    using var client = factory.CreateClient();

    var response = await client.GetAsync(Relative("/api/v1/users/me"));

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Theory]
  [InlineData("invalid-token")]
  [InlineData("expired-token")]
  public async Task ProtectedEndpoint_ShouldReturnUnauthorized_WhenTokenInvalidOrExpiredAsync(string token)
  {
    using var client = factory.CreateClient();
    SetBearer(client, token);

    var response = await client.GetAsync(Relative("/api/v1/users/me"));

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task UsersMe_ShouldRequireConsent_ThenReturnProfileAsync()
  {
    using var client = factory.CreateClient();
    SetBearer(client, "valid-patient");

    await factory.GrantConsentAsync("seed-PATIENT-IT", "profile_management");

    var success = await client.GetAsync(Relative("/api/v1/users/me"));
    success.StatusCode.Should().Be(HttpStatusCode.OK);

    using var payload = JsonDocument.Parse(await success.Content.ReadAsStringAsync());
    payload.RootElement.GetProperty("sub").GetString().Should().Be("seed-PATIENT-IT");
  }

  [Fact]
  public async Task EmergencyContacts_ShouldEnforceRbac_ForDoctorAsync()
  {
    using var client = factory.CreateClient();
    SetBearer(client, "valid-doctor");

    var response = await client.GetAsync(Relative("/api/v1/emergency-contacts"));

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task EmergencyContactCrud_ShouldWork_ForPatientWithConsentAsync()
  {
    using var client = factory.CreateClient();
    SetBearer(client, "valid-patient");

    await factory.GrantConsentAsync("seed-PATIENT-IT", "emergency_contact_management");

    var createResponse = await client.PostAsJsonAsync(
      Relative("/api/v1/emergency-contacts"),
      new
      {
        name = "Kin One",
        phoneNumber = "+919876543210",
        relationship = "brother",
        email = "kin.one@integration.dev"
      });
    createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    using var createdPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
    var contactId = createdPayload.RootElement.GetProperty("contactId").GetGuid();

    var listResponse = await client.GetAsync(Relative("/api/v1/emergency-contacts"));
    listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    using var listedPayload = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
    listedPayload.RootElement.EnumerateArray().Any(x => x.GetProperty("contactId").GetGuid() == contactId).Should().BeTrue();

    var deleteResponse = await client.DeleteAsync(Relative($"/api/v1/emergency-contacts/{contactId}"));
    deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task AccessGrantFlow_ShouldAllowPatientCreateAndDoctorReadReceivedAsync()
  {
    using var patientClient = factory.CreateClient();
    SetBearer(patientClient, "valid-patient");

    await factory.GrantConsentAsync("seed-PATIENT-IT", "medical_data_sharing");

    var createGrantResponse = await patientClient.PostAsJsonAsync(
      Relative("/api/v1/access-grants"),
      new
      {
        doctorSub = "seed-DOCTOR-IT",
        allReports = true,
        reportIds = Array.Empty<Guid>(),
        purpose = "integration-care",
        expiresAt = DateTimeOffset.UtcNow.AddDays(10)
      });
    createGrantResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    using var doctorClient = factory.CreateClient();
    SetBearer(doctorClient, "valid-doctor");

    await factory.GrantConsentAsync("seed-DOCTOR-IT", "medical_data_sharing");

    var receivedResponse = await doctorClient.GetAsync(Relative("/api/v1/access-grants/received"));
    receivedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    using var receivedPayload = JsonDocument.Parse(await receivedResponse.Content.ReadAsStringAsync());
    receivedPayload.RootElement.EnumerateArray().Any(x => x.GetProperty("doctorSub").GetString() == "seed-DOCTOR-IT").Should().BeTrue();
  }

  [Fact]
  public async Task AccessGrantEndpoint_ShouldRejectDoctorForPatientOnlyRouteAsync()
  {
    using var client = factory.CreateClient();
    SetBearer(client, "valid-doctor");

    var response = await client.GetAsync(Relative("/api/v1/access-grants"));

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task ReportUploadFlow_ShouldCreateReport_WhenConsentGrantedAsync()
  {
    using var client = factory.CreateClient();
    SetBearer(client, "valid-patient");

    await factory.GrantConsentAsync("seed-PATIENT-IT", "medical_records_processing");

    using var form = new MultipartFormDataContent();
    var fileContent = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
    form.Add(fileContent, "file", "report.pdf");

    var uploadResponse = await client.PostAsync(Relative("/api/v1/reports/upload"), form);

    uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    using var payload = JsonDocument.Parse(await uploadResponse.Content.ReadAsStringAsync());
    payload.RootElement.GetProperty("reportId").GetGuid().Should().NotBe(Guid.Empty);
    payload.RootElement.GetProperty("objectKey").GetString().Should().NotBeNullOrWhiteSpace();
  }

  private static void SetBearer(HttpClient client, string token)
  {
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
  }

  private static Uri Relative(string path)
  {
    return new Uri(path, UriKind.Relative);
  }
}
