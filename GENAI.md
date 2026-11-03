# Generative AI — Task Management API

> **Exercise:** use a GenAI coding tool to generate a RESTful API for a simple task management system
> (CRUD on tasks with `title`, `description`, `status` and `due_date`, each task associated with a user),
> show the prompt and a representative sample of the output, and describe how the output was validated
> and corrected.s

| | |
|---|---|
| **Tool** | Claude Code |
| **Stack** | .NET 9, ASP.NET Core Web API (controllers), SQL Server, ADO.NET, xUnit |
| **Approach** | one detailed prompt → review the plan → generate → review the code like a pull request → fix with tests first |

---

## 1. The prompt

```text
You are a senior .NET engineer. Build a RESTful API for a simple task management system, from scratch.

## Stack and architecture
- .NET 9, ASP.NET Core Web API with controllers, SQL Server.
- Clean Architecture in four projects: Domain <- Application <- Infrastructure <- Api
  (Api is the composition root).
- Data access with plain ADO.NET (Microsoft.Data.SqlClient). Do NOT use Entity Framework, Dapper
  or MediatR. Every value goes through a typed SqlParameter; every call is async and takes a
  CancellationToken.
- Use TimeProvider, never DateTime.Now / DateTime.UtcNow.
- Do not add any NuGet package that is not listed in your plan with the reason it is needed.

## Users and authentication
- A basic Users table already exists: Id (int identity), Email (unique), PasswordHash.
- Add JWT bearer authentication: POST /api/auth/login (email + password) returns an access token
  (HS256, 15 minutes) with the user id in the "sub" claim. Verify passwords with ASP.NET Core
  Identity's PasswordHasher. Unknown email and wrong password return the same 401.
- The signing key comes from configuration (user-secrets or environment variables), never from a
  committed file.
- Every /api/tasks endpoint requires a valid token.

## Task model
- Id (int identity), UserId (owner, FK to Users), Title, Description, Status, DueDate,
  CreatedAtUtc, UpdatedAtUtc.
- Title: required, trimmed, 1 to 200 characters.
- Description: optional, at most 2000 characters.
- Status: Todo, InProgress, Done. Serialized as strings in JSON. Unknown values are rejected.
- DueDate: optional calendar date (DateOnly). It cannot be in the past when a task is created.
- CreatedAtUtc is set on creation and never changes.

## Endpoints
- GET    /api/tasks?status=&dueBefore=&page=1&pageSize=20  -> 200, paged, pageSize at most 100
- GET    /api/tasks/{id}                                   -> 200 | 404
- POST   /api/tasks                                        -> 201 with Location | 400
- PUT    /api/tasks/{id}                                   -> 200 | 400 | 404
- DELETE /api/tasks/{id}                                   -> 204 | 404

## Security and ownership
- The owner always comes from the token, never from the request body or the query string.
- A user can only see or change their own tasks. Another user's task returns 404, not 403,
  so the API does not reveal which ids exist.
- Request DTOs must not contain Id, UserId or the timestamps (no over-posting).

## Errors
- Every error is ProblemDetails (RFC 7807): 400 validation, 401 missing/invalid token,
  404 not found, 500 with no exception details.
- Business rules and validation live in Domain/Application, not in controllers or repositories.

## Tests
- xUnit unit tests for the domain rules and the application service (NSubstitute for the ports).
- Integration tests for the repository against a real SQL Server (Testcontainers) and for the
  endpoints (WebApplicationFactory), including "user B cannot read, update or delete user A's task".

## Output
1. First, a short plan: projects, files, packages (with reasons) and every assumption you made.
   Stop and wait for my approval.
2. Then the SQL script for the Tasks table (constraints and indexes), the code project by project,
   and the tests.
3. End with a list of known limitations.
If a requirement is ambiguous, ask instead of guessing.
```

## 2. Output (representative sample)

### Plan (accepted)

- Projects: `TaskManager.Domain`, `TaskManager.Application`, `TaskManager.Infrastructure`, `TaskManager.Api`,
  `TaskManager.UnitTests`, `TaskManager.IntegrationTests`.
- Packages: `Microsoft.Data.SqlClient` (ADO.NET), `Microsoft.AspNetCore.Authentication.JwtBearer` (token validation),
  `Microsoft.Extensions.Identity.Core` (PasswordHasher), `xunit`, `NSubstitute`, `Shouldly`,
  `Testcontainers.MsSql`, `Microsoft.AspNetCore.Mvc.Testing`.
- Assumptions: due dates are date-only; `dueBefore` is exclusive; the list is ordered by due date, then id.

### SQL

