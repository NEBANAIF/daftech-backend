# DAFTECH CRM — ASP.NET Core Backend

Clean Architecture / DDD-style ASP.NET Core 8 Web API over MySQL, matching
the layering used on the Trade License Workflow project:

```
DaftechCrm.Domain          — entities, enums, no dependencies
DaftechCrm.Application     — DTOs, service interfaces + implementations, business rules
DaftechCrm.Infrastructure  — EF Core (Pomelo MySQL provider), DI wiring
DaftechCrm.Api             — controllers, Program.cs, appsettings
```

## Prerequisites

- .NET 8 SDK
- A running MySQL server (8.x)
- `dotnet-ef` CLI tool: `dotnet tool install --global dotnet-ef`

This was built in a sandbox with no .NET SDK or network access, so it
hasn't been compiled or migration-generated here — you'll need to do both
locally. I did a careful manual review pass (namespaces, DTO field order,
LINQ translatability) instead of a real build; if something doesn't
compile, send me the error and I'll fix it directly.

## Setup

1. **Set your MySQL connection string.** Don't edit `appsettings.json`
   directly with your real password — use user-secrets instead:
   ```bash
   cd src/DaftechCrm.Api
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:MySql" "Server=localhost;Port=3306;Database=daftech_crm;User=root;Password=YOUR_PASSWORD;TreatTinyAsBoolean=true;"
   ```

   Do the same for your SMTP credentials (SRS v2.0 §4.3.1 — account credential emails via MailKit):
   ```bash
   dotnet user-secrets set "Smtp:Host" "smtp.yourprovider.com"
   dotnet user-secrets set "Smtp:Port" "587"
   dotnet user-secrets set "Smtp:Username" "your-smtp-username"
   dotnet user-secrets set "Smtp:Password" "your-smtp-password"
   dotnet user-secrets set "Smtp:FromAddress" "no-reply@daftech.et"
   ```

2. **Generate the initial migration** (no migrations are checked in yet —
   the `Migrations/` folder is empty on purpose):
   ```bash
   cd src/DaftechCrm.Api
   dotnet ef migrations add InitialCreate --project ../DaftechCrm.Infrastructure --startup-project .
   ```

3. **Run it** — this applies the migration and seeds baseline data
   (matching the Angular mock data) automatically on startup:
   ```bash
   dotnet run --project src/DaftechCrm.Api
   ```
   Swagger UI is available at `/swagger` in development.

## Logging in with seeded demo accounts

Every seeded account (see `Infrastructure/Persistence/SeedData.cs`) uses
the same known dev password so you can log in immediately without
registering anyone first:

| Username | Name | Role | Password |
|---|---|---|---|
| `na1001` | Nahom Alehegne | Admin | `DaftechDemo1!` |
| `ns1002` | Nebil Sherefa | IT Support | `DaftechDemo1!` |
| `mf1003` | Mekdes Fikru | Employee/Technician | `DaftechDemo1!` |
| `rg1004` | Robel Getachew | Employee/Technician (Disabled) | `DaftechDemo1!` |
| `at2001` | Abyssinia Traders PLC (client) | — | `DaftechDemo1!` |
| `mm2002` | Merkato Micro-Finance (client) | — | `DaftechDemo1!` |

None of these seeded accounts require a forced password change — that
flow only triggers for accounts created through the real Employees/Clients
registration screens, which issue a random one-time password instead.

## Account registration and credential issuance

There is no self-service staff signup. Every staff account (Admin, IT
Support, Employee/Technician) is created by an Admin through
`POST /api/employees`, and every client can either self-signup
(`POST /api/clients/signup`, still lands in `Pending` for approval) or be
registered directly by an Admin (`POST /api/clients/register`, Approved
immediately).

Either registration path:
1. Generates a username from the person's initials + random digits (e.g.
   `mf4821`), retrying on collision — see `AccountCredentialService`.
2. Generates a random one-time password and hashes it (PBKDF2-SHA256, see
   `PasswordHasher`) before storing — the plaintext is never persisted.
