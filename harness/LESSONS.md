# LESSONS.md

Curated lessons learned across slices. Max ~20 entries. Only the `lessons-scribe` subagent writes here, except entries prefixed `[HUMAN]` which the operator adds directly and which take priority.

Read this file in full at the start of every slice. Paraphrase the entries you consider relevant, in your first message.

---

### L-00: Read the spec end-to-end before writing the first test
- **Observed in:** seed
- **Lesson:** Before a slice starts, read `specs/product-spec.md`, `specs/technical-spec.md`, and the current row of `specs/slice-plan.md` in full. Do not rely on remembered summaries.
- **Why:** Medium-context models routinely confabulate requirements when they skim. Every skipped read is a likely defect.

### L-H11 [HUMAN]: New features must be stylistically homogeneous with existing features
- **Observed in:** slice 5 attempt (Qwen3.5-122B) — `AddMemberHandler` threw `KeyNotFoundException` / `ArgumentException`, and `AddMemberEndpoint` wrapped the call in a try/catch ladder translating exceptions to HTTP status codes. No other feature in the codebase does this — `CreateGroupHandler`, `GetGroupHandler`, etc. return a typed `Results<Created<T>, ProblemHttpResult>` union and call `TypedResults.Problem(...)` inline. The endpoint registration in other features is a one-liner (`group.MapPost("/", XxxHandler.HandleAsync)`); `AddMemberEndpoint` redefined its own `HandleAsync` wrapper. Dead `AddMemberResponse` DTO and missing `AddMemberValidator` further diverged from the `CreateGroup` shape. The model had self-corrected one pattern (static-class handler) but not the deeper conventions.
- **Lesson (said once):** Every new feature folder MUST mirror the shape of the most recent sibling feature folder in its slice family. Handler return type, error-emission strategy, endpoint registration form, DTO layout, validator presence — all of these are conventions, not suggestions. Before writing the first line of a new handler/endpoint, the primary must `cat` at least one existing sibling (e.g. `CreateGroupHandler.cs` + `CreateGroupEndpoint.cs`) and match its structure exactly unless the slice plan gives a written reason to diverge.
- **Lesson (said again, different words):** Consistency across the codebase is load-bearing — not cosmetic. When a Groups feature's error handling uses exceptions-and-try/catch instead of typed `Results<…>` with inline `TypedResults.Problem()`, you have not "found a cleaner way" — you have forked the codebase's style and every downstream slice will now have to pick one of two camps. The rule is simple: read the newest neighbor in the same `Features/<Area>/` folder, copy its silhouette, change only the business logic. If you think the existing convention is wrong, stop and raise it — do not silently introduce a second pattern.
- **Why:** Qwen3.5-122B (and likely other thinking models in this class) imitate the *surface* of nearby code well (folder layout, class keyword, DI parameters) but re-invent deeper philosophies (error flow, return-type shape) when no one stops them. L-H9 already warns that the first slice sets a precedent; L-H11 extends that forward: every subsequent slice is also a precedent for the NEXT slice. One divergent slice will propagate, and within two or three slices the codebase is bifurcated with no clean migration path. The reviewer must gate this (see reviewer.md "Consistency check") and the primary must front-run it (read-the-neighbor before writing).

