# Design

## Context

See `proposal.md` — **Why**. The repository is an empty .NET solution with central package management and no existing implementation. Avalonia.Headless exposes in-process UI-thread APIs rather than an attachable desktop automation surface. Its default per-test lifecycle resets state between dispatches, so external requests require a deliberately persistent session.

The design must keep all control access inside the Headless process, preserve state across HTTP calls, and expose only bounded DTOs. The canonical domain terms are defined in `GLOSSARY.md`; externally observable requirements are defined under `specs/`.

## Goals / Non-Goals

**Goals:**

- Keep protocol adapters separate from the UI automation model while deploying them in one process.
- Make session lifetime, UI-thread execution, request serialization, and failure recovery explicit.
- Provide compact, stable JSON contracts without leaking Avalonia object references or arbitrary properties.
- Make the local service and Skill workflow independently debuggable through real HTTP requests.

**Non-Goals:**

- No IPC topology, remote listener, test-runner coupling, arbitrary expression evaluator, or MCP adapter in this release.
- No cross-request element handles, optimistic snapshot locking, implicit business assertions, or pixel-based control location.

## Decisions

### 1. Use one NuGet library with internal core and hosting boundaries

Create `Sakuno.Avalonia.Headless.Driver` as a `net10.0` library. Its automation core owns sessions, observations, queries, actions, waits, and errors without depending on HTTP request types. Its hosting layer exposes ASP.NET Core endpoints, configuration, authentication, and process lifecycle through a small `RunAsync` entry point for a project-owned Console Host.

The consuming repository creates a Console Host that references both this library and its Avalonia application. The Startup Adapter is compiled into that Host or a referenced test assembly. The driver resolves only configured types from already referenced assemblies and never loads request-supplied assemblies.

A single package minimizes first-release dependency and version alignment work. Separate core and hosting packages, a global dotnet tool, and dynamic application loading were rejected as unnecessary complexity.

### 2. Combine the HTTP server and Headless application in one long-lived process

Use `HeadlessUnitTestSession.StartNew` with `PerAssembly`, retain the session for the process lifetime, and dispose it during shutdown. The Console Host creates at most one session; closing that session stops the web host after the response is flushed and then exits. A reset is a new process, never an in-process session rebuild.

This matches the in-process UI model, preserves login state between calls, and avoids IPC. A separate server and application process would improve crash isolation but requires another protocol and supervisor. xUnit/NUnit-owned sessions were rejected because they couple lifecycle to a test runner and their default per-test behavior does not fit persistent external operation.

### 3. Start through an explicit Startup Adapter

Local JSON configuration identifies the Startup Adapter and contains rendering, timeout, snapshot, idle, and initial-window settings. The adapter supplies the Avalonia application builder, installs Skia with headless rendering for visual observations, establishes test services, and creates and shows the initial TopLevel inside the UI session.

The production `Program.Main` is never invoked. Startup completes before the readiness record is emitted; failures produce a bounded diagnostic on stderr and a nonzero process exit.

### 4. Serialize requests with a non-queueing session gate

A session-scoped nonblocking gate admits one observation, action, or wait at a time. A competing request receives HTTP 409 and `session_busy` immediately. The admitted operation enters `HeadlessUnitTestSession.Dispatch`; query resolution, target validation, bounds calculation, and input injection for one action occur in the same dispatch.

Use cancellation tokens that combine client disconnect, operation deadline, shutdown, and idle expiration. Waits poll structured conditions through short serialized UI dispatches with a small delay between evaluations. UI queue completion is treated only as a processing step, never as evidence that business asynchronous work has completed.

### 5. Generate bounded semantic DTOs from peers and trees

Build each snapshot in one UI dispatch. Use AutomationPeers for names, AutomationIds, roles, states, focus, values, and TopLevel-relative bounds. Use logical-tree metadata and a bounded set of known control properties as fallbacks for missing peer semantics; use visual-tree information only where bounds, visibility, popups, or hit testing require it.

Do not reflect over or serialize arbitrary Control or ViewModel properties. Maintain a session-owned TopLevel registry for initial and subsequent windows, dialogs, and observable popups. Window IDs are session-scoped DTO identifiers, not control handles.

Snapshots default to depth 4 and 500 nodes, with bounded text values. Configuration may lower or raise limits only to fixed server hard caps. Truncated nodes include a subtree query path rather than a persistent element reference. Snapshot IDs are monotonically increasing observation identifiers used only for diagnostics and action-result correlation.

### 6. Re-resolve Element Queries for every operation

An Element Query supports optional window scope, AutomationId, exact or contains name/text matching, role, control type, and visibility options. Multiple supplied criteria are conjunctive. Results are deterministic; zero and multiple matches produce distinct errors, and multiple matches include bounded candidate summaries.

No element object or tree index crosses the API boundary. Virtualized business items that have no realized control remain unobservable until scrolling or another UI action realizes them.

### 7. Execute high-level intent through real input paths

For click, toggle, and selection, resolve and validate the target, bring it into view when applicable, update layout, and inject pointer input at its current bounds. Text operations focus the target and use Headless keyboard/text-input APIs: `replace` selects existing content before input, `append` preserves it, and `clear` is separate. None implicitly sends Enter.

