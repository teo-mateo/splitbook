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