```sql
CREATE TABLE dbo.Tasks
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tasks PRIMARY KEY,
    UserId       INT               NOT NULL CONSTRAINT FK_Tasks_Users REFERENCES dbo.Users (Id) ON DELETE CASCADE,
    Title        NVARCHAR(200)     NOT NULL,
    Description  NVARCHAR(2000)    NULL,
    Status       TINYINT           NOT NULL CONSTRAINT CK_Tasks_Status CHECK (Status IN (1, 2, 3)),
    DueDate      DATE              NULL,
    CreatedAtUtc DATETIME2(7)      NOT NULL,
    UpdatedAtUtc DATETIME2(7)      NOT NULL
);

CREATE INDEX IX_Tasks_UserId_DueDate ON dbo.Tasks (UserId, DueDate) INCLUDE (Status);
```

### Domain entity (as generated)

```csharp
public enum TaskStatus { Todo = 1, InProgress = 2, Done = 3 }

public sealed class TaskItem
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private TaskItem() { }

    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskStatus Status { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static TaskItem Create(int userId, string title, string? description, TaskStatus status,
        DateOnly? dueDate, TimeProvider clock)
    {
        var task = new TaskItem { UserId = userId, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
        task.Apply(title, description, status, dueDate, clock);
        return task;
    }

    public void Update(string title, string? description, TaskStatus status, DateOnly? dueDate, TimeProvider clock) =>
        Apply(title, description, status, dueDate, clock);

    private void Apply(string title, string? description, TaskStatus status, DateOnly? dueDate, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainValidationException("Title is required.");

        title = title.Trim();
        if (title.Length > TitleMaxLength)
            throw new DomainValidationException($"Title must be at most {TitleMaxLength} characters.");

        if (description?.Length > DescriptionMaxLength)
            throw new DomainValidationException($"Description must be at most {DescriptionMaxLength} characters.");

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (dueDate < today)
            throw new DomainValidationException("Due date cannot be in the past.");

        Title = title;
        Description = description;
        Status = status;
        DueDate = dueDate;
        UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
    }
}
```

### Repository — list query (as generated)

```csharp
public async Task<PagedResult<TaskItem>> ListAsync(int userId, TaskFilter filter, CancellationToken ct)
{
    var sql = new StringBuilder("""
        SELECT Id, UserId, Title, Description, Status, DueDate, CreatedAtUtc, UpdatedAtUtc,
               COUNT(*) OVER () AS Total
        FROM dbo.Tasks
        WHERE UserId = @UserId
        """);

    await using var connection = await _connectionFactory.OpenAsync(ct);
    await using var command = connection.CreateCommand();
    command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

    if (filter.Status is { } status)
    {
        sql.Append(" AND Status = @Status");
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)status;
    }

    if (filter.DueBefore is { } dueBefore)
    {
        sql.Append(" AND DueDate < @DueBefore");
        command.Parameters.Add("@DueBefore", SqlDbType.Date).Value = dueBefore.ToDateTime(TimeOnly.MinValue);
    }

    sql.Append(" ORDER BY DueDate, Id OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;");
    command.Parameters.Add("@Offset", SqlDbType.Int).Value = (filter.Page - 1) * filter.PageSize;
    command.Parameters.Add("@PageSize", SqlDbType.Int).Value = filter.PageSize;
    command.CommandText = sql.ToString();

    var items = new List<TaskItem>();
    var total = 0;
    await using var reader = await command.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
        items.Add(Map(reader));
        total = reader.GetInt32(8);
    }

    return new PagedResult<TaskItem>(items, total, filter.Page, filter.PageSize);
}
```

### Controller (as generated, excerpt)

```csharp
[ApiController]
[Authorize]
[Route("api/tasks")]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    [HttpGet]
    public async Task<ActionResult<PagedResponse<TaskResponse>>> List(
        [FromQuery] TaskStatus? status, [FromQuery] DateOnly? dueBefore,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await tasks.ListAsync(CurrentUserId,
            new TaskFilter(status, dueBefore, page, Math.Clamp(pageSize, 1, 100)), ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskResponse>> Get(int id, CancellationToken ct) =>
        Ok(await tasks.GetAsync(CurrentUserId, id, ct));

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var task = await tasks.CreateAsync(CurrentUserId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TaskResponse>> Update(int id, UpdateTaskRequest request, CancellationToken ct) =>
        Ok(await tasks.UpdateAsync(CurrentUserId, id, request, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await tasks.DeleteAsync(CurrentUserId, id, ct);
        return NoContent();
    }
}
```

### Authentication setup and configuration (as generated)

```csharp
var jwt = builder.Configuration.GetSection("Jwt");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwt["Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
    });

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

```json
// appsettings.json
"Jwt": {
  "Issuer": "task-manager-api",
  "Audience": "task-manager-clients",
  "Key": "super-secret-development-key-1234567890"
}
```

```csharp
// AuthService.LoginAsync (excerpt)
var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
if (user is null)
    throw new UnauthorizedException("Invalid email or password.");

