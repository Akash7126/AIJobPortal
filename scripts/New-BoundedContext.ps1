<#
Scaffolds the project skeleton of one bounded context (the same layout as BC-03 Account Identity):
  src/Services/<Name>/JobPlatform.<Name>.{Domain,Application,Infrastructure,Api}
  tests/<Name>/JobPlatform.<Name>.{Domain.UnitTests,Application.UnitTests,Infrastructure.IntegrationTests,Api.IntegrationTests,ArchitectureTests}
and adds every project to JobPlatform.sln. Existing files are never overwritten.

  ./scripts/New-BoundedContext.ps1 -Name AuditLogging -Slug audit-logging -Port 5107 -Connection Audit -Database JobPlatform_AuditLogging
#>
param(
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $Slug,
    [Parameter(Mandatory)] [int] $Port,
    [Parameter(Mandatory)] [string] $Connection,
    [Parameter(Mandatory)] [string] $Database
)

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src/Services/$Name"
$tests = Join-Path $root "tests/$Name"
$p = "JobPlatform.$Name"

function Write-New([string] $path, [string] $content) {
    if (Test-Path $path) { return }
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    [System.IO.File]::WriteAllText($path, ($content -replace "`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))
}

$bb = '..\..\..\BuildingBlocks'
$bbt = '../../../src/BuildingBlocks'

Write-New "$src/$p.Domain/$p.Domain.csproj" @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>$p.Domain</AssemblyName>
    <RootNamespace>$p.Domain</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$bb\JobPlatform.SharedKernel\JobPlatform.SharedKernel.csproj" />
  </ItemGroup>
</Project>
"@

Write-New "$src/$p.Application/$p.Application.csproj" @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>$p.Application</AssemblyName>
    <RootNamespace>$p.Application</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Options" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\$p.Domain\$p.Domain.csproj" />
    <ProjectReference Include="$bb\JobPlatform.SharedKernel\JobPlatform.SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="$p.Application.UnitTests" />
    <InternalsVisibleTo Include="$p.Api.IntegrationTests" />
    <InternalsVisibleTo Include="$p.Infrastructure.IntegrationTests" />
    <InternalsVisibleTo Include="DynamicProxyGenAssembly2" />
  </ItemGroup>
</Project>
"@

Write-New "$src/$p.Infrastructure/$p.Infrastructure.csproj" @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>$p.Infrastructure</AssemblyName>
    <RootNamespace>$p.Infrastructure</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\$p.Application\$p.Application.csproj" />
    <ProjectReference Include="..\$p.Domain\$p.Domain.csproj" />
    <ProjectReference Include="$bb\JobPlatform.SharedKernel\JobPlatform.SharedKernel.csproj" />
    <ProjectReference Include="$bb\JobPlatform.BuildingBlocks.Infrastructure\JobPlatform.BuildingBlocks.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="$p.Infrastructure.IntegrationTests" />
    <InternalsVisibleTo Include="$p.Api.IntegrationTests" />
  </ItemGroup>
</Project>
"@

Write-New "$src/$p.Api/$p.Api.csproj" @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <AssemblyName>$p.Api</AssemblyName>
    <RootNamespace>$p.Api</RootNamespace>
    <UserSecretsId>jobplatform-$Slug</UserSecretsId>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\$p.Application\$p.Application.csproj" />
    <ProjectReference Include="..\$p.Infrastructure\$p.Infrastructure.csproj" />
    <ProjectReference Include="$bb\JobPlatform.SharedKernel\JobPlatform.SharedKernel.csproj" />
    <ProjectReference Include="$bb\JobPlatform.BuildingBlocks.Infrastructure\JobPlatform.BuildingBlocks.Infrastructure.csproj" />
    <ProjectReference Include="$bb\JobPlatform.BuildingBlocks.Api\JobPlatform.BuildingBlocks.Api.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="$p.Api.IntegrationTests" />
    <InternalsVisibleTo Include="$p.ArchitectureTests" />
  </ItemGroup>
</Project>
"@

Write-New "$src/$p.Api/Properties/launchSettings.json" @"
{
  "profiles": {
    "$p.Api": {
      "commandName": "Project",
      "launchBrowser": false,
      "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" },
      "applicationUrl": "http://localhost:$Port"
    }
  }
}
"@

Write-New "$src/$p.Api/appsettings.json" @"
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": { "Microsoft.AspNetCore": "Warning", "Microsoft.EntityFrameworkCore": "Warning", "System": "Warning" }
    },
    "WriteTo": [
      { "Name": "Console", "Args": { "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact" } }
    ]
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "$Connection": "Server=DESKTOP-1MJCTDJ;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
  },
  "Database": { "Provider": "SqlServer", "ApplyMigrationsOnStartup": false },
  "Cache": { "Provider": "Redis", "ConnectionString": "redis:6379", "KeyPrefix": "prod:$Slug" },
  "Messaging": {
    "Provider": "RabbitMq",
    "RabbitMq": {
      "HostName": "rabbitmq",
      "Port": 5672,
      "UserName": "guest",
      "Password": "REPLACE_WITH_SECRET",
      "VirtualHost": "/",
      "Exchange": "jobplatform.$Slug.events"
    }
  },
  "Outbox": { "BatchSize": 20, "PollInterval": "00:00:01", "MaxAttempts": 10, "Enabled": true },
  "Inbox": { "BatchSize": 20, "PollInterval": "00:00:01", "MaxAttempts": 5, "Enabled": true },
  "Jwt": {
    "Issuer": "https://identity.jobplatform.local",
    "Audience": "jobplatform",
    "JwksUri": "http://account-identity-api:8080/.well-known/jwks.json"
  },
  "Authorization": { "PermissionMode": "AdministratorImplicit" },
  "RateLimiting": { "Global": { "PermitLimit": 600 }, "Public": { "PermitLimit": 120 } },
  "Telemetry": { "Enabled": true, "Otlp": { "Endpoint": "" } },
  "Swagger": { "Enabled": false }
}
"@