3. Emails the plaintext username + one-time password to the person via
   MailKit/SMTP (SRS v2.0 §4.3.1) — see `MailKitEmailSender`.
4. Returns the plaintext username + one-time password **once** in the
   registration response body regardless of whether the email sent, along
   with `EmailSent`/`EmailError` — if delivery failed, the Admin still has
   the plaintext to relay manually, or can retry via
   `POST /api/employees/{id}/resend-credential-email` /
   `POST /api/clients/{id}/resend-credential-email`, which generates a
   fresh one-time password (the old one is invalidated, never re-shown).
5. Sets `MustChangePassword = true` on the new account.

On that account's first login, `MustChangePassword: true` comes back in
the login response. The frontend routes straight to a forced
change-password screen — `POST /api/auth/employee/{id}/change-password`
or `POST /api/auth/client/{id}/change-password` — before anything else is
reachable. Both endpoints require the current password to match (so the
one-time password itself acts as proof of identity for this one change)
and enforce that the new password and confirmation match server-side, not
just in the UI.

The API is configured to accept CORS from `http://localhost:4200` (the
Angular dev server) — see `Program.cs`.

## SRS v2.0 changes (this pass)

Per the formal SRS v2.0 document, with explicit decisions on where it
conflicted with earlier direction:

- **OTP delivery is now real email**, not just an on-screen reveal (see
  above) — this supersedes the earlier privacy-driven "no email" decision.
- **Ticket assignment remains fully automatic with no Admin override.**
  The SRS suggested the Admin should be able to manually reassign after
  auto-assignment; that was explicitly rejected — assignment stays
  system-only, as originally specified.
- **Client confirmation now asks "is it fixed?" before rating.**
  `POST /api/tickets/{id}/confirm` takes `IsFixed` (bool) plus an optional
  `SatisfactionStars` (1-5, required only when `IsFixed` is true). A "No"
  answer reopens the ticket to the assigned employee (`InProgress`) with
  no rating recorded and does NOT go through the Escalated queue —
  Escalated is reserved for a "yes, fixed" answer paired with a low
  rating. See `TicketService.ConfirmResolutionAsync`.
