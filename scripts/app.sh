#!/usr/bin/env bash
# App lifecycle helper for SplitBook. Designed to be called from opencode
# agents — handles the shell tricks needed to background a long-running
# dotnet server without hanging the caller's pipes.
#
# Usage:
#   scripts/app.sh start     — start the API in the background on PORT
#   scripts/app.sh stop      — kill the running API (if any)
#   scripts/app.sh reset     — stop + remove app.db (fresh filesystem)
#   scripts/app.sh status    — is it running? on what port? /health code?
#   scripts/app.sh smoke     — reset + start + health-check + stop
#   scripts/app.sh smoke-keep — reset + start + health-check (leave running)
#
# Environment:
#   PORT           — port to bind (default: 5124)
#   APP_URLS       — full ASPNETCORE_URLS (overrides PORT)
#   APP_ENV        — ASPNETCORE_ENVIRONMENT (default: Development)
#   READY_TIMEOUT  — seconds to wait for /health before giving up (default: 30)

set -u
set -o pipefail

PORT="${PORT:-5124}"
APP_URLS="${APP_URLS:-http://localhost:${PORT}}"
APP_ENV="${APP_ENV:-Development}"
READY_TIMEOUT="${READY_TIMEOUT:-30}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_DIR="${REPO_ROOT}/src/SplitBook.Api"
LOG_DIR="${REPO_ROOT}/harness/logs/runs"
LOG_FILE="${LOG_DIR}/app-last.log"
PID_FILE="${LOG_DIR}/app.pid"
DB_FILE="${API_DIR}/app.db"

mkdir -p "${LOG_DIR}"

die() { echo "!! $*" >&2; exit 1; }
say() { echo "-- $*"; }

stop_running() {
  local pid
  if [[ -f "${PID_FILE}" ]]; then
    pid="$(cat "${PID_FILE}" 2>/dev/null || true)"
    if [[ -n "${pid}" ]] && kill -0 "${pid}" 2>/dev/null; then
      say "stopping pid ${pid}"
      kill "${pid}" 2>/dev/null || true
      for _ in $(seq 1 10); do
        kill -0 "${pid}" 2>/dev/null || break
        sleep 0.5
      done
      kill -0 "${pid}" 2>/dev/null && { say "force killing pid ${pid}"; kill -9 "${pid}" 2>/dev/null || true; }
    fi
    rm -f "${PID_FILE}"
  fi
  # Belt-and-suspenders: sweep any orphan SplitBook.Api processes
  pkill -f "SplitBook.Api" 2>/dev/null || true
  # Give the kernel a moment to release the port
  sleep 1
}

wait_for_health() {
  local url="${APP_URLS%/}/health"
  local deadline=$(( $(date +%s) + READY_TIMEOUT ))
  local code
  while (( $(date +%s) < deadline )); do
    code="$(curl -s -o /dev/null -w "%{http_code}" "${url}" 2>/dev/null || echo 000)"
    if [[ "${code}" == "200" ]]; then return 0; fi
    sleep 0.5
  done
  return 1
}

cmd_start() {
  stop_running
  say "starting API on ${APP_URLS} (env=${APP_ENV})"

  # The nohup + disown + full FD redirection is what makes this safe
  # to call from opencode's Bash tool without pipe-hang.
  (
    cd "${API_DIR}"
    ASPNETCORE_ENVIRONMENT="${APP_ENV}" \
    ASPNETCORE_URLS="${APP_URLS}" \
    nohup dotnet run --no-build </dev/null >"${LOG_FILE}" 2>&1 &
    echo $! >"${PID_FILE}"
    disown
  )

  if ! wait_for_health; then
    say "API did not become healthy within ${READY_TIMEOUT}s. Last 40 log lines:"
    tail -n 40 "${LOG_FILE}" >&2 || true
    return 1
  fi
  say "API up (pid $(cat "${PID_FILE}"), log ${LOG_FILE})"
}

cmd_stop() {
  stop_running
  say "stopped"
}

cmd_reset() {
  stop_running
  if [[ -f "${DB_FILE}" ]]; then
    rm -f "${DB_FILE}"
    say "removed ${DB_FILE}"
  fi
  # SQLite WAL/SHM siblings
  rm -f "${DB_FILE}-shm" "${DB_FILE}-wal" 2>/dev/null || true
}

cmd_status() {
  if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" 2>/dev/null; then
    local code
    code="$(curl -s -o /dev/null -w "%{http_code}" "${APP_URLS%/}/health" 2>/dev/null || echo 000)"
    say "running (pid $(cat "${PID_FILE}"), /health=${code}, url=${APP_URLS})"
  else
    say "not running"
  fi
}

probe() {
  local path="$1"
  local code
  code="$(curl -s -o /dev/null -w "%{http_code}" "${APP_URLS%/}${path}" 2>/dev/null || echo 000)"
  printf '   %-36s %s\n' "${path}" "${code}"
  [[ "${code}" =~ ^2|^3 ]] || return 1
}

cmd_smoke() {
  cmd_reset
  cmd_start || return 1
  say "smoke probes:"
  local ok=0
  probe "/health"                   || ok=1
  probe "/swagger"                  || ok=1
  probe "/swagger/v1/swagger.json"  || ok=1
  if (( ok == 0 )); then
    say "smoke PASS"
  else
    say "smoke FAIL — see ${LOG_FILE}"
    tail -n 40 "${LOG_FILE}" >&2 || true
  fi
  cmd_stop
  return "${ok}"
}

cmd_smoke_keep() {
  cmd_reset
  cmd_start || return 1
  say "smoke probes:"
  local ok=0
  probe "/health"                   || ok=1
  probe "/swagger"                  || ok=1
  probe "/swagger/v1/swagger.json"  || ok=1
  if (( ok == 0 )); then
    say "smoke PASS (API left running on ${APP_URLS})"
  else
    say "smoke FAIL — see ${LOG_FILE}"
  fi
  return "${ok}"
}

case "${1:-}" in
  start)      cmd_start ;;
  stop)       cmd_stop ;;
  reset)      cmd_reset ;;
  status)     cmd_status ;;
  smoke)      cmd_smoke ;;
  smoke-keep) cmd_smoke_keep ;;
  *) die "usage: $0 {start|stop|reset|status|smoke|smoke-keep}" ;;
esac
