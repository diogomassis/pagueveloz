# PagueVeloz

Compact financial transaction service implementing Clean Architecture and DDD principles. This repository contains a ready-to-run implementation, tests and helper scripts to reproduce the evaluation scenarios locally.

> **Watch the Demo**: To make the evaluation process easier, I recorded a video demonstrating how to execute the project and the expected test results. It is available at [`.github/.video/tests.mp4`](./.github/.video/tests.mp4).

Documentation Available In: [English](./.github/.md/README.en.md) — [Português (BR)](./.github/.md/README.pt-br.md)

---

## Summary

PagueVeloz is a small, production-oriented service that models account balances, reservations and transfers with emphasis on correctness, idempotency and observability.

---

## What you will find here

- Solution root / entry: `PagueVeloz.sln`
- API implementation: `PagueVeloz.API/` (exposes OpenAPI at `/openapi/v1.json` when running)
- Application logic: `PagueVeloz.Application/`
- Domain model: `PagueVeloz.Domain/`
- Infrastructure adapters (Postgres, Redis, RabbitMQ fallbacks): `PagueVeloz.Infrastructure/`
- Tests (unit and integration): `PagueVeloz.Tests/`
- Dev and test scripts: `scripts/` (includes `docker-helpers.sh`, `run-integration-tests.sh`, `run-e2e-tests.sh`)
- Load tests (k6): `load-tests/k6/`
- Challenge specification (requirements): `.github/.pdf/pagueveloz-challenge.pdf`

---

## Mandatory Requirements (from challenge)

The following items are required for the submission and where to find them in this repository:

1. Public git project with commit history — this repository (root).
2. README with architecture & decisions, build/run instructions and examples — `README.md` and language-specific docs in `.github/.md/`.
3. C# (.NET 9) implementation — see `PagueVeloz.*` projects (`PagueVeloz.API`, `PagueVeloz.Application`, `PagueVeloz.Domain`, `PagueVeloz.Infrastructure`).
4. Unit and integration tests — `PagueVeloz.Tests/` (integration tests use Docker helpers).
5. API documentation (OpenAPI/Swagger) — produced by `PagueVeloz.API` and available at `/openapi/v1.json` when the service runs.

---

## Desirable Differentials (optional)

- Docker Compose setup for reproducing the environment — `docker-compose.yml` and `scripts/docker-helpers.sh`.
- Performance metrics and load tests — `load-tests/k6/loadtest.js`.
- Observability (OpenTelemetry metrics, structured logs) — custom metrics and logging added in `PagueVeloz.API` and `PagueVeloz.Application/Services/`; consult `Program.cs` and `TransactionProcessor.cs`.
- Cloud deployment or deployment scripts — optional; look for scripts/ or CI configs.
- Advanced architectural patterns (CQRS, Event Sourcing) — not required but listed as differentials.

---

## Short Usage

Run the API locally (recommended: use the included Docker helper for full stack):

```bash
# Start infra (Postgres, Redis, RabbitMQ) and pull the latest GHCR image
./scripts/docker-helpers.sh --build

# Run the API locally (no docker)
dotnet run --project PagueVeloz.API
```

For the full, step-by-step test flow please consult the language-specific READMEs:

- English: `.github/.md/README.en.md`
- Português: `.github/.md/README.pt-br.md`

---

For full evaluation instructions and detailed test commands see the localized READMEs referenced above.

---

## Requirements mapping (where to find each item)

This section maps every requirement from the challenge spec (mandatory, desirable/differential and example scenarios) to the file(s) in this repository that implement or demonstrate it.

### Mandatory requirements

- Public git project with commit history — repository root (confirm remote visibility on your Git provider).
- README with architecture/decisions/build/run/tests/examples — [README.md](README.md) and localized docs: [.github/.md/README.en.md](.github/.md/README.en.md), [.github/.md/README.pt-br.md](.github/.md/README.pt-br.md).
- C# (.NET 9) implementation — projects under solution: [PagueVeloz.sln](PagueVeloz.sln) referencing:
  - [PagueVeloz.API/](PagueVeloz.API/)
  - [PagueVeloz.Application/](PagueVeloz.Application/)
  - [PagueVeloz.Domain/](PagueVeloz.Domain/)
  - [PagueVeloz.Infrastructure/](PagueVeloz.Infrastructure/)
  - Project target frameworks: each project file has `<TargetFramework>net9.0</TargetFramework>`.
