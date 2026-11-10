# Proposal

## Why

Coding and testing Agents currently have no reliable way to observe, operate, and verify Avalonia.Headless applications through a persistent, debuggable workflow. This greenfield project will provide that workflow without modifying business UI code or expanding into general-purpose desktop automation.

## What Changes

- Add a .NET 10 driver and Console Host for Avalonia 12.1.x, with a project-owned Startup Adapter that initializes the application, test services, and initial window.
- Provide one long-lived Headless Session per Host process, preserve UI state across requests, serialize UI work, and reset state by restarting the process.
- Expose semantic control-tree observations, repeatable Element Queries, realistic UI input, structured condition waits, and optional rendered PNG observations.
- Add a versioned local JSON HTTP API with OpenAPI documentation, loopback-only binding, random ports, per-start authentication, structured errors, timeouts, and idle-process cleanup.
- Supply a version-matched Agent Skill template for Kimi Code and Claude Code that documents startup, discovery, authentication, automation workflows, and shutdown.
- Protect sensitive input through response and log redaction, restrict operations to predefined UI capabilities, and disallow arbitrary code execution, filesystem access, and remote requests.
- Add a sample Avalonia application and end-to-end HTTP tests covering login success and failure, state persistence, ambiguity, asynchronous waits, screenshots, redaction, and stale sessions.

## Capabilities

### New Capabilities

- `headless-session-hosting`: Starts and owns a persistent Avalonia.Headless application session, including startup composition, serialization, lifecycle, recovery, and platform compatibility.
- `agent-ui-automation`: Provides Agent-facing semantic observation, control location, realistic actions, condition waits, visual observation, and actionable automation diagnostics.
- `local-agent-integration`: Exposes the driver through a secure local HTTP contract and version-matched Agent Skills for startup, discovery, invocation, and shutdown.

### Modified Capabilities

None.

## Impact

This change introduces new .NET 10 projects for the driver, Console Host, sample application, and tests; NuGet dependencies aligned to Avalonia 12.1.x with Skia rendering; a versioned HTTP and OpenAPI contract; local Host configuration; and project-level Skill templates. It affects only new project code and planning artifacts because the repository currently contains no implementation or existing capabilities. MCP, Avalonia 11.x, .NET 8, remote access, OS-level desktop automation, arbitrary code execution, and in-process parallel sessions remain out of scope.
