#!/usr/bin/env bash
# Frontend app-lifecycle helper for SplitBook.Web. Designed to be called from
# opencode agents — handles the shell tricks needed to background the Vite dev
# server without hanging the caller's pipes. Frontend analogue of the backend
# scripts/app.sh (dotnet) — same command surface, different toolchain.
#
# Usage:
#   scripts/app.sh build      — pnpm build (clean: removes dist first)
#   scripts/app.sh start      — start Vite dev server in the background on PORT
#   scripts/app.sh stop       — kill the running dev server (if any)
#   scripts/app.sh reset      — stop + remove dist/ (fresh build artifact state)
#   scripts/app.sh status     — is it running? on what port? HTTP code?
#   scripts/app.sh smoke      — reset + build + start dev + probe + stop
#   scripts/app.sh smoke-keep — reset + build + start dev + probe (leave running)
#
# Environment:
#   PORT           — dev server port (default: 5173, per technical-spec §9 DoD)
#   READY_TIMEOUT  — seconds to wait for the dev server (default: 40)
#
# Notes:
#   - pnpm/node come from the launching login shell's PATH; we also prepend
#     ~/.local/node/bin defensively in case this runs under a non-login shell.
#   - Never run `pnpm dev` / `vite` directly from the opencode Bash tool: it
#     does not detach cleanly and the caller hangs. Always go through this.
#   - Vitest is NOT started here. Tests run via `pnpm exec vitest run` (non-watch).

set -u
set -o pipefail

export PATH="${HOME}/.local/node/bin:${PATH}"

PORT="${PORT:-5173}"
READY_TIMEOUT="${READY_TIMEOUT:-40}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB_DIR="${REPO_ROOT}/src/SplitBook.Web"
LOG_DIR="${REPO_ROOT}/harness/logs/runs"
LOG_FILE="${LOG_DIR}/app-last.log"
PID_FILE="${LOG_DIR}/app.pid"

mkdir -p "${LOG_DIR}"

die() { echo "!! $*" >&2; exit 1; }
say() { echo "-- $*"; }

require_web_dir() {
  [[ -d "${WEB_DIR}" ]] || die "web dir ${WEB_DIR} does not exist yet — slice 0 (Bootstrap) must scaffold it first"
  [[ -f "${WEB_DIR}/package.json" ]] || die "${WEB_DIR}/package.json missing — Vite project not initialised"
}

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
  # Belt-and-suspenders: sweep any orphan vite dev servers for this project
  pkill -f "vite.*${WEB_DIR}" 2>/dev/null || true
  pkill -f "${WEB_DIR}.*vite" 2>/dev/null || true
  sleep 1
}

wait_for_http() {
  local url="http://localhost:${PORT}/"
  local deadline=$(( $(date +%s) + READY_TIMEOUT ))
  local code
  while (( $(date +%s) < deadline )); do
    code="$(curl -s -o /dev/null -w "%{http_code}" "${url}" 2>/dev/null || echo 000)"
    if [[ "${code}" == "200" ]]; then return 0; fi
    sleep 0.5
  done
  return 1
}

cmd_build() {
  require_web_dir
  say "building (pnpm build) in ${WEB_DIR}"
  ( cd "${WEB_DIR}" && rm -rf dist && pnpm build ) 2>&1 | tee "${LOG_FILE}"
  local rc="${PIPESTATUS[0]}"
  if (( rc != 0 )); then
    say "build FAILED (rc=${rc}) — see ${LOG_FILE}"
    return 1
  fi
  say "build OK (artifacts in ${WEB_DIR}/dist)"
}

cmd_start() {
  require_web_dir
  stop_running
  say "starting Vite dev server on http://localhost:${PORT}"
  (
    cd "${WEB_DIR}"
    nohup pnpm exec vite --port "${PORT}" --strictPort </dev/null >"${LOG_FILE}" 2>&1 &
    echo $! >"${PID_FILE}"
    disown
  )
  if ! wait_for_http; then
    say "dev server did not answer on :${PORT} within ${READY_TIMEOUT}s. Last 40 log lines:"
    tail -n 40 "${LOG_FILE}" >&2 || true
    return 1
  fi
  say "dev server up (pid $(cat "${PID_FILE}"), log ${LOG_FILE})"
}

cmd_stop() {
  stop_running
  say "stopped"
}

cmd_reset() {
  stop_running
  if [[ -d "${WEB_DIR}/dist" ]]; then
    rm -rf "${WEB_DIR}/dist"
    say "removed ${WEB_DIR}/dist"
  fi
}

cmd_status() {
  if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}" 2>/dev/null)" 2>/dev/null; then
    local code
    code="$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:${PORT}/" 2>/dev/null || echo 000)"
    say "running (pid $(cat "${PID_FILE}"), HTTP=${code}, url=http://localhost:${PORT}/)"
  else
    say "not running"
  fi
}

probe() {
  local path="$1" needle="${2:-}"
  local body code
  body="$(curl -s -m 10 "http://localhost:${PORT}${path}" 2>/dev/null || true)"
  code="$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:${PORT}${path}" 2>/dev/null || echo 000)"
  if [[ -n "${needle}" ]]; then
    if grep -qi -- "${needle}" <<<"${body}"; then
      printf '   %-28s %s  (contains "%s")\n' "${path}" "${code}" "${needle}"
      return 0
    fi
    printf '   %-28s %s  (MISSING "%s")\n' "${path}" "${code}" "${needle}"
    return 1
  fi
  printf '   %-28s %s\n' "${path}" "${code}"
  [[ "${code}" =~ ^2|^3 ]] || return 1
}

_smoke() {
  cmd_reset
  cmd_build || return 1
  cmd_start || return 1
  say "smoke probes:"
  local ok=0
  probe "/" "SplitBook" || ok=1
  return "${ok}"
}

cmd_smoke() {
  _smoke; local ok=$?
  if (( ok == 0 )); then say "smoke PASS"; else say "smoke FAIL — see ${LOG_FILE}"; tail -n 40 "${LOG_FILE}" >&2 || true; fi
  cmd_stop
  return "${ok}"
}

cmd_smoke_keep() {
  _smoke; local ok=$?
  if (( ok == 0 )); then say "smoke PASS (dev server left running on :${PORT})"; else say "smoke FAIL — see ${LOG_FILE}"; fi
  return "${ok}"
}

case "${1:-}" in
  build)      cmd_build ;;
  start)      cmd_start ;;
  stop)       cmd_stop ;;
  reset)      cmd_reset ;;
  status)     cmd_status ;;
  smoke)      cmd_smoke ;;
  smoke-keep) cmd_smoke_keep ;;
  *) die "usage: $0 {build|start|stop|reset|status|smoke|smoke-keep}" ;;
esac