if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
    throw new UnauthorizedException("Invalid email or password.");
```

### Ownership test (as generated)

```csharp
[Fact]
public async Task User_cannot_read_another_users_task()
{
    var alice = await _factory.CreateClientForUserAsync("alice@test.com");
    var bob = await _factory.CreateClientForUserAsync("bob@test.com");

    var created = await (await alice.PostAsJsonAsync("/api/tasks", new { title = "Alice's task" }))
        .Content.ReadFromJsonAsync<TaskResponse>();

    var response = await bob.GetAsync($"/api/tasks/{created!.Id}");

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
}
```

---

## 3. How the output was validated

The output is reviewed like a pull request from a new team member: compiling and passing its own tests is
not enough, because the tests are written by the same model that wrote the code.

1. **Plan review before any code.** Check the projects and dependency direction, the package list
   (no EF, Dapper or MediatR, nothing unexplained) and the stated assumptions.
2. **Build and tests.** Then read the tests to see whether they assert the rules from the prompt or only
   what the generated code happens to do.
3. **Line-by-line review against a checklist:**
   - SQL built only from fixed fragments, every value a typed parameter (no `AddWithValue`, no interpolation of input).
   - Ownership in the SQL itself (`WHERE Id = @Id AND UserId = @UserId`) for read, update and delete.
   - Request DTOs without `Id`, `UserId` or timestamps.
   - Boundary values: blank title, 200 and 201 characters, unknown status, yesterday and today as due date,
     `page=0`, `pageSize=0`, `pageSize=10000`, a page past the last one.
   - Dates: `DateOnly` for due dates, UTC timestamps, `TimeProvider`.
   - Authentication: how the user id is read from the token, where the key lives, what login reveals.
   - Hallucinations: APIs or packages that don't exist in .NET 9.
4. **Every finding gets a failing test first,** then the fix (red → green), so the correction is proven
   and cannot silently come back.

What the output got right: the dependency direction, typed parameters everywhere, ownership filtered in the SQL,
no over-posting in the DTOs, ProblemDetails for errors, and a real SQL Server in the integration tests.

---

## 4. What was corrected or improved

| # | Problem in the generated code | Risk | Correction |
|---|---|---|---|
| 1 | The enum is named `TaskStatus`, which collides with `System.Threading.Tasks.TaskStatus` (an implicit using in every project). | Ambiguous-reference compile errors in any file that also uses `Task`. | Renamed to `TaskItemStatus`. |
| 2 | The JWT signing key is committed in `appsettings.json`, although the prompt explicitly forbade it. | Anyone with read access to the repository can forge tokens. | Key removed from committed files, read from user-secrets / environment variables, and the API refuses to start if it is missing or shorter than 32 bytes. |
| 3 | `User.FindFirstValue("sub")` returns `null`: the JWT handler maps inbound `sub` to `ClaimTypes.NameIdentifier` by default. With the `!` and `int.Parse`, every authorized request fails with a 500. | The whole API is unusable, and the generated tests did not catch it because they used a fake authentication handler. | `MapInboundClaims = false`, plus a `CurrentUser` accessor that returns 401 instead of throwing. Added an integration test that uses a real token. |
| 4 | `JsonStringEnumConverter` accepts integers by default, so `"status": 99` binds, and the domain never checks the enum. The database `CHECK` constraint then throws. | An invalid input becomes a 500 instead of a 400. | `Enum.IsDefined` in the domain, and `new JsonStringEnumConverter(allowIntegerValues: false)`. |
| 5 | `page=0` or a negative page produces a negative `OFFSET`, which SQL Server rejects. `pageSize` is silently clamped. | 500 for a bad query string; a client bug is hidden instead of reported. | Validation in the application layer: `page >= 1` and `1 <= pageSize <= 100`, otherwise 400. |
| 6 | `COUNT(*) OVER ()` returns no rows for a page past the last one, so `total` comes back as 0. | The client thinks the list is empty and its pagination breaks. | A separate `COUNT(*)` with the same `WHERE`, in the same connection. |
| 7 | The "due date cannot be in the past" rule also runs on update. | An overdue task can't be edited at all, not even to mark it as `Done`. | The rule runs on create, and on update only when the due date changes. |
| 8 | "Today" is the UTC date. At 22:00 in São Paulo (UTC-3) it is already tomorrow in UTC. | A user can't create a task due today in their own time zone. | Accept any due date on or after the earliest current date on Earth (UTC date minus one day). Documented as a deliberate trade-off; the alternative is to receive the client's time zone. |
| 9 | Ownership was tested only for `GET`. | An update or delete without the `UserId` filter would pass the test suite. | A theory covering `GET`, `PUT` and `DELETE` on another user's task (all 404), plus a test that the list never includes other users' tasks. |
| 10 | Login returns before hashing when the email is unknown, so that path is much faster. | Response time reveals which emails are registered, even with the same message. | Verify against a fixed dummy hash when the user does not exist, so both paths cost the same. |
| 11 | `ORDER BY DueDate` puts tasks without a due date first (SQL Server sorts `NULL` first). | The most urgent tasks are pushed down the list. | `ORDER BY CASE WHEN DueDate IS NULL THEN 1 ELSE 0 END, DueDate, Id`. |

### Corrected code (main changes)

```csharp
public enum TaskItemStatus { Todo = 1, InProgress = 2, Done = 3 }

