#!/usr/bin/env bash
set -euo pipefail

# Usage: TEST_CONNECTION_STRING="$CONNECTION_STRING" ./scripts/run-integration-tests.sh

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

COMPOSE_FILE="docker-compose.yml"

# Services to start — default to postgres, redis, rabbitmq
SERVICES="${SERVICES:-postgres redis rabbitmq}"

MARKER_FILE="$ROOT_DIR/.docker_helpers_done"
if [ ! -f "$MARKER_FILE" ]; then
  echo "docker-helpers has not been run. Please run ./scripts/docker-helpers.sh (or with --build) before running this script." >&2
  exit 3
fi

# Defaults for test connection strings / envs
if [ -z "${TEST_CONNECTION_STRING:-}" ]; then
  export TEST_CONNECTION_STRING="Host=localhost;Port=5432;Database=pagueveloz;Username=pagueveloz;Password=pagueveloz"
fi
if [ -z "${TEST_REDIS_CONNECTION:-}" ]; then
  export TEST_REDIS_CONNECTION="localhost:6379"
fi
if [ -z "${TEST_RABBIT_HOST:-}" ]; then
  export TEST_RABBIT_HOST="localhost"
fi
if [ -z "${TEST_RABBIT_PORT:-}" ]; then
  export TEST_RABBIT_PORT="5672"
fi
if [ -z "${TEST_RABBIT_USER:-}" ]; then
  export TEST_RABBIT_USER="pagueveloz"
fi
if [ -z "${TEST_RABBIT_PASS:-}" ]; then
  export TEST_RABBIT_PASS="pagueveloz"
fi

echo "Using TEST_CONNECTION_STRING=$TEST_CONNECTION_STRING"
echo "Using TEST_REDIS_CONNECTION=$TEST_REDIS_CONNECTION"
echo "Using TEST_RABBIT_HOST=$TEST_RABBIT_HOST TEST_RABBIT_PORT=$TEST_RABBIT_PORT"

echo "Waiting for services to accept connections (timeout ${WAIT_TIMEOUT:-60}s) ..."
timeout=${WAIT_TIMEOUT:-60}
elapsed=0
interval=1

while true; do
  all_ready=true

  if echo "$SERVICES" | grep -qw postgres; then
    if ! docker compose exec -T postgres pg_isready -U pagueveloz -d pagueveloz >/dev/null 2>&1; then
      all_ready=false
    fi
  fi

  if echo "$SERVICES" | grep -qw redis; then
    if ! docker compose exec -T redis redis-cli ping >/dev/null 2>&1; then
      all_ready=false
    fi
  fi

  if echo "$SERVICES" | grep -qw rabbitmq; then
    if ! docker compose exec -T rabbitmq rabbitmq-diagnostics -q ping >/dev/null 2>&1; then
      all_ready=false
    fi
  fi

  if [ "$all_ready" = true ]; then
    echo "All requested services are ready"
    break
  fi

  sleep "$interval"
  elapsed=$((elapsed + interval))
  if [ "$elapsed" -ge "$timeout" ]; then
    echo "Timed out waiting for services (waited ${timeout}s)" >&2
    docker compose logs --no-color || true
    docker compose down
    exit 2
  fi
done

# give services a small additional moment to finish startup tasks
sleep 3

# Ensure AMQP port on localhost is accepting TCP connections (avoid race where healthcheck passes but port not yet bound)
echo "Waiting for TCP port ${TEST_RABBIT_PORT} on localhost to accept connections (timeout ${WAIT_TIMEOUT:-60}s)..."
elapsed_tcp=0
while ! (echo > /dev/tcp/localhost/${TEST_RABBIT_PORT}) 2>/dev/null; do
  sleep 1
  elapsed_tcp=$((elapsed_tcp + 1))
  if [ "$elapsed_tcp" -ge "$timeout" ]; then
    echo "Timed out waiting for TCP port ${TEST_RABBIT_PORT} on localhost" >&2
    docker compose logs --no-color || true
    docker compose down
    exit 2
  fi
done


# If SKIP_TESTS=1 is set, stop here and leave services up (caller will run tests)
if [ "${SKIP_TESTS:-0}" = "1" ]; then
  echo "SKIP_TESTS=1 set — skipping test run and leaving services up"
  exit 0
fi

echo "Running integration tests..."
# Export legacy env var used by some tests
export CONNECTION_STRING="$TEST_CONNECTION_STRING"
export TEST_REDIS_CONNECTION
export TEST_RABBIT_HOST
export TEST_RABBIT_PORT
export TEST_RABBIT_USER
export TEST_RABBIT_PASS

dotnet test PagueVeloz.sln --filter "Category=Integration" -v minimal /p:ConnectionStrings__Default="$TEST_CONNECTION_STRING"
TEST_EXIT_CODE=$?

echo "Tearing down compose services..."
docker compose down

# Remove marker so user must explicitly run docker-helpers before next run
MARKER_FILE="$ROOT_DIR/.docker_helpers_done"
if [ -f "$MARKER_FILE" ]; then
  rm -f "$MARKER_FILE"
  echo "Removed marker $MARKER_FILE"
fi

exit $TEST_EXIT_CODE
