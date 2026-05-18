# pagueveloz

## Overview

PagueVeloz is a compact financial transaction service built with Clean Architecture and DDD principles. The intent is simple: keep the domain explicit, protect business invariants, and make the system easy to reason about when it is under load or when something fails.

The codebase is optimized for correctness first. The transactional path stays simple, the runtime dependencies remain small, and the system avoids patterns that would add ceremony without bringing real value at this stage.

## What This Service Is

This repository models a narrow transactional core:

- account creation
- balance-affecting transactions
- idempotent processing
- integration event publication
- operational caching and resilience support

That scope is intentional. It is easier to keep a small financial core correct than to stretch consistency guarantees across too many concerns at once.

## What This Service Is Not

This project is not trying to be:

- a distributed ledger
- an event-sourced financial platform
- a read-heavy analytics system
- a CQRS demo
- a microservices platform with unnecessary decomposition

Those choices were deferred on purpose. The current shape of the product does not justify the extra infrastructure and coordination costs.


## Why CQRS Was Not Introduced

CQRS is useful when read and write workloads diverge enough to justify separate models and scaling paths. That is not the case here.

The system is small, write-centric, and correctness-sensitive. Introducing separate command and query models would add mapping layers, duplication, and more failure points without solving a real bottleneck. In a larger system, CQRS can be justified. Here, it would be premature complexity.

## Docker Strategy

The runtime image is built with a multi-stage Dockerfile.

Only the published application output reaches the final image. SDK tooling, restore state, source code, and test artifacts are excluded from the runtime layer. That keeps the image smaller, reduces the attack surface, and makes local and CI builds more reproducible.

## Development Environment

The project was developed in the following environment:

- Operating system: Debian GNU/Linux 12 (bookworm)
- Architecture: x86_64
- CPU: 8 vCPUs, Intel Core i5-1135G7 @ 2.40 GHz
- Memory: 7.5 GiB RAM
- .NET SDK: 9.0.314
- Docker: 29.3.0
- Docker Compose: v5.1.0

## API Surface

- `GET /health`
- `POST /api/accounts`
- `POST /api/transactions`

## Practical Notes

- The container image uses a multi-stage build so only the published binaries reach the runtime layer.
- The local compose setup stays explicit to keep startup behavior predictable.
- The repository is structured to keep business rules testable independently from infrastructure details.

## Horizontal scalability

This repository includes a reference HAProxy configuration (haproxy.cfg) and two API instances (pagueveloz-app-1 and pagueveloz-app-2) defined in docker-compose.yml. The configuration demonstrates basic HTTP request distribution and health checking using a round-robin algorithm and is intended for local development and staging.

To run the load-balanced environment locally, build and start the required services:

```bash
docker compose up -d --build haproxy pagueveloz-app-1 pagueveloz-app-2 postgres rabbitmq redis
```

You can smoke-test the stack using:

```bash
curl -v http://localhost:9999/health
```

Operational notes:

- The HAProxy setup in this repository is a pragmatic convenience for testing and does not aim to represent a production deployment. Production-grade horizontal scaling requires an orchestrator that provides service discovery, rolling updates, autoscaling, and lifecycle management.
- For production environments we recommend Kubernetes: it provides built-in primitives for scaling, health checks, service routing, rolling upgrades, and integration with service meshes and observability tooling.
- The persistence model is unchanged by horizontalizing the API layer: PostgreSQL remains the single source of truth and transactional semantics do not change.
- If you need session affinity, path-based routing, or advanced routing logic, extend the HAProxy configuration rather than embedding routing decisions in the application.
