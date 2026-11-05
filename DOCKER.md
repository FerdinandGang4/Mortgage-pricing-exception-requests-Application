# Run with Docker

Prerequisites: Docker Desktop running with Linux containers. For host-side tests, install the .NET 10 SDK. No host Node.js installation is needed to run the containers.

## Build and start

Run from the repository root, which contains `compose.yaml`:

```powershell
docker compose up -d --build
docker compose ps
docker compose logs --tail 50 backend frontend
```

Frontend: http://localhost:5173

API Swagger: http://localhost:5267/swagger

The frontend is compiled and served by Nginx. Nginx proxies `/api` and `/openapi` to the backend over the Compose network. The backend listens on port 8080 inside its container. Both published ports bind to the host loopback interface.

## Test

From the solution directory:

```powershell
cd Backend/BlueHarvest.PricingExceptions
dotnet test
```

From the repository root, check the running container through the frontend proxy:

```powershell
Invoke-RestMethod http://localhost:5173/api/v1/users/me -Headers @{'X-User-Id'='11111111-1111-1111-1111-111111111111'}
```

## Restart, stop, and rebuild

Run from the repository root:

```powershell
docker compose restart
docker compose down
docker compose up -d
# After source changes:
docker compose up -d --build
```

`restart` reuses the existing image. Rebuild to include code changes. Stop host development servers on ports 5173 and 5267 before starting Compose.

## Environment variables

Compose defaults are usable without an `.env` file. Override the published ports in a root `.env` file if needed:

```dotenv
BACKEND_PORT=5267
FRONTEND_PORT=5173
```

The backend receives `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_HTTP_PORTS=8080`, and `Database__Name=PricingExceptions`. Development enables Swagger and seeds the existing demo users. Existing demo authentication is preserved; no new authentication is implemented.

## Database provider limitation

The existing provider is `Microsoft.EntityFrameworkCore.InMemory`. It has no database file or database server: a Docker volume cannot make it persistent. Requests, history, and retry records are lost when the backend process restarts. `EnsureCreatedAsync` initializes this store; EF Core relational migrations are unsupported by this provider.

Persistent storage and EF migrations require a change to a relational provider. That provider change is pending confirmation and is not part of the current container configuration. No volume is presented as database persistence because mounting one would not preserve the InMemory store.
