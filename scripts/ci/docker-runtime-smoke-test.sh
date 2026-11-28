#!/usr/bin/env bash
# Runs the already-built API image against an isolated PostgreSQL container and
# verifies both readiness recovery and readiness failure when PostgreSQL is down.
set -Eeuo pipefail

image="${1:-ecommerce-api:ci}"
: "${SMOKE_POSTGRES_PASSWORD:=ci_smoke_postgres_password_012345}"
: "${SMOKE_JWT_SECRET:=ci_smoke_jwt_secret_key_with_more_than_32_bytes}"

suffix="${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-1}-$$"
network="ecommerce-smoke-${suffix}"
postgres_container="ecommerce-smoke-postgres-${suffix}"
api_container="ecommerce-smoke-api-${suffix}"
temp_root="${RUNNER_TEMP:-${TMPDIR:-/tmp}}"
mkdir -p "$temp_root"
response_file="${temp_root}/ecommerce-smoke-health-${suffix}.json"

assert_health_payload() {
  local expected_overall_status="$1"
  local expected_database_status="$2"
  local context="$3"

  if ! grep -Eq "^[[:space:]]*\\{[[:space:]]*\\\"status\\\"[[:space:]]*:[[:space:]]*\\\"${expected_overall_status}\\\"" "$response_file"; then
    echo "Unexpected overall health status ${context}:"
    cat "$response_file"
    return 1
  fi

  if ! grep -Eq "\\\"name\\\"[[:space:]]*:[[:space:]]*\\\"database\\\"[[:space:]]*,[[:space:]]*\\\"status\\\"[[:space:]]*:[[:space:]]*\\\"${expected_database_status}\\\"" "$response_file"; then
    echo "Unexpected database health status ${context}:"
    cat "$response_file"
    return 1
  fi
}

cleanup() {
  docker rm --force "$api_container" "$postgres_container" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker network create "$network" >/dev/null

docker run --detach \
  --name "$postgres_container" \
  --network "$network" \
  --network-alias postgres \
  --env POSTGRES_DB=ecommerce \
  --env POSTGRES_USER=postgres \
  --env "POSTGRES_PASSWORD=${SMOKE_POSTGRES_PASSWORD}" \
  --health-cmd "pg_isready -U postgres -d ecommerce" \
  --health-interval 2s \
  --health-timeout 3s \
  --health-retries 30 \
  postgres:15-alpine >/dev/null

echo "Waiting for the smoke-test PostgreSQL container to become healthy..."
db_status="starting"
for attempt in $(seq 1 60); do
  db_status="$(docker inspect --format '{{.State.Health.Status}}' "$postgres_container" 2>/dev/null || true)"
  if [[ "$db_status" == "healthy" ]]; then
    break
  fi
  if [[ "$db_status" == "unhealthy" ]]; then
    docker logs "$postgres_container"
    exit 1
  fi
  sleep 2
done

if [[ "$db_status" != "healthy" ]]; then
  echo "PostgreSQL did not become healthy (status: ${db_status:-unknown})."
  docker logs "$postgres_container"
  exit 1
fi

docker run --detach \
  --name "$api_container" \
  --network "$network" \
  --publish 127.0.0.1::8080 \
  --env ASPNETCORE_ENVIRONMENT=Development \
  --env ASPNETCORE_HTTP_PORTS=8080 \
  --env "ConnectionStrings__ECommerceDb=Host=postgres;Port=5432;Database=ecommerce;Username=postgres;Password=${SMOKE_POSTGRES_PASSWORD};GSS Encryption Mode=Disable" \
  --env "Jwt__SecretKey=${SMOKE_JWT_SECRET}" \
  --env Jwt__Issuer=ECommerce.API \
  --env Jwt__Audience=ECommerce.API \
  --env HttpsRedirection__Enabled=false \
  --env SmtpEmail__Enabled=false \
  "$image" >/dev/null

host_port="$(docker port "$api_container" 8080/tcp 2>/dev/null | awk -F: 'END {print $NF}' || true)"
if [[ -z "$host_port" ]]; then
  echo "Could not determine the API container's published host port."
  docker logs "$api_container"
  exit 1
fi
health_url="http://127.0.0.1:${host_port}"

echo "Waiting for the API readiness endpoint at ${health_url}/health/ready..."
readiness_ok=false
for attempt in $(seq 1 90); do
  http_code="$(curl --silent --show-error --max-time 5 \
    --output "$response_file" --write-out '%{http_code}' \
    "${health_url}/health/ready" 2>/dev/null || true)"

  if [[ "$http_code" == "200" ]]; then
    assert_health_payload "Healthy" "Healthy" "while PostgreSQL is available"
    echo "Readiness is Healthy and the database check is Healthy."
    readiness_ok=true
    break
  fi

  container_running="$(docker inspect --format '{{.State.Running}}' "$api_container" 2>/dev/null || echo false)"
  if [[ "$container_running" != "true" ]]; then
    echo "The API container stopped before becoming ready (HTTP ${http_code:-unknown})."
    docker logs "$api_container"
    exit 1
  fi
  sleep 2
done

if [[ "$readiness_ok" != "true" ]]; then
  echo "The API did not become ready within the expected time."
  [[ -f "$response_file" ]] && cat "$response_file"
  docker logs "$api_container"
  exit 1
fi

curl --fail --silent --show-error "${health_url}/health/live" >/dev/null
echo "Liveness endpoint returned HTTP 200."

echo "Stopping PostgreSQL to verify that readiness detects database loss..."
docker stop "$postgres_container" >/dev/null
database_loss_detected=false
for attempt in $(seq 1 20); do
  http_code="$(curl --silent --show-error --max-time 5 \
    --output "$response_file" --write-out '%{http_code}' \
    "${health_url}/health/ready" 2>/dev/null || true)"

  if [[ "$http_code" == "503" ]]; then
    assert_health_payload "Unhealthy" "Unhealthy" "after PostgreSQL was stopped"
    echo "Readiness correctly reports Unhealthy and the database check is Unhealthy."
    database_loss_detected=true
    break
  fi
  sleep 2
done

if [[ "$database_loss_detected" != "true" ]]; then
  echo "Readiness did not return HTTP 503 after PostgreSQL was stopped."
  [[ -f "$response_file" ]] && cat "$response_file"
  docker logs "$api_container"
  exit 1
fi

curl --fail --silent --show-error "${health_url}/health/live" >/dev/null
echo "Liveness remains HTTP 200 while the database is unavailable."

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "## Docker runtime smoke test"
    echo "- API container started successfully from the built image."
    echo "- \`/health/ready\` returned \`200 Healthy\` with PostgreSQL available."
    echo "- \`/health/ready\` returned \`503 Unhealthy\` after PostgreSQL stopped."
    echo "- \`/health/live\` remained available during the database outage."
  } >> "$GITHUB_STEP_SUMMARY"
fi
