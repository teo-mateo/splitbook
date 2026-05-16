// Auto-discovered opencode plugin (.opencode/plugin/*.js).
//
// Logs a line every time a session goes idle — i.e. every time the model
// "stops" (finishes its turn / the headless run goes quiet). This is the
// signal the autopilot uses to tell "still working" from "stalled, needs a
// continue nudge". The bus event is `session.idle` (see opencode
// packages/opencode/src/session/status.ts — published whenever session
// status transitions to { type: "idle" }).
//
// Output: <project>/harness/logs/runs/session-stopped.log
//   Stopped at 2026-05-16T14:30:12.345Z  session=ses_xxx

import { appendFile, mkdir } from "node:fs/promises";
import { join, dirname } from "node:path";

export const StoppedLogger = async ({ directory, worktree }) => {
  const root = worktree || directory || process.cwd();
  const logFile = join(root, "harness", "logs", "runs", "session-stopped.log");

  return {
    event: async ({ event }) => {
      if (event?.type !== "session.idle") return;
      const sessionID = event?.properties?.sessionID ?? "unknown";
      const line = `Stopped at ${new Date().toISOString()}  session=${sessionID}\n`;
      try {
        await mkdir(dirname(logFile), { recursive: true });
        await appendFile(logFile, line);
      } catch {
        // never let logging break the agent
      }
    },
  };
};
