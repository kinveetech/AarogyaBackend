using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Authorization;
using Aarogya.Api.Features.V1.Common;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Api.RateLimiting;
using Aarogya.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aarogya.Api.Controllers.V1;

[ApiController]
[Route("api/v1/notifications")]
[Authorize(Policy = AarogyaPolicies.AnyRegisteredRole)]
[EnableRateLimiting(RateLimitPolicyNames.ApiV1)]
[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "ASP.NET Core controllers must be public to be discovered by the framework.")]
public sealed class NotificationsController(IPushNotificationService pushNotificationService) : ControllerBase
{
  [HttpGet("devices")]
  [ProducesResponseType(typeof(IReadOnlyList<DeviceTokenRegistrationResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> ListDevicesAsync(CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var devices = await pushNotificationService.ListRegisteredDevicesAsync(userSub, cancellationToken);
    return Ok(devices);
  }

  [HttpPost("devices")]
  [ProducesResponseType(typeof(DeviceTokenRegistrationResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> RegisterDeviceAsync(
    [FromBody] RegisterDeviceTokenRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var registered = await pushNotificationService.RegisterDeviceAsync(userSub, request, cancellationToken);
    return Created(new Uri($"/api/v1/notifications/devices/{Uri.EscapeDataString(registered.DeviceToken)}", UriKind.Relative), registered);
  }

  [HttpDelete("devices/{deviceToken}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeregisterDeviceAsync(
    string deviceToken,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var removed = await pushNotificationService.DeregisterDeviceAsync(
      userSub,
      Uri.UnescapeDataString(deviceToken),
      cancellationToken);

    return removed ? NoContent() : NotFound();
  }

  [HttpPost("test")]
  [ProducesResponseType(typeof(PushNotificationDeliveryResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> SendTestNotificationAsync(
    [FromBody] SendPushNotificationRequest request,
    CancellationToken cancellationToken)
  {
    var userSub = User.GetSubjectOrNull();
    if (userSub is null)
    {
      return Unauthorized();
    }

    var result = await pushNotificationService.SendToCurrentUserAsync(userSub, request, cancellationToken);
    return Ok(result);
  }
}
