#!/usr/bin/env bash
# Operator-side watcher for opencode sessions in this project.
# Reads the opencode SQLite DB to surface high-signal events.
#
# Usage:
#   ./harness/watch.sh              # snapshot current state
#   ./harness/watch.sh tail         # tail latest assistant text from the active session
#   ./harness/watch.sh session <id> # dump one session's tool-calls and text
set -euo pipefail

DB="${OPENCODE_DB:-$HOME/.local/share/opencode/opencode-dev.db}"
DIR="${PROJECT_DIR:-/home/teodor/little-projects/trash/30-app-benchmark}"

q() { sqlite3 "$DB" "$@"; }

snapshot() {
  echo "=== Sessions for $DIR (newest first) ==="
  q "SELECT id, COALESCE(parent_id,'(root)'), datetime(time_updated/1000,'unixepoch','localtime'), title
     FROM session WHERE directory='$DIR'
     ORDER BY time_updated DESC LIMIT 10" \
    | column -t -s '|'

  ROOT=$(q "SELECT id FROM session WHERE directory='$DIR' AND parent_id IS NULL ORDER BY time_updated DESC LIMIT 1")
  [ -z "$ROOT" ] && { echo "No root session found."; return; }

  echo ""
  echo "=== Root session: $ROOT ==="
  echo ""
  echo "--- Part counts (root) ---"
  q "SELECT json_extract(data,'\$.type'), COUNT(*) FROM part WHERE session_id='$ROOT' GROUP BY 1" | column -t -s '|'

  echo ""
  echo "--- Most recent assistant text (root, last 400 chars) ---"
  q "SELECT substr(json_extract(data,'\$.text'),1,400) FROM part
     WHERE session_id='$ROOT' AND json_extract(data,'\$.type')='text'
     ORDER BY time_created DESC LIMIT 1"

  echo ""
  echo "--- Children (subagent sessions) ---"
  q "SELECT id, title, datetime(time_updated/1000,'unixepoch','localtime')
     FROM session WHERE parent_id='$ROOT' ORDER BY time_created" | column -t -s '|'

  echo ""
  echo "=== Repo state ==="
  (cd "$DIR" && git status --short; echo; echo "Slice logs:"; ls harness/logs/ 2>/dev/null || echo "  (none)")
}

tail_text() {
  ROOT=$(q "SELECT id FROM session WHERE directory='$DIR' AND parent_id IS NULL ORDER BY time_updated DESC LIMIT 1")
  q "SELECT datetime(time_created/1000,'unixepoch','localtime') || '  ' || substr(json_extract(data,'\$.text'),1,800)
     FROM part WHERE session_id='$ROOT' AND json_extract(data,'\$.type')='text'
     ORDER BY time_created DESC LIMIT 3"
}

session_dump() {
  local sid="$1"
  echo "=== $sid ==="
  q "SELECT substr(json_extract(data,'\$.type'),1,10) || '  ' ||
            substr(COALESCE(
              json_extract(data,'\$.text'),
              json_extract(data,'\$.tool') || '(' || COALESCE(json_extract(data,'\$.state.status'),'') || '): ' ||
                COALESCE(json_extract(data,'\$.state.input.file_path'),
                         json_extract(data,'\$.state.input.command'),
                         json_extract(data,'\$.state.input.pattern'),''),
              ''),1,200)
     FROM part WHERE session_id='$sid' ORDER BY time_created"
}

case "${1:-snapshot}" in
  snapshot) snapshot ;;
  tail)     tail_text ;;
  session)  session_dump "$2" ;;
  *) echo "unknown: $1" >&2; exit 2 ;;
esac
