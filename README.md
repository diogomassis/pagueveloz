# PagueVeloz

Compact financial transaction service with Clean Architecture and DDD principles.

Documentation Available In:

- [🇺🇸 English](./.github/docs/README.en.md)
- [🇧🇷 Português Brasileiro](./.github/docs/README.pt-br.md)

---

## Tech Stack

### Core Framework & Runtime

| Technology | Version | Purpose |
|-----------|---------|---------|
| **.NET** | 9.0.314 | Application runtime and framework |
| **C#** | Latest | Primary programming language |
| **ASP NET Core** | Latest | Web framework and HTTP handling |

### Persistence & Storage

| Technology | Version | Purpose |
|-----------|---------|---------|
| **PostgreSQL** | Latest | Primary transactional database, source of truth |
| **Redis** | Latest | Distributed caching and idempotency store |
| **Entity Framework Core** | Latest | ORM for database operations |

### Messaging & Events

| Technology | Version | Purpose |
|-----------|---------|---------|
| **RabbitMQ** | Latest | Asynchronous event publication and message broker |

### Architecture & Design Patterns

| Pattern | Purpose |
|---------|---------|
| **Clean Architecture** | Layered separation of concerns (Domain, Application, Infrastructure, API) |
| **Domain-Driven Design (DDD)** | Domain-first approach to modeling business logic |
| **Repository Pattern** | Abstract data access layer |
| **Dependency Injection** | Loose coupling and testability |
| **CQRS** | Not used (intentionally simple for current scope) |
| **Event Sourcing** | Not used (PostgreSQL transactional model preferred) |

### Testing & Quality

| Technology | Purpose |
|-----------|---------|
| **xUnit / NUnit** | Unit and integration testing frameworks |
| **Moq** | Mocking library for dependency isolation |
| **k6** | Load and performance testing |

### DevOps & Containerization

| Technology | Version | Purpose |
|-----------|---------|---------|
| **Docker** | 29.3.0 | Container runtime and image building |
| **Docker Compose** | v5.1.0 | Multi-container orchestration (local development) |
| **HAProxy** | Latest | Load balancing and request distribution (local) |

### Observability & Documentation

| Technology | Purpose |
|-----------|---------|
| **OpenAPI / Swagger** | API documentation and specification |
| **Swagger UI** | Interactive API documentation interface |

### Development Environment

| Component | Specification |
|-----------|---------------|
| **OS** | Debian GNU/Linux 12 (bookworm) |
| **Architecture** | x86_64 |
| **CPU** | 8 vCPUs, Intel Core i5-1135G7 @ 2.40 GHz |
| **Memory** | 7.5 GiB RAM |
| **SDK** | .NET SDK 9.0.314 |

### Project Structure

```folder
PagueVeloz/
├── PagueVeloz.Domain/              # Entities, value objects, business rules
├── PagueVeloz.Application/         # Use cases, orchestration, DTOs
├── PagueVeloz.Infrastructure/      # Persistence, messaging, caching
│   ├── Persistence/                # Database implementations
│   ├── Messaging/                  # RabbitMQ publishers and handlers
│   ├── Caching/                    # Redis-backed caching
│   └── Hosting/                    # Infrastructure setup
├── PagueVeloz.API/                 # HTTP endpoints, OpenAPI, composition root
├── PagueVeloz.Tests/               # Unit and integration tests
├── scripts/                        # Automation and testing scripts
└── load-tests/                     # k6 load testing scenarios
```

---

## Quick Start

### With Docker

```bash
docker compose up -d --build
```

Access:

- **API**: <http://localhost:9999>
- **Swagger UI**: <http://localhost:9999/swagger/index.html>
- **OpenAPI**: <http://localhost:9999/openapi/v1.json>
- **RabbitMQ**: <http://localhost:15672>

### Without Docker

```bash
dotnet run --project PagueVeloz.API
```

---

## API Endpoints

- `GET /health` — Health check
- `POST /api/accounts` — Create account
- `POST /api/transactions` — Process transaction

---

## Testing

```bash
# Unit tests
dotnet test PagueVeloz.sln --filter "Category!=Integration" -v minimal

# Integration tests
WAIT_TIMEOUT=180 ./scripts/run-integration-tests.sh

# End-to-end tests
WAIT_TIMEOUT=180 ./scripts/run-e2e-tests.sh

# Load tests
k6 run load-tests/k6/loadtest.js
```

---

## Key Design Decisions

- **Strong Consistency**: PostgreSQL is source of truth for financial state
- **CAP Trade-off**: Prioritizes consistency over availability on writes
- **No CQRS**: Intentionally kept simple for current product scope
- **No Event Sourcing**: PostgreSQL transactional model is sufficient
- **Fallback Infrastructure**: In-memory implementations when dependencies unavailable

---

## Documentation

Complete architecture and operational guides:

- [Full Documentation (English)](./.md/README.en.md)
- [Documentação Completa (Português)](./.md/README.pt-br.md)

---

## Development Workflow

1. Clone repository
2. Run `docker compose up -d --build` or `dotnet run --project PagueVeloz.API`
3. Access Swagger UI at <http://localhost:9999/swagger/index.html>
4. Run tests: `dotnet test PagueVeloz.sln --filter "Category!=Integration"`