- **Employee fields extended** to match SRS §4.4.1: `Employee.Name` /
  `ContactDetails` are now `FullName`, `Email`, `PhoneNumber`, and
  `Specialization` (Front-end / Back-end / Database, free text —
  extendable per the SRS's "extendable list" wording, not a closed enum).
- **Client gained an `Email` field**, required for credential delivery.

Not yet implemented from SRS v2.0 (flagged, not started): session/presence
tracking (online/offline status, last-seen timestamps beyond login IP
logging that already existed), AI-assisted narrative performance report
summaries, and PWA installability (manifest, service worker, offline
caching).

## What changed in this pass

### Ticket assignment is now fully automatic
There is no "Admin assigns ticket" endpoint. The moment IT Support forwards
a ticket (`POST /api/tickets/{id}/forward`), `TicketAssignmentService`
picks the Active `Employee/Technician` with the fewest currently-open
tickets (ties broken by whoever's gone longest without a new assignment)
and assigns it in the same request. See
`Application/Services/TicketAssignmentService.cs`.

### Client confirmation + satisfaction gating
The ticket lifecycle is now:

```
Submitted → Forwarded → Assigned (auto) → In Progress → Resolved
  → AwaitingClientConfirmation → { Closed | Escalated | Closed (auto) }
```

- An employee marking a ticket **Resolved** (`PATCH /api/tickets/{id}/status`)
  doesn't close it — it flips to `AwaitingClientConfirmation` and starts a
  configurable response window (default **5 days**).
- The client confirms via `POST /api/tickets/{id}/confirm` with a 1-5 star
  rating. The service converts stars to a 0-100 score (`stars * 20`):
  - **≥ 90** → ticket closes normally (`ClosureReason.ClientConfirmedSatisfied`).
  - **< 90** → ticket status becomes `Escalated` for Admin review — it does
    *not* go back to the employee automatically.
- If the client never responds, `AutoCloseTicketsHostedService` (a
  background service polling every 15 minutes) closes the ticket once the
  deadline passes, tagged `ClosureReason.AutoClosedNoResponse` — **no
  rating is recorded**, so it doesn't skew the employee's CSAT average.
- `GET /api/tickets/escalated` is the Admin's escalation review queue.
- Each employee's `EmployeeDto.AverageSatisfactionScore` is the average of
  `SatisfactionScore` across their rated tickets only (auto-closes are
  excluded) — this is what would feed the Performance page.

Both the threshold (90) and the response window (5 days) are configurable
in `appsettings.json` under `TicketWorkflow`, not hardcoded — change them
without a code change if the number needs to move.

### Employee IP capture + disable/offboarding (carried over from the Angular mock)
- `POST /api/auth/employee-login` resolves the caller's real IP address
  server-side (`HttpCurrentRequestContext`, preferring `X-Forwarded-For`
  behind a reverse proxy) and logs every attempt — successful or blocked —
  via `LoginRecord`.
- `POST /api/employees/{id}/disable` immediately revokes all of that
  employee's active device sessions and blocks further logins; historical
  tickets/maintenance/time-logs are untouched.

## Session / presence tracking (SRS v2.0 §4.8)

Every login (Employee or Client) opens a `LoginSession` row via
`ISessionService.OpenSessionAsync`. The frontend then pings
`POST /api/sessions/touch` roughly once a minute while the tab is open
(`SessionService.startHeartbeat` on the Angular side) to keep
`OnlineStatus` true and `LastSeen` current. Logging out calls
`POST /api/sessions/close`; if that never happens (closed tab, crashed
browser), `SessionSweepHostedService` runs every 2 minutes and flips any
session whose last heartbeat is older than `Session:OfflineAfterMinutes`
(default 5) back to offline — this is what keeps "online" from getting
stuck permanently true.

`GET /api/sessions/activity` is the Admin's Session Activity page: current
status, last-seen, and most recent IP per account, across both Employees
and Clients. `GET /api/sessions/history?accountType=&accountId=` returns
the full session history for one account.

This is distinct from the existing `LoginRecord` audit log (which records
every login *attempt*, including blocked ones, and never changes after
the fact) — `LoginSession` is the live, updatable presence record.

## AI-assisted narrative performance reports (SRS v2.0 §4.10, NFR-11)

`GET /api/reports/employee-performance/{employeeId}?includeAiNarrative=true`
returns the same written/graphical metrics either way (tickets assigned/
resolved, on-time rate, average resolution time, average satisfaction,
hours worked); the AI narrative is additive and never replaces them.

This is off by default — `AiReporting:Enabled` is `false` in
`appsettings.json`, and `IAiNarrativeReportService` short-circuits to
`Available: false` without making a network call if disabled or if
`AiReporting:ApiKey` is empty. To turn it on:

```bash
cd src/DaftechCrm.Api
dotnet user-secrets set "AiReporting:Enabled" "true"
dotnet user-secrets set "AiReporting:ApiKey" "sk-ant-..."
```

`AnthropicNarrativeReportService` calls the Anthropic Messages API
directly over `HttpClient` (no SDK dependency). Per NFR-11, any failure —
disabled, missing key, timeout, non-2xx response, unexpected response
shape — returns `Available: false` with a human-readable
`UnavailableReason` rather than throwing; the underlying metrics are
always returned regardless. The prompt only narrates numbers already
computed elsewhere — it's explicitly told not to invent figures.

## Progressive Web App (SRS v2.0 NEW requirement)

The Angular project (see its own README) is configured as an installable
PWA via `@angular/service-worker`, with **two manifest variants** — one
for the Admin/Staff app, one for the Client Portal — swapped at runtime
based on the active route, since the two are meant to install as separate
apps even though they share one deployment. This is a frontend-only
concern; nothing in this backend changes for it, beyond the API already
being safe to call from a service-worker-cached page (GETs are
cached-with-freshness-fallback, POSTs are never cached, so offline
support never risks stale writes).

## Talking to the Angular frontend

The Angular services call this API directly via `HttpClient`
(`src/app/core/services/*.ts`), matching the DTO shapes above field for
field. Nothing here still runs on mock data.
