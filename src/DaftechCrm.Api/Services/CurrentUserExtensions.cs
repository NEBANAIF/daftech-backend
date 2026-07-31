using System.Security.Claims;
using DaftechCrm.Domain.Enums;
using DaftechCrm.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Services;

/// <summary>
/// Reads the identity out of the validated JWT on ControllerBase.User —
/// this is what every controller should use instead of trusting a
/// clientId/employeeId supplied in the route or body, which the caller
/// could set to anyone else's id.
/// </summary>
public static class CurrentUserExtensions
{
    public static Guid? GetAccountId(this ControllerBase controller)
    {
        var raw = controller.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static SessionAccountType? GetAccountType(this ControllerBase controller)
    {
        var raw = controller.User.FindFirstValue(CrmClaimTypes.AccountType);
        return Enum.TryParse<SessionAccountType>(raw, out var type) ? type : null;
    }

    public static bool IsInRole(this ControllerBase controller, EmployeeRole role) =>
        controller.User.IsInRole(role.ToString());
}
