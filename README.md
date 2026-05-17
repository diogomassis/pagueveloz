# pagueveloz

## Overview

PagueVeloz is a compact financial transaction service built with Clean Architecture and DDD principles. The intent is simple: keep the domain explicit, protect business invariants, and make the system easy to reason about when it is under load or when something fails.

The codebase is optimized for correctness first. The transactional path stays simple, the runtime dependencies remain small, and the system avoids patterns that would add ceremony without bringing real value at this stage.

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

