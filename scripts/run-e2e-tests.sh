#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

MARKER_FILE="$ROOT_DIR/.docker_helpers_done"
if [ ! -f "$MARKER_FILE" ]; then
  echo "docker-helpers has not been run. Please run ./scripts/docker-helpers.sh --build before running this script." >&2
  exit 3
fi

WAIT_TIMEOUT=${WAIT_TIMEOUT:-120}
echo "Waiting for API health on http://localhost:9999/health (timeout ${WAIT_TIMEOUT}s)..."
elapsed=0
while true; do
  if curl -s -o /dev/null -w "%{http_code}" http://localhost:9999/health 2>/dev/null | grep -q "200"; then
    echo "API healthy"
    break
  fi
  sleep 1
  elapsed=$((elapsed + 1))
  if [ "$elapsed" -ge "$WAIT_TIMEOUT" ]; then
    echo "Timed out waiting for API health" >&2
    docker compose logs --no-color || true
    docker compose down --volumes --remove-orphans
    exit 2
  fi
done

# Wait for RabbitMQ TCP port on localhost:5672 to be ready (avoid repeating inside health loop)
echo "Waiting for RabbitMQ on localhost:5672 (timeout ${WAIT_TIMEOUT}s)..."
if ! timeout ${WAIT_TIMEOUT}s bash -c 'until</dev/tcp/localhost/5672 >/dev/null 2>&1; do sleep 1; done'; then
  echo "RabbitMQ TCP port not ready in time"
  docker compose logs rabbitmq | sed -n '1,200p'
  exit 1
fi

TMPRESP=$(mktemp)
function request_and_expect() {
  local method="$1"; local path="$2"; local data="$3"; local expect_code=$4
  echo "--> $method $path $data"
  status=$(curl -s -o "$TMPRESP" -w "%{http_code}" -H "Content-Type: application/json" -X "$method" "http://localhost:9999$path" -d "$data")
  echo "HTTP $status"
  echo "Response:"; cat "$TMPRESP"; echo
  if [ "$status" -ne "$expect_code" ]; then
    echo "Unexpected HTTP status $status (expected $expect_code) for $method $path" >&2
    docker compose logs --no-color || true
    docker compose down --volumes --remove-orphans
    rm -f "$TMPRESP"
    exit 3
  fi
}

echo "Starting user-like scenario..."

# 1) Create two accounts
request_and_expect POST /api/accounts '{"ClientId":"CLI-1","AccountId":"ACC-1","InitialBalance":0,"CreditLimit":50000}' 200
request_and_expect POST /api/accounts '{"ClientId":"CLI-1","AccountId":"ACC-2","InitialBalance":0,"CreditLimit":50000}' 200

# 2) Credit ACC-1
request_and_expect POST /api/transactions '{"Operation":"credit","AccountId":"ACC-1","Amount":100000,"Currency":"BRL","ReferenceId":"r1"}' 200

# 3) Debit ACC-1
request_and_expect POST /api/transactions '{"Operation":"debit","AccountId":"ACC-1","Amount":20000,"Currency":"BRL","ReferenceId":"r2"}' 200

# 4) Reserve then capture
request_and_expect POST /api/transactions '{"Operation":"reserve","AccountId":"ACC-1","Amount":30000,"Currency":"BRL","ReferenceId":"r3"}' 200
request_and_expect POST /api/transactions '{"Operation":"capture","AccountId":"ACC-1","Amount":30000,"Currency":"BRL","ReferenceId":"r4"}' 200

# 5) Transfer from ACC-1 to ACC-2
request_and_expect POST /api/transactions '{"Operation":"transfer","AccountId":"ACC-1","TargetAccountId":"ACC-2","Amount":25000,"Currency":"BRL","ReferenceId":"r5"}' 200

echo "All scenario steps succeeded. Collecting logs and shutting down services..."
docker compose logs --no-color > e2e-logs.txt || true
docker compose down --volumes --remove-orphans
rm -f "$TMPRESP"

echo "E2E scenario completed successfully. Logs saved to e2e-logs.txt"

# Remove docker-helpers marker so next run needs explicit helper invocation
MARKER_FILE="$ROOT_DIR/.docker_helpers_done"
if [ -f "$MARKER_FILE" ]; then
  rm -f "$MARKER_FILE"
  echo "Removed marker $MARKER_FILE"
fi
