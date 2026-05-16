#!/usr/bin/env bash
# Frontend preflight: the SplitBook frontend depends on the live .NET backend.
# Verifies the backend is up at http://localhost:5000 and saves its OpenAPI
# contract to specs/openapi.json. Exit 0 = OK (contract written), non-zero = FULL STOP.
set -euo pipefail

BASE="${SPLITBOOK_API_URL:-http://localhost:5000}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/specs/openapi.json"

if ! curl -sf "$BASE/health" -o /dev/null; then
  echo "FULL STOP: backend not reachable at $BASE/health"
  echo "Operator must start it: ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=$BASE dotnet run --project src/SplitBook.Api/SplitBook.Api.csproj"
  exit 1
fi

if ! curl -sf "$BASE/swagger/v1/swagger.json" -o "$OUT"; then
  echo "FULL STOP: backend up but swagger.json not served at $BASE (is it running in Development?)"
  exit 1
fi

PATHS=$(python3 -c 'import json,sys; print(len(json.load(open(sys.argv[1]))["paths"]))' "$OUT" 2>/dev/null) || {
  echo "FULL STOP: $OUT is not valid OpenAPI JSON"
  exit 1
}

echo "backend OK — $PATHS endpoints, contract saved to specs/openapi.json"
