# Mobile App Backend API

ASP.NET Core 9.0 REST API following Clean Architecture principles.

## 🏗️ Architecture

```
src/
├── API/                    # Web API Layer
│   ├── Controllers/        # API endpoints
│   ├── Middleware/         # Custom middleware
│   └── Program.cs          # Application startup
├── Core/                   # Domain Layer
│   ├── Entities/           # Domain models
│   ├── Interfaces/         # Contracts
│   └── Services/           # Business logic
└── Infrastructure/         # Data & External Services
    ├── Data/               # EF Core context
    ├── Repositories/       # Data access
    └── Services/           # External integrations

tests/
├── API.Tests/              # API integration tests
└── Core.Tests/             # Unit tests
```

## 🚀 Getting Started

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server or PostgreSQL
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

   Edit `src/API/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=MobileAppDb;..."
     }
   }
   ```

4. **Run database migrations** (when available)
   ```bash
   dotnet ef database update --project src/Infrastructure --startup-project src/API
   ```

5. **Run the application**
   ```bash
   dotnet run --project src/API
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
dotnet test tests/API.Tests
```

## 🔧 Development Tools

### Code Formatting
```bash
dotnet format
```

### Database Migrations

Create migration:
```bash
dotnet ef migrations add MigrationName --project src/Infrastructure --startup-project src/API
```

Apply migration:
```bash
dotnet ef database update --project src/Infrastructure --startup-project src/API
```

Remove last migration:
```bash
dotnet ef migrations remove --project src/Infrastructure --startup-project src/API
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

## 🏃 Running in Production

### Using Docker (when Dockerfile is added)
```bash
docker build -t mobile-app-api .
docker run -p 8080:80 mobile-app-api
```

### Environment Variables
```bash
export ConnectionStrings__DefaultConnection="..."
export Jwt__Key="..."
dotnet run --project src/API
```

## 📊 Logging

This project uses Serilog for structured logging.

Configuration in `appsettings.json`:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning"
      }
    }
  }
}
```

## 🔍 API Documentation

Once running, visit Swagger UI:
- Development: `https://localhost:5001/swagger`
- Production: `https://your-domain.com/swagger` (if enabled)

## 🐛 Troubleshooting

### Port already in use
Change ports in `src/API/Properties/launchSettings.json`

### Database connection fails
1. Verify SQL Server is running
2. Check connection string in appsettings.json
3. Ensure database exists or run migrations

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
