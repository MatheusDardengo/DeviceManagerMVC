# Device Manager — Ballast Lane take-home (Senior Full-Stack)

## Stack
- .NET 9 Web API (controllers), SQL Server, Angular 22
- Clean Architecture: Domain ← Application ← Infrastructure ← Api

## Hard rules (from the exercise)
- NO Entity Framework, NO Dapper, NO MediatR. Data access is pure ADO.NET (Microsoft.Data.SqlClient),
  always parameterized, async with CancellationToken.
- Business rules live in Domain/Application, never in controllers or repositories.
- Every layer must have tests.

## Tests
- backend/tests/DeviceManagerMVC.UnitTests → Domain + Application (xUnit, Shouldly, NSubstitute). No I/O.
- backend/tests/DeviceManagerMVC.IntegrationTests → Infrastructure + Api (Testcontainers.MsSql, WebApplicationFactory).
- Strict TDD: write the failing test first, STOP and let me run it and commit (red),
  then implement the minimum to pass, STOP again (green), then propose refactors.

## Domain decisions
- Device: Id (int identity), Name, Brand (required, max 100), State (Available=1, InUse=2, Inactive=3),
  CreatedAtUtc (set on creation, never changes), UpdatedAtUtc.
- Rules: name/brand cannot change while InUse; an InUse device cannot be deleted.
- Use TimeProvider instead of DateTime.UtcNow.
- Errors → ProblemDetails: 400 validation, 401, 404, 409 business rule/conflict, 500 without details.

## Auth decisions
- Access token: JWT HS256, 15 min, kept in memory by the SPA.
- Refresh token: random 64 bytes, HttpOnly/Secure/SameSite=Strict cookie on /api/auth,
  only SHA-256 hash stored, rotation on every refresh, reuse → revoke whole family.
- Passwords: PasswordHasher from Microsoft.Extensions.Identity.Core.

## Working agreement
- Work one step at a time; do not implement things that were not asked.
- Explain decisions briefly so I can defend them in the code review.
- Never run git commit; I commit myself.