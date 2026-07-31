using DaftechCrm.Domain.Enums;
using DaftechCrm.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;

namespace DaftechCrm.Api.Extensions;

/// <summary>
/// Named policies layered on top of the three EmployeeRole values plus the
/// AccountType claim (Employee vs Client). Controllers reference these by
/// name via [Authorize(Policy = ...)] rather than repeating role lists.
/// </summary>
public static class AuthorizationPolicyNames
{
    /// <summary>Any authenticated account — employee or client.</summary>
    public const string AnyAccount = "AnyAccount";

    /// <summary>Any authenticated employee (Admin, ItSupport, or EmployeeTechnician), regardless of specific role.</summary>
    public const string AnyEmployee = "AnyEmployee";

    /// <summary>Admin role only — account management, reports, agreements, session activity.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Admin or IT Support — ticket intake/forwarding, client approval.</summary>
    public const string SupportStaff = "SupportStaff";

    /// <summary>An authenticated client account.</summary>
    public const string ClientOnly = "ClientOnly";

    public static void AddCrmAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AnyAccount, p => p.RequireAuthenticatedUser())
            .AddPolicy(AnyEmployee, p => p.RequireClaim(CrmClaimTypes.AccountType, SessionAccountType.Employee.ToString()))
            .AddPolicy(AdminOnly, p => p.RequireRole(EmployeeRole.Admin.ToString()))
            .AddPolicy(SupportStaff, p => p.RequireRole(EmployeeRole.Admin.ToString(), EmployeeRole.ItSupport.ToString()))
            .AddPolicy(ClientOnly, p => p.RequireClaim(CrmClaimTypes.AccountType, SessionAccountType.Client.ToString()));
    }
}
