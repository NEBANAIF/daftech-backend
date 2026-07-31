# DAFTECH CRM — API

ASP.NET Core 10 Web API, Clean Architecture, PostgreSQL. Part of the
[DAFTECH CRM](../../README.md) system — see the repository root README for
system architecture, domain model, ticket lifecycle, API surface, and demo
accounts, and [DEPLOYMENT.md](../../DEPLOYMENT.md) for deployment.

```text
DaftechCrm.Domain          entities, enums — no dependencies
DaftechCrm.Application     DTOs, service interfaces + implementations, business rules
DaftechCrm.Infrastructure  EF Core (Npgsql/PostgreSQL), email, storage, DI wiring
DaftechCrm.Api             controllers, Program.cs, appsettings
```

## Local run

```bash
export DATABASE_URL="postgres://postgres:postgres@localhost:5432/daftech_crm"
dotnet run --project src/DaftechCrm.Api
```

Swagger UI: `/swagger`. Migrations and seed data apply automatically on
startup — nothing to run manually. See the root README for demo account
credentials and SMTP/user-secrets setup.

## Adding a migration

Migrations aren't checked in until needed for a schema change:

```bash
cd src/DaftechCrm.Api
dotnet ef migrations add <Name> --project ../DaftechCrm.Infrastructure --startup-project .
```