### L-H10 [HUMAN]: Test-writer touches nothing under tests/Infrastructure/ + must run the full suite
- **Observed in:** slice 5 attempt (Gemma) — test-writer "cleaned up" `AppFactory.cs` by removing `IAsyncLifetime` / `EnsureCreatedAsync` / `EnsureDeletedAsync` because it looked redundant given slice 1.1's `Program.cs` `EnsureCreated`. It wasn't redundant — `AppFactory` uses a unique per-class SQLite file that Program.cs never touches. Result: all 40 pre-existing tests died with `ObjectDisposedException`. Test-writer ran only a FILTERED dotnet test of its own new test, didn't notice the fleet-wide damage, and returned "red confirmed" to the primary — even though the test's red was a fixture crash, not a real 404. Primary then spent 10+ edit-test cycles looping on the wrong file trying to fix a handler while the real problem was the shared fixture that test-writer had broken.
- **Lesson:** TWO hard rules for `@test-writer`:
  1. **Never edit anything under `tests/SplitBook.Api.Tests/Infrastructure/`.** That's shared test infrastructure. Code that looks redundant there is load-bearing until proven otherwise. If the infrastructure seems wrong for a criterion, STOP and delegate to `@researcher` or return with an explicit blocked-report. Do not "clean up."
  2. **Always run BOTH `dotnet test` (full, unfiltered) AND the filtered run.** Filtered runs hide fleet-wide damage — the whole point of the full run is to detect that your changes broke someone else's tests. A fixture crash in YOUR new test is never a valid "red" — if you see `ObjectDisposedException` or similar in setup/teardown, something is wrong, not achievements.
- **Why:** Silent "helpful" refactoring by a confident-but-wrong model is the worst failure mode — looks clean from the tool-call stream, damages propagate silently until primary inherits a broken fixture and loops trying to fix the wrong file. Full-suite verification turns this from an invisible-to-primary failure into an immediate return-with-error.

### L-H9 [HUMAN]: First slice locks in code style — pick well or fix explicitly
- **Observed in:** slices 1–4 — slice 1's tests used `JsonDocument.Parse` + `TryGetProperty` + `.Should().BeTrue()` chains for response assertions. Every subsequent test copied the pattern verbatim because `test-writer.md` says "re-use fixtures already present" and the model generalized "fixtures" to "any established pattern."
- **Lesson:** The first slice that introduces a new kind of code (test fixtures, handler shape, error mapping, logging, validation helpers) sets a precedent that propagates silently through every later slice on this model. If the first slice's pattern is suboptimal, fixing it requires an explicit prompt-level override ("do NOT replicate the slice-N antipattern — use approach X instead"). Retroactively refactoring older slices to the new pattern is optional; what matters is stopping the propagation.
- **Why:** Qwen3 aggressively imitates in-repo precedent — that's usually what we want (consistency). But it has no quality filter on what it imitates. Surface-level "re-use what's there" does not distinguish "legit shared fixture" from "suboptimal one-off that happened first." Must be told explicitly when a pattern is undesirable.

### L-H8 [HUMAN]: One test per @test-writer invocation
- **Observed in:** slice 1 (110K char spiral), slice 4 (15 malformed pending writes, hard stuck)
- **Lesson:** The primary invokes `@test-writer` **once per acceptance criterion**, not once per slice. Test-writer receives a single criterion, writes a single failing test, runs a filtered `dotnet test` on only that test, and returns. The primary then writes the minimum production code to turn that one test green, then invokes `@test-writer` for the next criterion. Never ask `@test-writer` for "all tests for this slice" in one shot.
- **Why:** Batch test-writing produces multi-K-token reasoning spirals and malformed tool-call streams on this model class. Slice 4 hung on 15 pending write tool calls with empty args because the subagent's context bloated past its ability to serialize tool calls cleanly. One-test-per-invocation keeps thinking traces short (~3–5K), keeps subagent context tiny, restores the per-test TDD signal, and contains failures to a single test when something goes wrong.

### L-H7 [HUMAN]: Tests green ≠ app works — smoke-test the running API
- **Observed in:** slice 1 (discovered post-scribe during manual run)
- **Lesson:** An integration-test project that uses `WebApplicationFactory` overrides startup in ways that hide production bugs. Slice 1 shipped with 16/16 tests green but `dotnet run` returned HTTP 500 on every endpoint because `Program.cs` never created the DB schema — the test factory did. For any slice that adds or changes something at application startup (DbContext, middleware order, DI wiring, migrations, auth config), the slice is NOT done until a real `dotnet run` + `curl` smoke against a fresh filesystem succeeds on the slice's golden path. The reviewer's checklist should include this for any startup-touching slice.
- **Why:** The test harness is not the production harness. Overrides in `AppFactory.cs` (connection strings, `EnsureCreated`, service re-registrations) silently substitute for production startup code. A slice can be 100% green and 100% broken at `dotnet run` — the worst failure mode the feedback loop produces, because it looks fine until a human tries it.

