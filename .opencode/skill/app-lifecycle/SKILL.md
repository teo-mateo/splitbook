---
name: app-lifecycle
description: Start, stop, reset, and smoke-test the SplitBook.Api dev server. ALWAYS use this skill instead of ad-hoc `dotnet run &` — opencode's Bash tool does not properly detach from backgrounded processes, so direct `&` invocations hang until timeout.
---

# SplitBook app lifecycle

All lifecycle operations go through `scripts/app.sh`. Never run `dotnet run` directly from a bash tool call, and never chain `dotnet run & sleep 5 && curl ...` inline — those commands hang opencode's Bash tool for ~120 seconds each because the server's stdout/stderr pipes never close. The script uses `nohup` + `disown` + full file-descriptor redirection so every call returns promptly.

## Commands

| Command | What it does |
|---|---|
| `scripts/app.sh start` | Kill any existing instance, start the API in the background on port 5124, wait for `/health` to return 200, then return. |
| `scripts/app.sh stop` | Kill the running API (if any). |
| `scripts/app.sh reset` | Stop + remove `app.db` (and its WAL/SHM siblings). Fresh filesystem for the next boot. |
| `scripts/app.sh status` | Report whether the API is running, on what URL, and the current `/health` code. |
| `scripts/app.sh smoke` | `reset` + `start` + probe `/health`, `/swagger`, `/swagger/v1/swagger.json` + `stop`. Exit 0 if every probe returned 2xx/3xx. |
| `scripts/app.sh smoke-keep` | Same as `smoke` but leaves the API running afterward (useful for follow-up manual probes). |

## When to use this skill

- **L-H7 smoke-test step at slice completion.** The last thing before invoking `@reviewer` on any slice that touches startup, middleware, DI wiring, or migrations. `scripts/app.sh smoke` is the canonical invocation.
- **Any time you want a clean-filesystem verification** that the app boots from scratch. Tests use `WebApplicationFactory` which overrides startup in ways that can hide bugs visible only to `dotnet run`.
- **Debugging a 500-on-every-endpoint regression.** Run `scripts/app.sh smoke-keep`, then curl specific endpoints against `http://localhost:5124` while reading `harness/logs/runs/app-last.log`.

## Environment variables (usually leave at defaults)

- `PORT` — default `5124`. Override if you need a different port (e.g. running two copies).
- `APP_URLS` — full `ASPNETCORE_URLS` value. Overrides `PORT`.
- `APP_ENV` — `ASPNETCORE_ENVIRONMENT`, default `Development`.
- `READY_TIMEOUT` — seconds to wait for `/health` to return 200 before giving up, default `30`.

## What to do when `start` fails

If `scripts/app.sh start` reports "API did not become healthy within 30s," it will dump the last 40 lines of the app log to stderr. Read them. Common causes:

- `EnsureCreated()` failing because the DB file is locked by a previous run — `scripts/app.sh reset` then retry.
- Port 5124 in use — `ss -tlnp | grep 5124` to find the holder, or pass `PORT=5125` and retry.
- Startup exception in `Program.cs` (most likely a slice-5.1-class issue: JWT validation missing a required claim, Swagger security scheme referencing an undefined scheme, EF Core migration pending that `EnsureCreated` can't apply).
- The log file lives at `harness/logs/runs/app-last.log`. Tail it with `tail -n 100 harness/logs/runs/app-last.log` for more context.

## Anti-patterns (do NOT do these)

- `dotnet run --urls="..."  &  sleep 5 && curl ...` — hangs the bash tool for 120 s.
- `nohup dotnet run ... &` without `</dev/null` — still hangs because stdin is bound to opencode's tool pipe.
- `pkill -9 -f dotnet` — can kill unrelated `dotnet test` or `dotnet build` processes running in other terminals. Use `scripts/app.sh stop` which scopes to `SplitBook.Api` only.
- Running the smoke test manually via `curl` chain — re-implement what `scripts/app.sh smoke` already does safely.
