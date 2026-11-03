# Device Manager

A small full-stack app to manage devices (name, brand, state) behind authentication.

- **Backend:** .NET 9 Web API, Clean Architecture, SQL Server accessed with plain ADO.NET (no EF, Dapper or MediatR).
- **Frontend:** Angular 22 (standalone components, signals, zoneless) with Angular Material.
- **Auth:** short-lived JWT access token kept in memory, plus a rotating refresh token in an HttpOnly cookie, with reuse detection.

---

## Contents

- [Repository layout](#repository-layout)
- [Running locally](#running-locally)
- [Running the tests](#running-the-tests)
- [API](#api)
- [Business rules](#business-rules)
- [Architecture and decisions](#architecture-and-decisions)
- [Authentication design](#authentication-design)
- [Frontend](#frontend)
- [Trade-offs and next steps](#trade-offs-and-next-steps)

---

## Repository layout

```
├── backend/
│   ├── DeviceManagerMVC.slnx
│   ├── src/
│   │   ├── DeviceManagerMVC.Domain/          entities, business rules, repository interfaces
│   │   ├── DeviceManagerMVC.Application/     use cases (DeviceService, AuthService), DTOs, ports
│   │   ├── DeviceManagerMVC.Infrastructure/  ADO.NET repositories, JWT, password hashing
│   │   └── DeviceManagerMVC.Api/             controllers, error handling, composition root
│   └── tests/
│       ├── DeviceManagerMVC.UnitTests/         Domain + Application (no I/O)
│       └── DeviceManagerMVC.IntegrationTests/  Infrastructure + API against a real SQL Server
├── frontend/                Angular app
├── database/                idempotent SQL scripts (schema + seed)
├── docs/                    Insomnia collection for manual API testing
├── docker-compose.yml       SQL Server + one-shot script runner
└── .env.example
```

---

## Running locally

### Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 9.0 or later |
| Node.js | 22.22+ or 24.15+ (required by Angular 22) |
| Docker | Docker Desktop or any engine with Compose |

The HTTPS development certificate must be trusted (once per machine):

```bash
dotnet dev-certs https --trust
```

### 1. Database

```bash
cp .env.example .env          # sets the SQL Server sa password
docker compose up -d
```

- **`sqlserver`** starts SQL Server 2022 on `localhost:1433`.
- **`db-init`** waits until SQL Server is healthy, runs `database/*.sql` in order (create database, tables, seed 8 devices), then exits with code 0. That `Exited (0)` status is expected.

The scripts are idempotent, so running `docker compose up -d` again is safe. Use `docker compose down -v` to delete the data.

### 2. API

The API listens on **https://localhost:7174**, and its OpenAPI document is at `/openapi/v1.json`. In Development the connection string and a development-only JWT signing key come from `appsettings.Development.json`.

### 3. Create a user

There is no sign-up screen, and the seed creates devices but no users, so create the first user through the API:

```bash
# bash / Git Bash
curl -k -X POST https://localhost:7174/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"demo@example.com","password":"Passw0rd!"}'
```

```powershell
# PowerShell
Invoke-RestMethod -Method Post -Uri https://localhost:7174/api/auth/register `
  -ContentType 'application/json' -Body '{"email":"demo@example.com","password":"Passw0rd!"}'
```

You can also run **Auth → Register** from the Insomnia collection (see [Manual API testing](#manual-api-testing)). Passwords need at least 8 characters.

### 4. Frontend

```bash
cd frontend
npm install
npm start
```

Open **http://localhost:4200** and sign in with the user from step 3.

The Angular dev server proxies `/api/*` to `https://localhost:7174` (`frontend/proxy.conf.json`), so the browser sees a single origin. The section [Why a dev-server proxy](#why-a-dev-server-proxy) explains why.

### Manual API testing

Import `docs/DeviceManager.insomnia.json` into Insomnia. It includes every endpoint, and the access token from **Login** is passed to the device requests automatically. Insomnia's cookie jar handles the refresh cookie. If Insomnia rejects the development certificate, turn off *Preferences → General → Validate certificates* (localhost only).

---

## Running the tests

```bash
# Backend: unit + integration (integration tests need Docker; they start their own SQL Server container)
cd backend
dotnet test DeviceManagerMVC.slnx

# Frontend: Vitest
cd frontend
npm test -- --watch=false
```

Stop the API before running the backend tests: a running API locks the build output.

| Suite | Covers | Tools |
|---|---|---|
| `UnitTests` | Domain entities and application services, with no I/O | xUnit, Shouldly, NSubstitute, FakeTimeProvider |
| `IntegrationTests` | ADO.NET repositories and the full HTTP pipeline, against the real schema | Testcontainers (SQL Server 2022), WebApplicationFactory |
| Frontend | services, interceptor, guard, components | Vitest, Angular TestBed, HttpTestingController, Material component harnesses |

The integration tests run the same `database/*.sql` scripts as Docker Compose, so tests and local runs share one schema.

---

## API

Every error response is `application/problem+json` (ProblemDetails).

### Auth (`/api/auth`)

| Method | Route | Success | Errors |
|---|---|---|---|
| POST | `/register` | 201 with the user | 400 invalid email / password under 8 characters, 409 email already registered |
| POST | `/login` | 200 `{ accessToken, expiresAtUtc }` and sets the refresh cookie | 401 invalid credentials |
| POST | `/refresh` | 200 with a new access token; rotates the refresh cookie | 401 missing, expired, revoked or reused token |
| POST | `/logout` | 204; revokes the session and clears the cookie | none (always 204) |

### Devices (`/api/devices`, bearer token required)

| Method | Route | Success | Errors |
|---|---|---|---|
| GET | `/?brand=&state=` | 200 list ordered by id; both filters optional, exact match | 400 unknown state |
| GET | `/{id}` | 200 | 404 |
| POST | `/` | 201 with a `Location` header | 400 |
| PUT | `/{id}` | 200 (full replacement) | 400, 404, 409 |
| PATCH | `/{id}` | 200 (omitted or null fields keep their value) | 400, 404, 409 |
| DELETE | `/{id}` | 204 | 404, 409 |

A request without a valid token gets **401**. States are serialized as strings: `"Available"`, `"InUse"`, `"Inactive"`.

### Error mapping

| Status | When |
|---|---|
| 400 | validation failed (domain validation or request binding) |
| 401 | missing or invalid access token, invalid credentials, invalid refresh token |
| 404 | resource not found, including one deleted concurrently between read and write |
| 409 | business rule violated, email already registered |
| 500 | anything unexpected; generic body, no exception details |

---

## Business rules

All rules live in the domain entities. Controllers and repositories only translate between HTTP or SQL and the domain.

- **Name and brand are required**, trimmed, and at most 100 characters.
- **State** is one of `Available`, `InUse`, `Inactive`.
- **Name and brand cannot change while a device is `InUse`** (409). The state itself can change, so a device has to be released before it can be renamed.
- **A device that is `InUse` cannot be deleted** (409).
- **`CreatedAtUtc`** is set on creation and never changes.
- **`UpdatedAtUtc`** changes only when a value actually changes. Saving identical data is not an update.
- **Users:** the email is normalized (trimmed, lower-case) and unique, and the password needs at least 8 characters.

---

## Architecture and decisions

### Clean Architecture

```
Domain  ←  Application  ←  Infrastructure  ←  Api (composition root)
```

- **Domain** has no dependencies. It holds the entities (`Device`, `User`, `RefreshToken`), the rules, the domain exceptions and the repository interfaces.
- **Application** orchestrates the use cases: load, call the domain, persist. It defines the ports for technical concerns (`IPasswordHasher`, `IAccessTokenIssuer`, `IRefreshTokenGenerator`).
- **Infrastructure** implements the repositories and the ports.
- **Api** contains only the HTTP adapters, and wires everything together in `Program.cs`.

### Data access with plain ADO.NET

- Every query is parameterized with **typed and sized** `SqlParameter`s (for example, `NVarChar(100)` sized from the domain constants). That rules out SQL injection, and SQL Server caches one execution plan per query.
- Every call is async and takes a `CancellationToken`.
- Entities are rebuilt with `Rehydrate` factory methods. Those don't validate: they trust what was persisted, and they restore `DateTimeKind.Utc`, because `DATETIME2` comes back as `Unspecified`.
- `Update` and `Delete` return whether a row was affected, so a device deleted by another request between the read and the write gives a 404 instead of a silent no-op.
- The database also enforces its own constraints (`CHECK`, `UNIQUE`, foreign keys) as a second line of defense. An integration test guards against the C# maximum lengths and the column sizes drifting apart.

### Database scripts

The schema is plain SQL in `database/`, with no migration tool. The scripts are idempotent (`IF ... IS NULL`), and Docker Compose and the integration tests both run them.

### Time

Every timestamp comes from an injected `TimeProvider`, never from `DateTime.UtcNow`, so tests control the clock with `FakeTimeProvider`.

### Errors

The domain throws `DomainValidationException` (400) and `BusinessRuleViolationException` (409), and the application layer throws `NotFoundException` (404) and `UnauthorizedException` (401). A single `IExceptionHandler` maps these to ProblemDetails. Anything else falls through to a generic 500 without exception details. Programming errors use `ArgumentException` or `InvalidOperationException` on purpose, so they never turn into a 4xx.

### Workflow

The backend was built with strict TDD, one layer at a time: a failing test committed first (red), then the minimum implementation (green). The commit history shows those steps.

---

## Authentication design

| Piece | Choice | Why |
|---|---|---|
| Access token | JWT HS256, 15 minutes, `sub` / `email` / `jti` claims | short-lived, so a leaked token expires quickly |
| Where the SPA keeps it | in memory only (an Angular signal) | not readable from `localStorage` by an XSS payload |
| Refresh token | 64 random bytes, Base64Url | 512 bits of entropy; safe to put in a cookie without escaping |
| Refresh cookie | `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`, 7 days | JavaScript can't read it; never sent cross-site (CSRF); only sent to refresh and logout |
| Storage | only the SHA-256 hash of the refresh token | a database leak exposes no usable tokens |
| Rotation | every refresh issues a new token and revokes the old one | a stolen token is only valid until its next legitimate use |
| Reuse detection | presenting an already-rotated token revokes the whole token family | a replayed token logs both the attacker and the victim out |
| Passwords | `PasswordHasher` from `Microsoft.Extensions.Identity.Core` | PBKDF2 with a random salt, from a maintained library |
| Validation | issuer, audience, key, `HS256` only, 30-second clock skew | rejects forged tokens and other algorithms |

### Implementation details worth knowing

- **Rotation is a compare-and-set in one SQL transaction.** The `UPDATE ... WHERE RevokedAtUtc IS NULL` makes the second of two concurrent refreshes match zero rows. The API treats that as reuse.
- **Login gives the same error for an unknown email and a wrong password,** so it can't be used to find out which emails are registered.
- **The JWT signing key is not in `appsettings.json`.** Outside Development it must come from configuration (for example, the `Jwt__SigningKey` environment variable), and the API refuses to start without it.

### Frontend side

- **Interceptor:** adds `Authorization: Bearer ...` to API calls. On a 401 it refreshes once and retries the request. It leaves `/api/auth/*` alone, so a failed refresh can't trigger another refresh.
- **Shared refresh:** concurrent 401s share **one** refresh request. Two parallel refreshes would send the same cookie twice, and the API would read the second one as token reuse and end the session.
- **Guard:** after a page reload the in-memory token is gone, so the guard tries one refresh with the cookie before redirecting to `/login`. A reload therefore doesn't log the user out.

### Why a dev-server proxy

In development the SPA (`http://localhost:4200`) and the API (`https://localhost:7174`) are different origins, and because the scheme differs they are also different sites. Without a proxy, every call would need CORS with credentials, and the `SameSite=Strict` cookie would never be sent. The Angular dev server's proxy makes the browser see one origin, so the security settings stay strict and need no development-only exceptions.

In production the same "single origin" property should come from a reverse proxy in front of both, or from the API serving the built SPA.

---

## Frontend

```
frontend/src/app/
├── core/
│   ├── auth/       AuthService, auth interceptor, auth guard
│   └── http/       ProblemDetails → user-facing message
├── features/
│   ├── login/      login page
│   └── devices/    device model + service, list page, create/edit dialog
└── shared/
    └── confirm-dialog/
```

- **Login:** client-side validation, with distinct messages for invalid credentials and server errors.
- **Device list:** a Material table with brand and state filters, plus loading, empty and error states.
- **Create / edit:** a dialog form. When the API rejects a change (for example, renaming a device in use), its ProblemDetails message is shown and the dialog stays open.
- **Delete:** asks for confirmation first, and shows the API's 409 message for a device in use.
- **The business rules are not duplicated in the frontend.** It validates the shape of the input for convenience, and the API stays the single source of truth for the rules.

---

## Trade-offs and next steps

**Trade-offs taken knowingly:**
- **No optimistic concurrency** (no `ROWVERSION`): two simultaneous edits of the same device are last-write-wins.
- **No sign-up screen:** users are created through `POST /api/auth/register`.
- **Refresh token lifetime** is a 7-day constant, not configuration.
- **An access token stays valid until it expires (at most 15 minutes) after logout.** That's the usual cost of stateless JWTs, kept small by the short lifetime.

**Possible improvements:**
- Rate limiting on login and register (built into ASP.NET Core) against brute force.
- Validate the JWT signing key length at startup instead of on first use.
- Clear the refresh cookie when a refresh fails.
- Equalize login response time for unknown emails (verify against a dummy hash).
- Optimistic concurrency with `ROWVERSION` and `If-Match` / ETags.
- Production hosting: serve the SPA from the API (or a reverse proxy) to keep a single origin, and containerize the API and frontend in Docker Compose.
