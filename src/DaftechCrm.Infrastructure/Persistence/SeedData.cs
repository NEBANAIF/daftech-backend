using DaftechCrm.Application.Services;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Infrastructure.Persistence;

/// <summary>
/// Deterministic seed data — same fixed Guids/dates every run so migrations
/// stay reproducible. Mirrors src/app/core/mock-data.ts on the Angular side
/// so a fresh dev database tells the same demo story as the frontend mocks.
///
/// Seeded accounts use known dev credentials (documented in the backend
/// README) so the demo can be logged into immediately without depending on
/// SMTP being configured. In a real deployment, every account is created
/// through Employees/Clients registration instead, which issues a random
/// one-time password and emails it via MailKit.
/// </summary>
public static class SeedData
{
    public static readonly Guid Emp1Admin = Guid.Parse("11111111-0000-0000-0000-000000000001");
    public static readonly Guid Emp2ItSupport = Guid.Parse("11111111-0000-0000-0000-000000000002");
    public static readonly Guid Emp3Tech = Guid.Parse("11111111-0000-0000-0000-000000000003");
    public static readonly Guid Emp4TechDisabled = Guid.Parse("11111111-0000-0000-0000-000000000004");

    public static readonly Guid Client1 = Guid.Parse("22222222-0000-0000-0000-000000000001");
    public static readonly Guid Client2 = Guid.Parse("22222222-0000-0000-0000-000000000002");

    /// <summary>Dev-only known password for every seeded account. Never used outside seed data.</summary>
    public const string SeedPassword = "DaftechDemo1!";

    public static IEnumerable<Employee> Employees()
    {
        yield return new Employee
        {
            Id = Emp1Admin, FullName = "Nahom Alehegne", Email = "nahom@daftech.et", PhoneNumber = "+251911000001",
            Specialization = "Back-end",
            Roles = new() { EmployeeRole.Admin }, AccountStatus = EmployeeAccountStatus.Active,
            AllowedIpAddresses = new() { "196.188.20.10" },
            Username = "na1001", PasswordHash = PasswordHasher.Hash(SeedPassword), MustChangePassword = false,
        };
        yield return new Employee
        {
            Id = Emp2ItSupport, FullName = "Nebil Sherefa", Email = "nebil@daftech.et", PhoneNumber = "+251911000002",
            Specialization = "Front-end",
            Roles = new() { EmployeeRole.ItSupport }, AccountStatus = EmployeeAccountStatus.Active,
            AllowedIpAddresses = new(),
            Username = "ns1002", PasswordHash = PasswordHasher.Hash(SeedPassword), MustChangePassword = false,
        };
        yield return new Employee
        {
            Id = Emp3Tech, FullName = "Mekdes Fikru", Email = "mekdes@daftech.et", PhoneNumber = "+251911000003",
            Specialization = "Database",
            Roles = new() { EmployeeRole.EmployeeTechnician }, AccountStatus = EmployeeAccountStatus.Active,
            AllowedIpAddresses = new() { "196.188.20.15", "196.188.20.16" },
            Username = "mf1003", PasswordHash = PasswordHasher.Hash(SeedPassword), MustChangePassword = false,
        };
        yield return new Employee
        {
            Id = Emp4TechDisabled, FullName = "Robel Getachew", Email = "robel@daftech.et", PhoneNumber = "+251911000004",
            Specialization = "Back-end",
            Roles = new() { EmployeeRole.EmployeeTechnician }, AccountStatus = EmployeeAccountStatus.Disabled,
            AllowedIpAddresses = new(), DisabledAt = DateTimeOffset.Parse("2026-06-30T09:15:00Z"), DisabledReason = "Left the company",
            Username = "rg1004", PasswordHash = PasswordHasher.Hash(SeedPassword), MustChangePassword = false,
        };
    }

    public static IEnumerable<Client> Clients()
    {
        yield return new Client
        {
            Id = Client1, Name = "Abyssinia Traders PLC", IdNumber = "ID-88213", PhoneNumber = "+251911223344",
            Email = "contact@abyssiniatraders.et",
            Office = "Bole Head Office", Location = "Addis Ababa", KycType = "Business License",
            KycContact = "Selam Tesfaye — +251911998877", AccountStatus = ClientAccountStatus.Approved,
            OnboardingDate = DateOnly.Parse("2025-02-10"),
            Username = "at2001", PasswordHash = PasswordHasher.Hash(SeedPassword), MustChangePassword = false,
        };
        yield return new Client
        {
            Id = Client2, Name = "Merkato Micro-Finance", IdNumber = "ID-77012", PhoneNumber = "+251922334455",
            Email = "info@merkatomf.et",
            Office = "Merkato Branch", Location = "Addis Ababa", KycType = "Financial Institution License",
            KycContact = "Dawit Alemu — +251922112233", AccountStatus = ClientAccountStatus.Approved,
            OnboardingDate = DateOnly.Parse("2024-11-03"),
            Username = "mm2002", PasswordHash = PasswordHasher.Hash(SeedPassword), MustChangePassword = false,
        };
    }
}