- Unit and integration tests — [PagueVeloz.Tests/](PagueVeloz.Tests/) (unit tests under [PagueVeloz.Tests/Application/](PagueVeloz.Tests/Application/); integration tests under [PagueVeloz.Tests/Integration/](PagueVeloz.Tests/Integration/)).
  - Example test files: [PagueVeloz.Tests/Application/TransactionProcessorUnitTests.cs](PagueVeloz.Tests/Application/TransactionProcessorUnitTests.cs), [PagueVeloz.Tests/Integration/TransactionScenariosIntegrationTests.cs](PagueVeloz.Tests/Integration/TransactionScenariosIntegrationTests.cs).
- API documentation (OpenAPI/Swagger) — produced by API startup: see [PagueVeloz.API/Program.cs](PagueVeloz.API/Program.cs) (OpenAPI available at `/openapi/v1.json`, Swagger UI at `/swagger/index.html`).

### Desirable / Differentials (where present)

- CI/CD via GitHub Actions — Automated workflows to build and publish Docker images directly to GHCR (`ghcr.io/diogomassis/pagueveloz`).
- Docker Compose and helper scripts — [docker-compose.yml](docker-compose.yml) and [scripts/docker-helpers.sh](scripts/docker-helpers.sh).
- Load tests / performance — [load-tests/k6/loadtest.js](load-tests/k6/loadtest.js).
- Observability (Telemtry, Metrics & Structured Logging) — 
  - Standard JSON logging on stdout for scalable log ingesting.
  - OpenTelemetry implemented via `OpenTelemetry.Instrumentation.AspNetCore` and `OpenTelemetry.Instrumentation.Http` in [PagueVeloz.API/Program.cs](PagueVeloz.API/Program.cs) (exposed via console exporter).
  - Custom application metrics (Counters and Histograms) tracked for account creation and transactions using `System.Diagnostics.Metrics.Meter` in [PagueVeloz.Application/Services/TransactionProcessor.cs](PagueVeloz.Application/Services/TransactionProcessor.cs) and [PagueVeloz.Application/Services/AccountService.cs](PagueVeloz.Application/Services/AccountService.cs).
- Retries / backoff and circuit breaker for messaging — implemented in:
  - Retry/backoff publisher: [PagueVeloz.Infrastructure/Messaging/RabbitMqEventPublisher.cs](PagueVeloz.Infrastructure/Messaging/RabbitMqEventPublisher.cs)
  - Circuit breaker wrapper: [PagueVeloz.Infrastructure/Messaging/CircuitBreakerEventPublisher.cs](PagueVeloz.Infrastructure/Messaging/CircuitBreakerEventPublisher.cs)
- Persistence with EF and transactional unit-of-work — [PagueVeloz.Infrastructure/Persistence/PagueVelozDbContext.cs](PagueVeloz.Infrastructure/Persistence/PagueVelozDbContext.cs), [EfUnitOfWork.cs](PagueVeloz.Infrastructure/Persistence/EfUnitOfWork.cs), [EfAccountRepository.cs](PagueVeloz.Infrastructure/Persistence/EfAccountRepository.cs).

### Where idempotency, locking and concurrency are implemented

- Idempotency store implementations: [PagueVeloz.Infrastructure/InMemoryIdempotencyStore.cs](PagueVeloz.Infrastructure/InMemoryIdempotencyStore.cs) and [PagueVeloz.Infrastructure/Persistence/EfIdempotencyStore.cs](PagueVeloz.Infrastructure/Persistence/EfIdempotencyStore.cs).
- Application usage (checks and saves idempotency): [PagueVeloz.Application/Services/TransactionProcessor.cs](PagueVeloz.Application/Services/TransactionProcessor.cs).
- Account-level locking (in-memory provider): [PagueVeloz.Infrastructure/InMemoryAccountLockProvider.cs](PagueVeloz.Infrastructure/InMemoryAccountLockProvider.cs).
- Advisory lock for DB initialization (prevents concurrent EnsureCreated attempts): [PagueVeloz.API/Program.cs](PagueVeloz.API/Program.cs) (uses pg_try_advisory_lock).

