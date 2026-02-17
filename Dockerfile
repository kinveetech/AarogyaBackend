# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Aarogya.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["src/Aarogya.Api/Aarogya.Api.csproj", "src/Aarogya.Api/"]
COPY ["src/Aarogya.Domain/Aarogya.Domain.csproj", "src/Aarogya.Domain/"]
COPY ["src/Aarogya.Infrastructure/Aarogya.Infrastructure.csproj", "src/Aarogya.Infrastructure/"]

RUN dotnet restore "src/Aarogya.Api/Aarogya.Api.csproj"

COPY . .
RUN dotnet publish "src/Aarogya.Api/Aarogya.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Aarogya.Api.dll"]
