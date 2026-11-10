# Spec Delta

## Purpose

Owns the startup, persistence, serialization, and recovery of a single Avalonia.Headless application session operated by an Agent.

## ADDED Requirements

### Requirement: Project-owned application startup
The Host SHALL start an application only through a project-owned Startup Adapter identified by local configuration. The adapter MUST establish the application, test services, and initial window without modifying business UI code or invoking the production entry point.

#### Scenario: Initial UI is explicitly composed
- **WHEN** the Host starts with a valid Startup Adapter configuration
- **THEN** it creates the configured test environment and displays the initial window in a new Headless Session

#### Scenario: Startup composition fails
- **WHEN** the Startup Adapter cannot initialize the application or initial window
- **THEN** the Host reports the startup failure and exits without accepting UI operations

### Requirement: Persistent single-session state
Each Host process SHALL allow exactly one active Headless Session and MUST preserve its application and UI state across sequential requests until the session closes.

#### Scenario: Login state survives separate requests
- **WHEN** an Agent enters credentials in one request and clicks the login button in a later request
- **THEN** both operations act on the same application state and control instances

#### Scenario: A second session is rejected
- **WHEN** an Agent requests a new session while another session is active
- **THEN** the Host rejects the request with a conflict and leaves the active session unchanged

### Requirement: Serialized UI execution
The Host SHALL execute all observation, action, and condition-wait work serially within a session. It MUST reject overlapping session requests rather than queue them for later UI execution.

#### Scenario: Concurrent action is rejected
- **WHEN** a second session request arrives while an action or wait is in progress
- **THEN** the Host returns a `session_busy` conflict and does not execute the overlapping UI work

### Requirement: Process-bound reset and recovery
Closing a Headless Session SHALL terminate its Host process after the close response is sent. Resetting application state or recovering from an unusable Host MUST use a fresh process and session identity.

#### Scenario: Agent resets the application
- **WHEN** an Agent closes a session and starts the Host again
- **THEN** the new process contains fresh application state and rejects requests bearing the old session identity

#### Scenario: Application crash interrupts the service
- **WHEN** the application under test crashes the Host process
- **THEN** the Agent detects the terminated service and recovers only by starting a new Host

### Requirement: Supported runtime and platforms
The first release SHALL support .NET 10 with Avalonia 12.1.x on Windows, macOS, and Linux, including environments without a display server. Avalonia package versions MUST be aligned, and earlier Avalonia or .NET versions MUST be rejected as unsupported.

#### Scenario: Supported CI environment
- **WHEN** the Host runs on a supported .NET 10 CI worker without a display server
- **THEN** it provides the complete Headless Session workflow

#### Scenario: Unsupported runtime
- **WHEN** the Host is run with an unsupported target runtime or incompatible Avalonia package set
- **THEN** it fails startup with an explicit compatibility diagnostic
