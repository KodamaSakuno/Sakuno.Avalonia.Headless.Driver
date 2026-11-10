# Tasks

## 1. Solution and dependency foundation

- [x] 1.1 Add the `net10.0` driver library, sample application, sample Console Host, and xUnit v3/MTPv2 executable test projects to the solution; verify `dotnet restore`, `dotnet build`, and an initial `dotnet test` through Microsoft.Testing.Platform v2 succeed without VSTest.
- [x] 1.2 Configure centrally managed Avalonia 12.1.x, Headless, Skia, ASP.NET Core/OpenAPI, xUnit v3, and Microsoft.Testing.Platform v2 dependencies; verify restore reports no target-framework or Avalonia version conflicts.

## 2. Persistent Headless session hosting

- [x] 2.1 Implement versioned local configuration and compiled Startup Adapter resolution without dynamic assembly loading; verify unit tests accept valid settings and reject unknown types, unsafe overrides, invalid limits, and unsupported runtime/package combinations.
- [x] 2.2 Implement the long-lived single-session owner and Startup Adapter execution with Skia Headless rendering; verify integration tests preserve login control state across calls and reject a second active session.
- [x] 2.3 Implement the non-queueing session gate, serialized UI dispatch, operation deadlines, and cancellation; verify tests return `session_busy` for overlap and cancel waits without running queued UI work.
- [x] 2.4 Implement close-and-exit and fresh-process recovery semantics; verify process tests complete the close response, terminate the Host, and reject the old session identity after restart.

## 3. Semantic observation and control queries

- [x] 3.1 Implement session/window/snapshot DTOs and bounded Semantic Tree generation from AutomationPeers with logical/visual-tree fallbacks; verify tests cover names, values, states, bounds, missing accessibility metadata, truncation markers, and subtree expansion.
- [x] 3.2 Implement TopLevel tracking for initial windows, subsequent windows, dialogs, and observable popups; verify tests distinguish window scope and close/remove stale window entries.
- [x] 3.3 Implement deterministic Element Queries with exact/contains matching, conjunctive criteria, hidden-control options, and bounded ambiguity diagnostics; verify tests cover unique, missing, ambiguous, cross-window, hidden, and unrealized virtualized targets without exposing object handles.

## 4. Actions, waits, and visual observations

- [x] 4.1 Implement atomic query-and-input actions for click, focus, toggle, selection, scrolling, key, and pointer input; verify tests exercise focus, hit testing, routed commands, disabled/hidden targets, and TopLevel-relative coordinates.
- [x] 4.2 Implement `replace`, `append`, and `clear` text actions without implicit Enter or payload echo; verify tests cover editing semantics and confirm plaintext input is absent from responses and logs.
- [x] 4.3 Implement structured condition evaluation and polling for existence, visibility, enablement, focus, selection, text, and values; verify tests distinguish business-condition timeout from completed UI queue processing and enforce the 60-second configuration ceiling.
- [x] 4.4 Implement per-window PNG capture and dimension/scaling metadata; verify tests decode a nonempty PNG and verify password fields remain masked in semantic observations.

## 5. Local HTTP API and process security

- [x] 5.1 Implement the `/v1` session, snapshot, query, action, wait, screenshot, and capability endpoints, and generate a static OpenAPI document at build time; verify contract tests compare real requests and responses with the packaged schemas, including binary `image/png` responses, and `/openapi/v1.json` returns 404.
- [x] 5.2 Implement loopback-only random-port binding, per-process bearer authentication, and the single JSON stdout readiness record with diagnostics on stderr; verify process tests reject missing/invalid tokens, parse the handshake, and find no repeated token in logs.
- [x] 5.3 Implement the stable error envelope and HTTP status mapping with bounded diagnostics and sanitized internal failures; verify tests cover 400, 401, 404, 408, 409, 410, 422, and 500 paths without default stack or sensitive-data disclosure.
- [x] 5.4 Implement request-size limits, authenticated activity tracking, configurable idle expiration, and shutdown cancellation; verify process tests reject disallowed code/assembly/shell/file inputs and exit after a shortened idle timeout.

## 6. Sample consumer and Agent Skill

- [x] 6.1 Complete the sample login application and its Startup Adapter with successful, failed, and delayed authentication paths; verify its Console Host starts without production `Program.Main` and the sample supports each intended test scenario.
- [x] 6.2 Author the English canonical Skill template and installation guidance for both `.agents/skills/avalonia-headless-driver/` and `.claude/skills/avalonia-headless-driver/`; verify the documented startup, capability check, curl/curl.exe, observation-action-verification, screenshot, and shutdown workflow against the real sample Host.

## 7. Release and cross-platform verification

- [ ] 7.1 Configure NuGet packaging to include the driver library, canonical Skill template, configuration schema, and static OpenAPI document; verify `dotnet pack` succeeds and a fresh sample consumer can restore and run the packaged artifacts.
- [ ] 7.2 Add a Windows, macOS, and Linux CI matrix that builds and runs xUnit v3 tests through Microsoft.Testing.Platform v2; verify all supported environments pass without VSTest, a display server, or cross-platform pixel-identical screenshot assertions.
- [ ] 7.3 Run the complete real-HTTP acceptance workflow for login success, bad password, delayed completion, ambiguity, timeout, concurrency conflict, screenshot, redaction, shutdown, idle recovery, and stale sessions; verify the final build, full test suite, package, and Skill-documented workflow all pass on the deliverable.
