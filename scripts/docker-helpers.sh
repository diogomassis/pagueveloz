#!/usr/bin/env bash
set -euo pipefail

# docker-helpers.sh
# Purpose: Centralize docker compose startup and automated repair steps
# Usage: ./scripts/docker-helpers.sh [--build]

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

BUILD_FLAG=0
if [ "${1:-}" = "--build" ] || [ "${1:-}" = "build" ]; then
  BUILD_FLAG=1
fi

MARKER_FILE="$ROOT_DIR/.docker_helpers_done"

echo "Running docker helpers (build=$BUILD_FLAG)"

echo "Cleaning previous compose state and project volumes that commonly cause issues..."
docker compose down --volumes --remove-orphans >/dev/null 2>&1 || true
docker volume rm pagueveloz_postgres_data pagueveloz_rabbitmq_data pagueveloz_redis_data >/dev/null 2>&1 || true

if [ "$BUILD_FLAG" -eq 1 ]; then
  echo "Bringing up all services with build..."
  docker compose up -d --build
else
  echo "Bringing up minimal infra services (postgres redis rabbitmq)..."
  docker compose up -d postgres redis rabbitmq
fi

# Detect early rabbitmq .erlang.cookie permission error and attempt auto-repair
sleep 2
if docker compose logs rabbitmq --no-color | grep -q "\.erlang.cookie: eacces"; then
  echo "Detected .erlang.cookie permission error in RabbitMQ logs — attempting repair"
  docker compose down --volumes --remove-orphans >/dev/null 2>&1 || true
  docker volume rm pagueveloz_rabbitmq_data >/dev/null 2>&1 || true
  docker compose up -d postgres redis rabbitmq
  sleep 2
  if docker compose logs rabbitmq --no-color | grep -q "\.erlang.cookie: eacces"; then
    echo "Permission error persists — removing .erlang.cookie inside the named volume"
    docker run --rm -v pagueveloz_rabbitmq_data:/data busybox sh -c 'rm -f /data/.erlang.cookie || true; echo "removed cookie if present"'
    echo "Retrying to bring up services after cleaning cookie"
    docker compose down --volumes --remove-orphans >/dev/null 2>&1 || true
    if [ "$BUILD_FLAG" -eq 1 ]; then
      docker compose up -d --build
    else
      docker compose up -d postgres redis rabbitmq
    fi
  fi
fi

echo "Waiting briefly for containers to settle..."
sleep 3

if docker compose ps rabbitmq --services --filter status=running | grep -q "rabbitmq"; then
  echo "RabbitMQ running"
fi

touch "$MARKER_FILE"
echo "Created marker file $MARKER_FILE — you can now run the integration and E2E scripts"
echo "To rebuild app images as part of the boot, re-run with: ./scripts/docker-helpers.sh --build"

exit 0
#!/usr/bin/env bash
set -euo pipefail

# Helper functions to manage docker-compose services used by integration/e2e scripts.
# Designed so the test scripts can call a single function and get reproducible
# recovery from common issues (RabbitMQ .erlang.cookie permission, stale volumes).

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

bring_up_services() {
  local services="${1:-postgres redis rabbitmq}"
  local build_flag="${2:-}" # pass 'build' to run `docker compose up -d --build`

  echo "Cleaning previous compose state and volumes..."
  docker compose down --volumes --remove-orphans >/dev/null 2>&1 || true
  echo "Cleaning stale volumes: pagueveloz_postgres_data pagueveloz_rabbitmq_data pagueveloz_redis_data"
  docker volume rm pagueveloz_postgres_data pagueveloz_rabbitmq_data pagueveloz_redis_data >/dev/null 2>&1 || true

  echo "Bringing up services: $services ${build_flag:+(with build)}"
  if [ "$build_flag" = "build" ]; then
    docker compose up -d --build $services
  else
    docker compose up -d $services
  fi

  # Detect early rabbitmq .erlang.cookie permission error and retry once after cleaning volumes
  sleep 2
  if docker compose logs rabbitmq --no-color | grep -q "\.erlang.cookie: eacces"; then
    echo "Detected .erlang.cookie permission error in RabbitMQ logs — retrying after removing rabbitmq_data volume"
    docker compose down --volumes --remove-orphans >/dev/null 2>&1 || true
    docker volume rm pagueveloz_rabbitmq_data >/dev/null 2>&1 || true
    if [ "$build_flag" = "build" ]; then
      docker compose up -d --build $services
    else
      docker compose up -d $services
    fi

    # Re-check logs; if still failing, attempt to remove the cookie file inside the volume
    sleep 2
    if docker compose logs rabbitmq --no-color | grep -q "\.erlang.cookie: eacces"; then
      echo "Permission error persists — attempting to remove .erlang.cookie from volume using a temporary container"
      docker run --rm -v pagueveloz_rabbitmq_data:/data busybox sh -c 'ls -la /data || true; rm -f /data/.erlang.cookie || true; echo "Removed .erlang.cookie if present"'
      echo "Retrying to bring up RabbitMQ after cleaning cookie"
      docker compose down --volumes --remove-orphans >/dev/null 2>&1 || true
      if [ "$build_flag" = "build" ]; then
        docker compose up -d --build $services
      else
        docker compose up -d $services
      fi
    fi
  fi
}

wait_for_tcp() {
  local host=${1:-localhost}
  local port=${2:-5672}
  local timeout=${3:-60}
  local elapsed=0
  while ! (echo > /dev/tcp/${host}/${port}) 2>/dev/null; do
    sleep 1
    elapsed=$((elapsed + 1))
    if [ "$elapsed" -ge "$timeout" ]; then
      echo "Timed out waiting for TCP port ${port} on ${host}" >&2
      return 1
    fi
  done
  return 0
}

echo "docker-helpers loaded"