Write-New "$src/$p.Api/appsettings.Development.json" @"
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": { "Microsoft.AspNetCore": "Warning", "Microsoft.EntityFrameworkCore": "Warning", "System": "Warning" }
    },
    "WriteTo": [ { "Name": "Console" } ]
  },
  "ConnectionStrings": { "$Connection": "Data Source=$Slug-dev.db" },
  "Database": { "Provider": "Sqlite" },
  "Cache": { "Provider": "InMemory", "KeyPrefix": "dev:$Slug" },
  "Messaging": { "Provider": "InMemory" },
  "Jwt": { "JwksUri": "http://localhost:5100/.well-known/jwks.json" },
  "Swagger": { "Enabled": true }
}
"@

Write-New "$src/$p.Api/Dockerfile" @"
# Build context = the repository root (the folder containing JobPlatform.sln):
#   docker build -f src/Services/$Name/$p.Api/Dockerfile -t jobplatform/$Slug`:dev .
# NOTE: authored on a machine without Docker - this image build has not been executed.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached layer): copy only the files that define the dependency graph.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/JobPlatform.SharedKernel/*.csproj src/BuildingBlocks/JobPlatform.SharedKernel/
COPY src/BuildingBlocks/JobPlatform.BuildingBlocks.Infrastructure/*.csproj src/BuildingBlocks/JobPlatform.BuildingBlocks.Infrastructure/
COPY src/BuildingBlocks/JobPlatform.BuildingBlocks.Api/*.csproj src/BuildingBlocks/JobPlatform.BuildingBlocks.Api/
COPY src/Services/$Name/$p.Domain/*.csproj src/Services/$Name/$p.Domain/
COPY src/Services/$Name/$p.Application/*.csproj src/Services/$Name/$p.Application/
COPY src/Services/$Name/$p.Infrastructure/*.csproj src/Services/$Name/$p.Infrastructure/
COPY src/Services/$Name/$p.Api/*.csproj src/Services/$Name/$p.Api/
RUN dotnet restore src/Services/$Name/$p.Api/$p.Api.csproj

COPY src/ src/
RUN dotnet publish src/Services/$Name/$p.Api/$p.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Debian-based image on purpose: Arabic culture data needs ICU (InvariantGlobalization is off).
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER `$APP_UID
HEALTHCHECK --interval=15s --timeout=3s --start-period=30s --retries=5 CMD curl -fsS http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "$p.Api.dll"]
"@

# ---------------------------------------------------------------- tests
$ref = @{
    Domain = "$bbt/../Services/$Name/$p.Domain/$p.Domain.csproj"
}
$svc = "../../../src/Services/$Name"
$all = @(
    "    <ProjectReference Include=`"$svc/$p.Api/$p.Api.csproj`" />",
    "    <ProjectReference Include=`"$svc/$p.Infrastructure/$p.Infrastructure.csproj`" />",
    "    <ProjectReference Include=`"$svc/$p.Application/$p.Application.csproj`" />",
    "    <ProjectReference Include=`"$svc/$p.Domain/$p.Domain.csproj`" />",
    "    <ProjectReference Include=`"$bbt/JobPlatform.SharedKernel/JobPlatform.SharedKernel.csproj`" />",
    "    <ProjectReference Include=`"$bbt/JobPlatform.BuildingBlocks.Infrastructure/JobPlatform.BuildingBlocks.Infrastructure.csproj`" />",
    "    <ProjectReference Include=`"$bbt/JobPlatform.BuildingBlocks.Api/JobPlatform.BuildingBlocks.Api.csproj`" />",
    "    <ProjectReference Include=`"../../BuildingBlocks/JobPlatform.TestSupport/JobPlatform.TestSupport.csproj`" />"
) -join "`n"

function Test-Csproj([string] $suffix, [string] $extraPackages, [string] $refs) {
    Write-New "$tests/$p.$suffix/$p.$suffix.csproj" @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>$p.$suffix</RootNamespace>
  </PropertyGroup>
$extraPackages  <ItemGroup>
$refs
  </ItemGroup>
</Project>
"@
}

$domainRefs = @(
    "    <ProjectReference Include=`"$svc/$p.Domain/$p.Domain.csproj`" />",
    "    <ProjectReference Include=`"$bbt/JobPlatform.SharedKernel/JobPlatform.SharedKernel.csproj`" />"
) -join "`n"
Test-Csproj 'Domain.UnitTests' '' $domainRefs
Test-Csproj 'Application.UnitTests' "  <ItemGroup>`n    <PackageReference Include=`"FluentValidation`" />`n  </ItemGroup>`n" $all
Test-Csproj 'Infrastructure.IntegrationTests' "  <ItemGroup>`n    <PackageReference Include=`"Testcontainers.MsSql`" />`n    <PackageReference Include=`"Microsoft.EntityFrameworkCore.Sqlite`" />`n  </ItemGroup>`n" $all
Test-Csproj 'Api.IntegrationTests' "  <ItemGroup>`n    <PackageReference Include=`"Microsoft.AspNetCore.Mvc.Testing`" />`n  </ItemGroup>`n" $all
Test-Csproj 'ArchitectureTests' "  <ItemGroup>`n    <PackageReference Include=`"NetArchTest.Rules`" />`n  </ItemGroup>`n" $all

# ---------------------------------------------------------------- solution
Push-Location $root
try {
    $projects = @(
        "src/Services/$Name/$p.Domain/$p.Domain.csproj",
        "src/Services/$Name/$p.Application/$p.Application.csproj",
        "src/Services/$Name/$p.Infrastructure/$p.Infrastructure.csproj",
        "src/Services/$Name/$p.Api/$p.Api.csproj"
    )
    foreach ($proj in $projects) { dotnet sln JobPlatform.sln add $proj --solution-folder "src/Services/$Name" | Out-Null }
    foreach ($s in 'Domain.UnitTests','Application.UnitTests','Infrastructure.IntegrationTests','Api.IntegrationTests','ArchitectureTests') {
        dotnet sln JobPlatform.sln add "tests/$Name/$p.$s/$p.$s.csproj" --solution-folder "tests/$Name" | Out-Null
    }
}
finally { Pop-Location }
Write-Host "Scaffolded $Name (port $Port)."