### Example scenarios (from the challenge PDF) and where they are covered

Case #1 — Basic credit and debit operations

- Demonstrated in unit tests: [PagueVeloz.Tests/Application/TransactionProcessorUnitTests.cs](PagueVeloz.Tests/Application/TransactionProcessorUnitTests.cs) (tests: credit, debit, insufficient balance).
- Executed end-to-end in integration: [PagueVeloz.Tests/Integration/TransactionScenariosIntegrationTests.cs](PagueVeloz.Tests/Integration/TransactionScenariosIntegrationTests.cs).

Case #2 — Operations with credit limit

- Handled in domain logic and covered by unit tests: [PagueVeloz.Domain/AccountDomain.cs](PagueVeloz.Domain/AccountDomain.cs) and [PagueVeloz.Tests/Application/TransactionProcessorUnitTests.cs](PagueVeloz.Tests/Application/TransactionProcessorUnitTests.cs).

Case #3 — Reserve and capture flow

- Implemented in domain (`Reserve`, `Capture`) and covered by unit + integration tests: [PagueVeloz.Domain/AccountDomain.cs](PagueVeloz.Domain/AccountDomain.cs), [PagueVeloz.Tests/Application/TransactionProcessorUnitTests.cs](PagueVeloz.Tests/Application/TransactionProcessorUnitTests.cs), [PagueVeloz.Tests/Integration/TransactionScenariosIntegrationTests.cs](PagueVeloz.Tests/Integration/TransactionScenariosIntegrationTests.cs).

Case #4 — Transfer between accounts

- Implemented in application/domain: [PagueVeloz.Application/Services/TransactionProcessor.cs](PagueVeloz.Application/Services/TransactionProcessor.cs) and covered by tests in [PagueVeloz.Tests/](PagueVeloz.Tests/).

Case #5 — Failures and retry (publish retry/backoff)

- Retry/backoff for message publication: [PagueVeloz.Infrastructure/Messaging/RabbitMqEventPublisher.cs](PagueVeloz.Infrastructure/Messaging/RabbitMqEventPublisher.cs).
- Circuit-breaker and fallback persistence: [PagueVeloz.Infrastructure/Messaging/CircuitBreakerEventPublisher.cs](PagueVeloz.Infrastructure/Messaging/CircuitBreakerEventPublisher.cs).
- Integration test that exercises publisher: [PagueVeloz.Tests/Integration/RabbitMqPublisherIntegrationTests.cs](PagueVeloz.Tests/Integration/RabbitMqPublisherIntegrationTests.cs).

### Additional important files

- Docker orchestration and HAProxy: [docker-compose.yml](docker-compose.yml), [haproxy.cfg](haproxy.cfg).
- Load tests and k6 scenario: [load-tests/k6/loadtest.js](load-tests/k6/loadtest.js).
- Test orchestration scripts: [scripts/docker-helpers.sh](scripts/docker-helpers.sh), [scripts/run-integration-tests.sh](scripts/run-integration-tests.sh), [scripts/run-e2e-tests.sh](scripts/run-e2e-tests.sh).
- Challenge specification PDF (source of the requirements): [.github/.pdf/pagueveloz-challenge.pdf](.github/.pdf/pagueveloz-challenge.pdf).

---

## Security Notice

**Note on Connection Strings:** For the purpose of this technical challenge and to ensure the application runs seamlessly out-of-the-box via Docker Compose, connection strings and credentials (e.g., PostgreSQL, Redis, RabbitMQ) have been hardcoded in `appsettings.Development.json` and `docker-compose.yml`.

**This is not a recommended practice for production environments.**

In a real-world, production-grade application, you should:

- Never commit secrets to version control.
- Use secure secret management solutions such as **Azure Key Vault**, **AWS Secrets Manager**, or **HashiCorp Vault**.
- Provide credentials dynamically via Environment Variables securely injected at runtime by your orchestration platform (e.g., Kubernetes Secrets, Docker Swarm Secrets).