### L-H6 [HUMAN]: One endpoint per slice
- **Observed in:** slice 1 (Register + Login, two endpoints — barely manageable)
- **Lesson:** A slice is one endpoint. Two is permissible only when they share infrastructure that makes splitting harder (Register+Login share JWT setup; add/remove member share membership lifecycle). Three endpoints in one slice is always too much — the model spirals writing tests in large batches, a single wrong guess contaminates many files, and there's no per-endpoint feedback signal. When in doubt, split. More but smaller slices = more commit-worthy checkpoints and faster recovery when something goes wrong.
- **Why:** Slice 1's Register+Login pairing produced 14 tests in one go, a long test-writer deliberation, phantom reviewer findings against the large surface area, and a large-blast-radius rewrite when the primary responded to those findings. The single-endpoint version would have landed cleanly in half the tokens.

### L-H5 [HUMAN]: Reviewer must verify L-H2 by evidence, not by prompt-echo
- **Observed in:** slice 1 reviewer trace
- **Lesson:** When the reviewer checks L-H2 (primary writes no logic before red), it must inspect `git log --reverse` ordering of this slice's commits, or the session's write-tool ordering — NOT just echo "L-H2 satisfied" because the prompt mentions it. Similarly, any finding that claims a framework-level fact the reviewer cannot verify by direct `read`/`grep` must be delegated to `@researcher` before being emitted, not speculated. A missing L-H2 check or a speculative finding is a harness-level failure.
- **Why:** In slice 1, the reviewer rubber-stamped L-H2 without checking ordering and generated speculative findings (claimed a package was unused without grepping, misread ASP.NET Core routing) while spiraling on middleware-order re-validation for thousands of tokens. The review signal becomes noise without enforcement.

### L-H4 [HUMAN]: Delegate research to @researcher
- **Observed in:** slice 1
- **Lesson:** When stuck on a library API or framework behavior (JWT claim emission, EF Core fluent config, FluentValidation rules, xUnit fixture lifecycles, `WebApplicationFactory` hooks, etc.) — **invoke `@researcher` with a focused question, do not webfetch/websearch inline**. The researcher returns a distilled answer + one code example + source URLs, keeping your main context clean. The preferred order when stuck: (1) grep existing code in this repo for precedent, (2) `@researcher "how do I X with library Y? need one C# example"`, (3) only then guess and let the build/test cycle correct you. Inline webfetch by the primary pulls entire doc pages into context and bloats every subsequent turn; the researcher absorbs that cost in its own disposable session and returns ~300 tokens.
- **Why:** Qwen3 thinking models will happily spend 10K+ reasoning tokens triangulating an API shape when a 5-second research call would give a definitive answer. Wrong guesses poison everything downstream. Context bloat from pulled docs degrades the primary's later turns. Delegation solves both.

### L-H3 [HUMAN]: Thinking-model output budget
- **Observed in:** slice 1 (test-writer returned empty)
- **Lesson:** For this Qwen3 thinking model, `max_tokens` must be generous (≥ 32K). Reasoning traces for multi-step subagent tasks (read 10 files + plan test structure + write tests) routinely consume 10K–20K tokens *before* the first tool call or text output. A low ceiling makes subagents return empty silently — the generation terminates mid-think with `finish_reason=length`, opencode reports "empty return", and the primary is forced to improvise.
- **Why:** Slice 1's `@test-writer` used all 8192 output tokens inside its thinking trace, never reached any tool call, and returned nothing. Primary fell back to writing tests itself — a harness protocol violation caused entirely by a client-side setting.

