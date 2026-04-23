#!/usr/bin/env bash
set -euo pipefail

# SplitBook smoke test — validates the golden path against a real running API
# Usage: bash scripts/smoke.sh

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
API_DIR="$PROJECT_DIR/src/SplitBook.Api"
HOST="127.0.0.1"
PORT=5080
BASE_URL="http://${HOST}:${PORT}"
DB_FILE="$API_DIR/splitbook.db"
LOG_FILE="/tmp/splitbook-smoke.log"
PID_FILE="/tmp/splitbook-smoke.pid"
PASS=0
FAIL=0
TEST_USER_EMAIL="smoke-$(date +%s)@splitbook.test"
TEST_USER_PASSWORD="SmokeTest123!"
TEST_USER_NAME="Smoke User"
JWT=""

cleanup() {
    if [ -f "$PID_FILE" ]; then
        kill "$(cat "$PID_FILE")" 2>/dev/null || true
        rm -f "$PID_FILE"
    fi
    rm -f "$DB_FILE"
}

trap cleanup EXIT

assert_http() {
    local desc="$1" url="$2" expected="$3" actual
    actual=$(curl -s -o /dev/null -w "%{http_code}" "$url" 2>/dev/null)
    if [ "$actual" = "$expected" ]; then
        echo "  PASS: $desc (got $actual)"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $desc (expected $expected, got $actual)"
        FAIL=$((FAIL + 1))
        return 1
    fi
}

echo "=== SplitBook Smoke Test ==="
echo ""

# Clean state
echo "[1/8] Cleaning up any previous state..."
rm -f "$DB_FILE"
rm -f "$LOG_FILE"

# Build
echo "[2/8] Building project..."
dotnet build "$PROJECT_DIR" --verbosity quiet --nologo

# Start API in background
echo "[3/8] Starting API on ${BASE_URL}..."
dotnet run --project "$API_DIR" --no-build --urls "$BASE_URL" > "$LOG_FILE" 2>&1 &
echo $! > "$PID_FILE"

# Wait for health
echo "[4/8] Waiting for /health..."
TIMEOUT=30
ELAPSED=0
while [ $ELAPSED -lt $TIMEOUT ]; do
    HEALTH=$(curl -s -o /dev/null -w "%{http_code}" "${BASE_URL}/health" 2>/dev/null || echo "000")
    if [ "$HEALTH" = "200" ]; then
        echo "  API is up after ${ELAPSED}s"
        break
    fi
    sleep 1
    ELAPSED=$((ELAPSED + 1))
done
if [ "$HEALTH" != "200" ]; then
    echo "  FAIL: /health did not return 200 within ${TIMEOUT}s"
    cat "$LOG_FILE"
    exit 1
fi

# Register
echo "[5/8] POST /auth/register..."
REG_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/auth/register" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"${TEST_USER_EMAIL}\",\"displayName\":\"${TEST_USER_NAME}\",\"password\":\"${TEST_USER_PASSWORD}\"}")
REG_CODE=$(echo "$REG_RESPONSE" | tail -1)
REG_BODY=$(echo "$REG_RESPONSE" | head -n -1)
if [ "$REG_CODE" = "201" ]; then
    echo "  PASS: Register returned 201"
    PASS=$((PASS + 1))
else
    echo "  FAIL: Register returned $REG_CODE (body: $REG_BODY)"
    FAIL=$((FAIL + 1))
fi

# Login
echo "[6/8] POST /auth/login..."
LOGIN_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "${BASE_URL}/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"${TEST_USER_EMAIL}\",\"password\":\"${TEST_USER_PASSWORD}\"}")
LOGIN_CODE=$(echo "$LOGIN_RESPONSE" | tail -1)
LOGIN_BODY=$(echo "$LOGIN_RESPONSE" | head -n -1)
if [ "$LOGIN_CODE" = "200" ]; then
    echo "  PASS: Login returned 200"
    PASS=$((PASS + 1))
    JWT=$(echo "$LOGIN_BODY" | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])" 2>/dev/null || echo "")
    if [ -z "$JWT" ]; then
        echo "  FAIL: Could not extract accessToken from login response"
        FAIL=$((FAIL + 1))
    fi
else
    echo "  FAIL: Login returned $LOGIN_CODE (body: $LOGIN_BODY)"
    FAIL=$((FAIL + 1))
fi

# GET /groups without token
echo "[7/8] GET /groups without token..."
NOAUTH_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${BASE_URL}/groups/" 2>/dev/null)
if [ "$NOAUTH_CODE" = "401" ]; then
    echo "  PASS: /groups without token returned 401"
    PASS=$((PASS + 1))
else
    echo "  FAIL: /groups without token returned $NOAUTH_CODE (expected 401)"
    FAIL=$((FAIL + 1))
fi

# GET /groups with token
echo "[8/8] GET /groups with token..."
AUTH_CODE=$(curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer ${JWT}" "${BASE_URL}/groups/" 2>/dev/null)
if [ "$AUTH_CODE" = "200" ]; then
    echo "  PASS: /groups with token returned 200"
    PASS=$((PASS + 1))
else
    echo "  FAIL: /groups with token returned $AUTH_CODE (expected 200)"
    FAIL=$((FAIL + 1))
fi

echo ""
echo "=== Results: $PASS passed, $FAIL failed ==="

if [ $FAIL -gt 0 ]; then
    echo "SMOKE TEST FAILED"
    exit 1
fi

echo "SMOKE TEST PASSED"
exit 0
