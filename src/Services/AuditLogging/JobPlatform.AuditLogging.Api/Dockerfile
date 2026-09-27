# Build context = the repository root (the folder containing JobPlatform.sln):
#   docker build -f src/Services/AuditLogging/JobPlatform.AuditLogging.Api/Dockerfile -t jobplatform/audit-logging:dev .
# NOTE: authored on a machine without Docker - this image build has not been executed.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached layer): copy only the files that define the dependency graph.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/JobPlatform.SharedKernel/*.csproj src/BuildingBlocks/JobPlatform.SharedKernel/
COPY src/BuildingBlocks/JobPlatform.BuildingBlocks.Infrastructure/*.csproj src/BuildingBlocks/JobPlatform.BuildingBlocks.Infrastructure/
COPY src/BuildingBlocks/JobPlatform.BuildingBlocks.Api/*.csproj src/BuildingBlocks/JobPlatform.BuildingBlocks.Api/
COPY src/Services/AuditLogging/JobPlatform.AuditLogging.Domain/*.csproj src/Services/AuditLogging/JobPlatform.AuditLogging.Domain/
COPY src/Services/AuditLogging/JobPlatform.AuditLogging.Application/*.csproj src/Services/AuditLogging/JobPlatform.AuditLogging.Application/
COPY src/Services/AuditLogging/JobPlatform.AuditLogging.Infrastructure/*.csproj src/Services/AuditLogging/JobPlatform.AuditLogging.Infrastructure/
COPY src/Services/AuditLogging/JobPlatform.AuditLogging.Api/*.csproj src/Services/AuditLogging/JobPlatform.AuditLogging.Api/
RUN dotnet restore src/Services/AuditLogging/JobPlatform.AuditLogging.Api/JobPlatform.AuditLogging.Api.csproj

COPY src/ src/
RUN dotnet publish src/Services/AuditLogging/JobPlatform.AuditLogging.Api/JobPlatform.AuditLogging.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Debian-based image on purpose: Arabic culture data needs ICU (InvariantGlobalization is off).
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=3s --start-period=30s --retries=5 CMD curl -fsS http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "JobPlatform.AuditLogging.Api.dll"]