### L-H2 [HUMAN]: Primary writes no logic before red
- **Observed in:** slice 1
- **Lesson:** Between reading the specs and `@test-writer` returning RED, the primary's file edits are limited to: `.csproj` (package refs), `Program.cs` (DI registrations and route mapping only), `appsettings.json`, and empty placeholder types (enough to let referenced types resolve — e.g. `public class JwtTokenService { }` with no body). Handler method bodies, domain logic, password hashing, token generation, EF model configuration, entity property logic, mapping code — NONE of these may be written before `@test-writer` confirms RED. If a test requires a type to exist, create an empty class; the body comes after red.
- **Why:** In slice 1 the primary wrote `JwtTokenService`, `PasswordHasher`, `AppDbContext`, and `User.cs` with full implementations before invoking `@test-writer`. Tests were then written to match existing code, which inverts TDD and destroys the design-pressure signal the harness depends on.

### L-H1 [HUMAN]: Subagents MUST verify with the tools they have
- **Observed in:** slice 0
- **Lesson:** `test-writer` must actually run `dotnet test` and quote its output before returning, not just infer the red state from having written tests. `reviewer` must actually run `git diff` and `dotnet test` and quote their exit codes in its report. If a bash command comes back "invalid" or is unavailable, STOP and surface this — do not silently fall back to reading files and claim you've reviewed.
- **Why:** In slice 0 the test-writer skipped `dotnet test` entirely and reviewer fell back to file-reading because bash was (incorrectly) disabled in the subagent configs. This broke the core L-01 safety signal. Verification must be explicit and visible in the transcript.

### L-09: Batch validate collection membership in a single query
- **Observed in:** slice 7
- **Lesson:** When validating that multiple items belong to a collection (e.g., all expense participants are group members), issue ONE batch query using `Contains()` or a JOIN — never iterate and query per item.
- **Why:** The primary wrote a loop that queried membership for each participant, producing an N+1 pattern. The fix was a single query checking all participant IDs at once. Every slice that validates "these users are all members of this group" will face the same trap.

### L-10: Enforce cross-entity invariants in the handler
- **Observed in:** slice 7
- **Lesson:** When a handler creates a child entity that references a parent (expense → group, settlement → group), validate that the child's properties are consistent with the parent's properties before persisting. Load the parent once and compare.
- **Why:** The primary accepted any currency for the expense without checking it matched the group's currency. The product spec says "every group has one currency" — the handler should have loaded the group and compared. This pattern applies to any parent-child relationship where the child inherits or must match parent properties.


### L-05: Use TypedResults.Problem() for ALL non-2xx error responses
- **Observed in:** slice 1 (BadRequest), slice 4 (NotFound)
- **Lesson:** When the spec demands RFC 7807 Problem+JSON for error responses, use `TypedResults.Problem()` for every non-2xx response — not just validation errors. Convenience helpers like `TypedResults.NotFound()`, `TypedResults.BadRequest<T>`, `TypedResults.Conflict()`, etc. produce bare JSON bodies, not the `type/title/status` Problem+JSON envelope. This applies to 404, 400, 409, 412, and any other error status code.
- **Why:** The primary used `TypedResults.BadRequest<T>` in slice 1 and `TypedResults.NotFound()` in slice 4 — neither matches the Problem+JSON shape. Every slice returning any kind of error response will face this choice. Sharpened from original (slice 1 only, framed as "validation/business-rule errors") to cover all error status codes after slice 4 surfaced the same issue with 404.

