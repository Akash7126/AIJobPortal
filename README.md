# JobPlatform - BC-03 Account Identity (backend)

Backend-only implementation of bounded context **BC-03 Account Identity** of the AI-driven job-matching platform (ASP.NET Core Web API, .NET 10, EF Core, DDD / Clean Architecture / CQRS, Outbox/Inbox, RabbitMQ, Redis).
The specification is `Handover/00-Shared-Foundation.md` and `Handover/BC-03-Account-Identity/BC-03-Account-Identity-Handover.md`.

> Honest status: everything builds with 0 warnings and every non-Docker test passes (699 tests: 191 domain, 176 application, 105 building-blocks, 26 architecture, 40 infrastructure, 161 API; line coverage 92.4 %, branch 82.4 %) on the machine that produced this code (Windows 11, .NET SDK 10.0.301, **no Docker, no SQL Server, no RabbitMQ, no Redis**). The Docker-tagged tests, `docker-compose.yml` and the `Dockerfile` were written but **never executed** here. See [Known limitations](#known-limitations).

## Layout

```
JobPlatform.sln
Directory.Build.props / Directory.Packages.props / .editorconfig / coverlet.runsettings
docker-compose.yml, .env.example, .dockerignore
scripts/check-ac-coverage.ps1, scripts/owned-story-acs.json     story -> test traceability gate
src/BuildingBlocks/
  JobPlatform.SharedKernel                    contracts + primitives only (Entity/AggregateRoot, Result, CQRS abstractions, ports, Email/MobileNumber,
                                              Roles/Permissions/claims, MessageEnvelope/RoutingKeys, IntegrationEvents/AccountIdentity/*, ApiContracts/AccountIdentity/*)
  JobPlatform.BuildingBlocks.Infrastructure   reusable plumbing: in-house dispatcher + pipeline behaviors (Logging, RateLimit, Validation, Authorization,
                                              Idempotency, UnitOfWork), BaseDbContext, Outbox (interceptor + processor), Inbox (writer + processor),
                                              IIntegrationEventBus (RabbitMQ / InMemory), ICacheStore (Redis / InMemory), rate limiter, idempotency store,
                                              ProblemDetails mapping, correlation-id middleware, health checks, durable idempotency store (`messaging.IdempotencyKeys`), telemetry source/meter, ETag helper (SharedKernel)
src/Services/AccountIdentity/
  JobPlatform.AccountIdentity.Domain          aggregates, value objects, business rules, domain events, domain services (no framework, no crypto, no HTTP)
  JobPlatform.AccountIdentity.Application     feature-based CQRS layout (one type per file, namespace = folder):
                                                Commands/<Feature>/  Queries/<Feature>/  Handlers/<Feature>/ (one handler per request)
                                                DTOs/<Feature>/ (request/response models; DTOs/Common when shared)  Services/<Feature>/ (application services)
                                                Validators/<Feature>/ (FluentValidation) + Validators/Common (shared rules)
                                              plus ports (Abstractions), base request records, event mapper + domain-event handlers, access authorizer
  JobPlatform.AccountIdentity.Infrastructure  DbContext + configurations + migration, repositories, read store, hashers, TOTP, JWT + signing keys, stores, delivery adapters
  JobPlatform.AccountIdentity.Api             controllers, auth/authorization, ProblemDetails, localisation (ar/en), Swagger/OpenAPI, Serilog, Program.cs
tests/
  BuildingBlocks/JobPlatform.BuildingBlocks.Infrastructure.UnitTests
  AccountIdentity/JobPlatform.AccountIdentity.{Domain.UnitTests, Application.UnitTests, Infrastructure.IntegrationTests, Api.IntegrationTests, ArchitectureTests}
```

Dependency rule (enforced by `ArchitectureTests`): Domain -> SharedKernel; Application -> Domain + SharedKernel; Infrastructure -> Application/Domain/SharedKernel/BuildingBlocks; Api is the composition root. No other BC is referenced anywhere.

Interfaces (enforced by `ArchitectureTests`): every interface lives in an `Interfaces` folder of its own layer and project, never beside its implementation. Domain: `Interfaces/Repositories` (repository contracts) and `Interfaces/Services` (domain-service contracts such as `ISecretVerifier` or `IJobPostingSchemaValidator`). Application: `Interfaces/` (ports that Infrastructure implements). Infrastructure and BuildingBlocks: `Interfaces/<Area>`. SharedKernel: `<Layer>/Interfaces/...` (e.g. `Application/Interfaces/Cqrs`, `Domain/Interfaces`). Similarly named contracts in different bounded contexts (e.g. `IKnownAccountRepository`, `IFileStorage`) are separate by design: each is owned by its BC and typed on that BC's model.

Repositories (enforced by `ArchitectureTests`): every repository implementation lives in its own file under the Infrastructure project's `Persistence/Repositories` folder (namespace `<Infrastructure>.Persistence.Repositories`), next to the DbContext it uses. Its contract is in the Domain project's `Interfaces/Repositories`, and it is registered in the BC's Infrastructure DI extension.

Application layout (enforced by `ArchitectureTests`): commands live in `<Application>.Commands.<Feature>`, queries in `.Queries.<Feature>`, and request handlers in `.Handlers.<Feature>`, one handler class per request. Orchestration shared by several handlers (e.g. `RegistrationWorkflow`, or the load/map helpers of a feature) is an application service in `Services/<Feature>`, registered in the BC's `Add<Bc>Application`. Handlers and validators are found by assembly scanning. Unit tests call handlers directly through `TestSupport.RequestHandlerSet`, which builds the handler for a request from the test's dependencies.

Validation: every command/query validator lives in the Application layer's `Validators` folder (namespace `<Application>.Validators.<Feature>`, enforced by `ArchitectureTests`); duplicated rule chains are shared per BC as extension methods in `Validators/Common`. Validators are registered by assembly scanning (`AddRequestHandlersFrom` -> `AddValidatorsFromAssembly`) and run by `ValidationBehavior` before the handler (malformed input = 400 with `VAL.*` codes); business rules stay in the domain.

## Running it

Prerequisite: .NET SDK 10 (`dotnet --version`).

### 1. Local, no Docker (SQLite + in-memory cache + in-memory bus)

```powershell
dotnet run --project src/Services/AccountIdentity/JobPlatform.AccountIdentity.Api --no-launch-profile -- --environment Development --urls http://localhost:5100
# or: $env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run --project src/Services/AccountIdentity/JobPlatform.AccountIdentity.Api
```

`appsettings.Development.json` selects `Database:Provider=Sqlite` (file `identity-dev.db`), `Cache:Provider=InMemory`, `Messaging:Provider=InMemory`, creates the schema, seeds roles / password policy / session timeout / signing key and a **bootstrap administrator**
(`admin@jobplatform.local` / `Adm1n!Passw0rd-dev`, dev only). Activation codes are logged at Debug level in this mode only (`Delivery:LogCodesInDevelopment=true`).
Swagger UI: `http://localhost:5100/swagger`, OpenAPI JSON: `/openapi/v1.json`, health: `/health/live`, `/health/ready`, JWKS: `/.well-known/jwks.json`.

Administrator sign-in always needs MFA (THR-029): `POST /api/v1/auth/login` -> `MfaEnrollmentRequired` + `mfaToken` -> `POST /api/v1/auth/mfa/enroll` (returns the TOTP seed once) -> add it to an authenticator app -> `POST /api/v1/auth/mfa/verify {mfaToken, code}` -> tokens.

The three switches are plain configuration, selectable per environment: `Database:Provider = SqlServer | Sqlite`, `Cache:Provider = Redis | InMemory`, `Messaging:Provider = RabbitMq | InMemory`.
Providers are resolved lazily from the final configuration, so environment variables / test overrides always win.

### 2. docker-compose (SQL Server + RabbitMQ + Redis + API) - not executed here

```powershell
copy .env.example .env      # then fill in the secrets (see comments in the file)
docker compose up --build
```

The API image is built from `src/Services/AccountIdentity/JobPlatform.AccountIdentity.Api/Dockerfile` (build context = repository root). Locally the API applies EF migrations at start-up
(`Database__ApplyMigrationsOnStartup=true`); in production use a migration job (foundation section 8). RabbitMQ management UI: http://localhost:15672.

### Regenerating the migration

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/Services/AccountIdentity/JobPlatform.AccountIdentity.Infrastructure --startup-project src/Services/AccountIdentity/JobPlatform.AccountIdentity.Api --context IdentityDbContext --output-dir Persistence/Migrations
```

Checked-in migrations (`InitialCreate`, `AddMfaReplayGuardAndIdempotencyKeys`) target SQL Server (schemas `identity` and `messaging`, filtered unique indexes, rowversion). SQLite (dev/tests) builds its schema from the model with `EnsureCreated`, so **delete `identity-dev.db` after pulling a model change** (the file is not migrated).

## Running the tests

```powershell
dotnet build JobPlatform.sln                         # 0 warnings, 0 errors (TreatWarningsAsErrors)
dotnet test JobPlatform.sln                          # everything that can run here
dotnet test JobPlatform.sln --filter "Category!=Docker"   # explicitly exclude Testcontainers tests
dotnet test JobPlatform.sln --filter "Category=Docker"    # only Docker tests (need Docker running)
dotnet test JobPlatform.sln --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
reportgenerator "-reports:TestResults/**/coverage.cobertura.xml" -targetdir:coverage -reporttypes:TextSummary
powershell -File scripts/check-ac-coverage.ps1       # story -> test traceability gate
```

* Docker tests use `[DockerFact]` + `[Trait("Category","Docker")]`: they skip themselves when Docker is unavailable (`DOCKER_TESTS=skip` forces a skip).
* API integration tests host the real API in-process (`WebApplicationFactory<Program>`) on SQLite, in-memory cache/bus, captured OTP delivery and a `FakeTimeProvider`; the outbox processor is driven explicitly for determinism.
* Traceability: every test that proves an acceptance criterion carries `[Trait("Story","US-...")]` and `[Trait("AC","AC-nn")]`. `TraceabilityTests` (and the PowerShell script) fail when any of the 58 ACs of the 13 owned stories has no tagged test.
  AC-06/AC-07 of US-3.1.1-01 (WCAG) and AC-02 of US-4.1-04 (legal compliance of the policy text) are UI/legal review items; their tests verify only what the API owes (bilingual messages, machine-readable field errors, auditable consent record). This is stated in the tests.

## Decisions taken for the open questions

| Item | Decision implemented |
|---|---|
| Q-01 build vs buy identity provider | **Build** (in-house issuing) while keeping every dependency behind ports (`IAccessTokenService`, `IMfaService`, `ISessionStore`); an external IdP can replace the adapters without touching the domain. |
| Q-02 / D-01 Account vs User Account, credential home | One `Account` aggregate (self-service + administrator facets); `ApiCredential` stays in BC-03. |
| Q-03 OTP / e-mail delivery | Option (a): BC-03 calls ports `IOtpSender` / `IEmailVerificationSender`. Adapters shipped: a **log adapter** (masked recipient, never the code; code at Debug only with the dev flag) and a capturing adapter for tests. No code travels through the broker. A BC-13 or SMS-gateway adapter is a drop-in replacement. Delivery failure does not fail registration (user can request a new code, rate limited). |
| Q-04 `AccountSuspended` + BC-04 request | Deactivation/deletion request is an **internal API** (`POST /internal/v1/accounts/{id}/deactivation-requests`, service token). `AccountSuspended` is raised on every deactivation (admin or owner) and carries `reason`, `standing` (`Deactivated`/`DeletionRequested`), `actorType`. Consumers BC-04/05/09/10/11/13 are outside this build. |
| Q-05 "AES-256 hashing" | **PBKDF2-HMAC-SHA512** (210,000 iterations, self-describing format, rehash detection). OTPs / refresh tokens / API secrets use keyed HMAC-SHA256. **AES-256-GCM** only for reversible secrets (TOTP seeds, signing private keys). Argon2id was not used (kept dependency-free); switching means a new `IPasswordHasher`. |
| Q-06 rate-limit source | IP + optional `X-Device-Id` header, hashed. Registration/activation/resend/OAuth use it. |
| Q-07 "MoL/PEF-authorised staff" | Administrator actor type holding permission `accounts.approve-partner` (checked by the pipeline, re-checked in the domain via `Actor.IsAuthorisedStaff`). |
| Q-08 admin MFA factor | **TOTP** (RFC 6238, SHA-1, 6 digits, +/-1 step); mandatory for administrators; failures count toward the lockout. |
| D-02 | New credential request revokes the previous one (flushed before the insert inside one transaction; filtered unique index enforces INV-13). |
| D-03 | Tokens carry role **ids**; permissions resolved per request from the cached role map. Improvement: the account's *current* role ids are resolved per request too, so assigning/removing a role also applies without re-login. |
| A-01 defaults | OTP 10 min, 5 attempts / 15 min, lock 15 min after 5 failures, session idle 30 min, password >= 8 with upper/lower/digit, credential default expiry 365 d (max 730 d), default limit 1000 req/h. |

### Other design notes
* Error codes: the published `code` is the external one (`E-JSRPM-DUPLICATE`, `E-AAFR-RATE-LIMITED`, ...); the internal rule code (`AI.Account.DUPLICATE`) is returned as `ruleCode`. Codes added by this implementation (not in the handover): `E-AAFR-INVALID-CREDENTIALS`, `E-AAFR-ACCOUNT-PENDING`, `E-AAFR-ACCOUNT-DEACTIVATED`, `E-AAFR-SESSION-EXPIRED`, `E-AAFR-MFA-REQUIRED`, `E-AAFR-PASSWORD-CHANGE-REQUIRED`, `E-AUM-STATE-CONFLICT`, `E-TPJPRI-REVOKED|PARTNER-NOT-ACTIVE|IP-NOT-ALLOWED`, `E-APIF-INVALID-CLIENT`, `E-ACCOUNT-NOT-FOUND`, `E-IDEMPOTENCY-*`, `E-CONCURRENCY-CONFLICT`. Every published code has an Arabic and an English message (test-enforced).
* Lockout is reported as **429** `E-AAFR-RATE-LIMITED` (handover allowed 423/429); an account locks on the 5th failure so the 6th attempt is refused.
* Failed attempts (wrong activation code, wrong password / MFA code, wrong client secret) must persist although the request fails: those commands implement `IPersistOnFailure` and the unit-of-work commits them.
* Idempotency-Key is honoured on the three registrations and on consent recording (24 h, request fingerprint, in-flight detection). The record is durable (`messaging.IdempotencyKeys`, primary key `(Scope, Key)`; the cache is only a fast path for completed keys), so a replay survives a cache flush or restart; expired keys are purged by the outbox housekeeping. It is deliberately **not** applied to API-credential issuing because the response contains the one-time secret, which must not be stored for replay.
* Optimistic concurrency (foundation section 11): the admin `GET`s (`/admin/accounts/{id}`, `/admin/password-policy`, `/admin/session-timeout`) return an `ETag` (RowVersion) header and the roles list carries `eTag` per role; every admin update (`PUT /admin/password-policy`, `PUT /admin/session-timeout`, account approve/ban/deactivate/reset-credentials/roles, role permission grant/revoke) honours an optional `If-Match`. A stale tag is **412** `E-PRECONDITION-FAILED` before the aggregate is touched; no header (or `*`) means unconditional; a concurrent write that slips through is still a 409 `E-CONCURRENCY-CONFLICT` from the rowversion.
* TOTP codes are single use: the aggregate stores the time step of the last accepted code (`Accounts.MfaLastUsedTimeStep`) and refuses that step or any earlier one (counts toward the lockout). Password hashes made with weaker PBKDF2 parameters are transparently upgraded after a successful login (`IPasswordHasher.NeedsRehash`).
* Handover section 9 lists Redis keys `otp:{accountId}` and `lock:{accountId}`. The activation challenge and the lockout live **in the aggregate** (`ActivationChallenges`, `Accounts.LockedUntilUtc`), so they are transactional with the account and survive a cache flush; Redis holds rate-limit counters, sessions, revoked tokens, the role map, the password policy and MFA/login-code challenges.
* Observability (foundation section 12): OpenTelemetry traces (ASP.NET Core, HttpClient, command handlers, outbox publish spans continuing the request trace through the `traceparent` outbox/message header) and metrics (handler duration, outbox published/failed/dead-lettered/lag, inbox processed/failed/lag, cache hits/misses). Nothing is exported unless `Telemetry:Otlp:Endpoint` is set. The `identity.AccessLog` is purged after `AccessLog:RetentionDays` (default 365, 0 = keep).
* Domain events are mapped to the five integration events by `AccountIdentityEventMapper` inside `SaveChanges` (outbox row in the same transaction); `aggregateVersion` is a per-aggregate event counter.

## Endpoint coverage vs handover section 6.1

All routes of the table are implemented. Differences and additions:

| Handover | Implemented |
|---|---|
| `GET/PUT /admin/roles`, `.../{id}/permissions` | `GET /api/v1/admin/roles`, `PUT` / `DELETE /api/v1/admin/roles/{roleId}/permissions/{permission}` (grant / revoke) |
| `POST /auth/mfa/verify` | plus `POST /auth/mfa/enroll` (TOTP enrolment needs it) |
| `PUT /admin/session-timeout` | plus `GET` |
| - | additions: `GET /admin/accounts` (paged list, masked PII), `PUT/DELETE /admin/accounts/{id}/roles/{roleId}`, `GET /api-credentials/current` |
| `POST /auth/login` mechanisms | `password`, `email-verification` (one-time code sent to a verified e-mail), `mfa` (password + TOTP in one call) |
| `GET /.well-known/jwks.json` | plus `GET /.well-known/openid-configuration` and `/.well-known/oauth-authorization-server` (issuer, JWKS and token endpoint discovery; client-credentials only, no interactive OIDC flow) |

## Known limitations

* **Never executed here (no Docker / SQL Server / RabbitMQ / Redis):** the 10 Docker-tagged tests (SQL Server migrations + constraints + `UPDLOCK/READPAST` outbox query + durable idempotency + MFA replay column, RabbitMQ publish/confirm/topology + retry tiers to DLQ, Redis counters/sessions), `docker-compose.yml`, the `Dockerfile`, `RabbitMqEventBus`, `RabbitMqInboxConsumer`, `RedisCacheStore` against a real server. They compile and are covered by SQLite/in-memory equivalents and by a substitute-channel unit test of the retry routing (three tiers 30 s / 2 min / 10 min, DLQ after 5 attempts).
* The RabbitMQ publisher declares its exchange lazily on the first publish or readiness probe (`/health/ready`), not eagerly at start-up, so the service starts even when the broker is still down.
* SQLite dev/test mode ignores schemas and stamps its own row versions; the SQL Server model (rowversion, filtered indexes, schemas) is verified by the generated migrations but not by a running server.
* No load test: registration p95 is checked with a 20-request in-process smoke test, not the 1,000-registrations/day NFR profile.
* PII columns (e-mail, mobile, identity key) are **not** encrypted at the application level (the handover marks them for column encryption / Always Encrypted). Deterministic app-level encryption would break the admin partial-match search and cannot be validated without SQL Server here, so this is left to SQL Server TDE / Always Encrypted configuration; only TOTP seeds and signing keys are AES-256-GCM, and read models mask PII.
* Not implemented: an inbox consumer for BC-03 (it consumes nothing; the inbox infrastructure and `RabbitMqInboxConsumer` are wired and unit-tested with SQLite), consumers of `AccountSuspended` in other BCs, the interactive OIDC authorization-code flow (discovery advertises client-credentials only), a distributed lock helper (none is required by BC-03), an OpenAPI diff in CI, `traceparent` extraction on the consumer side (BC-03 has no consumer).
* Access tokens are 15 minutes (`Jwt:AccessTokenMinutes`) and are checked against the live session on every request.
* Cache failures degrade to the database for the role map and password policy, but session/rate-limit/MFA state fails **closed** by design (auth-critical).
* Secrets in `appsettings.json` are placeholders; the master key, pepper, bootstrap admin password and service-client secrets must come from a secret store / environment.