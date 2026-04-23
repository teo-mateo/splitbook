# LESSONS.md

Curated lessons learned across slices. Max ~20 entries. Only the `lessons-scribe` subagent writes here, except entries prefixed `[HUMAN]` which the operator adds directly and which take priority.

Read this file in full at the start of every slice. Paraphrase the entries you consider relevant, in your first message.

---

### L-00: Read the spec end-to-end before writing the first test
- **Observed in:** seed
- **Lesson:** Before a slice starts, read `specs/product-spec.md`, `specs/technical-spec.md`, and the current row of `specs/slice-plan.md` in full. Do not rely on remembered summaries.
- **Why:** Medium-context models routinely confabulate requirements when they skim. Every skipped read is a likely defect.

### L-01: Red before green, always
- **Observed in:** seed
- **Lesson:** The `test-writer` subagent must run `dotnet test` and show that the new tests fail before any production code is written. If tests pass before implementation, they are wrong.
- **Why:** TDD only provides its safety signal when the red state is verified, not assumed.

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

### L-02: One slice's worth of files per session
- **Observed in:** seed
- **Lesson:** Do not touch files outside the current slice's feature folder except when explicitly adding an entity to `Domain/` or a migration to `Infrastructure/Persistence/Migrations/`. If you feel the urge, invoke `@reviewer` first.
- **Why:** Out-of-scope edits are the #1 source of regressions when small models "help."

### L-03: Keep subagents within their mandate boundaries
- **Observed in:** slice 0
- **Lesson:** The `test-writer` subagent writes tests only. If the tests require scaffolding (e.g., Program.cs, appsettings, a DbContext), the primary must provide that scaffolding first. A subagent that writes production code outside its mandate will hallucinate APIs and force the primary to fix them before reaching red.
- **Why:** In slice 0, `test-writer` wrote `Program.cs` with a non-existent Serilog API (`CreateBootstrapLogger`) and a missing NuGet package. The primary had to repair the scaffolding before the red state was even reachable.

### L-04: Document architectural decisions when the spec asks
- **Observed in:** slice 0
- **Lesson:** When `technical-spec.md` §9 lists open questions for the implementer to decide, the primary must document the decision in code comments or `DECISIONS.md` at the point it is made. Do not defer this to a later slice.
- **Why:** The reviewer flagged that the health endpoint's location (minimal API vs. controller) was decided but not recorded. Undocumented decisions accumulate technical debt and make cross-slice consistency checks impossible.

### L-05: Use TypedResults.Problem() for ALL non-2xx error responses
- **Observed in:** slice 1 (BadRequest), slice 4 (NotFound)
- **Lesson:** When the spec demands RFC 7807 Problem+JSON for error responses, use `TypedResults.Problem()` for every non-2xx response — not just validation errors. Convenience helpers like `TypedResults.NotFound()`, `TypedResults.BadRequest<T>`, `TypedResults.Conflict()`, etc. produce bare JSON bodies, not the `type/title/status` Problem+JSON envelope. This applies to 404, 400, 409, 412, and any other error status code.
- **Why:** The primary used `TypedResults.BadRequest<T>` in slice 1 and `TypedResults.NotFound()` in slice 4 — neither matches the Problem+JSON shape. Every slice returning any kind of error response will face this choice. Sharpened from original (slice 1 only, framed as "validation/business-rule errors") to cover all error status codes after slice 4 surfaced the same issue with 404.

### L-06: Wire packages you add, or don't add them
- **Observed in:** slice 1
- **Lesson:** If you add a NuGet package, wire it into the application in the same slice. A package that sits in `.csproj` without being referenced in `Program.cs` or any handler is dead weight — it signals intent the model forgot to fulfill, and it will confuse the reviewer into expecting behavior that doesn't exist. If the package adds friction (e.g., FluentValidation's DI registration and endpoint filter wiring), decide consciously whether to use it or drop it, and document the choice.
- **Why:** FluentValidation.AspNetCore was added to the project but never wired into the pipeline. The primary removed it mid-slice after realizing the friction, but the unwired package sat in the diff long enough to generate reviewer findings and confusion. Future slices will likely add packages (Serilog sinks, idempotency stores, etc.) and forget to wire them.

### L-07: FluentValidation `.Must()` throws on null — guard or use a null-safe rule
- **Observed in:** slice 2
- **Lesson:** When using FluentValidation's `.Must()` on a string property, either precede it with a null guard (e.g., `.NotEmpty()`) or make the predicate itself null-safe (e.g., `.Must(val => val == null || val.Length == 3)`). Without a guard, `.Must()` receives `null` and throws `ArgumentNullException` during validation, which produces a 500 instead of a 400 with a proper error message. Prefer built-in rules like `.Length()`, `.RegularExpression()`, or `.NotEmpty()` when they express the constraint directly — they handle nulls gracefully.
- **Why:** The primary wrote `.Must(currency => char.IsLetter(c) for all c)` on a string that could be null, causing a crash on empty input. The reviewer caught it, but every slice that uses FluentValidation with custom `.Must()` predicates on reference types will face the same trap.