Raw key and pointer operations remain available with explicit window scope and device-independent TopLevel coordinates. Direct property assignment, direct invoke-provider calls, arbitrary method dispatch, and code evaluation are excluded from the normal action implementation.

An action response contains the sanitized target summary and before/after observation IDs. It reports input execution only; verification remains a separate observation or condition wait.

### 8. Use a compact versioned HTTP surface

Use ASP.NET Core minimal APIs on Kestrel and `System.Text.Json`. Generate a version-matched static OpenAPI document from the same endpoint metadata at build time and distribute it with the NuGet package; do not expose a runtime documentation endpoint. The initial runtime surface is:

- `GET /v1/capabilities`
- `POST /v1/sessions`
- `GET /v1/sessions/{sessionId}/snapshot`
- `POST /v1/sessions/{sessionId}/query`
- `POST /v1/sessions/{sessionId}/actions`
- `POST /v1/sessions/{sessionId}/wait`
- `GET /v1/sessions/{sessionId}/windows/{windowId}/screenshot`
- `DELETE /v1/sessions/{sessionId}`

Wait requests contain a structured query, property, comparison, expected value, and optional timeout. Action requests use a discriminated action DTO with explicit text mode and no response echo of input text. Screenshots return `image/png` with pixel width, height, and render scaling in response metadata; they never accept output paths.

Error bodies contain `code`, `message`, `details`, and bounded diagnostics. Use 400 for invalid requests, 401 for authentication failures, 404 for missing targets, 409 for active/busy sessions and ambiguous targets, 410 for stale sessions, 422 for non-actionable state, 408 for enforced timeouts, and 500 for sanitized internal errors. Within `/v1`, add fields and optional capabilities compatibly rather than changing existing semantics.

### 9. Secure and delimit the local service

Bind Kestrel to `127.0.0.1` port 0. Generate a cryptographically random 256-bit bearer token at process start and require it for capability and session endpoints. Do not enable CORS, remote binding, request-body logging, or arbitrary file endpoints.

After the server and application are ready, write exactly one JSON line to stdout containing the URL, token, process ID, and protocol version. Configure all other console diagnostics for stderr. Never repeat the token in logs. Mask password observations and redact all text-action payloads from responses, errors, and logs.

Action and wait timeouts default to 5 and 10 seconds respectively, configurable up to 60 seconds. An authenticated-request activity timer exits the idle Host after 10 minutes by default; local configuration may adjust or disable idle expiration but cannot disable operation deadlines.

### 10. Ship one canonical Skill and real HTTP acceptance coverage

Maintain one version-matched `avalonia-headless-driver` Skill template with the package and repository. Document copying it to `.agents/skills/` for Kimi Code or `.claude/skills/` for Claude Code. The Skill starts the project Host, parses the readiness JSON, checks capabilities, uses bearer-authenticated HTTP, performs observation-action-verification, and closes the session.

Examples use curl as a replaceable optional client, call `curl.exe` where PowerShell requires it, and avoid jq or a custom CLI. Screenshot examples save the binary response in Agent-owned test artifacts, not through a driver file API.

Add a sample login application, a consuming Console Host, and tests that exercise the public HTTP contract. Cover successful and failed authentication, delayed completion, persistent state, ambiguous queries, non-actionable controls, timeouts, cancellation, concurrent-request rejection, screenshots, password/token redaction, idle shutdown, and stale session IDs after restart.

### 11. Run all tests with xUnit v3 on Microsoft.Testing.Platform v2

Use xUnit v3 with Microsoft.Testing.Platform v2 (MTPv2) for all unit, process, and HTTP integration tests. Test projects are MTP executable projects and use real `HttpClient` requests for public API coverage. Do not add VSTest adapters or `Avalonia.Headless.XUnit`; xUnit orchestrates tests but never owns the Headless application session.

This provides one current test execution model for local and CI `dotnet test` runs while keeping the persistent Host lifecycle independent of the test framework.

## Risks / Trade-offs

- **Application failure terminates the HTTP service** → Treat process restart as the recovery boundary and make stale session identities explicit.
- **Long-lived application and static state can leak between scenarios** → Reset only through process restart; do not promise in-process isolation.
- **Missing accessibility metadata limits semantic quality** → Combine peers with bounded logical-tree fallbacks and return ambiguity instead of guessing.
- **Dispatcher flushing does not await business work** → Use structured condition polling with enforced deadlines.
- **Rendered output varies with fonts, themes, DPI, and OS** → Treat images as observations, not portable pixel baselines.
- **A stuck UI operation may ignore cancellation** → Stop waiting at the client boundary and recreate the Host rather than attempting an unsafe in-process reset.
- **The readiness line contains a bearer credential** → Emit it only to the process creator, never repeat it in diagnostics, and bind exclusively to loopback.

## Migration Plan

This is a greenfield addition with no existing API or consumer migration. A consumer adopts it by adding the NuGet reference, creating a Console Host and Startup Adapter, and copying the Skill template. Rollback consists of stopping the Host and removing those project references and files; no persistent data or remote deployment is involved.
