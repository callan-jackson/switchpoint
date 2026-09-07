# syntax=docker/dockerfile:1.7
# SwitchPoint: one image serving the ASP.NET Core API and the React SPA.
#
#   1. web      node:22-alpine builds web/ into web/dist
#   2. build    .NET 10 SDK restores the whole solution (layer-cached on the project files), then
#               publishes SwitchPoint.Api framework-dependent
#   3. runtime  .NET 10 ASP.NET runtime, non-root, listening on 8080
#
# Build:   docker build -t switchpoint-api .
# Run:     docker run --rm -p 8080:8080 switchpoint-api      (SQLite + demo seed, http://localhost:8080)
# The deploy workflow pushes this image to ghcr.io/callan-jackson/switchpoint-api.

# ---------------------------------------------------------------------------------------------
# 1. SPA
# ---------------------------------------------------------------------------------------------
FROM node:26-alpine AS web
WORKDIR /web

# Dependencies first so the npm ci layer is reused while only source changes.
COPY web/package.json web/package-lock.json ./
RUN npm ci --legacy-peer-deps

COPY web/ ./
# vite build uses mode "production": no MSW mocks, /api is served by this same container.
ENV VITE_USE_MOCKS=false \
    VITE_ENV_LABEL=Production
RUN npm run build

# ---------------------------------------------------------------------------------------------
# 2. API
# ---------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Solution-level files and every project file first: `dotnet restore` is the slow step and this
# layer only changes when a package reference or project reference changes.
COPY global.json Directory.Build.props Directory.Packages.props SwitchPoint.sln ./
COPY src/SwitchPoint.Domain/SwitchPoint.Domain.csproj                         src/SwitchPoint.Domain/
COPY src/SwitchPoint.Calculation/SwitchPoint.Calculation.csproj               src/SwitchPoint.Calculation/
COPY src/SwitchPoint.Application/SwitchPoint.Application.csproj               src/SwitchPoint.Application/
COPY src/SwitchPoint.Infrastructure/SwitchPoint.Infrastructure.csproj         src/SwitchPoint.Infrastructure/
COPY src/SwitchPoint.Reports/SwitchPoint.Reports.csproj                       src/SwitchPoint.Reports/
COPY src/SwitchPoint.Api/SwitchPoint.Api.csproj                               src/SwitchPoint.Api/
COPY tests/SwitchPoint.Domain.Tests/SwitchPoint.Domain.Tests.csproj                 tests/SwitchPoint.Domain.Tests/
COPY tests/SwitchPoint.Calculation.Tests/SwitchPoint.Calculation.Tests.csproj       tests/SwitchPoint.Calculation.Tests/
COPY tests/SwitchPoint.Application.Tests/SwitchPoint.Application.Tests.csproj       tests/SwitchPoint.Application.Tests/
COPY tests/SwitchPoint.Infrastructure.Tests/SwitchPoint.Infrastructure.Tests.csproj tests/SwitchPoint.Infrastructure.Tests/
COPY tests/SwitchPoint.Reports.Tests/SwitchPoint.Reports.Tests.csproj               tests/SwitchPoint.Reports.Tests/
COPY tests/SwitchPoint.Api.Tests/SwitchPoint.Api.Tests.csproj                       tests/SwitchPoint.Api.Tests/
RUN dotnet restore SwitchPoint.sln

# Now the sources (bin/, obj/, node_modules/, web/dist are excluded by .dockerignore).
COPY src/ src/
RUN dotnet publish src/SwitchPoint.Api -c Release -o /app/publish -p:UseAppHost=false --no-restore

# The SPA is served by the API as static files from wwwroot.
COPY --from=web /web/dist /app/publish/wwwroot

# ---------------------------------------------------------------------------------------------
# 3. Runtime
# ---------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    # Seed JSON is baked into the image so the seeder never has to look for the repo's data/ folder.
    Seed__DataDirectory=/app/data \
    # Writable defaults so the image runs standalone (SQLite). Azure and docker-compose override
    # both with a SQL Server connection string (Key Vault reference / .env) and Reports__Path.
    Reports__Path=/app/data/reports \
    ConnectionStrings__SwitchPoint="Data Source=/app/data/switchpoint.db"

COPY --from=build /app/publish ./
# /app/data is the only directory the non-root user needs to write to.
COPY --chown=app:app data/ ./data/
RUN mkdir -p /app/data/reports && chown -R app:app /app/data

# The aspnet base image ships a non-root user "app" (uid 1654).
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "SwitchPoint.Api.dll"]
