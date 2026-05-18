# pagueveloz

## Overview

PagueVeloz is a compact financial transaction service built with Clean Architecture and DDD principles. The intent is simple: keep the domain explicit, protect business invariants, and make the system easy to reason about when it is under load or when something fails.

The codebase is optimized for correctness first. The transactional path stays simple, the runtime dependencies remain small, and the system avoids patterns that would add ceremony without bringing real value at this stage.

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

