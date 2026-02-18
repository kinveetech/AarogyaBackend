# Aarogya Backend API

ASP.NET Core 9.0 REST API following Clean Architecture principles.

## 🏗️ Architecture

```
src/
├── Aarogya.Api/            # Web API Layer
│   ├── Controllers/        # API endpoints
│   ├── Middleware/         # Custom middleware
│   └── Program.cs          # Application startup
├── Aarogya.Domain/         # Domain Layer
│   ├── Entities/           # Domain models
│   ├── Interfaces/         # Contracts
│   └── Services/           # Business logic
└── Aarogya.Infrastructure/ # Data & External Services
    ├── Data/               # EF Core context
    ├── Repositories/       # Data access
    └── Services/           # External integrations

tests/
├── Aarogya.Api.Tests/      # API integration tests
└── Aarogya.Domain.Tests/   # Unit tests
```

## ⚙️ Current Service Setup

| Mode | Services | Access |
|------|----------|--------|
| Local .NET | `Aarogya.Api` only | `http://localhost:5000` / `https://localhost:5001` |
| Docker Compose | `aarogya-api`, `aarogya-postgres`, `aarogya-redis`, `aarogya-pgadmin` | API: `http://localhost:8080/swagger/index.html`, pgAdmin: `http://localhost:5050` |
| Kubernetes (`kind`) | `aarogya-api`, `postgres`, `redis`, `pgadmin` in namespace `aarogya` | API: `kubectl -n aarogya port-forward svc/aarogya-api 8080:80`, pgAdmin: `kubectl -n aarogya port-forward svc/pgadmin 5050:80` |

## 🚀 Getting Started

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Docker Desktop
- `kubectl` (for Kubernetes setup)
- `kind` (for local Kubernetes setup)
- Visual Studio 2022 / VS Code / Rider

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd backend
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Update connection string**

   Edit `src/Aarogya.Api/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=MobileAppDb;..."
     }
   }
   ```

4. **Run database migrations** (when available)
   ```bash
   dotnet ef database update --project src/Aarogya.Infrastructure --startup-project src/Aarogya.Api
   ```

5. **Run the application**
   ```bash
   dotnet run --project src/Aarogya.Api
   ```

   API will be available at:
   - HTTPS: `https://localhost:5001`
   - HTTP: `http://localhost:5000`
   - Swagger UI: `https://localhost:5001/swagger`

## 📡 API Endpoints

### Authentication
```
POST   /api/auth/register          Register new user
POST   /api/auth/login             User login
POST   /api/auth/refresh           Refresh JWT token
POST   /api/auth/logout            User logout
POST   /api/auth/forgot-password   Password reset request
POST   /api/auth/reset-password    Complete password reset
```

### User Management
```
GET    /api/users/me               Get current user
PUT    /api/users/me               Update user profile
DELETE /api/users/me               Delete account
POST   /api/users/me/avatar        Upload avatar
```

## 🔐 Authentication

This API uses JWT Bearer token authentication.

### Getting a Token
```bash
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "YourPassword123!"
}
```

### Using the Token
```bash
GET /api/users/me
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Token Configuration
Edit `appsettings.json`:
```json
{
  "Jwt": {
    "Key": "your-secret-key-minimum-32-characters",
    "Issuer": "MobileAppAPI",
    "Audience": "MobileAppClients",
    "ExpiryInMinutes": 60
  }
}
```

## 🧪 Testing

### Run all tests
```bash
dotnet test
```

### Run with coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Run specific test project
```bash
dotnet test tests/Aarogya.Api.Tests
```

## 🔧 Development Tools

### Code Formatting
```bash
dotnet format
```

### Database Migrations

Create migration:
```bash
dotnet ef migrations add MigrationName --project src/Aarogya.Infrastructure --startup-project src/Aarogya.Api
```

Apply migration:
```bash
dotnet ef database update --project src/Aarogya.Infrastructure --startup-project src/Aarogya.Api
```

Remove last migration:
```bash
dotnet ef migrations remove --project src/Aarogya.Infrastructure --startup-project src/Aarogya.Api
```

## 📦 Dependencies

### API Layer
- Microsoft.AspNetCore.Authentication.JwtBearer
- Swashbuckle.AspNetCore (Swagger)
- Serilog.AspNetCore
- FluentValidation.AspNetCore

### Infrastructure Layer
- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.SqlServer
- Npgsql.EntityFrameworkCore.PostgreSQL

## 🐳 Local Docker Run

### Docker Compose (API + PostgreSQL + Redis + pgAdmin)
```bash
docker compose up --build -d
docker compose ps
curl http://localhost:8080/swagger/index.html
```

Compose services:
- API: `aarogya-api` (`8080`)
- PostgreSQL 16 + `pgcrypto`: `aarogya-postgres` (`5432`)
- PostgreSQL extension bootstrap: `aarogya-postgres-init` (one-shot `CREATE EXTENSION IF NOT EXISTS pgcrypto`)
- Redis 7: `aarogya-redis` (`6379`)
- pgAdmin: `aarogya-pgadmin` (`5050`)

pgAdmin default login:
- Email: `admin@aarogya.com`
- Password: `admin`

Environment configuration:
- Copy `.env.example` to `.env` and customize values as needed.
- `docker-compose.yml` reads DB, pgAdmin, Redis, and JWT settings from `.env` with safe defaults.

Named volumes used for persistence:
- `aarogyabackend_pgdata` (PostgreSQL data)
- `aarogyabackend_redisdata` (Redis AOF/persistence data)
- `aarogyabackend_pgadmindata` (pgAdmin settings and state)

Useful commands:
```bash
docker compose logs -f api
docker compose down -v
```

### Docker only (API)
```bash
docker build -t aarogya-api:dev .
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e Jwt__Key=development-key-change-this-to-32-plus-chars \
  aarogya-api:dev
