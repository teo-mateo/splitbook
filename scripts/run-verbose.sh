#!/usr/bin/env bash
# Launch opencode in headless mode with FULL visibility:
# reasoning/thinking traces, tool calls (with their args), and text output,
# labeled per agent (primary / spec-auditor / test-writer / reviewer / etc).
#
# Usage:
#   bash scripts/run-verbose.sh next-slice            # run a slash command
#   bash scripts/run-verbose.sh "do XYZ thing"        # or an arbitrary message
#
# Also saves raw JSON event stream to harness/logs/runs/raw-<ts>.jsonl
# so you can re-analyze later without re-running.
set -euo pipefail

ARG="${1:-next-slice}"
RAW="harness/logs/runs/raw-$(date +%s).jsonl"
mkdir -p "$(dirname "$RAW")"

# If argument has no spaces and matches a command file, treat as slash command
if [[ "$ARG" =~ ^[a-zA-Z0-9_-]+$ ]] && [ -f ".opencode/command/$ARG.md" ]; then
    CMD_ARGS=(--command "$ARG")
else
    CMD_ARGS=("$ARG")
fi

echo "▶ opencode run --format json ${CMD_ARGS[*]}"
echo "▶ raw stream → $RAW"
echo ""

stdbuf -oL -eL opencode run --format json "${CMD_ARGS[@]}" 2>&1 \
  | stdbuf -oL tee "$RAW" \
  | stdbuf -oL jq -r --unbuffered '
      # Agent label (primary if not set on subagent message)
      def agent_of:
        (.properties.info.agent // .properties.info.mode // "primary");

      # --- delta events: live streaming chunks ---
      if .type == "message.part.delta" then
        .properties as $p
        | ($p.info | (.agent // .mode // "primary")) as $a
        | (.properties.part.type // $p.part.type // "?") as $t
        | ($p.delta.text // $p.delta.reasoning // "") as $txt
        | if $txt != "" then
            # Collapse long whitespace runs to keep lines readable
            "[\($a) \($t)] \($txt)" | gsub("\n"; " ⏎ ") | gsub("\\s+"; " ")
          else empty end

      # --- finalized parts: full part content with headers ---
      elif .type == "message.part.updated" or .type == "message.part.added" then
        .properties.part as $p
        | (.properties.info | (.agent // .mode // "primary")) as $a
        | if $p.type == "reasoning" then
            "\n━━ [\($a) THINK] ━━\n" + ($p.text // "") + "\n"
          elif $p.type == "text" then
            "\n━━ [\($a) TEXT ] ━━\n" + ($p.text // "") + "\n"
          elif $p.type == "tool" then
            "[\($a) TOOL ] " + ($p.tool // "") +
            (if $p.state.input then " ← " + ((
               $p.state.input.command // $p.state.input.file_path //
               $p.state.input.pattern // $p.state.input.subagent_type //
               ($p.state.input | tostring[:200])) | tostring) else "" end) +
            (if $p.state.status then " [" + $p.state.status + "]" else "" end)
          elif $p.type == "step-start" then
            "\n─── step start (\($a)) ───"
          elif $p.type == "step-finish" then
            "─── step end (\($a)) ───"
          else empty end

      # --- session events: subagent spawned ---
      elif .type == "session.updated" then
        "▶ session: " + (.properties.info.title // "")

      else empty end
    '