### L-07: FluentValidation `.Must()` throws on null — guard or use a null-safe rule
- **Observed in:** slice 2
- **Lesson:** When using FluentValidation's `.Must()` on a string property, either precede it with a null guard (e.g., `.NotEmpty()`) or make the predicate itself null-safe (e.g., `.Must(val => val == null || val.Length == 3)`). Without a guard, `.Must()` receives `null` and throws `ArgumentNullException` during validation, which produces a 500 instead of a 400 with a proper error message. Prefer built-in rules like `.Length()`, `.RegularExpression()`, or `.NotEmpty()` when they express the constraint directly — they handle nulls gracefully.
- **Why:** The primary wrote `.Must(currency => char.IsLetter(c) for all c)` on a string that could be null, causing a crash on empty input. The reviewer caught it, but every slice that uses FluentValidation with custom `.Must()` predicates on reference types will face the same trap.

### L-08: When specs contradict, escalate — don't pick a side
- **Observed in:** slice 6
- **Lesson:** When `product-spec.md` and `technical-spec.md` (or `slice-plan.md`) say different things about the same behavior, STOP and surface the contradiction to the human. Do not pick a side and implement — even if one seems clearly right. Document the exact conflicting sections in the session log.
- **Why:** Slice 6 found product-spec §5 saying archive is the escape hatch for non-zero-balance groups, while technical-spec §4 and slice-plan said archive "fails if any non-zero balance." The primary chose product-spec (correctly), but the harness principle is "spec is ground truth" — when ground truth splits, only the spec owner can resolve it. A silent choice risks implementing the wrong behavior and discovering it late.

### L-11: Audit shared logic when extending existing handlers
- **Observed in:** slice 8
- **Lesson:** When extending an existing handler with a new code path (e.g., adding Exact split to an endpoint that already handles Equal split), identify every piece of shared logic that the new path must participate in — especially membership validation, authorization checks, and invariant enforcement. The new path must not bypass shared logic that existed for the original path.
- **Why:** The payer membership check was broken for Exact split because the primary collected participant IDs from the splits list only, and Exact split doesn't require the payer to be in the splits list. The Equal split path auto-added the payer, but that was specific to Equal's split-building logic. The membership validation (shared across all split methods) should have always included the payer. This pattern applies whenever a handler gains a new variant — the shared scaffolding must be audited, not assumed.

### L-12: When a test fails with 5xx and no body, rerun with detailed verbosity
- **Observed in:** slice 9
- **Lesson:** Integration tests assert status code with `response.StatusCode.Should().Be(HttpStatusCode.OK)`. When the actual status is 500, the FluentAssertions failure message says only `"Expected OK, but found InternalServerError"` — the response body and the host stack trace are NOT in the failure block. Do **not** start guessing at handler code. First action: rerun the single failing test with `dotnet test --filter "FullyQualifiedName~<TestName>" --logger "console;verbosity=detailed" --nologo`. The ASP.NET Core console logger then emits `[ERR] An unhandled exception has occurred...` followed by the full server-side stack trace inline with the test output. Read it before changing any code.
- **Why:** Default `dotnet test` verbosity suppresses the ASP.NET Core host log, and FluentAssertions on a status-code mismatch only reports the codes — not the response body. Without verbose mode, the actual exception is invisible, and guessing at the handler wastes time.

### L-13: SQLite EF Core cannot ORDER BY DateOnly or DateTimeOffset
- **Observed in:** slice 9
- **Lesson:** When querying with EF Core against SQLite, do not use `.OrderBy()` or `.OrderByDescending()` on `DateOnly` or `DateTimeOffset` columns — SQLite has no native type for these and EF Core cannot translate the expression. Either materialize first (`.ToListAsync()` then sort in-memory) or store a translatable surrogate (e.g., `DateTime` or `long Ticks`) for ordering. In-memory sort is acceptable when the spec doesn't require server-side pagination optimization.
- **Why:** The primary wrote `.OrderByDescending(e => e.OccurredOn)` on a `DateOnly` column, which threw at runtime. This will recur on any slice that lists or filters by date (settlements list, expense date filters, reports). The primary had to materialize the entire table then sort/page in-memory — correct per spec, but worth knowing upfront to avoid the runtime crash.
