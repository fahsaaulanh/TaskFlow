# TaskFlow

TaskFlow is a shared Kanban board built with Angular, ASP.NET Core, and SQL Server.

## Local development

Install the .NET 10 SDK, Node.js 24.15 or newer in the Node 24 line, and Docker with Compose. Run the following commands from the repository root in PowerShell.

1. On first setup, copy `.env.example` to `.env` and replace the example SQL password with a strong local password. Start Docker Desktop and run:

   ```powershell
   docker compose up -d --wait sqlserver
   ```

2. In a second terminal, start the API:

   ```powershell
   dotnet run --project backend/TaskFlow.Api --launch-profile http
   ```

   The API listens on `http://127.0.0.1:5252`. The `/health` endpoint checks that the API is responding. To set up the database, follow the migration instructions below.

3. In a third terminal, start Angular:

   ```powershell
   Set-Location frontend
   npm ci
   npm start
   ```

   Open `http://localhost:4200` and sign in with a seeded account to open the board. Angular's development server forwards `/api/**` and `/health` to the local API. Check `http://localhost:4200/health` for `{"status":"ok"}`.

## Database migrations

With the local SQL Server container running, open PowerShell at the repository root. These commands load the connection settings from Compose into the current terminal and apply the EF Core migrations to the local `TaskFlow` database.

```powershell
$sql = (docker compose config --format json | ConvertFrom-Json).services.sqlserver
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection['Server'] = "127.0.0.1,$($sql.ports[0].published)"
$connection['Database'] = 'TaskFlow'
$connection['User ID'] = 'sa'
$connection['Password'] = $sql.environment.MSSQL_SA_PASSWORD
$connection['Encrypt'] = 'True'
$connection['TrustServerCertificate'] = 'True'
$env:ConnectionStrings__TaskFlow = $connection.ConnectionString

dotnet tool restore
dotnet ef database update --project backend/TaskFlow.Infrastructure --startup-project backend/TaskFlow.Api
```

Run the API from that same terminal when using database features. `TrustServerCertificate=True` is for the local development container only.

## Demo data

After applying the migrations, use the same PowerShell terminal to seed an empty local database. Choose a local password of at least 12 characters and keep it for both demo accounts.

```powershell
$env:TaskFlow__DemoPassword = Read-Host 'Local demo password' -MaskInput
dotnet run --project backend/TaskFlow.Api --launch-profile http -- --seed-demo
Remove-Item Env:TaskFlow__DemoPassword
```

The seed creates `alex@taskflow.test` and `dina@taskflow.test`, a shared board, and three tasks. It runs only when explicitly requested in Development and skips databases with existing boards or users. The API does not seed on normal startup.

## Authentication API

Use same-origin `/api/auth` requests through the Angular development proxy. Login sets an HTTP-only session cookie. Outside Development, the session and CSRF cookies require HTTPS.

| Request | Result |
| --- | --- |
| `GET /api/auth/csrf` | Sets the `XSRF-TOKEN` cookie for subsequent writes. |
| `POST /api/auth/login` | Accepts JSON `email` and `password`. Returns the signed-in user or a generic `401` for invalid credentials. |
| `GET /api/auth/session` | Returns the signed-in user or `401` if there is no session. |
| `POST /api/auth/logout` | Clears the session cookie and returns `204`. |

Send `XSRF-TOKEN` as `X-XSRF-TOKEN` on login and logout. Fetch a fresh token after either successful authentication change.

Board members can create a task with `POST /api/boards/{boardId}/tasks`, edit it with `PUT /api/boards/{boardId}/tasks/{taskId}`, and archive it with `POST /api/boards/{boardId}/tasks/{taskId}/archive`. All three requests require the CSRF header. Edit and archive require the `rowVersion` from the task response. A stale version returns `409` so the client can reload the board. Archived tasks are retained in SQL Server and excluded from active task lists.

Drag a card onto another card to insert it before that card, or onto a column to move it to the end. The status menu also moves a task to the end of the selected column. The client sends `status`, `rowVersion`, and an optional `beforeTaskId` to `POST /api/boards/{boardId}/tasks/{taskId}/move` with the CSRF header.

Open **Insights & activity** from the board to see active task counts and status changes. The `analytics` route reads a SQL view. A task is overdue when its due date is before the current UTC date and it is neither done nor archived. The `activity` route uses a stored procedure with optional `from`, `to`, `page`, and `pageSize` parameters. Both date filters include the selected UTC day. Status changes are recorded in the same database transaction as the move and remain visible after archive.
