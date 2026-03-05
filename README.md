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

   The API listens on `http://127.0.0.1:5252`. The current `/health` endpoint checks that the API is responding. Database access will be configured when EF Core is added. That setup will use the ASP.NET Core environment variable `ConnectionStrings__TaskFlow` with a value in this format:

   ```text
   Server=127.0.0.1,14333;Database=TaskFlow;User ID=sa;Password=<local SQL password>;Encrypt=True;TrustServerCertificate=True
   ```

   Use the password and port from your local `.env`. Keep `TrustServerCertificate=True` for the local container only.

3. In a third terminal, start Angular:

   ```powershell
   Set-Location frontend
   npm ci
   npm start
   ```

   Open `http://localhost:4200`. Angular's development server forwards `/api/**` and `/health` to the local API. Check `http://localhost:4200/health` for `{"status":"ok"}`.