```

## ☸️ Local Kubernetes Run

### 1. Create cluster (first time only)
```bash
kind create cluster --name aarogya-backend
kubectl config use-context kind-aarogya-backend
```

### 2. Build image
```bash
docker build -t aarogya-api:dev .
```

### 3. Load image into your local cluster
For `kind` (cluster created above):
```bash
kind load docker-image aarogya-api:dev --name aarogya-backend
```

For `minikube`:
```bash
minikube image load aarogya-api:dev
```

### 4. Apply manifests
```bash
kubectl apply -k k8s
kubectl -n aarogya get pods
kubectl -n aarogya get svc
```

### 5. Access API
```bash
kubectl -n aarogya port-forward svc/aarogya-api 8080:80
```

Then open `http://localhost:8080/swagger/index.html`.

To access pgAdmin in Kubernetes:
```bash
kubectl -n aarogya port-forward svc/pgadmin 5050:80
```

Then open `http://localhost:5050` with:
- Email: `admin@aarogya.com`
- Password: `admin`

If using `k9s`, switch namespace to `aarogya` to view these pods.

## 🧰 Redis Cache Configuration

Redis settings are configured in:
- `src/Aarogya.Api/appsettings.json`
- `src/Aarogya.Api/appsettings.Development.json`

Core keys:
- `ConnectionStrings:Redis`
- `Redis:InstanceName`
- `Redis:Database`
- `Redis:ConnectTimeoutMilliseconds`
- `Redis:ConnectRetry`
- `Redis:SyncTimeoutMilliseconds`
- `Redis:DefaultExpirationMinutes`
- `Redis:KeyPrefix`

For local secrets, use `src/Aarogya.Api/secrets.template.json` and set with:
```bash
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379,abortConnect=false,connectTimeout=5000" --project src/Aarogya.Api
```

Redis key naming convention:
- Format: `<Redis:KeyPrefix>:<bounded-context>:<resource>:<id>`
- Development example: `aarogya:dev:reports:12345`
- Production example: `aarogya:prod:reports:12345`

## 📊 Logging

This project uses Serilog for structured logging.

Environment-specific logging configuration files:
- `src/Aarogya.Api/appsettings.Development.json`
- `src/Aarogya.Api/appsettings.Staging.json`
- `src/Aarogya.Api/appsettings.Production.json`

Logging profile:
- Development: `Debug` default level, console sink
- Staging: `Information` default level, console + rolling file sink
- Production: `Information` default level, console + rolling file sink

Request logging:
- Enabled via `UseSerilogRequestLogging()` in `src/Aarogya.Api/Program.cs`
- Logs method, path, status code, elapsed time, trace id, and client IP
- Does not log `Authorization` or `Cookie` values; only records if sensitive headers are present

Log retention:
- Staging file logs keep 14 rolling daily files
- Production file logs keep 30 rolling daily files

## 🔍 API Documentation

Once running, visit Swagger UI:
- Development: `https://localhost:5001/swagger`
- Production: `https://your-domain.com/swagger` (if enabled)

## ✅ PR Quality Gates

Current PR checks:
- `.NET Backend CI / build-and-test`
- `.NET Backend CI / lint`
- `PR Guardrails / semantic-pr-title`
- `PR Guardrails / dependency-review-disabled` (or `dependency-review` when enabled)

See `/docs/github-main-guardrails.md` for full guardrail and ruleset setup details.

## 🐛 Troubleshooting

### Port already in use
Change ports in `src/Aarogya.Api/Properties/launchSettings.json`

### Database connection fails
1. For Docker: verify `aarogya-postgres` is healthy (`docker compose ps`)
2. Verify `aarogya-redis` is healthy if Redis-backed features are enabled
3. For Kubernetes: verify `postgres` pod is running in namespace `aarogya`
4. Check `ConnectionStrings__DefaultConnection` override in your runtime environment

### JWT token errors
Ensure JWT:Key in appsettings.json is at least 32 characters

## 📝 Code Style

This project follows:
- Clean Architecture principles
- SOLID principles
- RESTful API conventions
- Async/await patterns
- Repository pattern

## 🤝 Contributing

1. Create feature branch: `git checkout -b feature/amazing-feature`
2. Commit changes: `git commit -m 'feat: add amazing feature'`
3. Push branch: `git push origin feature/amazing-feature`
4. Open Pull Request

## 📄 License

This project is licensed under the MIT License.
