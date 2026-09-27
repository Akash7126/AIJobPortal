# Build context = the repository root (the folder containing JobPlatform.sln):
#   docker build -f src/Services/HelpContent/JobPlatform.HelpContent.Api/Dockerfile -t jobplatform/help-content:dev .
# NOTE: authored on a machine without Docker - this image build has not been executed.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached layer): copy only the files that define the dependency graph.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/JobPlatform.SharedKernel/*.csproj src/BuildingBlocks/JobPlatform.SharedKernel/
COPY src/BuildingBlocks/JobPlatform.BuildingBlocks.Infrastructure/*.csproj src/BuildingBlocks/JobPlatform.BuildingBlocks.Infrastructure/
COPY src/BuildingBlocks/JobPlatform.BuildingBlocks.Api/*.csproj src/BuildingBlocks/JobPlatform.BuildingBlocks.Api/
COPY src/Services/HelpContent/JobPlatform.HelpContent.Domain/*.csproj src/Services/HelpContent/JobPlatform.HelpContent.Domain/
COPY src/Services/HelpContent/JobPlatform.HelpContent.Application/*.csproj src/Services/HelpContent/JobPlatform.HelpContent.Application/
COPY src/Services/HelpContent/JobPlatform.HelpContent.Infrastructure/*.csproj src/Services/HelpContent/JobPlatform.HelpContent.Infrastructure/
COPY src/Services/HelpContent/JobPlatform.HelpContent.Api/*.csproj src/Services/HelpContent/JobPlatform.HelpContent.Api/
RUN dotnet restore src/Services/HelpContent/JobPlatform.HelpContent.Api/JobPlatform.HelpContent.Api.csproj

COPY src/ src/
RUN dotnet publish src/Services/HelpContent/JobPlatform.HelpContent.Api/JobPlatform.HelpContent.Api.csproj \
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
ENTRYPOINT ["dotnet", "JobPlatform.HelpContent.Api.dll"]