# Spec Delta

## Purpose

Gives coding and testing Agents a secure, discoverable local HTTP workflow and version-matched operational guidance for using the Headless driver.

## ADDED Requirements

### Requirement: Versioned HTTP contract
The driver SHALL expose a versioned JSON HTTP API under `/v1` with session-scoped resources for observations, queries, actions, waits, and images. It MUST publish version-matched OpenAPI documentation with its package, MUST NOT expose a runtime documentation endpoint, and MUST return PNG images as binary content.

#### Scenario: Agent uses the packaged contract
- **WHEN** an Agent inspects the packaged OpenAPI document and invokes a documented session operation
- **THEN** the request and response conform to the published schema without requiring a custom client library

#### Scenario: Runtime documentation is unavailable
- **WHEN** a client requests `/openapi/v1.json` from the running Host
- **THEN** the Host returns 404 because documentation is distributed only as a packaged build artifact

### Requirement: Local-only authenticated transport
The Host SHALL bind only to a loopback address on a random available port and generate a new bearer token for each process start. It MUST reject unauthenticated, incorrectly authenticated, and non-loopback access and MUST NOT expose a remote-network listener.

#### Scenario: Agent supplies the startup token
- **WHEN** an Agent uses the URL and bearer token emitted by its Host process
- **THEN** the Host accepts the local authenticated request

#### Scenario: Token is missing or invalid
- **WHEN** a request arrives without the correct bearer token
- **THEN** the Host rejects it before creating or operating a session

### Requirement: Machine-readable startup handshake
The Host SHALL emit one structured stdout readiness record containing its URL, token, process identity, and protocol version. All diagnostic logging MUST use stderr, and neither channel MAY log bearer tokens after the readiness record.

#### Scenario: Skill discovers a random-port Host
- **WHEN** the Skill starts the Host and reads the readiness record
- **THEN** it obtains the connection parameters without a fixed port or a discovery file

### Requirement: Version and capability discovery
The Host SHALL expose its Host, protocol, Avalonia, and available feature versions. Within an API major version, changes MUST remain backward-compatible, and an Agent MUST check compatibility before relying on optional features.

#### Scenario: Host lacks a requested optional feature
- **WHEN** an Agent checks capabilities before requesting an unavailable feature
- **THEN** it receives a clear unsupported-capability result instead of an undocumented operation failure

### Requirement: Locally authorized configuration
The Host SHALL obtain its Startup Adapter, rendering, timeout, snapshot, and initial-window settings from versioned local configuration. Requests MUST NOT override security settings, load arbitrary assemblies, execute code or shell commands, or access arbitrary filesystem paths.

#### Scenario: Agent attempts dynamic code execution
- **WHEN** a request contains a code expression, assembly path, shell command, or output file path
- **THEN** the Host rejects it as outside the supported UI contract

### Requirement: Enforced operation timeouts and cancellation
The Host SHALL enforce server-side timeouts of 5 seconds for actions and 10 seconds for waits by default, with local configuration allowed up to 60 seconds. Client disconnection MUST cancel the associated work when possible.

#### Scenario: Wait exceeds its deadline
- **WHEN** a condition remains unsatisfied at its configured deadline
- **THEN** the Host stops the wait and returns a structured timeout even if the client remains connected

### Requirement: Idle Host cleanup
The Host SHALL close its session and exit after 10 minutes without an authenticated request by default. Local configuration MAY adjust or disable this idle limit without disabling operation timeouts.

#### Scenario: Agent exits without closing the Host
- **WHEN** no valid request arrives for the configured idle period
- **THEN** the Host releases the application session and terminates without permanent manual cleanup

### Requirement: Stable HTTP failures
HTTP failures SHALL use suitable status codes and a consistent body containing `code`, `message`, `details`, and bounded diagnostics. Internal failures MUST NOT expose full .NET stacks unless local diagnostic logging explicitly captures them.

#### Scenario: Agent can react to a busy session
- **WHEN** the Host rejects overlapping work
- **THEN** it returns HTTP 409 with stable code `session_busy` so the Agent can retry sequentially

### Requirement: Project-level Agent Skill delivery
The release SHALL provide one version-matched Skill template that can be copied into `.agents/skills/avalonia-headless-driver/` for Kimi Code and `.claude/skills/avalonia-headless-driver/` for Claude Code. The Skill MUST document the complete startup-to-shutdown workflow and MUST NOT require global installation.

#### Scenario: Either Agent discovers the project Skill
- **WHEN** a project places the template in the documented directory for its Agent
- **THEN** that Agent can discover and follow the workflow without additional global configuration

### Requirement: Replaceable HTTP client examples
Skill examples SHALL use real HTTP requests and identify curl as an optional external prerequisite, using `curl.exe` explicitly where PowerShell aliases require it. The Skill MUST NOT require jq, a custom CLI, or a particular shell for correct driver operation.

#### Scenario: curl is unavailable
- **WHEN** an Agent has another HTTP client capable of equivalent requests
- **THEN** it can follow the same API contract without installing the example tooling