public static TaskItem Create(int userId, string title, string? description, TaskItemStatus status,
    DateOnly? dueDate, TimeProvider clock)
{
    EnsureNotInPast(dueDate, clock);
    var now = clock.GetUtcNow().UtcDateTime;
    var task = new TaskItem { UserId = userId, CreatedAtUtc = now };
    task.Apply(title, description, status, dueDate, now);
    return task;
}

public void Update(string title, string? description, TaskItemStatus status, DateOnly? dueDate, TimeProvider clock)
{
    // An overdue task must stay editable; the rule only applies to a new due date.
    if (dueDate != DueDate)
    {
        EnsureNotInPast(dueDate, clock);
    }

    Apply(title, description, status, dueDate, clock.GetUtcNow().UtcDateTime);
}

private static void EnsureNotInPast(DateOnly? dueDate, TimeProvider clock)
{
    // The earliest time zone (UTC-12) is at most one day behind UTC.
    var earliestToday = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-1);
    if (dueDate < earliestToday)
        throw new DomainValidationException("Due date cannot be in the past.");
}

private void Apply(string title, string? description, TaskItemStatus status, DateOnly? dueDate, DateTime nowUtc)
{
    // ... title / description validation unchanged ...
    if (!Enum.IsDefined(status))
        throw new DomainValidationException($"Status '{(int)status}' is not valid.");
    // ... assignments ...
}
```

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep "sub" as "sub"
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(jwt.KeyBytes), // validated at startup: >= 32 bytes
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
```

```csharp
[Theory]
[InlineData("GET")]
[InlineData("PUT")]
[InlineData("DELETE")]
public async Task Another_users_task_returns_404(string method)
{
    var (alice, bob) = (await _factory.ClientForAsync("alice@test.com"), await _factory.ClientForAsync("bob@test.com"));
    var task = await alice.CreateTaskAsync("Alice's task");

    var request = new HttpRequestMessage(new HttpMethod(method), $"/api/tasks/{task.Id}");
    if (method == "PUT")
        request.Content = JsonContent.Create(new { title = "Hijacked", status = "Done" });

    var response = await bob.SendAsync(request);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    (await alice.GetTaskAsync(task.Id)).Title.ShouldBe("Alice's task"); // unchanged
}
```

---

## 5. Edge cases, authentication and validation

| Area | Final behavior |
|---|---|
| **Authentication** | JWT HS256, 15 minutes, `sub` = user id. Issuer, audience, key and algorithm are validated, with a 30-second clock skew. The key comes from secrets and is checked at startup. Missing or invalid token → 401 ProblemDetails. |
| **Login** | Same 401 message and similar response time for an unknown email and a wrong password. Passwords hashed with PBKDF2 (`PasswordHasher`). |
| **Ownership** | The user id comes only from the token. Every query filters by `UserId` in the SQL. Another user's task → 404. |
| **Over-posting** | Request DTOs only carry `title`, `description`, `status` and `dueDate`. |
| **Title / description** | Title required, trimmed, at most 200 characters. Description at most 2000. Enforced in the domain and again by the column sizes. |
| **Status** | Only `Todo`, `InProgress` or `Done`, as strings. Integers and unknown values → 400. |
| **Due date** | Date-only. Not in the past on create (with a one-day margin for time zones). On update, checked only when it changes, so overdue tasks stay editable. |
| **Pagination** | `page >= 1`, `1 <= pageSize <= 100`, otherwise 400. Correct total even past the last page. Tasks without a due date are listed last. |
| **Not found / concurrency** | Update and delete check the affected row count, so a task deleted in between returns 404. |
| **Errors** | Everything is ProblemDetails. 500 responses carry no exception details. |

---

## 6. Takeaways

- **The prompt removes most problems before they happen.** Explicit constraints, rules and status codes meant
  the output had the right shape. The remaining problems were in the details the model had to decide by itself:
  framework defaults (claim mapping, enum binding), boundary values and time zones.
- **AI-generated tests share the blind spots of AI-generated code.** Finding 3 passed the generated tests because
  they used a fake authentication handler. Tests that hit the real pipeline, and tests written from the
  requirements rather than from the code, are what caught the gaps.
- **A model can ignore an explicit instruction** (finding 2). Every hard constraint in the prompt must also be
  checked in the review.
