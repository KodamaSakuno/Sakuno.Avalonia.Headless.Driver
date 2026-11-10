# Spec Delta

## Purpose

Provides Agents with deterministic, verifiable observation and operation of logical Avalonia controls without exposing unrestricted object or code access.

## ADDED Requirements

### Requirement: Bounded semantic observations
The driver SHALL provide a Semantic Tree containing windows, control types, names, AutomationIds, relevant values, states, bounds, and parent-child relationships. Responses MUST enforce depth, node, and text limits, identify truncation, and allow bounded subtree expansion.

#### Scenario: Agent observes a login window
- **WHEN** an Agent requests the default snapshot of a login window
- **THEN** it receives the identifiable login controls and their user-relevant states without a complete Avalonia object graph

#### Scenario: Large tree is truncated
- **WHEN** a requested snapshot exceeds configured depth or node limits
- **THEN** the response marks the affected branches as truncated and provides enough identity to request those subtrees separately

### Requirement: Repeatable and unambiguous control location
The driver SHALL locate controls through repeatable Element Queries over AutomationId, exact or explicitly contained name or text, role, and control type. Queries MUST be deterministic and MUST NOT use object references, fixed tree indexes, fuzzy matching, or implicit first-match selection.

#### Scenario: Unique control is resolved
- **WHEN** an Element Query matches exactly one observable control
- **THEN** the driver returns or operates on that control

#### Scenario: Ambiguous query requires disambiguation
- **WHEN** an Element Query matches multiple controls
- **THEN** the driver returns an ambiguous-match error with bounded candidate details and performs no action

### Requirement: Observable window and realization boundaries
Observations SHALL distinguish TopLevel windows and their associated dialogs or popups. They MUST include only visible, instantiated controls by default, expose hidden controls only on explicit request, and identify unrealized virtualized content as currently unobservable.

#### Scenario: Dialogs do not cause silent ambiguity
- **WHEN** matching controls exist in both a main window and a modal dialog
- **THEN** the Agent must supply a window scope or a more specific query before an action proceeds

#### Scenario: Virtualized item is not instantiated
- **WHEN** an Agent queries a virtualized list item whose control has not been realized
- **THEN** the driver reports that the item is currently unobservable and does not synthesize a control node

### Requirement: Realistic high-level UI actions
The driver SHALL provide click, focus, text input, key, toggle, selection, scrolling, and pointer actions through Avalonia focus, hit-testing, and input-event paths. Each action MUST atomically resolve its query, validate target state, and execute input without an intervening session request.

#### Scenario: Button command follows the input path
- **WHEN** an Agent clicks an enabled button
- **THEN** the driver sends pointer input to the resolved target so normal hit-testing, routing, and command behavior occur

#### Scenario: Target is not actionable
- **WHEN** the resolved control is hidden, disabled, or otherwise unable to accept the requested action
- **THEN** the driver rejects the action before injecting input and reports the target state

### Requirement: Explicit text editing semantics
Text actions SHALL distinguish `replace` and `append`, provide an independent `clear` action, and MUST NOT implicitly submit with Enter. The driver MUST NOT echo entered text in action responses or logs.

#### Scenario: Agent replaces a username
- **WHEN** an Agent applies `replace` to a text box and later submits the form through a separate action
- **THEN** only the replacement text remains and no submission occurs until the separate action

### Requirement: Structured condition waits
The driver SHALL wait for structured conditions over control existence, visibility, enablement, focus, selection, text, and values. Conditions MUST use Element Queries and supported comparisons rather than arbitrary scripts, and every wait MUST have an enforced timeout.

#### Scenario: Login completion is observed
- **WHEN** a successful login produces an expected control or state before the timeout
- **THEN** the wait returns the matching observation

#### Scenario: Business operation never completes
- **WHEN** the expected condition remains false after UI queues have been processed
- **THEN** the wait returns a timeout diagnostic instead of treating UI queue completion as business success

### Requirement: Action outcomes are not business assertions
An action response SHALL identify the resolved target and related observation identifiers and MUST indicate only that input was executed and UI work was processed. The Agent MUST use subsequent observations or waits to verify business outcomes.

#### Scenario: Click does not imply login success
- **WHEN** a login click completes without an input error
- **THEN** the response reports the executed click but does not claim that authentication succeeded

### Requirement: Rendered visual observations
The driver SHALL provide a PNG observation for an individual Headless window, including its pixel dimensions and render scaling. Visual observations MUST supplement rather than replace semantic location and MUST NOT imply cross-platform pixel-identical output.

#### Scenario: Agent captures a login window
- **WHEN** rendering is enabled and an Agent requests the window image
- **THEN** the driver returns the rendered PNG with its dimensions and scaling metadata

### Requirement: Sensitive UI data protection
The driver SHALL mask password values in observations and MUST exclude text input, password contents, access tokens, and sensitive exception data from logs and diagnostics.

#### Scenario: Password is entered and observed
- **WHEN** an Agent enters a password and requests diagnostics or a new snapshot
- **THEN** neither the plaintext password nor the input payload appears in responses or logs

### Requirement: Actionable automation failures
Automation failures SHALL return stable error codes for invalid requests, missing or ambiguous targets, non-actionable state, timeout, busy or invalid sessions, and internal failures, with bounded relevant diagnostics and no default .NET stack disclosure.

#### Scenario: Missing target can be diagnosed
- **WHEN** an action query finds no matching control
- **THEN** the response identifies the not-found failure and includes a bounded relevant snapshot without exposing internal stacks
